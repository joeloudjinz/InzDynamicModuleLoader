using Example.Module.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Example.Module.EFCore.Repositories;

public interface IEntityFrameworkCoreDbContext
{
    public DbSet<TestEntity> TestEntities { get; set; }
}