# Conventions

Development guidelines, rules, and conventions for this repository. Follow these for all contributions.

## Pull requests

- **Target the `dev` branch.** Open all pull requests for development tasks against `dev`, not `main`. `main` receives changes from `dev` via release merges.
- Keep a PR focused on one logical unit of work.

## Versioning

Follow [Semantic Versioning 2.0.0](https://semver.org/) — `MAJOR.MINOR.PATCH`:

- **MAJOR** — incompatible / breaking API changes.
- **MINOR** — backwards-compatible new functionality.
- **PATCH** — backwards-compatible bug fixes.

The two packages are versioned independently: `InzDynamicModuleLoader.Core` and `InzDynamicModuleLoader.Abstractions` each own their `<Version>` (and `<AssemblyVersion>` / `<FileVersion>`) in their `.csproj`. Bump the version there and pass the matching tag as the publish workflow's `version_tag` input.

## Git commits

Use [Conventional Commits](https://www.conventionalcommits.org/): `<type>(<optional scope>): <subject>`.

- Common types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `build`, `ci`.
- Subject in imperative mood, no trailing period (e.g. `fix: guard against null FullName in module discovery`).
- Keep the subject line ≤ ~72 characters; use the body to explain the what and why when it isn't obvious.
- Align commits with SemVer above: `feat` → MINOR, `fix` → PATCH, and a `!` suffix or `BREAKING CHANGE:` footer → MAJOR.

## Coding standards & style

Match the style of the existing code:

- **Language / runtime:** C# on .NET 9.0, with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`. Honor nullable annotations; don't suppress warnings without cause.
- **Namespaces:** file-scoped (`namespace Foo.Bar;`).
- **Braces:** Allman style (opening brace on its own line). Single-line guard clauses may omit braces, e.g. `if (x is null) throw new ...;`, matching existing code.
- **Naming:** PascalCase for types, methods, and properties; `_camelCase` for private fields; camelCase for locals and parameters.
- **Modern C#:** prefer collection expressions (`[]`), `var` for locals, pattern matching, and expression-bodied members where they read cleanly — consistent with the current sources.
- **Access modifiers:** keep the public API surface minimal; use `internal` for implementation types (the test project reaches them via `InternalsVisibleTo`).
- **Guard clauses:** validate inputs early and throw with descriptive, context-rich messages.

## XML documentation

- Provide XML doc comments on public types and members, and on non-trivial internal ones — as the existing code does. Use `<summary>`, and `<param>` / `<returns>` / `<exception>` where they add clarity.
- Projects build with `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. `CS1591` (missing XML comment) is suppressed via `NoWarn`, but document the public API regardless, and keep docs accurate as code changes.
