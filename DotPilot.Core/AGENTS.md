# AGENTS.md

Project: `DotPilot.Core`
Stack: `.NET 10`, class library, non-UI contracts, providers, persistence, orchestration, and local-host infrastructure

## Purpose

- This project owns all non-UI code and must stay independent from the Uno presentation host.
- It contains both contract-shaped folders and operational/system folders, but they all live in one project by design.

## Entry Points

- `DotPilot.Core.csproj`
- `AgentBuilder/{Configuration,Models,Services}/*`
- `ChatSessions/{Commands,Configuration,Contracts,Diagnostics,Execution,Interfaces,Models,Persistence}/*`
- `ControlPlaneDomain/{Identifiers,Contracts,Models,Policies}/*`
- `HttpDiagnostics/DebugHttpHandler.cs`
- `LocalAgentHost/{Composition,Configuration,Grains}/*`
- `Providers/{Configuration,Infrastructure,Interfaces,Models,Services}/*`
- `Settings/Models/*`
- `Workspace/{Interfaces,Services}/*`

## Boundaries

- Keep this project free of `Uno Platform`, XAML, brushes, and page/view-model concerns.
- Keep app-shell, app-host, and application-configuration types out of this project; those belong in `DotPilot`.
- Organize code by vertical feature slice, not by shared horizontal folders such as generic `Services` or `Helpers`.
- Do not create a second non-UI project for control-plane logic; stack providers, persistence, host code, and orchestration directly in `DotPilot.Core`.
- Do not collect unrelated code under an umbrella directory such as `AgentSessions`; split session, workspace, settings, providers, and host code into explicit feature roots when the surface grows.
- Keep `ControlPlaneDomain` explicit too: identifiers belong under `Identifiers`, participant/provider/session DTOs under `Contracts`, cross-flow state under `Models`, and policy shapes under `Policies` instead of leaving one flat dump.
- Keep contract-centric slices explicit inside each feature root: commands live under `Commands`, public DTO shapes live under `Contracts`, public service seams live under `Interfaces`, state records or enums live under `Models`, diagnostics under `Diagnostics`, persistence under `Persistence`, and host/grain seams under `LocalAgentHost`.
- Keep the top level readable as two kinds of folders:
  - shared/domain folders such as `ControlPlaneDomain`, `Settings`, and `Workspace`
  - operational/system folders such as `AgentBuilder`, `ChatSessions`, `Providers`, `LocalAgentHost`, and `HttpDiagnostics`
- Keep provider-independent testing seams real and deterministic so CI can validate core flows without external CLIs.

## Local Commands

- `build-core`: `dotnet build DotPilot.Core/DotPilot.Core.csproj`
- `test-core`: `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-dotnet-features`
- `mcaf-testing`
- `mcaf-solid-maintainability`
- `mcaf-architecture-overview`

## Local Risks Or Protected Areas

- This project is now the full non-UI stack, so naming drift or folder chaos will spread quickly across contracts, providers, persistence, and host logic.
- Avoid baking UI assumptions into this project, and avoid baking low-level CLI-process details into the contract-facing folders when provider/session terms are enough.
