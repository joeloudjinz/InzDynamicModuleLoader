using Microsoft.Extensions.Configuration;

namespace InzDynamicLoader.Core;

internal static class ModuleInitializer
{
    public static void Initialize(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        foreach (var module in ModuleRegistry.LoadedModuleDefinitions)
        {
            module.InitializeServices(serviceProvider, configuration);
            Console.WriteLine($"Module {module.GetType().Assembly.FullName} initialized.");
        }
    }
}