using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Example.Module.EFCore.MySQL;

public class MySqlDesignTimeFactory : IDesignTimeDbContextFactory<MySqlDataContext>
{
    public MySqlDataContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<MySQLEntityFrameworkModule>()
            .Build();

        var connectionString = configuration.GetConnectionString("MySQlConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Could not find connection string in User Secrets of [Example.Module.EFCore.MySQL]. " +
                "Run: dotnet user-secrets set 'ConnectionStrings:MySQlConnectionString' '...' --project Example.Module.EFCore.MySQL"
            );
        }

        var builder = new DbContextOptionsBuilder<MySqlDataContext>();
        builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        return new MySqlDataContext(builder.Options);
    }
}