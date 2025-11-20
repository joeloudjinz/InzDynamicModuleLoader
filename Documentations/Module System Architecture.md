# HexInz Module System Architecture

## Overview

The HexInz application implements a modular, plugin-based architecture that allows infrastructure adapters to be loaded dynamically at runtime.

## Table of Contents

- [Architecture Components](#architecture-components)
- [Module Loading Process](#module-loading-process)
- [Module Registration Process](#module-registration-process)
- [Module Initialization Process](#module-initialization-process)
- [Assembly Loading Context](#assembly-loading-context)
- [Dependency Resolution](#dependency-resolution)
- [Module Contract Interface](#module-contract-interface)
- [Configuration Management](#configuration-management)
- [Known Issues and Solutions](#known-issues-and-solutions)
- [Best Practices](#best-practices)

## Architecture Components

The module system is composed of several key components:

### Core Components

1. **ModuleLoader** - Responsible for loading module assemblies from disk
2. **ModuleRegistry** - Maintains references to loaded assemblies and module definitions
3. **ModuleInitializer** - Handles post-registration initialization of modules
4. **ModuleAssemblyLoadContext** - (Currently commented out) Custom assembly loading context
5. **ModuleManagerExtensions** - Provides extension methods for service registration and initialization

### Supporting Components

1. **IAmModule Interface** - Contract that defines the module interface
2. **AssemblyDependencyResolver** - Handles dependency resolution for modules

## Module Loading Process

### Entry Point
The module loading process begins when `ModuleManagerExtensions.RegisterModules()` is called, typically during application startup.

### Step-by-Step Process

1. **Configuration Reading**: The system reads module names from the `Modules` configuration section in `appsettings.json`.

2. **Module Path Resolution**: The system determines the module root directory by checking:
   - Production: `Modules` folder next to the executable (created by `CopyModulesToPublish` target)
   - Development: `BuiltModules` directory in the solution root, found by traversing parent directories

3. **Assembly Loading**: For each module:
   - Constructs the module path based on the structure `BuiltModules/{Name}/{Name}.dll`
   - Verifies the assembly file exists at the expected location
   - Creates an `AssemblyDependencyResolver` for the module's `.deps.json` file
   - Loads the assembly into the **DEFAULT** `AssemblyLoadContext` using `AssemblyLoadContext.Default.LoadFromAssemblyPath()`
   - Registers the loaded assembly with `ModuleRegistry`

4. **Dependency Resolution Setup**: Hook into the `AssemblyResolve` event to handle dependencies that aren't found in the default context.

### Key Implementation Details

```csharp
private static Assembly? ResolveDependencies(object? sender, ResolveEventArgs args)
{
    var assemblyName = new AssemblyName(args.Name);

    foreach (var resolver in DependencyResolvers)
    {
        var path = resolver.ResolveAssemblyToPath(assemblyName);
        if (path == null) continue;

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    }

    return null;
}
```

## Module Registration Process

### Service Registration Phase

After all modules are loaded, the system enters the registration phase:

1. **Module Definition Instantiation**: `ModuleRegistry.InstantiateModuleDefinitions()` is called to:
   - Iterate through all loaded assemblies
   - Find types that implement the `IAmModule` interface
   - Verify there's exactly one `IAmModule` implementation per assembly
   - Create instances of these implementations using `Activator.CreateInstance()`
   - Store the instances in the module definitions map

2. **Service Registration**: For each module implementation:
   - Calls the `RegisterServices(IServiceCollection, IConfiguration)` method
   - This allows modules to register their services with the dependency injection container
   - Passes the configuration to allow module-specific configuration

### Registry Management

The `ModuleRegistry` maintains two key collections:
- `LoadedAssemblies`: List of all loaded module assemblies
- `LoadedModuleDefinitions`: List of all instantiated module definitions

## Module Initialization Process

### Initialization Phase

After service registration is complete, modules undergo initialization:

1. **Initialization Method Call**: `ModuleInitializer.Initialize()` iterates through all loaded module definitions and calls:
   - `InitializeServices(IServiceProvider, IConfiguration)` method
   - This allows modules to perform post-registration initialization tasks
   - Provides access to the fully configured service provider

2. **Configuration Integration**: Each module receives the application configuration during initialization, allowing for runtime configuration of module behavior.

### Lifecycle Management

The initialization process allows modules to:
- Set up event handlers or background services
- Initialize connections or resources
- Configure module-specific behaviors
- Perform any setup required after all services are registered

## Assembly Loading Context

### Historical Context

The system previously used a custom `ModuleAssemblyLoadContext` to isolate module loading, but this caused type identity issues when modules referenced the same assemblies as the host application.

### Current Implementation

The system now loads all modules into the **default** `AssemblyLoadContext` to maintain type compatibility between the host application and loaded modules.

### Why This Approach Was Chosen

- **Type Identity**: Avoids runtime type casting failures when the same interface (like `IAmModule`) exists in multiple contexts
- **Service Compatibility**: Ensures services registered by modules are compatible with the host application
- **Simplified Architecture**: Reduces complexity by using the default context for all assemblies

## Dependency Resolution

### AssemblyDependencyResolver Integration

Each module has its own `AssemblyDependencyResolver` that:

- Reads the module's `.deps.json` file to understand its dependencies
- Determines the correct paths to dependency assemblies
- Allows the system to load module-specific dependencies correctly

### Shared Dependencies

- Common dependencies (like EF Core, ASP.NET Core) are resolved from the default context when already loaded
- This prevents duplicate assemblies in memory while maintaining compatibility
- Dependencies are loaded into the default context to ensure type compatibility

### Resolution Strategy

1. First, check if the assembly is already loaded in the default context
2. If not found, use the module's dependency resolver to locate it
3. If the dependency is found in the resolver, load it into the default context
4. Return null if the dependency cannot be resolved, causing a `FileNotFoundException`

## Module Contract Interface

### IAmModule Interface Definition

The system uses the `IAmModule` interface as a contract for all modules:

```csharp
public interface IAmModule
{
    IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration);
    IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration);
}
```

### Interface Location

The `IAmModule` interface is located in `HexInz.Infrastructure.Common` to ensure:
- Shared contract between host application and modules
- Type identity consistency across contexts
- Elimination of assembly loading conflicts

### Implementation Requirements

Each module must provide exactly one implementation of `IAmModule` that:
- Registers services during the registration phase
- Performs initialization during the initialization phase
- Handles configuration-specific logic in both phases

## Configuration Management

### Module Configuration

Modules are configured through the `appsettings.json` file using the `Modules` section:

```json
{
  "Modules": [
    "HexInz.Domain",
    "HexInz.Application.Contracts",
    "HexInz.Infrastructure.EF.MySQL"
  ]
}
```

### Configuration Integration

- Each module receives both the registration context (`IServiceCollection`) and runtime context (`IServiceProvider`)
- Configuration is passed to both registration and initialization phases
- Modules can access specific configuration sections relevant to their functionality

### Module Discovery

The system discovers modules by:
- Reading module names from configuration
- Constructing file paths based on the naming convention
- Validating that module assemblies exist before loading

## Known Issues and Solutions

### Type Identity Issue

**Problem**: Initial implementation used custom `AssemblyLoadContext` which caused type casting failures when the same interface existed in multiple contexts.

**Solution**: Move shared contracts to `HexInz.Infrastructure.Common` and load all modules into the default context.

**Documentation Reference**: See the Architectural Decision Record: "Resolving Type Identity in Modular Assembly Loading"

### Module Validation

**Problem**: Multiple `IAmModule` implementations in a single assembly could cause confusion.

**Solution**: The system enforces that each assembly contains exactly one `IAmModule` implementation.

### Dependency Resolution

**Problem**: Missing dependencies in module directories could cause runtime failures.

**Solution**: Use `CopyLocalLockFileAssemblies=true` to ensure all dependencies are included in module output directories.

## Best Practices

### Module Development

- Implement exactly one `IAmModule` interface per assembly
- Use `HexInz.Infrastructure.Common` as a reference for shared contracts
- Handle configuration gracefully in both registration and initialization phases
- Keep module initialization lightweight to avoid startup delays

### Dependency Management

- Ensure all module dependencies are copied to the output directory
- Use `Directory.Packages.props` for centralized package version management
- Avoid conflicting dependencies that could cause the diamond dependency problem
- Test modules in isolation to ensure they work correctly when loaded

### Error Handling

- Handle module loading failures gracefully to prevent application crashes
- Log detailed error messages when modules fail to load or initialize
- Implement proper fallback mechanisms when optional modules are not available
- Validate module assemblies exist before attempting to load them

### Performance Considerations

- Load only necessary modules to reduce startup time
- Optimize module initialization to minimize application startup delays
- Monitor memory usage as loaded modules remain in memory until application shutdown
- Consider module loading order and dependency relationships

## Architecture Benefits

### Flexibility
- Runtime configuration of infrastructure components
- Ability to swap database implementations at runtime
- Support for plugin-style architecture

### Maintainability
- Clear separation of concerns between modules
- Standardized module interface contract
- Simplified module addition and removal

### Scalability
- Modular design allows for independent module development
- Support for multiple persistence mechanisms simultaneously
- Clean extension points for new functionality

## Architecture Drawbacks

### Complexity
- Additional complexity in assembly loading and dependency resolution
- Requires careful attention to type identity and context management
- More complex debugging when modules fail to load or initialize

### Performance
- Potential for longer startup times with multiple modules
- Additional overhead from dependency resolution
- Memory overhead from loaded modules

This modular architecture enables HexInz to remain flexible and adaptable while maintaining clean separation of concerns between different infrastructure components.