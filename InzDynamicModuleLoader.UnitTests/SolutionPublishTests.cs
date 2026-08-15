namespace InzDynamicModuleLoader.UnitTests;

/// <summary>
/// A solution-level publish must give every host a complete module set.
/// Nothing in the build graph connects the module projects to a host, so MSBuild is free to
/// publish a host before the modules are built.
/// </summary>
[Trait("Category", "Integration")]
public class SolutionPublishTests
{
    private static readonly string[] ExpectedModules =
    [
        "Example.Module.EFCore.MySQL",
        "Example.Module.EFCore.PostgreSQL",
        "Example.Module.EFCore.Repositories"
    ];

    [Fact]
    public void SolutionPublish_GivesEveryHostACompleteModuleSet()
    {
        var repoRoot = ProcessRunner.FindRepoRoot();
        using var clone = new RepoClone(repoRoot);

        ProcessRunner.Dotnet(
            $"publish \"{Path.Combine(clone.Path, "InzDynamicLoader.sln")}\" -c Release --nologo",
            clone.Path,
            TimeSpan.FromMinutes(10));

        var publishDir = Path.Combine(clone.Path, "Example.Module.WebStartup", "bin", "Release", "net9.0", "publish");
        var modulesDir = Path.Combine(publishDir, "Modules");

        Assert.True(Directory.Exists(modulesDir), $"'{modulesDir}' is missing after a solution publish");

        var found = Directory.GetDirectories(modulesDir).Select(Path.GetFileName).ToArray();
        var missing = ExpectedModules.Except(found!).ToArray();

        Assert.True(missing.Length == 0,
            $"the published host is missing {missing.Length} module(s): {string.Join(", ", missing)}. " +
            $"Found: {(found.Length == 0 ? "nothing" : string.Join(", ", found))}.");
    }

    /// <summary>
    /// A throwaway copy of the repository, so the test cannot disturb the working tree.
    /// This copies the working tree, not a git commit, so it includes changes that are not committed yet.
    /// A git clone would only see committed work, and the fix under test is committed after it is verified.
    /// </summary>
    private sealed class RepoClone : IDisposable
    {
        private static readonly string[] SkipDirectories =
            ["bin", "obj", ".git", ".vs", ".idea", ".claude", "BuiltModules", "nupkg", "TestResults"];

        private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("inz-solution-publish");
        public string Path { get; }

        public RepoClone(string repoRoot)
        {
            Path = System.IO.Path.Combine(_dir.FullName, "repo");
            CopyTree(new DirectoryInfo(repoRoot), new DirectoryInfo(Path));
        }

        private static void CopyTree(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            foreach (var file in source.EnumerateFiles())
                file.CopyTo(System.IO.Path.Combine(target.FullName, file.Name), overwrite: true);

            foreach (var directory in source.EnumerateDirectories())
            {
                if (SkipDirectories.Contains(directory.Name, StringComparer.OrdinalIgnoreCase)) continue;
                CopyTree(directory, new DirectoryInfo(System.IO.Path.Combine(target.FullName, directory.Name)));
            }
        }

        public void Dispose() { try { _dir.Delete(recursive: true); } catch { /* best effort */ } }
    }
}
