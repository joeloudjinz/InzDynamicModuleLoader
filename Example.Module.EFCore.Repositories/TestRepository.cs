using Example.Module.Common.Contracts;
using Example.Module.Common.Data;

namespace Example.Module.EFCore.Repositories;

internal class TestRepository(IEntityFrameworkCoreDbContext context, IUnitOfWork uow) : ITestRepository
{
    public async Task<bool> Test(CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a test entity
            var testEntity = new TestEntity
            {
                Name = $"Test_{DateTime.UtcNow:yyyy-MM-dd_HH:mm:ss}",
                CreatedAt = DateTime.UtcNow
            };

            // Add the entity to the context
            context.TestEntities.Add(testEntity);

            // Save changes to the database
            await uow.SaveChangesAsync(cancellationToken);

            // Verify the entity was created by retrieving it
            var retrievedEntity = await context.TestEntities.FindAsync([testEntity.Id], cancellationToken);
            if (retrievedEntity == null) return retrievedEntity != null;

            // Clean up: remove the test entity
            context.TestEntities.Remove(retrievedEntity);
            await uow.SaveChangesAsync(cancellationToken);

            // Return true if the entity was successfully created and retrieved
            return true;
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            return false;
        }
    }
}