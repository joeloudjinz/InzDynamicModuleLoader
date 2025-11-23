using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Example.Module.EFCore.PostgreSQL;

public class PostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<PostgreSqlDataContext>
{
    public PostgreSqlDataContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<PostgreSQLEntityFrameworkModule>()
            .Build();

        var connectionString = configuration.GetConnectionString("PgSQlConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Could not find connection string in User Secrets of [Example.Module.EFCore.PosgtreSQL]. " +
                "Run: dotnet user-secrets set 'ConnectionStrings:PgSQlConnectionString' '...' --project Example.Module.EFCore.PosgtreSQL"
            );
        }

        var builder = new DbContextOptionsBuilder<PostgreSqlDataContext>();
        builder.UseNpgsql(connectionString);
        return new PostgreSqlDataContext(builder.Options);
    }
}