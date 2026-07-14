# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> Development conventions — branching (PRs against `dev`), Semantic Versioning, Conventional Commits, code style, and XML documentation — live in [`Conventions.md`](Conventions.md). Follow them for all changes.

## What this is

A .NET 9.0 library (shipped as two NuGet packages) that gives a host application a plugin architecture: modules are compiled to standalone assemblies, discovered on disk at startup, and registered into the host's DI container based on configuration. The headline use case is swapping infrastructure (e.g. MySQL vs PostgreSQL) without recompiling the host — you change a config list, not code.

## Common commands

Run from the repository root.

```shell
dotnet build                                   # builds the solution AND deploys every module project into BuiltModules/
dotnet test                                    # runs the xUnit suite (InzDynamicModuleLoader.UnitTests)
dotnet test --filter "FullyQualifiedName~InstantiateModuleDefinitions_SingleValidModule_AddsModuleToLoadedDefinitions"   # single test
dotnet run --project Example.Module.WebStartup      # run the web example (reads Modules from appsettings.json)
dotnet run --project Example.Module.ConsoleStartup  # run the console example (sets Modules via env vars in Program.cs)
dotnet pack InzDynamicModuleLoader.Core/InzDynamicModuleLoader.Core.csproj --configuration Release --output ./nupkg
```

**You must `dotnet build` the solution before running any startup app.** Host startup projects do not reference the module projects — modules are found at runtime by scanning `BuiltModules/`, which only gets populated as a build side effect (see Build system below). Running a host against a stale/empty `BuiltModules/` throws `DirectoryNotFoundException` or "No modules were loaded".

EF Core migrations for the example providers (requires `dotnet tool install --global dotnet-ef` and user-secrets set — see `Documentations/Example Breakdown.md`):

```shell
dotnet ef database update -p Example.Module.EFCore.MySQL
dotnet ef database update -p Example.Module.EFCore.PostgreSQL
```

## Architecture

### Two packages, deliberate split

- **`InzDynamicModuleLoader.Abstractions`** — contains only `IAmModule`. A *module* project references this and nothing else from the library. Keeping the contract in a tiny separate package means modules don't take a dependency on the loader internals.
- **`InzDynamicModuleLoader.Core`** — the loader engine. A *host* application references this. Depends on Abstractions.

Watch the namespaces: `IAmModule` lives in `InzDynamicModuleLoader.Abstractions`, while the host-facing extension methods live in `InzDynamicModuleLoader.Core`. (The README's module snippet showing `using InzDynamicLoader.Core;` for `IAmModule` is inaccurate — module classes import `InzDynamicModuleLoader.Abstractions`.)

### The module contract — `IAmModule`

Two methods mirroring a two-phase lifecycle:
- `RegisterServices(IServiceCollection, IConfiguration)` — add the module's services to DI.
- `InitializeServices(IServiceProvider, IConfiguration)` — post-registration hook (open connections, warm caches, etc.).

### Host API — `InzDynamicLoaderExtensions`

The entire public surface is two extension methods:
- `IServiceCollection.RegisterModules(configuration)` — reads the `Modules` config array, loads each assembly, then calls every module's `RegisterServices`. Also registers the `ModuleManagerService` as a singleton so phase two can find it.
- `IServiceProvider.InitializeModules(configuration)` — retrieves that singleton and calls every module's `InitializeServices`.

Call `RegisterModules` at service-registration time and `InitializeModules` after the provider is built. See `Example.Module.WebStartup/Program.cs` and `Example.Module.ConsoleStartup/Program.cs`.

### Loading engine — `ModuleManagerService` (internal)

The heart of the system. Key behaviors, all in `InzDynamicModuleLoader.Core/ModuleManagerService.cs`:

- **Module path convention:** a module named `X` in the `Modules` config array is loaded from `{root}/X/X.dll`. The config name must match the assembly/dll name *exactly*.
- **Where `{root}` is** (`GetModulesRootDirectory`): production = a `Modules/` folder next to the executable; development = walks up to 6 parent directories looking for `BuiltModules/`. This dual lookup is why the same build runs from `bin/Debug/...` in dev and a flat deploy in prod.
- **Discovery rule** (`InstantiateModuleDefinitions`): each module assembly must contain **exactly one** concrete (non-interface, non-abstract) `IAmModule` implementation. Zero → warning + skip; more than one → throws.
- **Assemblies load into `AssemblyLoadContext.Default`**, not into isolated load contexts. There is no unloading and no type-level isolation between modules. "Isolation" in this project means *dependency-version* isolation via per-module resolvers (below), not ALC isolation — keep this in mind before assuming plugins can be hot-swapped or that two modules can load conflicting versions of the same type.

### Dependency resolution

Because modules pull in their own transitive dependencies, `ModuleManagerService` hooks `AppDomain.CurrentDomain.AssemblyResolve` and resolves via a per-module `AssemblyDependencyResolver` (built from each module's `deps.json`). The resolve path is optimized in four steps: skip `.resources` assemblies, check a `ConcurrentDictionary` cache, try the *requesting* assembly's own resolver first (locality), then linear-scan all resolvers as a fallback. This is the reason each module must be deployed *with* its full dependency set and `deps.json`.

### Build system is load-bearing — `Directory.Build.targets`

The root `Directory.Build.targets` is what makes modules deployable, and every module project opts in with `<IsModuleProject>true</IsModuleProject>` in its `.csproj`. For those projects it:
1. Sets `CopyLocalLockFileAssemblies=true` and `GenerateDependencyFile=true` so the full dependency closure + `deps.json` land in the output folder.
2. Runs an `AfterBuild` target that copies the output into `BuiltModules/{ProjectName}/`.

So the workflow to add a new module is: create a class library, set `IsModuleProject=true`, implement one `IAmModule`, and build — deployment to `BuiltModules/` is automatic. `BuiltModules/` is generated output.

### Diamond-dependency management

When multiple modules need the same package at different versions, only one can win at runtime. The intended fix (documented for consumers, see `Documentations/Directory.Packages.props Documentation.md`) is Central Package Management via a `Directory.Packages.props` at the solution root pinning shared versions. This repo's example does not currently include one.

## Testing notes

- xUnit. `InzDynamicModuleLoader.Core/AssemblyInfo.cs` exposes internals to the test project via `InternalsVisibleTo`, so tests can construct `ModuleManagerService` directly.
- Tests synthesize module assemblies in-memory with `System.Reflection.Emit` (see the helpers in `ModuleManagerServiceTests.cs`) rather than loading files from disk — this is how discovery edge cases (multiple implementations, interfaces/abstracts, generic types with null `FullName`) are exercised. Coverage currently centers on `InstantiateModuleDefinitions`.

## The example (`Example.*` projects)

A runnable demonstration of infra-switching, not part of the shipped packages:
- `Example.Module.Common` — shared contracts/entities/config, referenced by the other example projects.
- `Example.Module.EFCore.MySQL` / `Example.Module.EFCore.PostgreSQL` — interchangeable provider modules.
- `Example.Module.EFCore.Repositories` — repository implementations module.
- `Example.Module.WebStartup` / `Example.Module.ConsoleStartup` — host apps. Switch the database provider by editing the `Modules` array in `appsettings.json` (web) or the env-var assignments in `Program.cs` (console) — no code changes to the host logic.

## Publishing

Two manual (`workflow_dispatch`) GitHub Actions — `.github/workflows/publish-nuget.yml` (Core) and `publish-abstractions-nuget.yml` (Abstractions) — build in Release and push to NuGet.org via trusted publishing (OIDC). The published version comes from the workflow's `version_tag` input and is independent of the `<Version>` in the `.csproj`, so bump both when releasing.
