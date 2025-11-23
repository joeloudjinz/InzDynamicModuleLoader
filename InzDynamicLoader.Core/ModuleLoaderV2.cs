using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace InzDynamicLoader.Core;

internal static class ModuleLoaderV2
{
    // Map: Assembly (The Module) -> Its specific Resolver.
    // Allows us to check the "Parent's" folder first.
    private static readonly Dictionary<Assembly, AssemblyDependencyResolver> ModuleResolvers = new();

    // Fallback list for when we don't know who is asking.
    private static readonly List<AssemblyDependencyResolver> GlobalResolvers = [];

    // The Cache: AssemblyName string -> File Path (or null if definitely not found).
    // This turns O(N) file checks into O(1) memory lookups.
    private static readonly ConcurrentDictionary<string, string?> ResolutionCache = new();

    public static void Load(string[] moduleNames)
    {
        Load(moduleNames, GetModulesRootDirectory());
    }

    internal static void Load(string[] moduleNames, string rootPath)
    {
        // 1. Hook the event immediately
        AppDomain.CurrentDomain.AssemblyResolve += ResolveDependencies;

        var modulesRootPath = rootPath;
        InzConsole.Log($"Modules Root Path: [{modulesRootPath}]");

        var loadedCount = 0;

        foreach (var moduleName in moduleNames)
        {
            try
            {
                var modulePath = Path.Combine(modulesRootPath, moduleName, $"{moduleName}.dll");
                LoadModule(moduleName, modulePath);
                loadedCount++;
            }
            catch (Exception ex)
            {
                InzConsole.Error($"Failed to load module [{moduleName}]: {ex.Message}");
                // Decision: Do we throw? Or continue? 
                // Usually better to log and continue so valid modules still work.
            }
        }

        if (loadedCount == 0) throw new InvalidOperationException("No modules were successfully loaded.");

        // 2. Instantiate modules
        ModuleRegistry.InstantiateModuleDefinitions();

        // 3. OPTIONAL: Force a GC compaction. 
        // Startup generates a lot of garbage in the Large Object Heap (LOH). 
        // Since we are in the Default Context (no unloading), compacting now frees RAM for the app's lifetime.
        // GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        // GC.Collect();
    }

    private static void LoadModule(string moduleName, string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"Module not found at {filePath}");

        // Create the resolver for this specific module path
        var resolver = new AssemblyDependencyResolver(filePath);

        // Add to global list for fallback searches
        GlobalResolvers.Add(resolver);

        // Load into DEFAULT Context
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);

        // Register the specific mapping: "If THIS assembly asks for help, ask THIS resolver first."
        lock (ModuleResolvers)
        {
            ModuleResolvers[assembly] = resolver;
        }

        ModuleRegistry.Add(assembly);
        InzConsole.SuccessWithNewLine($"Loaded: {moduleName}");
    }

    private static Assembly? ResolveDependencies(object? sender, ResolveEventArgs args)
    {
        // OPTIMIZATION: Filter out resources early. 
        // The CLR fires this event for .resources.dll which often don't exist.
        if (args.Name.Contains(".resources")) return null;

        InzConsole.SecondLevelItem($"Resolving [{args.Name}]");
        
        // 1. Check the Cache (O(1) lookup)
        // We use the full name as key to ensure version exactness, 
        // but fall back to SimpleName if needed in your logic.
        if (ResolutionCache.TryGetValue(args.Name, out var cachedPath))
        {
            return cachedPath != null ? LoadAssemblyFromPathSafe(cachedPath) : null;
        }

        var assemblyName = new AssemblyName(args.Name);
        string? resolvedPath = null;

        // 2. Optimization: Locality of Reference
        // If we know who is asking (RequestingAssembly), check THEIR folder first.
        if (args.RequestingAssembly != null)
        {
            lock (ModuleResolvers)
            {
                if (ModuleResolvers.TryGetValue(args.RequestingAssembly, out var localResolver))
                {
                    resolvedPath = localResolver.ResolveAssemblyToPath(assemblyName);
                    if (resolvedPath != null)
                    {
                        InzConsole.ThirdLevelItem($"Resolved [{assemblyName.Name}] locally via [{args.RequestingAssembly.GetName().Name}]");
                    }
                }
            }
        }

        // 3. Fallback: Linear Scan (The "Global" search)
        // Only do this if the local check failed.
        if (resolvedPath == null)
        {
            foreach (var resolver in GlobalResolvers)
            {
                resolvedPath = resolver.ResolveAssemblyToPath(assemblyName);
                if (resolvedPath != null) break; // Found it!
            }
        }

        // 4. Update Cache
        // Whether we found it (path) or not (null), cache the result so we don't search again.
        ResolutionCache.TryAdd(args.Name, resolvedPath);

        return resolvedPath == null ? null : LoadAssemblyFromPathSafe(resolvedPath);
    }

    private static Assembly? LoadAssemblyFromPathSafe(string path)
    {
        try
        {
            // Default context automatically handles "already loaded" checks usually,
            // but explicitly catching ensures we don't crash if file locks exist.
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }
        catch (Exception ex)
        {
            InzConsole.Error($"Resolution found path '{path}' but failed to load: {ex.Message}");
            return null;
        }
    }

    private static string GetModulesRootDirectory()
    {
        var baseDir = AppContext.BaseDirectory;

        // SCENARIO 1: Production / Docker
        // Expect a "Modules" folder sitting right next to the executable. This folder is created by the "CopyModulesToPublish" target in the .csproj.
        var localModulesPath = Path.Combine(baseDir, "Modules");
        if (Directory.Exists(localModulesPath)) return localModulesPath;

        // SCENARIO 2: Development
        // The executable is running in bin/Debug/net9.0/, but the modules are in {SolutionRoot}/BuiltModules/. We need to search upwards.
        var devModulesPath = FindBuiltModulesDirectory(baseDir);
        if (devModulesPath == null)
        {
            throw new DirectoryNotFoundException(
                $"Could not locate 'Modules' folder at '{localModulesPath}' " +
                $"or 'BuiltModules' in any parent directory of '{baseDir}'."
            );
        }

        InzConsole.WarningWithNewLine($"[Dev Mode] Loading modules from solution output: {devModulesPath}");
        return devModulesPath;
    }

    private static string? FindBuiltModulesDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        // Limit traversal to avoid infinite loops or access errors
        var maxDepth = 6;

        while (dir != null && maxDepth > 0)
        {
            var candidate = Path.Combine(dir.FullName, "BuiltModules");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
            maxDepth--;
        }

        return null;
    }
}