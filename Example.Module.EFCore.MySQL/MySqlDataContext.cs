using Example.Module.Common.Contracts;
using Example.Module.Common.Data;
using Example.Module.EFCore.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Example.Module.EFCore.MySQL;

public class MySqlDataContext(DbContextOptions<MySqlDataContext> options) : DbContext(options), IDataContext, IUnitOfWork, IEntityFrameworkCoreDbContext
{
    public DbSet<TestEntity> TestEntities { get; set; }

}