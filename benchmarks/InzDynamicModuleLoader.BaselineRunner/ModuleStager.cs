namespace InzDynamicModuleLoader.BaselineRunner;

public static class ModuleStager
{
    /// <summary>Cleans {destDir}/Modules and copies each {sourceRoot}/{name} folder into it (full closure).</summary>
    public static string Stage(string sourceRoot, string destDir, IReadOnlyList<string> moduleNames)
    {
        var modulesDir = Path.Combine(destDir, "Modules");
        if (Directory.Exists(modulesDir)) Directory.Delete(modulesDir, recursive: true);
        Directory.CreateDirectory(modulesDir);
        foreach (var name in moduleNames)
        {
            var src = Path.Combine(sourceRoot, name);
            if (!Directory.Exists(src)) throw new DirectoryNotFoundException($"Module folder not found: {src}");
            DirectoryCopy.Recursive(src, Path.Combine(modulesDir, name));
        }
        return modulesDir;
    }
}
