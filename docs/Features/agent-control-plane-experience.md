# dotPilot Agent Control Plane Experience

## Summary

`dotPilot` is a desktop-first control plane for local-first agent operations, but its visible product shape is a chat client for sessions. The operator should feel like they are working inside a persistent session with local agents, not bouncing between backlog-shaped product slices.

The product must support coding sessions, but it must not be limited to coding. The same shell should support research, analysis, orchestration, review, and operator workflows.

## Scope

### In Scope

- desktop chat shell with session list, active transcript, and streaming activity
- provider readiness settings for `Codex`, `Claude Code`, `GitHub Copilot`, and the deterministic debug provider
- agent profiles backed by provider SDK or `IChatClient`-style integrations
- Orleans-backed session and agent-profile state
- local persistence through `EF Core` + `SQLite`
- visible tool/status streaming in the transcript
- optional repo/git actions as tools inside a session

### Out Of Scope

- cloud orchestration
- remote Orleans clustering
- auto-installing provider CLIs without operator confirmation
- local-model runtime integration beyond the current debug provider
- adding `MLXSharp` in the first product wave

## Product Rules

1. `dotPilot` must feel like a desktop chat app for local agents, not like a workbench made from backlog slices.
2. Settings are about provider readiness and install guidance, not about separate product centers.
3. A session is the primary container for work and must persist across app restarts.
4. Each agent participating in that experience must have durable identity and configuration outside the UI layer.
5. Provider status must be explicit before live use:
   - installed or missing
   - enabled or disabled
   - ready or blocked
   - install/help command visible when blocked
6. Transcript output must show more than assistant text:
   - user messages
   - assistant messages
   - tool start and completion events
   - status updates
   - error states
7. Repo/git flows are optional tools inside the session experience, not a separate shell.
8. The provider-independent baseline must work through the built-in debug provider so UI tests and CI can always exercise the end-to-end chat flow.

## Primary Operator Flow

```mermaid
flowchart LR
  Open["Open dotPilot"]
  Settings["Open settings"]
  Providers["Verify provider readiness or install guidance"]
  Agent["Create agent profile"]
  Session["Create or resume session"]
  Chat["Send message"]
  Stream["Observe streaming transcript and tool activity"]
  Continue["Continue session or switch sessions"]

  Open --> Settings --> Providers --> Agent --> Session --> Chat --> Stream --> Continue
```

## Session Runtime Flow

```mermaid
sequenceDiagram
  participant Operator
  participant UI as Uno UI
  participant Service as AgentSessionService
  participant DB as SQLite
  participant Grain as SessionGrain
  participant Provider as Provider SDK / Debug Client

  Operator->>UI: Send message
  UI->>Service: SendMessageAsync(...)
  Service->>DB: Persist user message
  Service->>Provider: Stream response
  Provider-->>Service: Assistant/status/tool updates
  Service->>DB: Persist transcript entries
  Service->>Grain: Upsert session state
  Service-->>UI: Stream SessionStreamEntry updates
```

## Main Behaviour

### Provider Setup

- The operator opens settings.
- The app detects whether each provider CLI is installed and available on `PATH`.
- The app shows:
  - current status summary
  - installed version when available
  - whether agent creation is currently allowed
  - an install/help command when setup is missing
- The deterministic debug provider is always available for local verification.

### Agent Profiles

- The operator creates an agent profile by selecting:
  - provider
  - role
  - model
  - capabilities
  - system prompt
- Agent profiles are durable and survive restarts.
- The current shipped flow creates one provider-backed primary agent per session, while the architecture keeps room for later multi-agent expansion.

### Session Execution

- The operator starts or resumes a session from the chat sidebar.
- Each session has durable transcript history.
- The transcript shows:
  - user messages
  - assistant output
  - status entries
  - tool-start entries
  - tool-complete entries
  - errors
- The composer behaves like a terminal-style message input, with visible progress during send and stream.

### Repo and Git Actions

- Repo and git operations can exist as tools invoked inside a session.
- The app only needs the common operator actions in the first wave:
  - create repository
  - fetch
  - pull
  - push
  - merge
  - inspect diffs
- These actions must show up as tool activity or session results, not as a separate product mode.

## Edge and Failure Flows

### Provider Missing

- If a provider is not installed, settings must show that state before agent creation.
- The app must expose the suggested install command instead of silently failing later.

### Provider Disabled

- If a provider is disabled, the app must say so explicitly and block agent creation for that provider.

### Session Resume

- If the app restarts, previously persisted sessions and transcript history must still load from the local store.

### Live Provider Not Yet Wired

- If a provider is configured but live execution is not implemented yet, the session flow must surface that state as an explicit transcript error entry.

## Verification Strategy

- `docs/Architecture.md` reflects the same boundaries described here.
- `docs/ADR/ADR-0001-agent-control-plane-architecture.md` records the session-first desktop architecture and SDK-first provider direction.
- `docs/ADR/ADR-0003-vertical-slices-and-ui-only-uno-app.md` records the presentation-only app boundary and slice layout.
- `DotPilot.Tests` cover provider readiness, agent creation, session creation, and deterministic transcript persistence.
- `DotPilot.UITests` cover the main operator flow:
  1. open app
  2. open settings
  3. enable debug provider
  4. create agent
  5. create session
  6. send message
  7. observe streamed transcript output
