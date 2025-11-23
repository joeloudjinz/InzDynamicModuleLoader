# InzDynamicModuleLoader

InzDynamicModuleLoader is a .NET 9.0 library that enables plugin-based architecture by loading modules at startup time. This allows for better
separation of concerns, module isolation, and flexible infrastructure switching while maintaining clean architecture boundaries.

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [How to Use](#how-to-use)
  - [1. Install the Package](#1-install-the-package)
  - [2. Configure Build Targets](#2-configure-build-targets)
  - [3. Create a Module Project](#3-create-a-module-project)
  - [4. Implement Your Module](#4-implement-your-module)
  - [5. Configure Modules](#5-configure-modules)
  - [6. Register and Initialize Modules](#6-register-and-initialize-modules)
- [Project Structure](#project-structure)
- [Managing Dependencies](#managing-dependencies)
- [IAmModule Interface Explained](#iammodule-interface-explained)
- [Example Implementation](#example-implementation)
- [Troubleshooting](#troubleshooting)
- [Learn More](#learn-more)
- [Contributing](#contributing)
- [License](#license)

## Features

- **Dynamic Module Loading**: Load modules at runtime based on configuration
- **Plugin Architecture**: Create extensible applications with modular functionality
- **Easy Integration**: Simple setup with .NET's built-in dependency injection
- **Dependency Management**: Automatic resolution of module-specific dependencies
- **Configuration-Driven**: Control which modules load through configuration files
- **Development & Production**: Works seamlessly in both environments

## Prerequisites

- .NET 9.0 or higher

## How to Use

### 1. Install the Package

Add the Inz Dynamic Module Loader to your startup project:

```shell
dotnet add package InzSoftwares.NetDynamicModuleLoader
```

### 2. Configure Build Targets

Create a `Directory.Build.targets` file in your solution root directory with this content. This ensures all module dependencies are properly copied:

```xml

<Project>
    <PropertyGroup Condition="'$(IsModuleProject)' == 'true'">
        <!-- Forces MSBuild to copy all dependencies to the output directory -->
        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
        <!-- Generates dependency file for proper resolution -->
        <GenerateDependencyFile>true</GenerateDependencyFile>
    </PropertyGroup>

    <!-- Custom target to deploy modules to a unified folder after build -->
    <Target Name="DeployModuleToUnifiedFolder" AfterTargets="Build" Condition="'$(IsModuleProject)' == 'true'">
        <PropertyGroup>
            <UnifiedModulePath>$(MSBuildThisFileDirectory)BuiltModules/$(MSBuildProjectName)/</UnifiedModulePath>
        </PropertyGroup>

        <ItemGroup>
            <ModuleFiles Include="$(OutputPath)**/*.*"/>
        </ItemGroup>

        <Message Text="[Module Deployment] Copying $(MSBuildProjectName) to $(UnifiedModulePath)" Importance="High"/>

        <Copy SourceFiles="@(ModuleFiles)"
              DestinationFolder="$(UnifiedModulePath)%(RecursiveDir)"
              SkipUnchangedFiles="true"/>
    </Target>
</Project>
```

This configuration ensures each dynamically loaded module includes all its dependencies and the `deps.json` file. The custom target creates a
dedicated `BuiltModules` directory in the solution directory where each dynamically loaded module's dependencies can be found and resolved correctly.

For more details about this file and its purpose, see
the [Directory.Build.targets Documentation](https://github.com/joeloudjinz/InzDynamicModuleLoader/blob/main/Documentations/Directory.Build.targets%20Documentation.md).

### 3. Create a Module Project

1. Create a new .NET Class Library project
2. In the `.csproj` file, add the `IsModuleProject` property:

```xml

<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <!-- Enables the module deployment logic -->
        <IsModuleProject>true</IsModuleProject>
    </PropertyGroup>

</Project>
```

### 4. Implement Your Module

In your module project, create a class that implements the `IAmModule` interface:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InzDynamicLoader.Core;

public class MyExampleModule : IAmModule
{
    public IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register your module's services here
        // Example: services.AddScoped<IMyService, MyServiceImplementation>();

        return services;
    }

    public IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration)
    {
        // Initialize services after registration (optional)
        // Example: Initialize database connections, event handlers, etc.

        return services;
    }
}
```

### 5. Configure Modules

In your main application's `appsettings.json`, specify which modules to load:

```json
{
  "Modules": [
    "MyExampleModule",
    "AnotherModule"
  ]
}
```

### 6. Register and Initialize Modules

In your main application's `Program.cs` (or `Startup.cs`), register and initialize the modules:

```csharp
using InzDynamicLoader.Core;

var builder = WebApplication.CreateBuilder(args);

// Register modules and their services
builder.Services.RegisterModules(builder.Configuration);

var app = builder.Build();

// Initialize modules after services are registered
app.Services.InitializeModules(builder.Configuration);

// Continue with your application setup
app.Run();
```

## Project Structure

After setup, your solution will have this structure:

```
YourSolution/
├── MainApplication/          # Your main application
├── MyExampleModule/          # First module project
├── MyOtherModule/            # Second module project
├── Directory.Build.targets   # Build configuration
├── Directory.Packages.props  # Centralized package versions
└── BuiltModules/             # Automatically created - contains compiled modules
    ├── MyExampleModule/
    │   ├── MyExampleModule.dll
    │   ├── MyExampleModule.deps.json
    │   └── Dependencies...
    └── MyOtherModule/
        ├── MyOtherModule.dll
        ├── MyOtherModule.deps.json
        └── Dependencies...
```

## Managing Dependencies

When working with multiple modules, you may encounter the "Diamond Dependency" problem. This occurs when different modules require the same dependency
but at different versions. For example:

- Module A requires Newtonsoft.Json v13
- Module B requires Newtonsoft.Json v9
- At runtime, only one version can be loaded, which may cause compatibility issues

To solve this problem and ensure version consistency across all modules, create a `Directory.Packages.props` file in your solution root. This file
enables Central Package Management, which defines package versions in one central location:

```xml

<Project>
    <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    </PropertyGroup>

    <ItemGroup>
        <!-- Define package versions once for the entire solution -->
        <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0"/>
        <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="9.0.0"/>
        <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.0"/>
        <!-- Add other package versions here -->
    </ItemGroup>
</Project>
```

When using this approach, reference packages without specifying versions in your project files:

```xml

<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions"/>
```

For more information about this file and its content, see the [Directory.Packages.props Documentation](https://github.com/joeloudjinz/InzDynamicModuleLoader/blob/main/Documentations/Directory.Packages.props%20Documentation.md).

## IAmModule Interface Explained

The `IAmModule` interface has two methods:

- **RegisterServices**: Called first, registers services with the dependency injection container
- **InitializeServices**: Called after registration, allows for service initialization and configuration

## Example Implementation

The project includes a comprehensive example that demonstrates the dynamic loading capabilities of the InzDynamicModuleLoader system. The example
showcases a real-world scenario where database infrastructure can be switched at startup without code changes.

The example includes:

- Multiple database provider implementations (MySQL and PostgreSQL)
- Core shared components with contracts and data models
- Common EF Core repository patterns
- Runtime switching between database providers based on configuration
- Clean separation of concerns between modules

The example uses modules with the `Example.` prefix to demonstrate:

- `Example.Module.Common` - Contains shared contracts, entities, and configurations
- `Example.Module.EFCore.MySQL` - MySQL-specific implementation
- `Example.Module.EFCore.PostgreSQL` - PostgreSQL-specific implementation
- `Example.Module.EFCore.Repositories` - Repository implementations
- `Example.Module.WebStartup` - Web application startup project

This architecture demonstrates how to build flexible applications where infrastructure concerns can be swapped out dynamically, maintaining clean
separation of concerns while enabling maximum flexibility.

For step-by-step instructions on how to run and understand the example, see the [Example Breakdown](../Documentations/Example%20Breakdown.md)
documentation.

## Troubleshooting

Problem: Module dependencies not found

- Solution: Ensure `IsModuleProject=true` is set in your module project

Problem: Type conflicts between modules

- Solution: Use `Directory.Packages.props` to ensure version consistency

Problem: Module not loading

- Solution: Check that the module name in `appsettings.json` matches the assembly name exactly

## Learn More

For detailed technical information about how the module loading system works, check out
the [Module System Architecture](../Documentations/Module%20System%20Architecture.md) documentation.

## Contributing

Contributions are welcome! Feel free to submit issues or pull requests to improve this library.

## License

This project is licensed under the MIT License.