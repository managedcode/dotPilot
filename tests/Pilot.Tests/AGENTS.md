# AGENTS.md

Project: Pilot.Tests

Parent: `../../AGENTS.md`

## Purpose

- This project contains automated tests for `Pilot.Core` and future solution modules that adopt `TUnit`.
- It exists to keep verification separate from production code while using one consistent test framework and runner model.

## Entry Points

- `PilotCoreBootstrapTests` is the current bootstrap verification entry point.
- Future test fixtures, data builders, and test-session hooks belong under this project.

## Boundaries

- In scope:
  - automated tests
  - test-only helpers and fixtures
  - coverage and runner extensions needed for verification
- Out of scope:
  - production logic that belongs in `Pilot.Core` or other future projects
  - additional test frameworks
- Protected or high-risk areas:
  - package references and runner configuration that define the repo-wide test contract

## Project Commands

- `build`: `dotnet build tests/Pilot.Tests/Pilot.Tests.csproj`
- `test`: `dotnet test --project tests/Pilot.Tests/Pilot.Tests.csproj`
- `format`: `dotnet format DotPilot.slnx --include tests/Pilot.Tests`
- `analyze`: `dotnet build tests/Pilot.Tests/Pilot.Tests.csproj -warnaserror`

For this project:

- the active test framework is `TUnit`
- the runner model is `Microsoft.Testing.Platform`
- analyzer severity lives in the repo-root `.editorconfig`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-testing`
- `mcaf-dotnet-tunit`
- `mcaf-dotnet-quality-ci`
- `mcaf-dotnet-complexity`

## Local Constraints

- Stricter maintainability limits, if any:
  - `file_max_loc`: `300`
  - `type_max_loc`: `150`
  - `function_max_loc`: `40`
  - `max_nesting_depth`: `3`
- Required local docs:
  - update `docs/Architecture.md` when test structure changes in a way that affects solution navigation
- Local exception policy:
  - any exception must be documented in the nearest feature doc, ADR, or this file before merge

## Local Rules

- Use `TUnit` only. Do not add xUnit, NUnit, MSTest, or mixed-framework adapters.
- Keep tests focused on public behavior and cross-project contracts rather than re-implementing production logic in the test project.
