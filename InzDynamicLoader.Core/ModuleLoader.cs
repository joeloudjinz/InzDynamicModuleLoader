using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace InzDynamicLoader.Core;

/// <summary>
/// Module loader responsible for dynamically loading assemblies and resolving their dependencies.
/// Implements an optimized dependency resolution strategy with caching and locality-based resolution.
/// </summary>
internal static class ModuleLoader
{
    /// <summary>
    /// Maps each loaded module assembly to its specific dependency resolver.
    /// This enables checking the module's parent folder first when resolving dependencies.
    /// </summary>
    private static readonly Dictionary<Assembly, AssemblyDependencyResolver> ModuleResolvers = new();

    /// <summary>
    /// Collection of dependency resolvers used for fallback resolution when the requesting assembly is unknown.
    /// </summary>
    private static readonly List<AssemblyDependencyResolver> GlobalResolvers = [];

    /// <summary>
    /// Caches assembly name to file path mappings to avoid redundant file system checks.
    /// This transforms O(N) file checks into O(1) memory lookups for faster dependency resolution.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string?> ResolutionCache = new();

    /// <summary>
    /// Loads the specified modules from the default modules root directory determined by the execution environment.
    /// </summary>
    /// <param name="moduleNames">An array of module names to load.</param>
    /// <exception cref="InvalidOperationException">Thrown when no modules were successfully loaded.</exception>
    public static void Load(string[] moduleNames)
    {
        Load(moduleNames, GetModulesRootDirectory());
    }

    /// <summary>
    /// Loads the specified modules from the given root path and registers the assembly resolver.
    /// This method hooks the AssemblyResolve event, loads modules, and instantiates module definitions.
    /// </summary>
    /// <param name="moduleNames">An array of module names to load.</param>
    /// <param name="rootPath">The root directory where modules are located.</param>
    /// <exception cref="InvalidOperationException">Thrown when no modules were successfully loaded, or one module could not be loaded.</exception>
    internal static void Load(string[] moduleNames, string rootPath)
    {
        // Hook the assembly resolution event to handle dependency loading
        AppDomain.CurrentDomain.AssemblyResolve += ResolveDependencies;

        var modulesRootPath = rootPath;
        // InzConsole.Log($"Modules Root Path: [{modulesRootPath}]");

        foreach (var moduleName in moduleNames)
        {
            try
            {
                var modulePath = Path.Combine(modulesRootPath, moduleName, $"{moduleName}.dll");
                LoadModule(moduleName, modulePath);
            }
            catch (Exception ex)
            {
                InzConsole.Error($"Failed to load module [{moduleName}]: {ex.Message}");
                throw new InvalidOperationException($"Failed to load module [{moduleName}]: {ex.Message}");
            }
        }

        // Instantiate the module definitions after all modules are loaded
        ModuleRegistry.InstantiateModuleDefinitions();
    }

    /// <summary>
    /// Loads a single module from the specified file path, creating a dependency resolver and registering it.
    /// This method handles the complete process of loading a module: creating a resolver, loading into the
    /// default context, and establishing the resolver mapping for dependency resolution.
    /// </summary>
    /// <param name="moduleName">The name of the module being loaded.</param>
    /// <param name="filePath">The full path to the module assembly file.</param>
    /// <exception cref="FileNotFoundException">Thrown when the module file does not exist at the specified path.</exception>
    private static void LoadModule(string moduleName, string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"Module not found at {filePath}");

        // Create a dependency resolver for this specific module path
        var resolver = new AssemblyDependencyResolver(filePath);

        // Add to global resolvers list for fallback searches when requesting assembly is unknown
        GlobalResolvers.Add(resolver);

        // Load the assembly into the default load context
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);

        // Establish the mapping: "If THIS assembly requests dependencies, check THIS resolver first"
        lock (ModuleResolvers)
        {
            ModuleResolvers[assembly] = resolver;
        }

        // Register the loaded assembly with the module registry for later instantiation
        ModuleRegistry.Add(assembly);
        // InzConsole.SuccessWithNewLine($"Loaded: {moduleName}");
    }

    /// <summary>
    /// Resolves assembly dependencies when the CLR cannot find them automatically.
    /// Implements an optimized four-step search strategy: resource filtering, cache lookup,
    /// local resolution (based on requesting assembly), and global resolution.
    /// </summary>
    /// <param name="sender">The source of the event (typically an AppDomain).</param>
    /// <param name="args">Arguments containing information about the assembly being resolved.</param>
    /// <returns>The resolved assembly, or null if resolution failed.</returns>
    private static Assembly? ResolveDependencies(object? sender, ResolveEventArgs args)
    {
        // OPTIMIZATION: Filter out resources early.
        // The CLR fires this event for .resources.dll which often don't exist.
        if (args.Name.Contains(".resources")) return null;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            InzConsole.Headline($"Resolving [{args.Name}]");

            // 1. Check the Cache (O(1) lookup)
            // We use the full name as key to ensure version exactness.
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
                            InzConsole.Log($"Resolved [{assemblyName.Name}] locally via [{args.RequestingAssembly.GetName().Name}]");
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
            // Whether we found it (path) or not (null), cache the result to avoid future searches.
            ResolutionCache.TryAdd(args.Name, resolvedPath);

            return resolvedPath == null ? null : LoadAssemblyFromPathSafe(resolvedPath);
        }
        finally
        {
            stopwatch.Stop();
            if (!args.Name.Contains(".resources"))
            {
                InzConsole.FirstLevelItem($"Dependency resolution for [{args.Name}] took {stopwatch.ElapsedMilliseconds} ms");
                InzConsole.EndHeadline();
            }
        }
    }

    /// <summary>
    /// Safely loads an assembly from the specified path, with proper exception handling.
    /// This method attempts to load an assembly into the default context and logs errors if loading fails.
    /// </summary>
    /// <param name="path">The file path of the assembly to load.</param>
    /// <returns>The loaded assembly if successful, or null if loading failed.</returns>
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

    /// <summary>
    /// Determines the appropriate modules root directory based on the execution environment.
    /// First checks for the 'Modules' folder in the application directory (production scenario),
    /// then searches for 'BuiltModules' in parent directories (development scenario).
    /// </summary>
    /// <returns>The path to the modules root directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when neither the production 'Modules' folder nor the development 'BuiltModules' directory is found.</exception>
    private static string GetModulesRootDirectory()
    {
        var baseDir = AppContext.BaseDirectory;

        // SCENARIO 1: Production / Docker
        // Expect a "Modules" folder sitting right next to the executable. This folder is created by the "CopyModulesToPublish" target in the .csproj.
        var localModulesPath = Path.Combine(baseDir, "Modules");
        if (Directory.Exists(localModulesPath))
        {
            InzConsole.WarningWithNewLine($"[Production] Loading modules from: {localModulesPath}");
            return localModulesPath;
        }

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

    /// <summary>
    /// Searches parent directories up to a maximum depth to find the BuiltModules directory.
    /// This method is used in development environments to locate modules in the solution's output directory.
    /// </summary>
    /// <param name="startPath">The starting path for the directory search.</param>
    /// <returns>The path to the BuiltModules directory if found, otherwise null.</returns>
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