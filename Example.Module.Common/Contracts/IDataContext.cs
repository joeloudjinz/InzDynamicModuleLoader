namespace Example.Module.Common.Contracts;

public interface IDataContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    int SaveChanges();
}