namespace InzDynamicModuleLoader.UnitTests;

/// <summary>
/// Covers the path a real consumer takes: install the package from a feed, then publish.
/// The repository's own examples use a project reference, so they never exercise this path.
/// </summary>
[Trait("Category", "Integration")]
public class PackagedConsumerTests
{
    private const string PackageId = "InzSoftwares.NetDynamicModuleLoader";

    // A new version for every run, so a package from an earlier run cannot be taken from the
    // global NuGet cache. The test never clears the user's cache.
    private static readonly string TestVersion = $"9.9.9-test{DateTime.UtcNow.Ticks}";

    [Fact]
    public void DirectConsumer_GetsModulesInPublishOutput()
    {
        using var scratch = new ScratchConsumer(TestVersion, directReference: true);
        scratch.Publish();
        scratch.AssertModuleWasCopied();
    }

    [Fact]
    public void IndirectConsumer_GetsModulesInPublishOutput()
    {
        // App.Host -> App.Infrastructure -> package. This fails until the package
        // also ships the targets file in buildTransitive.
        using var scratch = new ScratchConsumer(TestVersion, directReference: false);
        scratch.Publish();
        scratch.AssertModuleWasCopied();
    }

    [Fact]
    public void EmptyModuleFolder_WarningIsSuppressible_AndStrictModeFails()
    {
        using var scratch = new ScratchConsumer(TestVersion, directReference: true);
        scratch.RemoveModules();

        // The warning must carry a code, so a strict build can silence it by name.
        var suppressed = scratch.TryPublish("-warnaserror -p:NoWarn=INZ001");
        Assert.True(suppressed.ExitCode == 0,
            $"publish with -warnaserror and NoWarn=INZ001 should succeed, but failed:\n{suppressed.Output}");

        // Strict mode must turn the warning into an error.
        var strict = scratch.TryPublish("-p:InzRequireModulesOnPublish=true");
        Assert.False(strict.ExitCode == 0, "publish with InzRequireModulesOnPublish=true should fail");
        Assert.Contains("INZ001", strict.Output);
    }

    /// <summary>Builds the package, then creates and publishes a scratch consumer solution.</summary>
    private sealed class ScratchConsumer : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("inz-consumer");
        private readonly string _hostDir;
        private readonly string _version;

        public ScratchConsumer(string version, bool directReference)
        {
            _version = version;
            var repoRoot = ProcessRunner.FindRepoRoot();
            var feed = Path.Combine(_root.FullName, "feed");
            _hostDir = Path.Combine(_root.FullName, "App.Host");

            // Core depends on Abstractions by project reference, and -p:PackageVersion propagates
            // through the build graph. Both packages must therefore exist in the feed at this version.
            foreach (var project in new[] { "InzDynamicModuleLoader.Abstractions", "InzDynamicModuleLoader.Core" })
            {
                ProcessRunner.Dotnet(
                    $"pack \"{Path.Combine(repoRoot, project, project + ".csproj")}\" " +
                    $"-c Release -p:PackageVersion={_version} -o \"{feed}\" --nologo",
                    repoRoot);
            }

            File.WriteAllText(Path.Combine(_root.FullName, "nuget.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <add key="local" value="{feed}" />
                  </packageSources>
                </configuration>
                """);

            // The anchor the copy target looks for, plus a dummy module to copy.
            File.WriteAllText(Path.Combine(_root.FullName, "Directory.Build.targets"), "<Project />");
            var dummy = Path.Combine(_root.FullName, "BuiltModules", "DummyModule");
            Directory.CreateDirectory(dummy);
            File.WriteAllText(Path.Combine(dummy, "DummyModule.dll"), "not a real assembly");
            File.WriteAllText(Path.Combine(dummy, "DummyModule.deps.json"), "{}");

            var packageRef = $"""<PackageReference Include="{PackageId}" Version="{_version}" />""";

            if (directReference)
            {
                WriteProject(_hostDir, "App.Host", exe: true, items: packageRef);
            }
            else
            {
                var infraDir = Path.Combine(_root.FullName, "App.Infrastructure");
                WriteProject(infraDir, "App.Infrastructure", exe: false, items: packageRef);
                WriteProject(_hostDir, "App.Host", exe: true,
                    items: """<ProjectReference Include="..\App.Infrastructure\App.Infrastructure.csproj" />""");
            }
        }

        private static void WriteProject(string dir, string name, bool exe, string items)
        {
            Directory.CreateDirectory(dir);
            var output = exe ? "<OutputType>Exe</OutputType>" : "";
            File.WriteAllText(Path.Combine(dir, $"{name}.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    {output}
                    <TargetFramework>net9.0</TargetFramework>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                  <ItemGroup>
                    {items}
                  </ItemGroup>
                </Project>
                """);
            if (exe) File.WriteAllText(Path.Combine(dir, "Program.cs"), "System.Console.WriteLine(\"host\");");
            else File.WriteAllText(Path.Combine(dir, "Class1.cs"), $"namespace {name}; public class Marker {{ }}");
        }

        public string PublishDir => Path.Combine(_root.FullName, "out");

        public void RemoveModules() => Directory.Delete(Path.Combine(_root.FullName, "BuiltModules"), recursive: true);

        public void Publish()
        {
            var result = TryPublish("");
            Assert.True(result.ExitCode == 0, $"publish failed:\n{result.Output}");
        }

        public ProcessRunner.Result TryPublish(string extraArgs)
        {
            // The csproj path must be relative to the working directory, not absolute. On macOS the
            // temp root returned by Directory.CreateTempSubdirectory sits under a symlinked prefix
            // (/var -> /private/var). Passing an absolute path built from that unresolved prefix makes
            // MSBuild's restore graph treat the entry project and its own ProjectReference targets
            // (resolved relative to the project file, which canonicalizes through the symlink) as two
            // different identities, so the entry project's ProjectReference items are silently dropped
            // from the restore graph. A path relative to the working directory avoids the mismatch.
            var relativeCsproj = Path.Combine("App.Host", "App.Host.csproj");
            var result = ProcessRunner.Run("dotnet",
                $"publish \"{relativeCsproj}\" -c Release -o \"{PublishDir}\" --nologo {extraArgs}",
                _root.FullName);

            if (result.Output.Contains("Unable to load the service index") ||
                result.Output.Contains("No such host is known"))
            {
                throw new InvalidOperationException(
                    "The scratch consumer could not restore its packages. This machine may be offline and the " +
                    "dependencies may be absent from the NuGet cache. This is an environment fault, not a code fault.\n"
                    + result.Output);
            }
            return result;
        }

        public void AssertModuleWasCopied()
        {
            var copied = Path.Combine(PublishDir, "Modules", "DummyModule", "DummyModule.dll");
            Assert.True(File.Exists(copied),
                $"the copy target did not run for this consumer: '{copied}' is missing");
        }

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best effort */ }
        }
    }
}
