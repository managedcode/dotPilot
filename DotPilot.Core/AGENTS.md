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
- `Identifiers/*`
- `Contracts/*`
- `Models/*`
- `Policies/*`
- `HttpDiagnostics/DebugHttpHandler.cs`
- `Providers/{Configuration,Infrastructure,Interfaces,Models,Services}/*`
- `Workspace/{Interfaces,Services}/*`

## Boundaries

- Keep this project free of `Uno Platform`, XAML, brushes, and page/view-model concerns.
- Keep app-shell, app-host, and application-configuration types out of this project; those belong in `DotPilot`.
- Do not keep UI-only preference models or shell interaction settings here; if a setting exists only to control presentation behavior such as composer key handling, it belongs in `DotPilot`.
- Do not add cache-specific abstractions, services, or snapshot layers here by default; when the user explicitly asks for a cache boundary, keep it small, runtime-owned, and tied to one real source of truth instead of creating broad mirror layers.
- Provider readiness is now an explicit runtime-owned cache boundary: keep one startup snapshot/cache for provider CLI metadata and readiness in `DotPilot.Core`, and invalidate it only on explicit refresh, provider-setting changes, or the next app start.
- Do not introduce fabricated role enums, hardcoded tool catalogs, skill catalogs, or encoded capability tags for agents unless the product has a real backing registry and runtime implementation for them.
- Organize code by vertical feature slice, not by shared horizontal folders such as generic `Services` or `Helpers`.
- `DotPilot.Core` is the default non-UI home, not a permanent dumping ground: when a feature becomes large enough to justify its own architectural boundary, extract it into a dedicated DLL that references `DotPilot.Core`
- do not introduce a generic `Runtime` naming layer inside this project or split code out into a vaguely named runtime assembly unless the user explicitly asks for that boundary; keep non-UI logic in explicit feature slices under `DotPilot.Core`
- when a feature is extracted out of `DotPilot.Core`, keep `DotPilot.Core` as the abstraction/shared-contract layer and make the desktop app reference the new feature DLL explicitly
- do not leave extracted subsystem contracts half-inside `DotPilot.Core`; when a future subsystem is split into its own DLL, its feature-facing interfaces and implementation seams should move with it
- keep feature-specific heavy infrastructure out of this project once it becomes its own subsystem; `DotPilot.Core` should stay cohesive instead of half-owning an extracted runtime
- Do not collect unrelated code under an umbrella directory such as `AgentSessions`; split session, workspace, settings, providers, and host code into explicit feature roots when the surface grows.
- Do not introduce or keep a `ControlPlaneDomain` umbrella in this project; shared identifiers belong under `Identifiers`, participant/provider/session DTOs under `Contracts`, cross-flow state under `Models`, and policy shapes under `Policies`.
- Keep contract-centric slices explicit inside each feature root: commands live under `Commands`, public DTO shapes live under `Contracts`, public service seams live under `Interfaces`, state records or enums live under `Models`, diagnostics under `Diagnostics`, and persistence under `Persistence`.
- When a slice exposes `Commands` and `Results`, use the solution-standard `ManagedCode.Communication` primitives instead of hand-rolled command/result record types.
- Keep the top level readable as two kinds of folders:
  - shared roots such as `Identifiers`, `Contracts`, `Models`, `Policies`, and `Workspace`
  - operational/system folders such as `AgentBuilder`, `ChatSessions`, `Providers`, and `HttpDiagnostics`
- keep this structure SOLID at the folder and project level too: cohesive feature slices stay together, but once a slice becomes too large or too independent, it should graduate into its own project instead of turning `DotPilot.Core` into mud
- Keep provider-independent testing seams real and deterministic so CI can validate core flows without external CLIs.
- Keep provider readiness probing explicit and coalesced: ordinary workspace reads may share one in-flight startup-owned snapshot read, but normal navigation must not fan out into repeated PATH/version probing loops.
- Provider initialization must be a Core-owned startup task fan-out: probe each provider/CLI in parallel, bound every probe with a timeout, and return partial/cached startup state instead of serially blocking the desktop shell.
- Keep Core flows async-first: provider, persistence, filesystem, process, and orchestration paths should expose async `Task`/`ValueTask` APIs instead of new synchronous methods so callers do not reintroduce blocking behavior above the Core boundary.
- Session lifecycle is Core-owned: creating a chat session must eagerly create the provider/runtime conversation, and closing a session must dispose or terminate the backing provider/runtime state instead of leaving teardown to the Uno layer or to eventual process cleanup.
- Send-time runtime failures must be persisted as explicit session error state before the Core flow returns failure to the shell; do not let provider/model-load exceptions disappear into logs while the transcript still looks active.
- Startup hydration state must distinguish "still running" from "finished but failed"; Core startup coordinators must not keep the shell blocked in a loading state after the initial attempt has already ended.
- When startup hydration, provider readiness state, session lifecycle, or similar long-lived runtime coordination is meant to be actor-owned, model it with Orleans grains keyed by the real runtime identity instead of singleton coordinators plus local locks.
- Do not introduce or keep `AgentSessionProviderCatalog`, `AgentSessionCommandProbe`, or provider-specific wrapper chat clients in this project; provider session creation and readiness must compose directly from `Microsoft Agent Framework` plus the provider SDK extension packages.
- Treat superseded async loads as cancellation, not failure; Core services should not emit error-level noise for expected state invalidation or navigation churn.

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
