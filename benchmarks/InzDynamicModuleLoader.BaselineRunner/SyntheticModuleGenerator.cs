using System.Diagnostics;

namespace InzDynamicModuleLoader.BaselineRunner;

/// <summary>
/// Generates and builds real synthetic module projects with a real dependency-depth chain, so that
/// "depth" produces actual assembly-load/resolution events when the module is registered, instead of
/// being a purely cosmetic project count.
/// </summary>
public class SyntheticModuleGenerator
{
    private readonly string _workDir;
    private readonly string _abstractionsCsproj;

    /// <summary>Root folder holding the built (post-`dotnet build`) output of every generated module.</summary>
    public string OutputRoot { get; }

    public SyntheticModuleGenerator(string workDir, string abstractionsCsproj)
    {
        _workDir = workDir;
        _abstractionsCsproj = Path.GetFullPath(abstractionsCsproj);
        OutputRoot = Path.Combine(workDir, "SyntheticBuiltModules");
        Directory.CreateDirectory(OutputRoot);
    }

    /// <summary>
    /// Generates and builds a synthetic module named "Synth_N{index}_D{depth}", backed by a chain of
    /// {depth} class-library projects (Lib1..Lib{depth}) where each LibN references and calls into
    /// LibN-1. The module's RegisterServices touches Lib{depth}, forcing the whole chain to resolve and
    /// load at registration time. Returns the module name; the built output lands under
    /// {OutputRoot}/{name}.
    /// </summary>
    public string Generate(int index, int depth)
    {
        var name = $"Synth_N{index}_D{depth}";

        for (var d = 1; d <= depth; d++)
        {
            CreateLibProject(name, d);
        }

        CreateModuleProject(name, depth);

        var moduleDir = Path.Combine(_workDir, name);
        BuildProject(moduleDir, $"{name}.csproj");

        var builtOutput = Path.Combine(moduleDir, "bin", "Release", "net9.0");
        DirectoryCopy.Recursive(builtOutput, Path.Combine(OutputRoot, name));

        return name;
    }

    private void CreateLibProject(string name, int d)
    {
        var libName = $"{name}_Lib{d}";
        var libDir = Path.Combine(_workDir, libName);
        Directory.CreateDirectory(libDir);

        var csprojLines = new List<string>
        {
            "<Project Sdk=\"Microsoft.NET.Sdk\">",
            "    <PropertyGroup>",
            "        <TargetFramework>net9.0</TargetFramework>",
            "        <ImplicitUsings>enable</ImplicitUsings>",
            "        <Nullable>enable</Nullable>",
            "        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>",
            "        <GenerateDependencyFile>true</GenerateDependencyFile>",
            "    </PropertyGroup>",
        };

        if (d > 1)
        {
            var previousLibName = $"{name}_Lib{d - 1}";
            csprojLines.Add("    <ItemGroup>");
            csprojLines.Add($"        <ProjectReference Include=\"..\\{previousLibName}\\{previousLibName}.csproj\"/>");
            csprojLines.Add("    </ItemGroup>");
        }

        csprojLines.Add("</Project>");
        File.WriteAllText(Path.Combine(libDir, $"{libName}.csproj"), string.Join(Environment.NewLine, csprojLines));

        var previousCall = d > 1 ? $" + {name}_Lib{d - 1}.Value{d - 1}.Get()" : string.Empty;
        var codeLines = new[]
        {
            $"namespace {libName};",
            "",
            $"public static class Value{d}",
            "{",
            $"    public static int Get() => {d}{previousCall};",
            "}",
        };
        File.WriteAllText(Path.Combine(libDir, $"Value{d}.cs"), string.Join(Environment.NewLine, codeLines));
    }

    private void CreateModuleProject(string name, int depth)
    {
        var moduleDir = Path.Combine(_workDir, name);
        Directory.CreateDirectory(moduleDir);

        var csprojLines = new List<string>
        {
            "<Project Sdk=\"Microsoft.NET.Sdk\">",
            "    <PropertyGroup>",
            "        <TargetFramework>net9.0</TargetFramework>",
            "        <ImplicitUsings>enable</ImplicitUsings>",
            "        <Nullable>enable</Nullable>",
            "        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>",
            "        <GenerateDependencyFile>true</GenerateDependencyFile>",
            "    </PropertyGroup>",
            "    <ItemGroup>",
            $"        <ProjectReference Include=\"{_abstractionsCsproj}\"/>",
        };

        if (depth >= 1)
        {
            var topLibName = $"{name}_Lib{depth}";
            csprojLines.Add($"        <ProjectReference Include=\"..\\{topLibName}\\{topLibName}.csproj\"/>");
        }

        csprojLines.Add("    </ItemGroup>");
        csprojLines.Add("</Project>");
        File.WriteAllText(Path.Combine(moduleDir, $"{name}.csproj"), string.Join(Environment.NewLine, csprojLines));

        // Touching Lib{depth} here cascades the resolution/load of the entire Lib1..Lib{depth} chain
        // the moment the module is registered - this is what makes "depth" an observable cost rather
        // than an inert project count.
        var touchTopOfChain = depth >= 1 ? $"var _ = {name}_Lib{depth}.Value{depth}.Get();" : string.Empty;

        var codeLines = new List<string>
        {
            "using InzDynamicModuleLoader.Abstractions;",
            "using Microsoft.Extensions.Configuration;",
            "using Microsoft.Extensions.DependencyInjection;",
            "",
            $"namespace {name};",
            "",
            $"public class {name}Module : IAmModule",
            "{",
            "    public IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration)",
            "    {",
        };

        if (touchTopOfChain.Length > 0)
        {
            codeLines.Add($"        {touchTopOfChain}");
        }

        codeLines.Add("        return services;");
        codeLines.Add("    }");
        codeLines.Add("");
        codeLines.Add("    public IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration)");
        codeLines.Add("    {");
        codeLines.Add("        return services;");
        codeLines.Add("    }");
        codeLines.Add("}");

        File.WriteAllText(Path.Combine(moduleDir, $"{name}Module.cs"), string.Join(Environment.NewLine, codeLines));
    }

    private static void BuildProject(string projectDir, string csprojFileName)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("build");
        psi.ArgumentList.Add(csprojFileName);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Release");
        psi.ArgumentList.Add("--nologo");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start 'dotnet build' for {csprojFileName}.");

        // Drain both streams concurrently before blocking on exit to avoid a full-pipe deadlock.
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var stdOut = stdOutTask.GetAwaiter().GetResult();
        var stdErr = stdErrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet build failed for {csprojFileName} (exit code {process.ExitCode}):{Environment.NewLine}{stdOut}{Environment.NewLine}{stdErr}");
        }
    }
}
