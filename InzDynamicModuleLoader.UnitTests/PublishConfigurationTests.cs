using System.Reflection;
using System.Runtime.InteropServices;

namespace InzDynamicModuleLoader.UnitTests;

/// <summary>
/// A published host must contain modules built in the same configuration as the host.
/// A Debug module inside a Release deployment has unoptimised code and debug behaviour.
/// </summary>
[Trait("Category", "Integration")]
public class PublishConfigurationTests
{
    private const string ModuleName = "Example.Module.EFCore.Repositories";

    [Fact]
    public void PublishedModule_MatchesThePublishConfiguration()
    {
        var repoRoot = ProcessRunner.FindRepoRoot();
        var publishConfiguration = ProcessRunner.CurrentConfiguration;          // Debug in a normal test run
        var oppositeConfiguration = publishConfiguration == "Debug" ? "Release" : "Debug";

        using var temp = new TempDir();

        // 1. Put the WRONG configuration in the shared module folder first.
        ProcessRunner.Dotnet(
            $"build \"{Path.Combine(repoRoot, ModuleName, ModuleName + ".csproj")}\" -c {oppositeConfiguration} --nologo",
            repoRoot);

        // 2. Publish the probe in the test's configuration.
        ProcessRunner.Dotnet(
            $"publish \"{Path.Combine(repoRoot, "Example.Module.PublishProbe", "Example.Module.PublishProbe.csproj")}\" " +
            $"-c {publishConfiguration} -o \"{temp.Path}\" --nologo",
            repoRoot);

        // 3. The copied module must match the publish configuration, not step 1.
        var copied = Path.Combine(temp.Path, "Modules", ModuleName, ModuleName + ".dll");
        Assert.True(File.Exists(copied), $"'{copied}' is missing from the publish output");

        var isDebug = IsDebugAssembly(copied);
        var expectDebug = publishConfiguration == "Debug";
        Assert.True(isDebug == expectDebug,
            $"the published module is a {(isDebug ? "Debug" : "Release")} build, but the host was published " +
            $"in {publishConfiguration}. The module folder still holds the {oppositeConfiguration} build from step 1.");
    }

    /// <summary>
    /// Reads DebuggableAttribute from the file without loading it into this process.
    /// A Debug assembly sets DisableOptimizations (0x100).
    /// </summary>
    private static bool IsDebugAssembly(string assemblyPath)
    {
        var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
        var resolver = new PathAssemblyResolver(runtimeAssemblies.Append(assemblyPath));
        using var context = new MetadataLoadContext(resolver);

        var assembly = context.LoadFromAssemblyPath(assemblyPath);
        var attribute = assembly.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Diagnostics.DebuggableAttribute");

        if (attribute is null) return false;                       // no attribute means an optimised build
        if (attribute.ConstructorArguments.Count != 1) return false;

        var modes = Convert.ToInt32(attribute.ConstructorArguments[0].Value);
        const int disableOptimizations = 0x100;
        return (modes & disableOptimizations) != 0;
    }

    private sealed class TempDir : IDisposable
    {
        private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("inz-publish-config");
        public string Path => _dir.FullName;
        public void Dispose() { try { _dir.Delete(recursive: true); } catch { /* best effort */ } }
    }
}
