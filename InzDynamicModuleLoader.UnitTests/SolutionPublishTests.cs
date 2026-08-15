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
            // CreateTempSubdirectory() returns a path under /var on macOS, but the temp directory
            // itself is not a symlink - /var is (it points at /private/var). If we hand MSBuild the
            // unresolved /var path, restore can see the same project through two path spellings and
            // race to write the same generated file (e.g. obj/*.csproj.nuget.g.props), failing with
            // "file already exists". ResolveRealPath walks up to find and resolve that symlinked
            // ancestor, so MSBuild only ever sees one identity for the clone.
            Path = System.IO.Path.Combine(ResolveRealPath(_dir.FullName), "repo");
            CopyTree(new DirectoryInfo(repoRoot), new DirectoryInfo(Path));
        }

        /// <summary>
        /// Resolves every symlink in a path's ancestry, not just the path itself.
        /// DirectoryInfo.ResolveLinkTarget only resolves an entry that is itself a symlink, but on
        /// macOS the temp directory is real and merely sits under a symlinked ancestor (/var), so that
        /// alone leaves the path unresolved.
        /// </summary>
        private static string ResolveRealPath(string path)
        {
            var info = new DirectoryInfo(path);
            var segments = new Stack<string>();
            while (info is not null)
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    var result = target.FullName;
                    while (segments.Count > 0) result = System.IO.Path.Combine(result, segments.Pop());
                    return result;
                }

                segments.Push(info.Name);
                info = info.Parent;
            }

            return path;
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
