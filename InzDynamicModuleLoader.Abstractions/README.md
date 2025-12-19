# Abstractions for InzSoftwares - Net Dynamic Module Loader

Abstractions for [InzSoftwares.NetDynamicModuleLoader](https://www.nuget.org/packages/InzSoftwares.NetDynamicModuleLoader), a .NET 9.0 library that
enables plugin-based architecture by loading modules at startup time. This allows for better separation of concerns, module isolation, and flexible
infrastructure switching while maintaining clean architecture boundaries.

## IAmModule Interface Explained

The `IAmModule` interface has two methods:

- **RegisterServices**: Called first, registers services with the dependency injection container
- **InitializeServices**: Called after registration, allows for service initialization and configuration