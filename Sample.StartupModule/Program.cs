using InzDynamicLoader.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterModules(builder.Configuration);

var app = builder.Build();
app.Services.InitializeModules(app.Configuration);
app.Run();