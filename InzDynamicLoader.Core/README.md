# Inz Dynamic Module Loader

NetDynamicModuleLoader is a .NET 9.0 package that has robust dynamic module loading capabilities which enables registered modules in the configuration
layer to be loaded at runtime, ensuring a better separation of concerns and module isolation while maintaining clean architecture boundaries.

## Features

- Robust plugin-based architecture with runtime loading of modules ensuring complete isolation
- Configuration-driven module loading from `appsettings.json` with dynamic dependency resolution
- Modules implement the `IAmModule` interface to register services with dependency injection container
- Support for module initialization after service registration ensuring proper startup sequence
- Each module maintains clear separation of concerns and can provide its own infrastructure implementations

## Module Loading Process

TODO generate short paragraph talking about the module loading process briefly then point to the main article like below
Learn more about the modules loading logic from the article [Module System Architecture](../Documentations/Module%20System%20Architecture.md)

## Technical Details

TODO generate short paragraph talking about the technical details of the full package

## How To Use

### Install NetDynamicModuleLoader

You will have to install the package into one of your .NET projects in your solution

```shell
  dotnet add package InzSoftwares.NetDynamicModuleLoader
```

### Define The Build Targets

So that your startup project can figure out all the dependencies required by all the dynamically modules run properly, create a
`Directory.Build.targets` file with the following content:

```msbuild

<Project>
    <PropertyGroup Condition="'$(IsModuleProject)' == 'true'">
        <!-- 
            This property forces MSBuild to copy all assemblies from the project's dependency graph (including NuGet packages) into 
            the output directory (e.g., bin/Debug/netX.X) on a normal build.
            This ensures that when a module is loaded dynamically, all its dependencies are present in the same directory.
        -->
        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>

        <!-- Explicitly ensure lock file assemblies are copied -->
        <GenerateDependencyFile>true</GenerateDependencyFile>
    </PropertyGroup>

    <!-- 
       Define a Custom Target that runs after the build is finished.
       It copies the output to: {SolutionRoot}/BuiltModules/{ProjectName}/
    -->
    <Target Name="DeployModuleToUnifiedFolder" AfterTargets="Build" Condition="'$(IsModuleProject)' == 'true'">
        <PropertyGroup>
            <!-- Calculate the destination path -->
            <UnifiedModulePath>$(MSBuildThisFileDirectory)BuiltModules/$(MSBuildProjectName)/</UnifiedModulePath>
        </PropertyGroup>

        <ItemGroup>
            <!-- Select all files in the build output directory -->
            <ModuleFiles Include="$(OutputPath)**/*.*"/>
        </ItemGroup>

        <!-- Log a message to the console so you know it's working -->
        <Message Text="[Module Deployment] Copying $(MSBuildProjectName) to $(UnifiedModulePath)" Importance="High"/>

        <!-- Perform the copy -->
        <Copy SourceFiles="@(ModuleFiles)"
              DestinationFolder="$(UnifiedModulePath)%(RecursiveDir)"
              SkipUnchangedFiles="true"/>
    </Target>
</Project>
```

This logic will make sure that each dynamically loaded module will include all its dependencies with it, hence the tag `CopyLocalLockFileAssemblies`,
and also include the `deps.json` file, hence the tag `GenerateDependencyFile`.
The custom target logic will create a dedicated directory called `BuiltModules` in the solution directory so each dynamically loaded module's
dependencies can be found and resolved correctly by the NetDynamicModuleLoader.

Read more about this type of file and its content in the following [documentation](../Documentations/Directory.Build.targets%20Documentation.md)

### Create Your First Module

Now create a new .NET project in your solution, it can be a simple class library project with `OutputType` as `Library`.
In `.csproj` of the new module, add the following tag in the `PropertyGroup` section:

```msbuild 
<!-- Triggers the logic in Directory.Build.props -->
<IsModuleProject>true</IsModuleProject>
```

### Register & Initialize Services

If your module should register (using `IServiceCollection`) or initialize (using `IServiceProvider`) services (e.g. EF Core with MySQL),
create a class that implements `IAmModule` and implement the methods as needed.

```csharp
public class MySuperDooperModule : IAmModule
{
   public IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration)
   {
      // Use configuration to get config options
      // Use services to rgister services into the DI
      return services;
   }
   
   public IServiceProvider InitializeServices(IServiceProvider services, IConfiguration configuration)
   {
      // Use configuration to get config options
      // Use service to initialize services
      return services;
   }
}
```

### Update Startup Project

In your startup project, define your modules list in the configuration file (e.g. appsettings.json) like the following:

```json
{
  "Modules": [
    "MySuperDooperModule",
    "MyOtherSuperDooperModule"
  ]
}
```

**_It is important to list all the modules that should be loaded dynamically in the `Modules` list except the startup module._**

In the `Program.cs` of your startup project, call the extension methods so the modules are loaded dynamically and services get registered and
initialized.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterModules(configuration);
var app = builder.Build();
app.Services.InitializeModules(configuration);
```

### Avoiding "Diamond Dependency" problem

Since each dynamically loaded module can have its own dependencies with specific versions, and these modules alongside their dependencies will be
loaded into the main context of the startup project, it is possible to face the issue where multiple modules might require the same dependency, like
`Newtonsoft.Json`, with different versions. During loading time, there will be a version override and one of the dynamically loaded modules which
requires the overriden version of the dependency might during runtime.
To avoid this problem and be able to manage versions in the same place for all the dynamically loaded modules, define a file named
`Directory.Packages.props` in the solution directory with the following content structure:

```msbuild

<Project>
    <!--  .NET Central Package Management.  -->
    <!--  This file used to avoid the "Diamond Dependency" problem  -->
    <!--  
        Since everything loads into the Default context, version conflicts are fatal.
            - Scenario: Module A uses Newtonsoft.Json v13. Module B uses Newtonsoft.Json v9.
            - Result: The first one loaded wins. The second module will likely crash with MethodNotFoundException at runtime.
            - Mitigation: You must enforce strict version alignment across all modules.
    -->
    <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    </PropertyGroup>

    <!-- Define versions ONCE for the whole solution -->
    <ItemGroup>
        <!-- Core Dependencies Example -->
        <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0"/>
        <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="9.0.0"/>

        <!-- Third Party Dependencies Example -->
        <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.0"/>
        <PackageVersion Include="Pomelo.EntityFrameworkCore.MySql" Version="9.0.0"/>

        <!-- Test Dependencies Example -->
        <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0"/>
        <PackageVersion Include="xunit" Version="2.9.2"/>
        <PackageVersion Include="FluentAssertions" Version="8.8.0"/>

        <!-- Other external dependencies -->
    </ItemGroup>
</Project>
```

Read more about this type of file and its content in the following [documentation](../Documentations/Directory.Packages.props%20Documentation.md)

In `.csproj` file of your .NET projects in your solution, you can just specify the required package without the version:

```msbuild

<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions"/>
```