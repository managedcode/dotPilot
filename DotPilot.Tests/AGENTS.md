# AGENTS.md

Project: `DotPilot.Tests`
Stack: `.NET 10`, `NUnit`, `FluentAssertions`, `coverlet.collector`

## Purpose

- This project owns automated unit-level verification for the `DotPilot` app project.
- It should validate caller-visible behavior and stable application contracts without introducing test-only abstractions that hide real behavior.

## Entry Points

- `DotPilot.Tests.csproj`
- `AppInfoTests.cs`

## Boundaries

- Keep tests focused on behavior that can run reliably in-process.
- Do not move browser, driver, or end-to-end smoke concerns into this project; those belong in `DotPilot.UITests`.
- Prefer production-facing flows and public contracts over implementation-detail assertions.

## Local Commands

- `test`: `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`
- `coverage`: `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --collect:"XPlat Code Coverage"`
- `build`: `dotnet build DotPilot.Tests/DotPilot.Tests.csproj`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-testing`

## Local Risks Or Protected Areas

- The current unit-test surface is thin, so new production behavior should raise coverage rather than relying on the existing baseline.
- Keep assertions meaningful; do not add placeholder tests that only prove object construction with no behavioral value.
