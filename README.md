# dotPilot

`dotPilot` is a desktop-first, local-first control plane for AI agents built with `.NET 10` and `Uno Platform`.

## Product Summary

`dotPilot` is designed as a single operator workbench for running, supervising, and reviewing agent workflows from one desktop UI. Coding workflows are first-class, but the product is not limited to coding agents. The same control plane is intended to support research, analysis, orchestration, reviewer, and operator-style flows.

From the workbench, the operator should be able to:

- manage agent profiles and fleets
- connect external agent runtimes such as `Codex`, `Claude Code`, and `GitHub Copilot`
- run local models through `LLamaSharp` and `ONNX Runtime`
- browse repositories, inspect files, review diffs, and work with Git
- orchestrate sessions, approvals, telemetry, replay, and evaluation from one UI

## Main Features

### Available In The Current Repository

- a desktop-first three-pane workbench shell
- repository tree search and open-file navigation
- read-only file inspection and diff-review surface
- artifact dock and runtime log console
- unified settings shell for providers, policies, and storage
- dedicated agent-builder screen
- deterministic runtime foundation panel for provider readiness and control-plane state
- `NUnit` unit tests plus `Uno.UITest` browser UI coverage

### Main Product Capabilities On The Roadmap

- multi-agent session composition and orchestration
- embedded local-first runtime hosting with `Orleans`
- SDK-first provider integrations for `Codex`, `Claude Code`, and `GitHub Copilot`
- local model runtime support through `LLamaSharp` and `ONNX Runtime`
- approvals, replay, audit trails, and artifact inspection
- OpenTelemetry-first observability and official `.NET` AI evaluation flows

## Current Status

The repository is in the **active foundation and workbench implementation** stage.

What already exists:

- the first runtime foundation slices in `DotPilot.Core` and `DotPilot.Runtime`
- the first operator workbench slice for repository browsing, document inspection, artifacts, logs, and settings
- a presentation-only `Uno Platform` app shell with separate non-UI class-library boundaries
- unit, coverage, and UI automation validation paths
- architecture docs, ADRs, feature specs, and GitHub backlog tracking

What is planned next:

- embedded `Orleans` hosting inside the desktop app
- `Microsoft Agent Framework` orchestration and session workflows
- richer provider adapters and toolchain management
- MCP and repository-intelligence tooling
- local runtime execution flows
- telemetry, replay, and evaluation surfaces backed by real runtime events

## Product Direction

The approved architectural defaults are:

- `dotPilot` stays desktop-first and reuses the current shell direction instead of replacing it
- the first runtime cut is **local-first** with an embedded `Orleans` silo
- `Session = grain`, with related workspace, fleet, artifact, and policy state
- `Microsoft Agent Framework` is the preferred orchestration layer
- provider integrations are SDK-first:
  - `ManagedCode.CodexSharpSDK`
  - `ManagedCode.ClaudeCodeSharpSDK`
  - `GitHub.Copilot.SDK`
- tool federation is centered on `ManagedCode.MCPGateway`
- repository intelligence is centered on `ManagedCode.RagSharp`
- agent quality and safety evaluation use `Microsoft.Extensions.AI.Evaluation*`
- observability is OpenTelemetry-first, with local-first visibility and optional cloud export later
- `MLXSharp` is explicitly **not** part of the first roadmap wave

## Documentation Map

Start here if you want the current source of truth:

- [Architecture Overview](docs/Architecture.md)
- [ADR-0001: Agent Control Plane Architecture](docs/ADR/ADR-0001-agent-control-plane-architecture.md)
- [ADR-0003: Vertical Slices And UI-Only Uno App](docs/ADR/ADR-0003-vertical-slices-and-ui-only-uno-app.md)
- [Feature Spec: Agent Control Plane Experience](docs/Features/agent-control-plane-experience.md)
- [Feature Spec: Workbench Foundation](docs/Features/workbench-foundation.md)
- [Task Plan: Vertical Slice Runtime Foundation](vertical-slice-runtime-foundation.plan.md)
- [Task Plan: Workbench Foundation](issue-13-workbench-foundation.plan.md)
- [Root Governance](AGENTS.md)

GitHub tracking:

- [Issue Backlog](https://github.com/managedcode/dotPilot/issues)

## Repository Layout

```text
.
├── DotPilot/                 # Uno desktop presentation host
├── DotPilot.Core/            # Vertical-slice contracts and typed identifiers
├── DotPilot.Runtime/         # Provider-independent runtime implementations
├── DotPilot.ReleaseTool/     # Release automation utilities
├── DotPilot.Tests/           # NUnit contract and composition tests
├── DotPilot.UITests/         # Uno.UITest browser coverage
├── docs/
│   ├── ADR/                  # architectural decisions
│   ├── Features/             # executable feature specs
│   └── Architecture.md       # repo architecture map
├── AGENTS.md                 # root governance for humans and agents
├── vertical-slice-runtime-foundation.plan.md
├── issue-13-workbench-foundation.plan.md
└── DotPilot.slnx             # solution entry point
```

## Getting Started

### Prerequisites

- `.NET SDK 10.0.103`
- `Uno.Sdk 6.5.31`
- a supported desktop environment for `net10.0-desktop`

### Core Commands

```bash
dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false
dotnet test DotPilot.slnx
dotnet format DotPilot.slnx --verify-no-changes
dotnet publish DotPilot/DotPilot.csproj -c Release -f net10.0-desktop
```

`build` and `analyze` use the same serialized `-warnaserror` command because the multi-target Uno app must not build in parallel in a shared workspace or CI cache.

### Run the App

```bash
dotnet run --project DotPilot/DotPilot.csproj -f net10.0-desktop
```

### Run the Browser UI Suite

```bash
dotnet test DotPilot.UITests/DotPilot.UITests.csproj
```

## Quality Gates

This repository treats the following as mandatory:

- real `NUnit` unit tests
- real `Uno.UITest` browser UI coverage
- repo-root `.editorconfig` as the formatting and analyzer source of truth
- central package management through `Directory.Packages.props`
- descriptive GitHub Actions validation and desktop artifact publishing

## Notes

- The current repository still contains prototype data in the shell; the new backlog tracks the transition to runtime-backed features.
- If you are working on non-trivial changes, start with [AGENTS.md](AGENTS.md) and [docs/Architecture.md](docs/Architecture.md).
- The current machine-local baseline may still hit a `Uno.Resizetizer` file-lock during `dotnet build`; that risk is documented in [ci-build-lock-fix.plan.md](ci-build-lock-fix.plan.md).
