using InzDynamicModuleLoader.BaselineRunner;

namespace InzDynamicModuleLoader.Bench.Tests;

[Trait("Category", "Integration")]
public class SyntheticModuleGeneratorTests
{
    [Fact]
    public void Generate_BuildsModule_WithDepsJsonAndDepthChain()
    {
        var work = Directory.CreateTempSubdirectory();
        try
        {
            var abstractionsCsproj = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "InzDynamicModuleLoader.Abstractions", "InzDynamicModuleLoader.Abstractions.csproj"));

            var gen = new SyntheticModuleGenerator(work.FullName, abstractionsCsproj);
            var moduleName = gen.Generate(index: 0, depth: 2);

            var builtDir = Path.Combine(gen.OutputRoot, moduleName);
            Assert.True(File.Exists(Path.Combine(builtDir, $"{moduleName}.dll")));
            Assert.True(File.Exists(Path.Combine(builtDir, $"{moduleName}.deps.json")));
            Assert.True(File.Exists(Path.Combine(builtDir, $"{moduleName}_Lib1.dll")));
            Assert.True(File.Exists(Path.Combine(builtDir, $"{moduleName}_Lib2.dll")));
        }
        finally { work.Delete(recursive: true); }
    }
}
