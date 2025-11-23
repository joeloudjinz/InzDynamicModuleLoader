using Example.Module.Common.Configurations;
using Example.Module.Common.Contracts;
using Example.Module.EFCore.Repositories;
using InzDynamicLoader.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Example.Module.EFCore.PostgreSQL;

public class PostgreSQLEntityFrameworkModule : IAmModule
{
    public IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        Console.WriteLine("Registering PostgreSQL module db context & services");
        var databaseConfigOptions = new DatabaseConfigOptions(configuration);
        services.AddDbContext<IDataContext, PostgreSqlDataContext>(options => { options.UseNpgsql(databaseConfigOptions.ConnectionString); });

        // Using the lambda factory so when getting IEntityFrameworkCoreDbContext or IUnitOfWork, the DI will return the same instance of MySqlDataContext  
        services.AddScoped<IEntityFrameworkCoreDbContext>(sp => sp.GetRequiredService<PostgreSqlDataContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PostgreSqlDataContext>());
        return services;
    }

    public IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration)
    {
        Console.WriteLine("Initializing PostgreSQL module db context & services");
        return services;
    }
}