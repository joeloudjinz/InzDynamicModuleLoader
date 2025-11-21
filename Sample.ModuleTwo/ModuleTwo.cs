using InzDynamicLoader.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.ModuleTwo;

public class ModuleTwo : IAmModule
{
    public IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        Console.WriteLine("Registering module two services");
        return services;
    }

    public IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration)
    {
        Console.WriteLine("Initializing module two services");
        return services;
    }
}