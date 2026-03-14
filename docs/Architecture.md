# Architecture Overview

Goal: give humans and agents a fast map of the active `DotPilot` solution, the current `Uno Platform` shell, the foundation contracts from epic `#11`, the workbench foundation for epic `#13`, the Toolchain Center for epic `#14`, and the runtime-host backlog that builds on those contracts in epic `#12`.

This file is the required start-here architecture map for non-trivial tasks.

## Summary

- **System:** `DotPilot` is a `.NET 10` `Uno Platform` desktop-first application that is evolving from a static prototype into a local-first control plane for agent operations.
- **Presentation boundary:** [../DotPilot/](../DotPilot/) is now the presentation host only. It owns XAML, routing, desktop startup, and UI composition, while non-UI feature logic moves into separate DLLs.
- **Workbench boundary:** epic [#13](https://github.com/managedcode/dotPilot/issues/13) is landing as a `Workbench` slice that will provide repository navigation, file inspection, artifact and log inspection, and a unified settings shell without moving that behavior into page code-behind.
- **Toolchain Center boundary:** epic [#14](https://github.com/managedcode/dotPilot/issues/14) now lives as a `ToolchainCenter` slice. [../DotPilot.Core/Features/ToolchainCenter](../DotPilot.Core/Features/ToolchainCenter) defines the readiness, diagnostics, configuration, action, and polling contracts; [../DotPilot.Runtime/Features/ToolchainCenter](../DotPilot.Runtime/Features/ToolchainCenter) probes local provider CLIs for `Codex`, `Claude Code`, and `GitHub Copilot`; the Uno app surfaces the slice through the settings shell.
- **Foundation contract boundary:** epic [#11](https://github.com/managedcode/dotPilot/issues/11) is represented through [../DotPilot.Core/Features/ControlPlaneDomain](../DotPilot.Core/Features/ControlPlaneDomain) and [../DotPilot.Core/Features/RuntimeCommunication](../DotPilot.Core/Features/RuntimeCommunication). These slices define the shared agent/session/tool model and the `ManagedCode.Communication` result/problem language that later runtime work reuses.
- **Runtime foundation boundary:** [../DotPilot.Core/](../DotPilot.Core/) owns issue-aligned contracts, typed identifiers, and public slice interfaces; [../DotPilot.Runtime/](../DotPilot.Runtime/) owns provider-independent runtime implementations such as the deterministic test client, toolchain probing, and future embedded-host integration points.
- **Embedded runtime backlog boundary:** epic [#12](https://github.com/managedcode/dotPilot/issues/12) now builds on the epic `#11` foundation contracts through the `RuntimeFoundation` slice instead of treating issues `#22` and `#23` as runtime-host work.
- **Automated verification:** [../DotPilot.Tests/](../DotPilot.Tests/) covers API-style and contract flows through the new DLL boundaries; [../DotPilot.UITests/](../DotPilot.UITests/) covers the visible workbench flow, Toolchain Center, and runtime-foundation UI surface. Provider-independent flows must pass in CI through deterministic or environment-agnostic checks, while provider-specific checks can run only when the matching toolchain is available.

## Scoping

- **In scope for the current repository state:** the Uno workbench shell, the `DotPilot.Core` and `DotPilot.Runtime` libraries, the epic `#11` foundation-contract slices, the runtime-foundation planning surface for epic `#12`, and the automated validation boundaries around them.
- **In scope for future implementation:** embedded Orleans hosting, `Microsoft Agent Framework`, provider adapters, persistence, telemetry, evaluation, Git tooling, and local runtimes.
- **Out of scope in the current slice:** full Orleans hosting, live provider execution, remote workers, and cloud-only control-plane services.

## Diagrams

### Solution module map

```mermaid
flowchart LR
  Root["dotPilot repository root"]
  Governance["AGENTS.md"]
  Architecture["docs/Architecture.md"]
  Adr1["ADR-0001 control-plane direction"]
  Adr3["ADR-0003 vertical slices + UI-only app"]
  Feature["agent-control-plane-experience.md"]
  Toolchains["toolchain-center.md"]
  Plan["epic-11-foundation-contracts.plan.md"]
  Ui["DotPilot Uno UI host"]
  Core["DotPilot.Core contracts"]
  Runtime["DotPilot.Runtime services"]
  Unit["DotPilot.Tests"]
  UiTests["DotPilot.UITests"]

  Root --> Governance
  Root --> Architecture
  Root --> Adr1
  Root --> Adr3
  Root --> Feature
  Root --> Toolchains
  Root --> Plan
  Root --> Ui
  Root --> Core
  Root --> Runtime
  Root --> Unit
  Root --> UiTests
  Ui --> Core
  Ui --> Runtime
  Unit --> Ui
  Unit --> Core
  Unit --> Runtime
```

### Workbench foundation slice for epic #13

```mermaid
flowchart TD
  Epic["#13 Desktop workbench"]
  Shell["#28 Primary workbench shell"]
  Tree["#29 Repository tree"]
  File["#30 File surface + diff review"]
  Dock["#31 Artifact dock + runtime console"]
  Settings["#32 Settings shell"]
  CoreSlice["DotPilot.Core/Features/Workbench"]
  RuntimeSlice["DotPilot.Runtime/Features/Workbench"]
  UiSlice["MainPage + SettingsPage + workbench controls"]

  Epic --> Shell
  Epic --> Tree
  Epic --> File
  Epic --> Dock
  Epic --> Settings
  Shell --> CoreSlice
  Tree --> CoreSlice
  File --> CoreSlice
  Dock --> CoreSlice
  Settings --> CoreSlice
  CoreSlice --> RuntimeSlice
  RuntimeSlice --> UiSlice
```

### Toolchain Center slice for epic #14

```mermaid
flowchart TD
  Epic["#14 Provider toolchain center"]
  UiIssue["#33 Toolchain Center UI"]
  Codex["#34 Codex readiness"]
  Claude["#35 Claude Code readiness"]
  Copilot["#36 GitHub Copilot readiness"]
  Diagnostics["#37 Connection diagnostics"]
  Config["#38 Provider configuration"]
  Polling["#39 Background polling"]
  CoreSlice["DotPilot.Core/Features/ToolchainCenter"]
  RuntimeSlice["DotPilot.Runtime/Features/ToolchainCenter"]
  UiSlice["SettingsViewModel + ToolchainCenterPanel"]

  Epic --> UiIssue
  Epic --> Codex
  Epic --> Claude
  Epic --> Copilot
  Epic --> Diagnostics
  Epic --> Config
  Epic --> Polling
  UiIssue --> CoreSlice
  Codex --> CoreSlice
  Claude --> CoreSlice
  Copilot --> CoreSlice
  Diagnostics --> CoreSlice
  Config --> CoreSlice
  Polling --> CoreSlice
  CoreSlice --> RuntimeSlice
  RuntimeSlice --> UiSlice
```

### Foundation contract slices for epic #11

```mermaid
flowchart TD
  Epic["#11 Desktop control-plane foundation"]
  Domain["#22 Domain contracts"]
  Comm["#23 Communication contracts"]
  DomainSlice["DotPilot.Core/Features/ControlPlaneDomain"]
  CommunicationSlice["DotPilot.Core/Features/RuntimeCommunication"]
  RuntimeContracts["DotPilot.Core/Features/RuntimeFoundation"]
  DeterministicClient["DotPilot.Runtime/Features/RuntimeFoundation/DeterministicAgentRuntimeClient"]
  Tests["DotPilot.Tests contract coverage"]

  Epic --> Domain
  Epic --> Comm
  Domain --> DomainSlice
  Comm --> CommunicationSlice
  DomainSlice --> RuntimeContracts
  CommunicationSlice --> RuntimeContracts
  CommunicationSlice --> DeterministicClient
  DomainSlice --> DeterministicClient
  DeterministicClient --> Tests
  RuntimeContracts --> Tests
```

### Runtime-host backlog slices for epic #12

```mermaid
flowchart TD
  Epic["#12 Embedded agent runtime host"]
  Host["#24 Embedded Orleans host"]
  MAF["#25 Agent Framework runtime"]
  Policy["#26 Grain traffic policy"]
  Sessions["#27 Session persistence and resume"]
  Foundation["#11 Foundation contracts"]
  DomainSlice["DotPilot.Core/Features/ControlPlaneDomain"]
  CommunicationSlice["DotPilot.Core/Features/RuntimeCommunication"]
  CoreSlice["DotPilot.Core/Features/RuntimeFoundation"]
  RuntimeSlice["DotPilot.Runtime/Features/RuntimeFoundation"]
  UiSlice["DotPilot runtime panel + banner"]

  Foundation --> DomainSlice
  Foundation --> CommunicationSlice
  Epic --> Host
  Epic --> MAF
  Epic --> Policy
  Epic --> Sessions
  DomainSlice --> CommunicationSlice
  CommunicationSlice --> CoreSlice
  Host --> RuntimeSlice
  MAF --> RuntimeSlice
  Policy --> CoreSlice
  Sessions --> CoreSlice
  Sessions --> RuntimeSlice
  CoreSlice --> UiSlice
  RuntimeSlice --> UiSlice
```

### Current composition flow

```mermaid
flowchart LR
  App["DotPilot/App.xaml.cs"]
  Views["MainPage + SecondPage + SettingsShell + RuntimeFoundationPanel + ToolchainCenterPanel"]
  ViewModels["MainViewModel + SecondViewModel + SettingsViewModel"]
  Catalog["RuntimeFoundationCatalog"]
  Toolchains["ToolchainCenterCatalog"]
  TestClient["DeterministicAgentRuntimeClient"]
  Probe["ProviderToolchainProbe"]
  ToolchainProbe["ToolchainCommandProbe + provider profiles"]
  Contracts["Typed IDs + contracts"]
  Future["Future Orleans + Agent Framework integrations"]

  App --> ViewModels
  Views --> ViewModels
  ViewModels --> Catalog
  ViewModels --> Toolchains
  Catalog --> TestClient
  Catalog --> Probe
  Catalog --> Contracts
  Toolchains --> ToolchainProbe
  Toolchains --> Contracts
  Future --> Contracts
  Future --> Catalog
```

## Navigation Index

### Planning and decision docs

- `Solution governance` — [../AGENTS.md](../AGENTS.md)
- `Task plan` — [../epic-11-foundation-contracts.plan.md](../epic-11-foundation-contracts.plan.md)
- `Primary architecture decision` — [ADR-0001](./ADR/ADR-0001-agent-control-plane-architecture.md)
- `Vertical-slice solution decision` — [ADR-0003](./ADR/ADR-0003-vertical-slices-and-ui-only-uno-app.md)
- `Feature spec` — [Agent Control Plane Experience](./Features/agent-control-plane-experience.md)
- `Issue #13 feature doc` — [Workbench Foundation](./Features/workbench-foundation.md)
- `Issue #14 feature doc` — [Toolchain Center](./Features/toolchain-center.md)
- `Issue #22 feature doc` — [Control Plane Domain Model](./Features/control-plane-domain-model.md)
- `Issue #23 feature doc` — [Runtime Communication Contracts](./Features/runtime-communication-contracts.md)

### Modules

- `Production Uno app` — [../DotPilot/](../DotPilot/)
- `Contracts and typed identifiers` — [../DotPilot.Core/](../DotPilot.Core/)
- `Provider-independent runtime services` — [../DotPilot.Runtime/](../DotPilot.Runtime/)
- `Unit and API-style tests` — [../DotPilot.Tests/](../DotPilot.Tests/)
- `UI tests` — [../DotPilot.UITests/](../DotPilot.UITests/)
- `Shared build and analyzer policy` — [../Directory.Build.props](../Directory.Build.props), [../Directory.Packages.props](../Directory.Packages.props), [../global.json](../global.json), and [../.editorconfig](../.editorconfig)

### High-signal code paths

- `Application startup and composition` — [../DotPilot/App.xaml.cs](../DotPilot/App.xaml.cs)
- `Chat workbench view model` — [../DotPilot/Presentation/MainViewModel.cs](../DotPilot/Presentation/MainViewModel.cs)
- `Settings view model` — [../DotPilot/Presentation/SettingsViewModel.cs](../DotPilot/Presentation/SettingsViewModel.cs)
- `Agent builder view model` — [../DotPilot/Presentation/SecondViewModel.cs](../DotPilot/Presentation/SecondViewModel.cs)
- `Toolchain Center panel` — [../DotPilot/Presentation/Controls/ToolchainCenterPanel.xaml](../DotPilot/Presentation/Controls/ToolchainCenterPanel.xaml)
- `Reusable runtime panel` — [../DotPilot/Presentation/Controls/RuntimeFoundationPanel.xaml](../DotPilot/Presentation/Controls/RuntimeFoundationPanel.xaml)
- `Toolchain Center contracts` — [../DotPilot.Core/Features/ToolchainCenter/ToolchainCenterContracts.cs](../DotPilot.Core/Features/ToolchainCenter/ToolchainCenterContracts.cs)
- `Toolchain Center issue catalog` — [../DotPilot.Core/Features/ToolchainCenter/ToolchainCenterIssues.cs](../DotPilot.Core/Features/ToolchainCenter/ToolchainCenterIssues.cs)
- `Shell configuration contract` — [../DotPilot.Core/Features/ApplicationShell/AppConfig.cs](../DotPilot.Core/Features/ApplicationShell/AppConfig.cs)
- `Runtime foundation contracts` — [../DotPilot.Core/Features/RuntimeFoundation/RuntimeFoundationContracts.cs](../DotPilot.Core/Features/RuntimeFoundation/RuntimeFoundationContracts.cs)
- `Runtime communication problems` — [../DotPilot.Core/Features/RuntimeCommunication/RuntimeCommunicationProblems.cs](../DotPilot.Core/Features/RuntimeCommunication/RuntimeCommunicationProblems.cs)
- `Control-plane domain contracts` — [../DotPilot.Core/Features/ControlPlaneDomain/SessionExecutionContracts.cs](../DotPilot.Core/Features/ControlPlaneDomain/SessionExecutionContracts.cs)
- `Provider and tool contracts` — [../DotPilot.Core/Features/ControlPlaneDomain/ProviderAndToolContracts.cs](../DotPilot.Core/Features/ControlPlaneDomain/ProviderAndToolContracts.cs)
- `Runtime issue catalog` — [../DotPilot.Core/Features/RuntimeFoundation/RuntimeFoundationIssues.cs](../DotPilot.Core/Features/RuntimeFoundation/RuntimeFoundationIssues.cs)
- `Toolchain Center catalog implementation` — [../DotPilot.Runtime/Features/ToolchainCenter/ToolchainCenterCatalog.cs](../DotPilot.Runtime/Features/ToolchainCenter/ToolchainCenterCatalog.cs)
- `Toolchain snapshot factory` — [../DotPilot.Runtime/Features/ToolchainCenter/ToolchainProviderSnapshotFactory.cs](../DotPilot.Runtime/Features/ToolchainCenter/ToolchainProviderSnapshotFactory.cs)
- `Runtime catalog implementation` — [../DotPilot.Runtime/Features/RuntimeFoundation/RuntimeFoundationCatalog.cs](../DotPilot.Runtime/Features/RuntimeFoundation/RuntimeFoundationCatalog.cs)
- `Deterministic test client` — [../DotPilot.Runtime/Features/RuntimeFoundation/DeterministicAgentRuntimeClient.cs](../DotPilot.Runtime/Features/RuntimeFoundation/DeterministicAgentRuntimeClient.cs)
- `Provider toolchain probing` — [../DotPilot.Runtime/Features/RuntimeFoundation/ProviderToolchainProbe.cs](../DotPilot.Runtime/Features/RuntimeFoundation/ProviderToolchainProbe.cs)

## Dependency Rules

- `DotPilot` owns XAML, routing, and startup composition only.
- `DotPilot.Core` owns non-UI contracts and typed identifiers arranged by feature slice.
- `DotPilot.Runtime` owns provider-independent runtime implementations and future integration seams, but not XAML or page logic.
- `DotPilot.Tests` validates contracts, composition, deterministic runtime behavior, and conditional provider-availability checks through public boundaries.
- `DotPilot.UITests` validates the visible workbench shell, runtime-foundation panel, and agent-builder flow through the browser-hosted UI.

## Key Decisions

- The Uno app must remain a presentation-only host instead of becoming a dump for runtime logic.
- Feature work should land as vertical slices with isolated contracts and implementations, not as shared horizontal folders.
- Epic `#11` establishes the reusable contract and communication foundation before epic `#12` begins embedded runtime-host work.
- Epic `#12` builds on that foundation instead of re-owning issues `#22` and `#23`.
- Epic `#14` makes external-provider toolchain readiness explicit before session creation, so install, auth, diagnostics, and configuration state stays visible instead of being inferred later.
- CI must stay meaningful without external provider CLIs by using the in-repo deterministic runtime client.
- Real provider checks may run only when the corresponding toolchain is present and discoverable.

## Known Repository Risks

- Provider-dependent validation for real `Codex`, `Claude Code`, and `GitHub Copilot` toolchains is intentionally environment-gated; the deterministic runtime client is the mandatory CI baseline for agent-flow verification.

## Where To Go Next

- Editing the Uno app shell: [../DotPilot/AGENTS.md](../DotPilot/AGENTS.md)
- Editing contracts: [../DotPilot.Core/AGENTS.md](../DotPilot.Core/AGENTS.md)
- Editing runtime services: [../DotPilot.Runtime/AGENTS.md](../DotPilot.Runtime/AGENTS.md)
- Editing unit and API-style tests: [../DotPilot.Tests/AGENTS.md](../DotPilot.Tests/AGENTS.md)
- Editing UI tests: [../DotPilot.UITests/AGENTS.md](../DotPilot.UITests/AGENTS.md)
