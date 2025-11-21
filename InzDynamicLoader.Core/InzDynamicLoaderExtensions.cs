using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InzDynamicLoader.Core;

/// <summary>
/// Provides extension methods for dynamically loading and initializing modules in an application.
/// </summary>
public static class InzDynamicLoaderExtensions
{
    /// <summary>
    /// Registers all modules specified in the configuration with the service collection.
    /// This method reads module names from the configuration, loads the corresponding assemblies,
    /// and registers their services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to register module services with.</param>
    /// <param name="configuration">The application configuration containing the list of modules to load.</param>
    /// <exception cref="System.Exception">Thrown when no modules are specified in the configuration.</exception>
    public static void RegisterModules(this IServiceCollection services, IConfiguration configuration)
    {
        var moduleNames = configuration.GetSection(Constants.ModulesConfigurationLabel).Get<string[]>() ?? [];
        if (moduleNames.Length == 0) throw new Exception("No modules are specified in configuration");

        ModuleLoader.Load(moduleNames);

        foreach (var module in ModuleRegistry.LoadedModuleDefinitions) module.RegisterServices(services, configuration);
    }

    /// <summary>
    /// Initializes all previously loaded modules using the configured service provider and application configuration.
    /// This method should be called after modules have been registered and their services have been added to the container.
    /// </summary>
    /// <param name="services">The service provider containing registered services.</param>
    /// <param name="configuration">The application configuration to pass to the modules during initialization.</param>
    /// <exception cref="System.Exception">Thrown when no assemblies have been loaded yet.</exception>
    public static void InitializeModules(this IServiceProvider services, IConfiguration configuration)
    {
        if (ModuleRegistry.LoadedAssemblies.Count == 0) throw new Exception("No assemblies are loaded!");

        ModuleInitializer.Initialize(services, configuration);
    }
}