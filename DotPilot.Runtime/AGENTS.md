# AGENTS.md

Project: `DotPilot.Runtime`
Stack: `.NET 10`, class library, provider-independent runtime services and diagnostics

## Purpose

- This project owns non-UI runtime implementations that sit behind the contracts in `DotPilot.Core`.
- It is the first landing zone for deterministic test clients, runtime probes, and later embedded-host integrations so the Uno app stays focused on presentation and startup composition.

## Entry Points

- `DotPilot.Runtime.csproj`
- `Features/RuntimeFoundation/*`
- `Features/HttpDiagnostics/DebugHttpHandler.cs`

## Boundaries

- Keep this project free of `Uno Platform`, XAML, and page/view-model logic.
- Implement feature slices against `DotPilot.Core` contracts instead of reaching back into the app project.
- Prefer deterministic runtime behavior and environment probing here so tests can exercise real flows without mocks.
- Keep external-provider assumptions soft: absence of Codex, Claude Code, or GitHub Copilot in CI must not break the provider-independent baseline.

## Local Commands

- `build-runtime`: `dotnet build DotPilot.Runtime/DotPilot.Runtime.csproj`
- `test-runtime`: `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter FullyQualifiedName~RuntimeFoundation`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-dotnet-features`
- `mcaf-testing`
- `mcaf-solid-maintainability`
- `mcaf-architecture-overview`

## Local Risks Or Protected Areas

- Runtime services introduced here will become composition roots for later Orleans and Agent Framework work, so keep boundaries explicit.
- Toolchain probing must stay deterministic and side-effect free; do not turn startup checks into live external calls.
