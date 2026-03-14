# AGENTS.md

Project: `DotPilot.Runtime`
Stack: `.NET 10`, class library, provider-backed runtime services, local persistence, and deterministic session orchestration

## Purpose

- This project owns non-UI runtime implementations that sit behind the contracts in `DotPilot.Core`.
- It is the first landing zone for deterministic test clients, runtime probes, and later embedded-host integrations so the Uno app stays focused on presentation and startup composition.

## Entry Points

- `DotPilot.Runtime.csproj`
- `Features/AgentSessions/*`
- `Features/HttpDiagnostics/DebugHttpHandler.cs`

## Boundaries

- Keep this project free of `Uno Platform`, XAML, and page/view-model logic.
- Implement feature slices against `DotPilot.Core` contracts instead of reaching back into the app project.
- Prefer deterministic runtime behavior, provider readiness probing, and `SQLite`-backed persistence here so tests can exercise real flows without mocks.
- Keep provider readiness and installed-model/toolchain state behind a runtime-owned cached loop so UI navigation does not rerun expensive probes on every view-model load.
- Keep hot-path state in memory and safe background workers: ordinary UI navigation should consume cached runtime projections, while refresh/probe/update loops run off the UI thread and publish changes back asynchronously.
- When conversation continuity is required, keep the durable chat/session runtime state split correctly:
  - transcript and operator-facing projections can stay in `SQLite`
  - the opaque `AgentSession` and chat-history provider state should persist in a local folder-backed Agent Framework store
- Keep external-provider assumptions soft: absence of Codex, Claude Code, or GitHub Copilot in CI must not break the provider-independent baseline.
- For the first embedded Orleans host implementation, stay local-first with `UseLocalhostClustering` and in-memory storage/reminders so the desktop runtime remains self-contained.
- Use `ILogger` as the default diagnostics path for runtime operations; provider probes, agent/session lifecycle events, and provider execution failures should be observable without relying on console output.

## Local Commands

- `build-runtime`: `dotnet build DotPilot.Runtime/DotPilot.Runtime.csproj`
- `test-runtime`: `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter FullyQualifiedName~AgentSessions`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-dotnet-features`
- `mcaf-testing`
- `mcaf-solid-maintainability`
- `mcaf-architecture-overview`

## Local Risks Or Protected Areas

- Runtime services introduced here are the composition root for provider readiness, agent creation, session persistence, and streaming transcript state, so keep boundaries explicit.
- CLI probing must stay deterministic and side-effect free; do not turn startup checks into live external calls.
