using InzDynamicModuleLoader.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// A probe host for the publish tests. It loads the modules and prints how many it loaded.
// It never asks the container for a service, so no DbContext is built and no database is contacted.
// Do not add a GetService, GetRequiredService or CreateScope call to this file.

var modules = new[] { "Example.Module.EFCore.MySQL", "Example.Module.EFCore.Repositories" };

var settings = new Dictionary<string, string?>
{
    // Bound by the example modules, never used, because no service is resolved.
    ["Database:ConnectionString"] = "Server=unused;Database=unused;Uid=unused;Pwd=unused;"
};
for (var i = 0; i < modules.Length; i++) settings[$"Modules:{i}"] = modules[i];

var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

try
{
    var services = new ServiceCollection();
    services.RegisterModules(configuration);

    var provider = services.BuildServiceProvider();
    provider.InitializeModules(configuration);

    Console.WriteLine($"MODULES-LOADED: {modules.Length}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"PROBE-FAILED: {ex.Message}");
    return 1;
}
