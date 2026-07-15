using System.Diagnostics;

namespace InzDynamicModuleLoader.UnitTests;

/// <summary>
/// Regression test for the production publish gap: `dotnet publish` of a host must copy the built
/// modules into {publishDir}/Modules/ so the deployed application can find them at runtime.
/// </summary>
[Trait("Category", "Integration")]
public class PublishModulesTests
{
    private const string MySqlModule = "Example.Module.EFCore.MySQL";
    private const string RepositoriesModule = "Example.Module.EFCore.Repositories";

    [Fact]
    public void Publish_CopiesModulesIntoPublishOutput_SoDeployedHostCanLoadThem()
    {
        var repoRoot = FindRepoRoot();
        var temp = Directory.CreateTempSubdirectory("inz-publish-test");
        try
        {
            // 1. Build the module projects so BuiltModules/ is known-populated.
            foreach (var module in new[] { MySqlModule, RepositoriesModule })
                RunDotnet($"build \"{Path.Combine(repoRoot, module, module + ".csproj")}\" -c Release --nologo", repoRoot);

            // 2. Publish the host OUTSIDE the repo tree.
            var host = Path.Combine(repoRoot, "Example.Module.ConsoleStartup", "Example.Module.ConsoleStartup.csproj");
            RunDotnet($"publish \"{host}\" -c Release -o \"{temp.FullName}\" --nologo", repoRoot);

            // 3. Modules must be in the published artifact, with their dependency closure.
            var mysqlDir = Path.Combine(temp.FullName, "Modules", MySqlModule);
            Assert.True(File.Exists(Path.Combine(mysqlDir, MySqlModule + ".dll")),
                $"'{MySqlModule}.dll' is missing from the publish output at {mysqlDir}");
            Assert.True(File.Exists(Path.Combine(mysqlDir, MySqlModule + ".deps.json")),
                $"'{MySqlModule}.deps.json' is missing - the module's dependency closure was not copied");
            Assert.True(Directory.Exists(Path.Combine(temp.FullName, "Modules", RepositoriesModule)),
                $"'{RepositoriesModule}' is missing from the publish output");

            // 4. Modules must live only under Modules/, not loose in the publish root.
            Assert.False(File.Exists(Path.Combine(temp.FullName, MySqlModule + ".dll")),
                "a module assembly leaked into the publish root");

            // 5. The deployed app must get past module loading. It still fails later on the
            //    database - that is expected and out of scope.
            var output = RunPublishedApp(temp.FullName);
            Assert.DoesNotContain("Could not locate 'Modules' folder", output);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "InzDynamicLoader.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new DirectoryNotFoundException($"Could not find InzDynamicLoader.sln above {AppContext.BaseDirectory}");
    }

    private static void RunDotnet(string arguments, string workingDirectory)
    {
        var (exitCode, output) = Run("dotnet", arguments, workingDirectory);
        if (exitCode != 0) throw new InvalidOperationException($"`dotnet {arguments}` failed (exit {exitCode}):\n{output}");
    }

    private static string RunPublishedApp(string publishDir)
    {
        // Do not throw on failure: the app is expected to fail later on the database.
        var (_, output) = Run("dotnet", "Example.Module.ConsoleStartup.dll", publishDir);
        return output;
    }

    private static (int ExitCode, string Output) Run(string fileName, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(psi)!;
        // Drain both streams concurrently to avoid a full-pipe deadlock on large build output.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
