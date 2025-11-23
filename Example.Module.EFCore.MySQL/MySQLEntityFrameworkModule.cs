using Example.Module.Common.Configurations;
using Example.Module.Common.Contracts;
using Example.Module.EFCore.Repositories;
using InzDynamicModuleLoader.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Example.Module.EFCore.MySQL;

public class MySQLEntityFrameworkModule : IAmModule
{
    public IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        Console.WriteLine("Registering MySQL module db context & services");
        var databaseConfigOptions = new DatabaseConfigOptions(configuration);
        services.AddDbContext<IDataContext, MySqlDataContext>(options => { options.UseMySql(databaseConfigOptions.ConnectionString, ServerVersion.AutoDetect(databaseConfigOptions.ConnectionString)); });

        // Using the lambda factory so when getting IEntityFrameworkCoreDbContext or IUnitOfWork, the DI will return the same instance of MySqlDataContext  
        services.AddScoped<IEntityFrameworkCoreDbContext>(sp => sp.GetRequiredService<MySqlDataContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MySqlDataContext>());
        return services;
    }

    public IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration)
    {
        Console.WriteLine("Initializing MySQL module db context & services");
        return services;
    }
}