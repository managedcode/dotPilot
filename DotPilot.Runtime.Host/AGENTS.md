# AGENTS.md

Project: `DotPilot.Runtime.Host`
Stack: `.NET 10`, class library, embedded Orleans host and local runtime-host services

## Purpose

- This project owns the desktop-embedded Orleans host for `dotPilot`.
- It keeps cluster hosting, grain registration, and host lifecycle code out of the Uno app and away from browser-targeted runtime libraries.

## Entry Points

- `DotPilot.Runtime.Host.csproj`
- `Features/AgentSessions/*`

## Boundaries

- Keep this project free of `Uno Platform`, XAML, and page/view-model logic.
- Keep it focused on local embedded host concerns: silo configuration, grain registration, and host lifecycle.
- Use `UseLocalhostClustering` plus in-memory storage/reminders for the first runtime-host cut.
- Do not add remote clustering, external durable stores, or provider-specific orchestration here unless a later issue explicitly requires them.

## Local Commands

- `build-host`: `dotnet build DotPilot.Runtime.Host/DotPilot.Runtime.Host.csproj`
- `test-runtime-host`: `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter FullyQualifiedName~AgentSessions`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-dotnet-features`
- `mcaf-testing`
- `mcaf-solid-maintainability`
- `mcaf-architecture-overview`

## Local Risks Or Protected Areas

- This project must remain invisible to the browserwasm path; keep app references conditional so UI tests stay green.
- Grain contracts belong in `DotPilot.Core`; do not let this project become the source of truth for shared runtime abstractions.
- Keep the host focused on agent-profile and session grains for the active chat workflow; do not reintroduce old demo host catalogs.
