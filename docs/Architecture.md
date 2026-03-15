# Architecture Overview

Goal: give humans and agents a fast map of the shipped `DotPilot` direction: a local-first desktop chat app for agent sessions.

This file is the required start-here architecture map for non-trivial tasks.

## Summary

- **Product shape:** `DotPilot` is a desktop chat client for local agent sessions. The default operator flow is: open settings, verify providers, create an agent, start or resume a session, send a message, and watch streaming status/tool output in the transcript.
- **Presentation boundary:** [../DotPilot/](../DotPilot/) is the `Uno Platform` shell only. It owns desktop startup, routes, XAML composition, `MVUX` screen models plus generated view-model proxies, and visible operator flows such as session list, transcript, agent creation, and provider settings.
- **Contracts boundary:** [../DotPilot.Core/](../DotPilot.Core/) owns the durable non-UI contracts for provider readiness, agent profiles, session lists, transcript entries, commands, and Orleans grain interfaces.
- **Runtime boundary:** [../DotPilot.Runtime/](../DotPilot.Runtime/) owns provider catalogs, CLI readiness checks, deterministic debug-provider behavior, `EF Core` + `SQLite` projection persistence, local folder-backed `AgentSession` storage, local folder-backed chat-history persistence through `ChatHistoryProvider`, and the `IAgentSessionService` implementation.
- **Embedded host boundary:** [../DotPilot.Runtime.Host/](../DotPilot.Runtime.Host/) owns the embedded Orleans host and the grains that represent session and agent-profile state. The product stays local-first with `UseLocalhostClustering`, in-memory reminders, and local folder-backed Orleans grain storage through `ManagedCode.Storage`.
- **Verification boundary:** [../DotPilot.Tests/](../DotPilot.Tests/) covers caller-visible runtime, persistence, contract, and view-model flows through public boundaries. [../DotPilot.UITests/](../DotPilot.UITests/) covers the desktop operator journey from provider setup to streaming chat.

## Scoping

- **In scope for the active rewrite:** chat-first session UX, provider readiness/settings, agent creation, Orleans-backed session and agent state, local persistence via `SQLite`, local folder-backed `AgentSession` and chat-history storage, deterministic debug provider, transcript/tool streaming, and optional repo/git utilities inside a session.
- **In scope for later slices:** multi-agent sessions, richer workflow composition, provider-specific live execution, session export/replay, and deeper git/worktree utilities.
- **Out of scope in the current repository slice:** remote workers, remote Orleans clustering, cloud persistence, multi-user identity, and external durable stores.

## Diagrams

### Solution module map

```mermaid
flowchart LR
  Root["dotPilot repository root"]
  Governance["AGENTS.md"]
  Architecture["docs/Architecture.md"]
  Ui["DotPilot Uno desktop shell"]
  Core["DotPilot.Core contracts"]
  Runtime["DotPilot.Runtime runtime + SQLite + folder session storage"]
  Host["DotPilot.Runtime.Host Orleans host + grains + folder grain state"]
  Unit["DotPilot.Tests"]
  UiTests["DotPilot.UITests"]

  Root --> Governance
  Root --> Architecture
  Root --> Ui
  Root --> Core
  Root --> Runtime
  Root --> Host
  Root --> Unit
  Root --> UiTests
  Ui --> Core
  Ui --> Runtime
  Ui --> Host
  Runtime --> Core
  Host --> Core
  Host --> Runtime
  Unit --> Ui
  Unit --> Core
  Unit --> Runtime
  Unit --> Host
  UiTests --> Ui
```

### Operator flow

```mermaid
flowchart LR
  Settings["Settings"]
  Providers["Provider readiness + install actions"]
  AgentCreate["Create agent"]
  SessionList["Session list"]
  Session["Active session"]
  Stream["Streaming transcript + status + tool activity"]
  Git["Optional repo/git actions"]

  Settings --> Providers
  Providers --> AgentCreate
  AgentCreate --> SessionList
  SessionList --> Session
  Session --> Stream
  Session --> Git
```

### Runtime flow

```mermaid
flowchart TD
  Ui["Uno shell"]
  ViewModels["MVUX screen models + generated view-model proxies"]
  Service["IAgentSessionService"]
  ProjectionStore["EF Core + SQLite projections"]
  SessionStore["Folder AgentSession + chat history"]
  SessionGrain["ISessionGrain"]
  AgentGrain["IAgentProfileGrain"]
  GrainStore["ManagedCode.Storage Orleans filesystem store"]
  ProviderCatalog["Provider catalog + readiness probe"]
  ProviderClient["Provider SDK / IChatClient or debug client"]
  Stream["SessionStreamEntry updates"]

  Ui --> ViewModels
  ViewModels --> Service
  Service --> ProjectionStore
  Service --> SessionStore
  Service --> SessionGrain
  Service --> AgentGrain
  SessionGrain --> GrainStore
  AgentGrain --> GrainStore
  Service --> ProviderCatalog
  ProviderCatalog --> ProviderClient
  Service --> ProviderClient
  ProviderClient --> Stream
  Stream --> ViewModels
```

### Persistence and resume shape

```mermaid
sequenceDiagram
  participant UI as Uno UI
  participant Service as AgentSessionService
  participant DB as SQLite projections
  participant FS as Local folder AgentSession/history store
  participant SG as SessionGrain
  participant AG as AgentProfileGrain
  participant GS as Local folder Orleans grain store
  participant Provider as Provider SDK / Debug Client

  UI->>Service: CreateAgentAsync(...)
  Service->>DB: Save agent profile
  Service->>AG: UpsertAsync(agent profile)
  AG->>GS: Persist grain state
  UI->>Service: CreateSessionAsync(...)
  Service->>DB: Save session + initial status entry
  Service->>FS: Create/persist opaque AgentSession
  Service->>SG: UpsertAsync(session)
  SG->>GS: Persist grain state
  UI->>Service: SendMessageAsync(...)
  Service->>DB: Save user message
  Service->>Provider: Run / stream
  Provider-->>Service: Streaming updates
  Service->>DB: Persist transcript entries
  Service->>FS: Persist ChatHistoryProvider state + serialized AgentSession
  Service-->>UI: SessionStreamEntry updates
```

## Navigation Index

### Planning and governance

- `Solution governance` — [../AGENTS.md](../AGENTS.md)
- `Uno app rules` — [../DotPilot/AGENTS.md](../DotPilot/AGENTS.md)
- `Core contracts rules` — [../DotPilot.Core/AGENTS.md](../DotPilot.Core/AGENTS.md)
- `Runtime rules` — [../DotPilot.Runtime/AGENTS.md](../DotPilot.Runtime/AGENTS.md)
- `Embedded host rules` — [../DotPilot.Runtime.Host/AGENTS.md](../DotPilot.Runtime.Host/AGENTS.md)
- `Test rules` — [../DotPilot.Tests/AGENTS.md](../DotPilot.Tests/AGENTS.md), [../DotPilot.UITests/AGENTS.md](../DotPilot.UITests/AGENTS.md)

### Modules

- `Production Uno app` — [../DotPilot/](../DotPilot/)
- `Contracts and typed identifiers` — [../DotPilot.Core/](../DotPilot.Core/)
- `Runtime services and provider adapters` — [../DotPilot.Runtime/](../DotPilot.Runtime/)
- `Embedded Orleans host` — [../DotPilot.Runtime.Host/](../DotPilot.Runtime.Host/)
- `Unit and integration-style tests` — [../DotPilot.Tests/](../DotPilot.Tests/)
- `UI tests` — [../DotPilot.UITests/](../DotPilot.UITests/)

### High-signal code paths

- `Application startup and route registration` — [../DotPilot/App.xaml.cs](../DotPilot/App.xaml.cs)
- `Chat shell route` — [../DotPilot/Presentation/AgentSessions/Chat/MainPage.xaml](../DotPilot/Presentation/AgentSessions/Chat/MainPage.xaml)
- `Agent creation route` — [../DotPilot/Presentation/AgentSessions/Builder/SecondPage.xaml](../DotPilot/Presentation/AgentSessions/Builder/SecondPage.xaml)
- `Settings shell` — [../DotPilot/Presentation/AgentSessions/Settings/Controls/SettingsShell.xaml](../DotPilot/Presentation/AgentSessions/Settings/Controls/SettingsShell.xaml)
- `Active runtime contracts` — [../DotPilot.Core/Features/AgentSessions/AgentSessionContracts.cs](../DotPilot.Core/Features/AgentSessions/AgentSessionContracts.cs)
- `Active runtime commands` — [../DotPilot.Core/Features/AgentSessions/AgentSessionCommands.cs](../DotPilot.Core/Features/AgentSessions/AgentSessionCommands.cs)
- `Session runtime service` — [../DotPilot.Runtime/Features/AgentSessions/Execution/AgentSessionService.cs](../DotPilot.Runtime/Features/AgentSessions/Execution/AgentSessionService.cs)
- `Provider readiness catalog` — [../DotPilot.Runtime/Features/AgentSessions/Providers/AgentSessionProviderCatalog.cs](../DotPilot.Runtime/Features/AgentSessions/Providers/AgentSessionProviderCatalog.cs)
- `Session grain` — [../DotPilot.Runtime.Host/Features/AgentSessions/SessionGrain.cs](../DotPilot.Runtime.Host/Features/AgentSessions/SessionGrain.cs)
- `UI end-to-end flow` — [../DotPilot.UITests/Features/AgentSessions/Flows/GivenChatSessionsShell.cs](../DotPilot.UITests/Features/AgentSessions/Flows/GivenChatSessionsShell.cs)

## Review Focus

- Keep the product framed as a chat-first local-agent client, not as a backlog-shaped workbench.
- Replace seed-data assumptions with real provider, agent, session, transcript, and durable runtime state.
- Keep repo/git operations as optional tools inside a session, not as the app's primary information architecture.
- Keep presentation models long-lived and projection-only so desktop navigation stays memory-hot instead of rehydrating each screen from scratch.
- Prefer provider SDKs and `IChatClient`-style abstractions over custom parallel request/result wrappers unless a concrete gap forces an adapter layer.
- Keep the persistence split explicit:
  - `SQLite` for operator-facing projections and settings
  - local folder-backed `AgentSession` plus chat history for agent continuity
  - local folder-backed Orleans storage for grain state
