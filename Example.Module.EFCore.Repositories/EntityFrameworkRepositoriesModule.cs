using Example.Module.Common.Contracts;
using InzDynamicModuleLoader.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Example.Module.EFCore.Repositories;

public class EntityFrameworkRepositoriesModule : IAmModule
{
    public IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        Console.WriteLine("Registering EF Core Repositories module services");
        services.AddScoped<ITestRepository, TestRepository>();
        return services;
    }

    public IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration)
    {
        Console.WriteLine("Initializing EF Core Repositories module services");
        return services;
    }
}