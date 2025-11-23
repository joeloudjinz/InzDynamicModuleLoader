using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InzDynamicModuleLoader.Core;

/// <summary>
/// Defines the contract for a module that can be dynamically loaded into an application.
/// Implementing this interface allows a module to register its services with the dependency injection container
/// and initialize its services after they have been registered.
/// </summary>
public interface IAmModule
{
    /// <summary>
    /// Registers the module's services with the dependency injection container.
    /// This method should be called during the service registration phase to add the module's required services.
    /// </summary>
    /// <param name="services">The service collection to register module services with.</param>
    /// <param name="configuration">The application configuration that may be used during service registration.</param>
    /// <returns>The service collection to enable method chaining.</returns>
    IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Initializes the module's services after they have been registered with the dependency injection container.
    /// This method should be called during the application initialization phase after all services have been registered.
    /// </summary>
    /// <param name="services">The service provider containing all registered services.</param>
    /// <param name="configuration">The application configuration that may be used during service initialization.</param>
    /// <returns>The service provider to enable method chaining.</returns>
    IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration);
}