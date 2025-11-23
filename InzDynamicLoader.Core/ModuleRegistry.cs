using System.Reflection;

namespace InzDynamicLoader.Core;

internal static class ModuleRegistry
{
    private static readonly Dictionary<string, Assembly> AssembliesMap = [];
    private static readonly Dictionary<string, IAmModule> ModuleDefinitionsMap = [];

    public static List<Assembly> LoadedAssemblies => AssembliesMap.Values.ToList();
    public static List<IAmModule> LoadedModuleDefinitions => ModuleDefinitionsMap.Values.ToList();

    public static void Add(Assembly assembly)
    {
        AssembliesMap.Add(assembly.GetName().Name!, assembly);
    }

    public static void InstantiateModuleDefinitions()
    {
        // InzConsole.Headline("InstantiateModuleDefinitions()");
        foreach (var (key, assembly) in AssembliesMap)
        {
            // InzConsole.FirstLevelItem($"Assembly: [{key}]");
            var types = assembly.GetTypes()
                .Where(t =>
                    t.GetInterfaces().Any(ti => ti.FullName!.Equals(typeof(IAmModule).FullName)) &&
                    t is { IsInterface: false, IsAbstract: false }
                ).ToList();
            if (types.Count == 0)
            {
                InzConsole.Warning($"No IAmModule implementation found in assembly [{key}]");
                continue;
            }

            if (types.Count != 1) throw new Exception($"IAmModule contract must have only one implementation in assembly [{key}]");

            ModuleDefinitionsMap.Add(key, Activator.CreateInstance(types.First()) as IAmModule ?? throw new Exception($"Could not cast type {types.First().Name} to IAmModule"));
            InzConsole.Success($"IModule definition created for [{key}]");
        }

        // InzConsole.EndHeadline();
    }
}