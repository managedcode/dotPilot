# Embedded Orleans Host

## Summary

`DotPilot.Runtime.Host` embeds the Orleans silo used by the desktop runtime path without polluting the `Uno` app project or the browserwasm build. The current cut is intentionally local-first: `UseLocalhostClustering`, in-memory grain storage, and in-memory reminders only.

## Scope

### In Scope

- a dedicated `DotPilot.Runtime.Host` class library for Orleans hosting
- Orleans grain interfaces for agent profiles and sessions in `DotPilot.Core/Features/AgentSessions`
- `AgentProfileGrain` and `SessionGrain`
- desktop startup integration through the Uno host builder
- automated tests for lifecycle, grain round-trips, mismatched keys, and in-memory volatility across restarts

### Out Of Scope

- remote clusters
- external durable Orleans storage providers
- moving durable transcript persistence into Orleans storage
- UI redesign around the runtime host

## Flow

```mermaid
flowchart LR
  App["DotPilot/App.xaml.cs"]
  HostExt["UseDotPilotAgentSessions()"]
  Silo["Embedded Orleans silo"]
  Store["In-memory grain storage + reminders"]
  Session["SessionGrain"]
  Agent["AgentProfileGrain"]
  Contracts["DotPilot.Core AgentSessions grain contracts"]

  App --> HostExt
  HostExt --> Silo
  Silo --> Store
  Silo --> Session
  Silo --> Agent
  Session --> Contracts
  Agent --> Contracts
```

## Design Notes

- The app references `DotPilot.Runtime.Host` only on non-browser targets so `DotPilot.UITests` and the browserwasm build do not carry the server-only Orleans host.
- `DotPilot.Core` owns the grain interfaces and the durable agent/session descriptors used by those grains.
- `DotPilot.Runtime.Host` owns:
  - Orleans host configuration
  - host option names
  - grain implementations
- The initial cluster configuration is intentionally local:
  - `UseLocalhostClustering`
  - named in-memory grain storage
  - in-memory reminders
- Durable transcript history and provider settings live in the sibling runtime slice through `SQLite`; Orleans stores only the in-cluster session and agent grain state for this product wave.

## Verification

- `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter FullyQualifiedName~AgentSessions`
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`
- `dotnet test DotPilot.slnx`

## References

- [Architecture Overview](../Architecture.md)
- [ADR-0003: Keep the Uno App Presentation-Only and Move Feature Work into Vertical-Slice Class Libraries](../ADR/ADR-0003-vertical-slices-and-ui-only-uno-app.md)
- [Local development configuration](https://learn.microsoft.com/dotnet/orleans/host/configuration-guide/local-development-configuration)
- [Quickstart: Build your first Orleans app with ASP.NET Core](https://learn.microsoft.com/dotnet/orleans/quickstarts/build-your-first-orleans-app)
