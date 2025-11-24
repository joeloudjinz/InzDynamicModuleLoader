using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace InzDynamicModuleLoader.Core;

/// <summary>
/// Service for managing the entire module lifecycle (loading, registration, initialization).
/// </summary>
internal class ModuleManagerService : IModuleManager
{
    /// <summary>
    /// Maps each loaded module assembly to its specific dependency resolver.
    /// This enables checking the module's parent folder first when resolving dependencies.
    /// </summary>
    private readonly Dictionary<Assembly, AssemblyDependencyResolver> _moduleResolvers = new();

    /// <summary>
    /// Collection of dependency resolvers used for fallback resolution when the requesting assembly is unknown.
    /// </summary>
    private readonly List<AssemblyDependencyResolver> _globalResolvers = [];

    /// <summary>
    /// Caches assembly name to file path mappings to avoid redundant file system checks.
    /// This transforms O(N) file checks into O(1) memory lookups for faster dependency resolution.
    /// </summary>
    private readonly ConcurrentDictionary<string, string?> _resolutionCache = new();

    /// <summary>
    /// Collection of loaded module definitions.
    /// </summary>
    public List<IAmModule> LoadedModuleDefinitions { get; } = [];

    public void LoadModules(string[] moduleNames)
    {
        if (moduleNames.Length == 0) throw new InvalidOperationException("No modules are specified in configuration");

        // Hook the assembly resolution event to handle dependency loading
        AppDomain.CurrentDomain.AssemblyResolve += ResolveDependencies;

        var loadedAssemblies = LoadModules(moduleNames, GetModulesRootDirectory());

        // Instantiate the module definitions after all modules are loaded
        InstantiateModuleDefinitions(loadedAssemblies);
    }

    internal List<Assembly> LoadModules(string[] moduleNames, string rootPath)
    {
        List<Assembly> loadedAssemblies = [];
        foreach (var moduleName in moduleNames)
        {
            try
            {
                var modulePath = Path.Combine(rootPath, moduleName, $"{moduleName}.dll");
                var assembly = LoadModule(modulePath);
                loadedAssemblies.Add(assembly);
            }
            catch (Exception ex)
            {
                InzConsole.Error($"Failed to load module [{moduleName}]: {ex.Message}");
                throw new InvalidOperationException($"Failed to load module [{moduleName}]: {ex.Message}");
            }
        }

        return loadedAssemblies;
    }

    internal Assembly LoadModule(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"Module not found at {filePath}");

        // Create a dependency resolver for this specific module path
        var resolver = new AssemblyDependencyResolver(filePath);

        // Add to global resolvers list for fallback searches when requesting assembly is unknown
        _globalResolvers.Add(resolver);

        // Load the assembly into the default load context
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);

        // Establish the mapping: "If THIS assembly requests dependencies, check THIS resolver first"
        lock (_moduleResolvers)
        {
            _moduleResolvers[assembly] = resolver;
        }

        return assembly;
    }

    internal void InstantiateModuleDefinitions(List<Assembly> loadedAssemblies)
    {
        foreach (var assembly in loadedAssemblies)
        {
            var assemblyName = assembly.GetName().Name!;
            var types = assembly.GetTypes()
                .Where(t =>
                    t.GetInterfaces().Any(ti => ti.FullName!.Equals(typeof(IAmModule).FullName)) &&
                    t is { IsInterface: false, IsAbstract: false }
                ).ToList();
            if (types.Count == 0)
            {
                InzConsole.Warning($"No IAmModule implementation found in assembly [{assemblyName}]");
                continue;
            }

            if (types.Count != 1) throw new Exception($"IAmModule contract must have only one implementation in assembly [{assemblyName}]");

            LoadedModuleDefinitions.Add(Activator.CreateInstance(types.First()) as IAmModule ?? throw new Exception($"Could not cast type {types.First().Name} to IAmModule"));
            InzConsole.Success($"IModule definition created for [{assemblyName}]");
        }
    }

    /// <summary>
    /// Resolves assembly dependencies when the CLR cannot find them automatically.
    /// Implements an optimized four-step search strategy: resource filtering, cache lookup,
    /// local resolution (based on requesting assembly), and global resolution.
    /// </summary>
    /// <param name="sender">The source of the event (typically an AppDomain).</param>
    /// <param name="args">Arguments containing information about the assembly being resolved.</param>
    /// <returns>The resolved assembly, or null if resolution failed.</returns>
    private Assembly? ResolveDependencies(object? sender, ResolveEventArgs args)
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
            if (_resolutionCache.TryGetValue(args.Name, out var cachedPath))
            {
                return cachedPath != null ? LoadAssemblyFromPathSafe(cachedPath) : null;
            }

            var assemblyName = new AssemblyName(args.Name);
            string? resolvedPath = null;

            // 2. Optimization: Locality of Reference
            // If we know who is asking (RequestingAssembly), check THEIR folder first.
            if (args.RequestingAssembly != null)
            {
                lock (_moduleResolvers)
                {
                    if (_moduleResolvers.TryGetValue(args.RequestingAssembly, out var localResolver))
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
                foreach (var resolver in _globalResolvers)
                {
                    resolvedPath = resolver.ResolveAssemblyToPath(assemblyName);
                    if (resolvedPath != null) break; // Found it!
                }
            }

            // 4. Update Cache
            // Whether we found it (path) or not (null), cache the result to avoid future searches.
            _resolutionCache.TryAdd(args.Name, resolvedPath);

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