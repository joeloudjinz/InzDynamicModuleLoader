namespace InzDynamicModuleLoader.UnitTests;

/// <summary>
/// A published host must contain the modules, and must be able to load them.
/// This test publishes the probe host. The probe never asks the container for a service,
/// so no database is contacted. Do not change it to publish an example application.
/// </summary>
[Trait("Category", "Integration")]
public class PublishModulesTests
{
    private static readonly string[] ExpectedModules =
    [
        "Example.Module.EFCore.MySQL",
        "Example.Module.EFCore.Repositories"
    ];

    [Fact]
    public void PublishedProbe_ContainsModules_AndLoadsThem()
    {
        var repoRoot = ProcessRunner.FindRepoRoot();
        using var temp = new TempDir();

        // Publish in the same configuration as this test run, so the shared module folder
        // is not left in a different configuration.
        ProcessRunner.Dotnet(
            $"publish \"{Path.Combine(repoRoot, "Example.Module.PublishProbe", "Example.Module.PublishProbe.csproj")}\" " +
            $"-c {ProcessRunner.CurrentConfiguration} -o \"{temp.Path}\" --nologo",
            repoRoot);

        foreach (var module in ExpectedModules) AssertModulePublished(temp.Path, module);

        // Positive check: the deployed application really loaded the modules.
        var result = ProcessRunner.Run("dotnet", "Example.Module.PublishProbe.dll", temp.Path, TimeSpan.FromMinutes(2));

        Assert.True(result.ExitCode == 0, $"the published probe failed to run:\n{result.Output}");
        Assert.Contains($"MODULES-LOADED: {ExpectedModules.Length}", result.Output);
    }

    private static void AssertModulePublished(string publishRoot, string module)
    {
        var dir = Path.Combine(publishRoot, "Modules", module);
        Assert.True(Directory.Exists(dir), $"'{module}' is missing from the publish output at {dir}");
        Assert.True(File.Exists(Path.Combine(dir, module + ".dll")), $"'{module}.dll' is missing from {dir}");
        Assert.True(File.Exists(Path.Combine(dir, module + ".deps.json")),
            $"'{module}.deps.json' is missing from {dir}; the dependency closure was not copied");
        Assert.False(File.Exists(Path.Combine(publishRoot, module + ".dll")),
            $"'{module}.dll' leaked into the publish root; modules must live only under Modules/");
    }

    private sealed class TempDir : IDisposable
    {
        private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("inz-publish-test");
        public string Path => _dir.FullName;
        public void Dispose() { try { _dir.Delete(recursive: true); } catch { /* best effort */ } }
    }
}
