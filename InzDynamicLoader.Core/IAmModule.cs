using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InzDynamicLoader.Core;

public interface IAmModule
{
    IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration);
    IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration);
}