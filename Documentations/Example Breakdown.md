# Inz Dynamic Module Loader Example

## Table of Contents

- [Overview](#overview)
- [Structure](#structure)
    - [Core Shared Components](#core-shared-components)
    - [Database Provider Implementations](#database-provider-implementations)
    - [Common EF Core Repository](#common-ef-core-repository)
    - [Web Startup Project](#web-startup-project)
    - [Console Startup Project](#console-startup-project)
- [How to run the example](#how-to-run-the-example)
    - [Prerequisites](#prerequisites)
    - [Create The Databases](#create-the-databases)
    - [Update Database](#update-database)
    - [Update WebStartup Configuration](#update-webstartup-configuration)
    - [Build](#build)
    - [RUN](#run)
- [Console Application Alternative](#console-application-alternative)
    - [Console Startup Features](#console-startup-features)
    - [Console Startup Configuration](#console-startup-configuration)
    - [How to run the Console Example](#how-to-run-the-console-example)
    - [Console Example Usage](#console-example-usage)

## Overview

This example demonstrates the dynamic loading capabilities of the InzDynamicModuleLoader system by showcasing a real-world scenario where database
infrastructure can be switched at startup without code changes. The example project includes multiple database provider implementations (MySQL and
PostgreSQL) that can be loaded dynamically based on configuration settings. This architecture enables developers to build flexible applications where
infrastructure concerns can be swapped out. The example includes a common module with shared contracts, data models, and repositories, along with
separate modules for each database provider implementation, demonstrating how the system maintains clean separation of concerns while enabling
flexibility.

## Structure

The example project structure demonstrates modular architecture with the following key components:

### Core Shared Components

```
Example.Module.Common/
├── Example.Module.Common.csproj              # Shared contracts and entities project file
├── Configurations/
│   └── DatabaseConfigOptions.cs             # Configuration options for database connection
├── Contracts/
│   ├── IDataContext.cs                      # Data context interface
│   ├── ITestRepository.cs                   # Repository interface
│   └── IUnitOfWork.cs                       # Unit of work interface
└── Data/
    └── TestEntity.cs                        # Shared data entity
```

### Database Provider Implementations

```
Example.Module.EFCore.MySQL/
├── Example.Module.EFCore.MySQL.csproj       # MySQL module project file
├── MySqlDataContext.cs                      # MySQL-specific data context
├── MySqlDesignTimeFactory.cs                # Design time factory for migrations
└── MySQLEntityFrameworkModule.cs            # MySQL module implementation of IAmModule

Example.Module.EFCore.PostgreSQL/
├── Example.Module.EFCore.PostgreSQL.csproj  # PostgreSQL module project file
├── PostgreSqlDataContext.cs                 # PostgreSQL-specific data context
├── PostgreSqlDesignTimeFactory.cs           # Design time factory for migrations
└── PostgreSQLEntityFrameworkModule.cs       # PostgreSQL module implementation of IAmModule
```

### Common EF Core Repository

```
Example.Module.EFCore.Repositories/
├── Example.Module.EFCore.Repositories.csproj # EF Core repositories module project file
├── EntityFrameworkRepositoriesModule.cs     # EF Core repository module implementation
├── IEntityFrameworkCoreDbContext.cs         # EF Core-specific DB context interface
├── TestEntityConfiguration.cs               # Entity configuration
└── TestRepository.cs                        # Test repository implementation
```

### Web Startup Project

```
Example.Module.WebStartup/
├── Example.Module.WebStartup.csproj         # Web startup project file
├── Program.cs                              # Main application entry point
├── appsettings.json                        # Configuration file
└── appsettings.Development.json            # Development-specific configuration
```

### Console Startup Project

```
Example.Module.ConsoleStartup/
├── Example.Module.ConsoleStartup.csproj     # Console startup project file
├── Program.cs                              # Main application entry point
```

## How to run the example

### Prerequisites

1. Install and run both MySQL and PostgreSQL database servers on your local machine
2. Ensure you have the .NET SDK 9.0 or higher installed
3. Install the EF Core tools by running `dotnet tool install --global dotnet-ef`

### Create The Databases

Create 2 databases, the first one using MySQL and the other one using PostgreSQL. Make sure you have both of these database managers installed on your
local machine.

### Update Database

First, run the following commands to configure user secrets for EF Core migrations:
For MySQL:

```shell
dotnet user-secret init -p Example.Module.EFCore.MySQL
dotnet user-secret set 'ConnectionStrings:MySQlConnectionString' '[your-mysql-connection-string]' -p Example.Module.EFCore.MySQL
```

For PostgreSQL:

```
dotnet user-secret init -p Example.Module.EFCore.PosgreSQL
dotnet user-secret set 'ConnectionStrings:PgSQlConnectionString' '[your-connection-string]' -p Example.Module.EFCore.PostgreSQL
```

This is important in order for EF tools to get the correct connection string for the next commands:
For MySQL:

```shell
dotnet ef database update -p Example.Module.EFCore.MySQL
```

For PostgreSQL:

```shell
dotnet ef database update -p Example.Module.EFCore.PostgreSQL
```

This will update the database created earlier with the required tables.

### Update WebStartup Configuration

For MySQL, update the `appsettings.json` to enable EF Core MySQL adapter along with the EF Core module:

```json
{
  "Modules": [
    "Example.Module.EFCore.MySQL",
    "Example.Module.EFCore.Repositories"
  ]
}
```

Then update the database connection string:

```json
{
  "Database": {
    "ConnectionString": "Host=localhost;Port=port;Database=your-db;Username=your-user;Password=your-pass"
  }
}
```

For PostgreSQL, update the `appsettings.json` to enable EF Core PostgreSQL adapter along with the EF Core module:

```json
{
  "Modules": [
    "Example.Module.EFCore.PostgreSQL",
    "Example.Module.EFCore.Repositories"
  ]
}
```

Then update the database connection string:

```json
{
  "Database": {
    "ConnectionString": "Host=localhost;Port=port;Database=your-db;Username=your-user;Password=your-pass"
  }
}
```

### Build

It is important that you run the build command before running the example web project to ensure all modules are properly compiled and deployed to the
`BuiltModules` directory:

```shell
dotnet build
```

This step ensures that the Directory.Build.targets configuration properly copies all required module files and dependencies to the modules output
directory.

### RUN

Now you can run the web app from your IDE or from the command line using:

```shell
dotnet run --project Example.Module.WebStartup
```

The application will dynamically load the specified modules based on the configuration, register services, and perform a database test by creating,
retrieving, and deleting a test entity, demonstrating the runtime flexibility of the modular architecture.

## Console Application Alternative

The example also includes a console application alternative that demonstrates dynamic module loading in a console application context. The console application uses environment variables and user secrets for configuration instead of appsettings.json files.

### Console Startup Features

- Programmatically sets environment variables to specify which modules to load
- Uses user-secrets for sensitive configuration like connection strings
- Demonstrates dynamic module loading in a console application context

### Console Startup Configuration

The console application programmatically sets environment variables for module configuration at startup:

```csharp
// Set environment variable for modules to load (comma-separated string format)
Environment.SetEnvironmentVariable("Modules__0", "Example.Module.EFCore.MySQL");
Environment.SetEnvironmentVariable("Modules__1", "Example.Module.EFCore.Repositories");
```

Configure your database connection string using user secrets:

```bash
# For MySQL
dotnet user-secrets set "Database:ConnectionString" "your-mysql-connection-string" -p Example.Module.ConsoleStartup

# For PostgreSQL
dotnet user-secrets set "Database:ConnectionString" "your-postgresql-connection-string" -p Example.Module.ConsoleStartup
```

### How to run the Console Example

1. Ensure the required database (MySQL or PostgreSQL) is running
2. Configure the connection string using user secrets
3. Run the application:

```bash
# Build all projects first
dotnet build

# Run the console application
dotnet run --project Example.Module.ConsoleStartup
```

### Console Example Usage

The console application has the module configuration set programmatically in the code. To run with different database providers, you can modify the environment variable settings in the Program.cs file or use user secrets to configure the database connection:

```bash
# Example for MySQL
dotnet user-secrets set "Database:ConnectionString" "Server=localhost;Database=testdb;Uid=user;Pwd=password;" -p Example.Module.ConsoleStartup
dotnet run --project Example.Module.ConsoleStartup

# Example for PostgreSQL
dotnet user-secrets set "Database:ConnectionString" "Host=localhost;Database=testdb;Username=user;Password=password;" -p Example.Module.ConsoleStartup
dotnet run --project Example.Module.ConsoleStartup
```