namespace Example.Module.Common.Contracts;

public interface ITestRepository
{
    Task<bool> Test(CancellationToken cancellationToken);
}