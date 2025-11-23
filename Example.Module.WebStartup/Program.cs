using Example.Module.Common.Contracts;
using InzDynamicModuleLoader.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterModules(builder.Configuration);

var app = builder.Build();
app.Services.InitializeModules(app.Configuration);

await Task.Delay(2000);

using var scope = app.Services.CreateScope();
var testRepository = scope.ServiceProvider.GetService<ITestRepository>()!;
Console.WriteLine($"DB test result => {await testRepository.Test(CancellationToken.None)}");
Console.WriteLine("Done!");