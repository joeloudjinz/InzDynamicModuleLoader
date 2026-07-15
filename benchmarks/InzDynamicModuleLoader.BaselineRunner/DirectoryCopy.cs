namespace InzDynamicModuleLoader.BaselineRunner;

public static class DirectoryCopy
{
    /// <summary>Recursively copies every file under <paramref name="src"/> into <paramref name="dest"/>, preserving structure.</summary>
    public static void Recursive(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dest, Path.GetRelativePath(src, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
