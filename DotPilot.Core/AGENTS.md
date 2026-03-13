# AGENTS.md

Project: `DotPilot.Core`
Stack: `.NET 10`, class library, feature-aligned contracts and provider-independent runtime foundations

## Purpose

- This project owns non-UI contracts, typed identifiers, and feature slices that must stay independent from the Uno presentation host.
- It provides the stable public shapes for runtime, orchestration, providers, and shell configuration so UI and future runtime implementations can evolve without circular coupling.

## Entry Points

- `DotPilot.Core.csproj`
- `Features/ApplicationShell/AppConfig.cs`
- `Features/ControlPlaneDomain/*`
- `Features/RuntimeCommunication/*`
- `Features/RuntimeFoundation/*`

## Boundaries

- Keep this project free of `Uno Platform`, XAML, brushes, and page/view-model concerns.
- Organize code by vertical feature slice, not by shared horizontal folders such as generic `Services` or `Helpers`.
- Prefer stable contracts, typed identifiers, and public interfaces here; concrete runtime integrations can live in separate libraries.
- Keep provider-independent testing seams real and deterministic so CI can validate core flows without external CLIs.

## Local Commands

- `build-core`: `dotnet build DotPilot.Core/DotPilot.Core.csproj`
- `test-core`: `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter FullyQualifiedName~RuntimeFoundation`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-dotnet-features`
- `mcaf-testing`
- `mcaf-solid-maintainability`
- `mcaf-architecture-overview`

## Local Risks Or Protected Areas

- These contracts will become shared dependencies across future slices, so naming drift or unclear boundaries will amplify quickly.
- Avoid baking provider-specific assumptions into the core runtime contracts unless an ADR or feature spec explicitly requires them.
