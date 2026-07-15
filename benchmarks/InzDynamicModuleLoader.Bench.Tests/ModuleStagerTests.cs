using InzDynamicModuleLoader.BaselineRunner;

namespace InzDynamicModuleLoader.Bench.Tests;

public class ModuleStagerTests
{
    [Fact]
    public void Stage_CopiesFullModuleFolder_AndCleansPrevious()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            var src = Path.Combine(tmp.FullName, "src");
            var mod = Path.Combine(src, "ModA");
            Directory.CreateDirectory(mod);
            File.WriteAllText(Path.Combine(mod, "ModA.dll"), "x");
            File.WriteAllText(Path.Combine(mod, "ModA.deps.json"), "{}");
            var dest = Path.Combine(tmp.FullName, "probe");
            var stale = Path.Combine(dest, "Modules", "Old");
            Directory.CreateDirectory(stale);

            ModuleStager.Stage(src, dest, ["ModA"]);

            var staged = Path.Combine(dest, "Modules", "ModA");
            Assert.True(File.Exists(Path.Combine(staged, "ModA.dll")));
            Assert.True(File.Exists(Path.Combine(staged, "ModA.deps.json")));
            Assert.False(Directory.Exists(stale));
        }
        finally { tmp.Delete(recursive: true); }
    }
}
