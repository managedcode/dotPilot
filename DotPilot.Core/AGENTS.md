# AGENTS.md

Project: `DotPilot.Core`
Stack: `.NET 10`, class library, non-UI contracts, orchestration, persistence, and shared application/domain code

## Purpose

- This project is the default non-UI home and must stay independent from the Uno presentation host.
- It owns shared application/domain code for agent-centric features until a slice becomes big enough to earn its own DLL.

## Entry Points

- `DotPilot.Core.csproj`
- `AgentBuilder/{Configuration,Models,Services}/*`
- `ChatSessions/{Commands,Configuration,Contracts,Diagnostics,Execution,Interfaces,Models,Persistence}/*`
- `ControlPlaneDomain/{Identifiers,Contracts,Models,Policies}/*`
- `HttpDiagnostics/DebugHttpHandler.cs`
- `Providers/{Configuration,Infrastructure,Interfaces,Models,Services}/*`
- `Settings/Models/*`
- `Workspace/{Interfaces,Services}/*`

## Boundaries

- Keep this project free of `Uno Platform`, XAML, brushes, and page/view-model concerns.
- Keep app-shell, app-host, and application-configuration types out of this project; those belong in `DotPilot`.
- Organize code by vertical feature slice, not by shared horizontal folders such as generic `Services` or `Helpers`.
- `DotPilot.Core` is the default non-UI home, not a permanent dumping ground: when a feature becomes large enough to justify its own architectural boundary, extract it into a dedicated DLL that references `DotPilot.Core`
- do not introduce a generic `Runtime` naming layer inside this project or split code out into a vaguely named runtime assembly unless the user explicitly asks for that boundary; keep non-UI logic in explicit feature slices under `DotPilot.Core`
- when a feature is extracted out of `DotPilot.Core`, keep `DotPilot.Core` as the abstraction/shared-contract layer and make the desktop app reference the new feature DLL explicitly
- do not leave extracted subsystem contracts half-inside `DotPilot.Core`; when a future subsystem is split into its own DLL, its feature-facing interfaces and implementation seams should move with it
- keep feature-specific heavy infrastructure out of this project once it becomes its own subsystem; `DotPilot.Core` should stay cohesive instead of half-owning an extracted runtime
- Do not collect unrelated code under an umbrella directory such as `AgentSessions`; split session, workspace, settings, providers, and host code into explicit feature roots when the surface grows.
- Keep `ControlPlaneDomain` explicit too: identifiers belong under `Identifiers`, participant/provider/session DTOs under `Contracts`, cross-flow state under `Models`, and policy shapes under `Policies` instead of leaving one flat dump.
- Keep contract-centric slices explicit inside each feature root: commands live under `Commands`, public DTO shapes live under `Contracts`, public service seams live under `Interfaces`, state records or enums live under `Models`, diagnostics under `Diagnostics`, and persistence under `Persistence`.
- When a slice exposes `Commands` and `Results`, use the solution-standard `ManagedCode.Communication` primitives instead of hand-rolled command/result record types.
- Keep the top level readable as two kinds of folders:
  - shared/domain folders such as `ControlPlaneDomain`, `Settings`, and `Workspace`
  - operational/system folders such as `AgentBuilder`, `ChatSessions`, `Providers`, and `HttpDiagnostics`
- keep this structure SOLID at the folder and project level too: cohesive feature slices stay together, but once a slice becomes too large or too independent, it should graduate into its own project instead of turning `DotPilot.Core` into mud
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

- This project is now the full non-UI stack, so naming drift or folder chaos will spread quickly across contracts, providers, persistence, and runtime logic.
- Avoid baking UI assumptions into this project, and avoid baking low-level CLI-process details into the contract-facing folders when provider/session terms are enough.
