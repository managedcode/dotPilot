# AGENTS.md

Project: Pilot.Core

Parent: `../../AGENTS.md`

## Purpose

- This project is the shared `.NET 10` class library for reusable production code.
- It exists so future UI and application layers can depend on one stable core package instead of duplicating shared logic.

## Entry Points

- `PilotCoreMarker` is the current bootstrap entry type for solution-level integration checks.
- Future public domain and application types should start here before higher-level UI layers depend on them.

## Boundaries

- In scope:
  - reusable production code
  - domain-safe abstractions and value types
  - logic that should be testable without UI hosting concerns
- Out of scope:
  - test-only helpers
  - UI-specific frameworks or rendering code
  - infrastructure that belongs in separate projects later
- Protected or high-risk areas:
  - public types and contracts that future projects will consume

## Project Commands

- `build`: `dotnet build src/Pilot.Core/Pilot.Core.csproj`
- `test`: `dotnet test --project tests/Pilot.Tests/Pilot.Tests.csproj`
- `format`: `dotnet format DotPilot.slnx --include src/Pilot.Core`
- `analyze`: `dotnet build src/Pilot.Core/Pilot.Core.csproj -warnaserror`

For this project:

- the active test framework is `TUnit`
- the runner model is `Microsoft.Testing.Platform`
- analyzer severity lives in the repo-root `.editorconfig`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-dotnet-features`
- `mcaf-testing`
- `mcaf-dotnet-tunit`
- `mcaf-dotnet-quality-ci`
- `mcaf-solid-maintainability`

## Local Constraints

- Stricter maintainability limits, if any:
  - `file_max_loc`: `300`
  - `type_max_loc`: `150`
  - `function_max_loc`: `40`
  - `max_nesting_depth`: `3`
- Required local docs:
  - update `docs/Architecture.md` when this project gains new public modules or cross-project contracts
- Local exception policy:
  - any exception must be documented in the nearest feature doc, ADR, or this file before merge

## Local Rules

- Keep `Pilot.Core` free of UI-specific dependencies until the solution architecture explicitly introduces them.
- Prefer small public types and explicit boundaries because this project will become a dependency root for the rest of the solution.
