using Example.Module.Common.Contracts;
using InzDynamicModuleLoader.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Starting InzDynamicModuleLoader Console Application...");

// Set environment variable for modules to load (comma-separated string format)
Environment.SetEnvironmentVariable("Modules__0", "Example.Module.EFCore.MySQL");
Environment.SetEnvironmentVariable("Modules__1", "Example.Module.EFCore.Repositories");

// Build configuration using environment variables and user secrets
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables() // Load environment variables
    .AddUserSecrets<Program>() // Load user secrets for connection string
    .Build();

// Display which modules are configured to load
var modules = configuration.GetSection("Modules").Get<string[]>();
if (modules is { Length: > 0 })
{
    Console.WriteLine($"Modules configured to load: {string.Join(", ", modules)}");
}
else
{
    Console.WriteLine("No modules configured to load. Set the 'Modules' environment variable.");
    Console.WriteLine("Example: export Modules=\"Example.Module.EFCore.MySQL,Example.Module.EFCore.Repositories\"");
    return;
}

// Create service collection
var services = new ServiceCollection();

try
{
    // Register modules based on environment variable
    services.RegisterModules(configuration);

    // Build service provider
    var serviceProvider = services.BuildServiceProvider();

    // Initialize modules
    serviceProvider.InitializeModules(configuration);

    // Wait a bit for initialization to complete
    await Task.Delay(2000);

    // Create scope and test the database functionality
    using var scope = serviceProvider.CreateScope();
    var testRepository = scope.ServiceProvider.GetService<ITestRepository>();

    if (testRepository != null)
    {
        Console.WriteLine("Testing database connection and functionality...");
        var result = await testRepository.Test(CancellationToken.None);
        Console.WriteLine($"DB test result => {result}");
    }
    else
    {
        Console.WriteLine("Test repository not found - check that modules are properly loaded");
    }

    Console.WriteLine("Console application completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error occurred: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}