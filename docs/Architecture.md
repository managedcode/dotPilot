# Architecture Overview

Goal: give humans and agents a fast map of the active `DotPilot` solution, the current `Uno Platform` shell, and the new vertical-slice runtime foundation that starts epic `#12`.

This file is the required start-here architecture map for non-trivial tasks.

## Summary

- **System:** `DotPilot` is a `.NET 10` `Uno Platform` desktop-first application that is evolving from a static prototype into a local-first control plane for agent operations.
- **Presentation boundary:** [../DotPilot/](../DotPilot/) is now the presentation host only. It owns XAML, routing, desktop startup, and UI composition, while non-UI feature logic moves into separate DLLs.
- **Runtime foundation boundary:** [../DotPilot.Core/](../DotPilot.Core/) owns issue-aligned contracts, typed identifiers, and public slice interfaces; [../DotPilot.Runtime/](../DotPilot.Runtime/) owns provider-independent runtime implementations such as the deterministic test client, toolchain probing, and future embedded-host integration points.
- **Domain slice boundary:** issue [#22](https://github.com/managedcode/dotPilot/issues/22) now lives in `DotPilot.Core/Features/ControlPlaneDomain`, which defines the shared agent, session, fleet, provider, runtime, approval, artifact, telemetry, and evaluation model that later slices reuse.
- **Communication slice boundary:** issue [#23](https://github.com/managedcode/dotPilot/issues/23) lives in `DotPilot.Core/Features/RuntimeCommunication`, which defines the shared `ManagedCode.Communication` result/problem language for runtime public boundaries.
- **First implementation slice:** epic [#12](https://github.com/managedcode/dotPilot/issues/12) is represented locally through the `RuntimeFoundation` slice, which sequences issues `#22`, `#23`, `#24`, and `#25` behind a stable contract surface instead of mixing runtime work into the Uno app.
- **Automated verification:** [../DotPilot.Tests/](../DotPilot.Tests/) covers API-style and contract flows through the new DLL boundaries; [../DotPilot.UITests/](../DotPilot.UITests/) covers the visible workbench flow and the runtime-foundation UI surface. Provider-independent flows must pass in CI through the deterministic test client, while provider-specific checks can run only when the matching toolchain is available.

## Scoping

- **In scope for the current repository state:** the Uno workbench shell, the new `DotPilot.Core` and `DotPilot.Runtime` libraries, the runtime-foundation slice, and the automated validation boundaries around them.
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
  Plan["vertical-slice-runtime-foundation.plan.md"]
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

### Runtime foundation slice for epic #12

```mermaid
flowchart TD
  Epic["#12 Embedded agent runtime host"]
  Domain["#22 Domain contracts"]
  Comm["#23 Communication contracts"]
  Host["#24 Embedded Orleans host"]
  MAF["#25 Agent Framework runtime"]
  DomainSlice["DotPilot.Core/Features/ControlPlaneDomain"]
  CommunicationSlice["DotPilot.Core/Features/RuntimeCommunication"]
  CoreSlice["DotPilot.Core/Features/RuntimeFoundation"]
  RuntimeSlice["DotPilot.Runtime/Features/RuntimeFoundation"]
  UiSlice["DotPilot runtime panel + banner"]

  Epic --> Domain
  Epic --> Comm
  Epic --> Host
  Epic --> MAF
  Domain --> DomainSlice
  DomainSlice --> CommunicationSlice
  CommunicationSlice --> CoreSlice
  Comm --> CommunicationSlice
  Host --> RuntimeSlice
  MAF --> RuntimeSlice
  CoreSlice --> UiSlice
  RuntimeSlice --> UiSlice
```

### Current composition flow

```mermaid
flowchart LR
  App["DotPilot/App.xaml.cs"]
  Views["MainPage + SecondPage + RuntimeFoundationPanel"]
  ViewModels["MainViewModel + SecondViewModel"]
  Catalog["RuntimeFoundationCatalog"]
  TestClient["DeterministicAgentRuntimeClient"]
  Probe["ProviderToolchainProbe"]
  Contracts["Typed IDs + contracts"]
  Future["Future Orleans + Agent Framework integrations"]

  App --> ViewModels
  Views --> ViewModels
  ViewModels --> Catalog
  Catalog --> TestClient
  Catalog --> Probe
  Catalog --> Contracts
  Future --> Contracts
  Future --> Catalog
```

## Navigation Index

### Planning and decision docs

- `Solution governance` — [../AGENTS.md](../AGENTS.md)
- `Task plan` — [../vertical-slice-runtime-foundation.plan.md](../vertical-slice-runtime-foundation.plan.md)
- `Primary architecture decision` — [ADR-0001](./ADR/ADR-0001-agent-control-plane-architecture.md)
- `Vertical-slice solution decision` — [ADR-0003](./ADR/ADR-0003-vertical-slices-and-ui-only-uno-app.md)
- `Feature spec` — [Agent Control Plane Experience](./Features/agent-control-plane-experience.md)
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
- `Agent builder view model` — [../DotPilot/Presentation/SecondViewModel.cs](../DotPilot/Presentation/SecondViewModel.cs)
- `Reusable runtime panel` — [../DotPilot/Presentation/Controls/RuntimeFoundationPanel.xaml](../DotPilot/Presentation/Controls/RuntimeFoundationPanel.xaml)
- `Shell configuration contract` — [../DotPilot.Core/Features/ApplicationShell/AppConfig.cs](../DotPilot.Core/Features/ApplicationShell/AppConfig.cs)
- `Runtime foundation contracts` — [../DotPilot.Core/Features/RuntimeFoundation/RuntimeFoundationContracts.cs](../DotPilot.Core/Features/RuntimeFoundation/RuntimeFoundationContracts.cs)
- `Runtime communication problems` — [../DotPilot.Core/Features/RuntimeCommunication/RuntimeCommunicationProblems.cs](../DotPilot.Core/Features/RuntimeCommunication/RuntimeCommunicationProblems.cs)
- `Control-plane domain contracts` — [../DotPilot.Core/Features/ControlPlaneDomain/SessionExecutionContracts.cs](../DotPilot.Core/Features/ControlPlaneDomain/SessionExecutionContracts.cs)
- `Provider and tool contracts` — [../DotPilot.Core/Features/ControlPlaneDomain/ProviderAndToolContracts.cs](../DotPilot.Core/Features/ControlPlaneDomain/ProviderAndToolContracts.cs)
- `Runtime issue catalog` — [../DotPilot.Core/Features/RuntimeFoundation/RuntimeFoundationIssues.cs](../DotPilot.Core/Features/RuntimeFoundation/RuntimeFoundationIssues.cs)
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
- Epic `#12` starts with contracts, sequencing, deterministic runtime coverage, and UI exposure before live Orleans or provider integration.
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
