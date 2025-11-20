using System.Reflection;
using System.Runtime.Loader;

namespace InzDynamicLoader.Core;

internal static class ModuleLoader
{
    // Keeping reference to resolvers so they don't get Garbage Collected,
    // and so to query them later when the runtime asks for dependencies.
    private static readonly List<AssemblyDependencyResolver> DependencyResolvers = [];

    public static void Load(string[] moduleNames)
    {
        // Hook into the AssemblyResolve event.
        // This is the critical piece that allows dependencies (like EF Core) to be loaded from the module's directory into the Default Context.
        AppDomain.CurrentDomain.AssemblyResolve += ResolveDependencies;

        // Determine where the modules are located (Dev vs Prod)
        var modulesRootPath = GetModulesRootDirectory();
        InzConsole.Log($"Modules Root Path: [{modulesRootPath}]");

        foreach (var moduleName in moduleNames)
        {
            try
            {
                // Construct the path based on the "BuiltModules/{Name}/{Name}.dll" structure
                var modulePath = Path.Combine(modulesRootPath, moduleName, $"{moduleName}.dll");
                LoadModule(moduleName, modulePath);
            }
            catch (Exception ex)
            {
                // Catch here so one broken module doesn't crash the whole app immediately, providing a chance to log the specific failure.
                InzConsole.Error($"Failed to load module [{moduleName}]: {ex.Message}");
                throw;
            }
        }

        if (ModuleRegistry.LoadedAssemblies.Count == 0) throw new InvalidOperationException("No modules were successfully loaded.");

        // Instantiate the IAmModule classes
        ModuleRegistry.InstantiateModuleDefinitions();
    }

    private static void LoadModule(string moduleName, string filePath)
    {
        InzConsole.Headline($"Loading Module: {moduleName}");
        InzConsole.FirstLevelItem($"Path: {filePath}");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Module assembly not found. Expected at: {filePath}");
        }

        // Create a resolver for this module. This reads the {Module}.deps.json file to understand its dependencies.
        DependencyResolvers.Add(new AssemblyDependencyResolver(filePath));

        // Load the assembly into the DEFAULT context. This ensures types are shared and compatible with the host application.
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);

        // Register it
        ModuleRegistry.Add(assembly);
        InzConsole.SuccessWithNewLine("Assembly loaded successfully");
    }

    /// <summary>
    /// This event handler is called by the CLR whenever it fails to find a DLL in the main application directory.
    /// </summary>
    private static Assembly? ResolveDependencies(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);

        // Loop through all our loaded modules and ask: 
        // "Do you have a dependency that matches this name?"
        foreach (var resolver in DependencyResolvers)
        {
            var path = resolver.ResolveAssemblyToPath(assemblyName);
            if (path == null) continue;

            InzConsole.SecondLevelItem($"Resolved dependency [{assemblyName.Name}] from module path");
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }

        // return null so the CLR throws a FileNotFoundException.
        return null;
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
        if (devModulesPath != null)
        {
            InzConsole.WarningWithNewLine($"[Dev Mode] Loading modules from solution output: {devModulesPath}");
            return devModulesPath;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate 'Modules' folder at '{localModulesPath}' " +
            $"or 'BuiltModules' in any parent directory of '{baseDir}'.");
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