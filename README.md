# dotPilot

`dotPilot` is a desktop-first, local-first control plane for AI agents built with `.NET 10` and `Uno Platform`.

The product is being shaped as a single operator workbench where you can:

- manage agent profiles and fleets
- connect external agent runtimes such as `Codex`, `Claude Code`, and `GitHub Copilot`
- run local models through `LLamaSharp` and `ONNX Runtime`
- browse repositories, inspect files, review diffs, and work with Git
- orchestrate sessions, approvals, telemetry, replay, and evaluation from one UI

Coding workflows are first-class, but `dotPilot` is not a coding-only shell. The target product also supports research, analysis, orchestration, reviewer, and operator-style agent flows.

## Current Status

The repository is currently in the **planned architecture and backlog** stage for the control-plane direction.

What already exists:

- a desktop-first `Uno Platform` shell with the future workbench information architecture
- a dedicated agent-builder screen
- `NUnit` unit tests and `Uno.UITest` browser UI coverage
- planning artifacts for the approved direction
- a detailed GitHub issue backlog for implementation

What is planned next:

- embedded `Orleans` host inside the desktop app
- `Microsoft Agent Framework` orchestration and session workflows
- SDK-first provider adapters
- MCP and repo-intelligence tooling
- local runtime support
- OpenTelemetry-first observability and official `.NET` AI evaluation

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
- [Feature Spec: Agent Control Plane Experience](docs/Features/agent-control-plane-experience.md)
- [Task Plan: Agent Control Plane Backlog](agent-control-plane-backlog.plan.md)
- [Root Governance](AGENTS.md)

GitHub tracking:

- [Issue Backlog](https://github.com/managedcode/dotPilot/issues)

## Repository Layout

```text
.
├── DotPilot/                 # Uno desktop app and current shell
├── DotPilot.Tests/           # NUnit in-process tests
├── DotPilot.UITests/         # Uno.UITest browser coverage
├── docs/
│   ├── ADR/                  # architectural decisions
│   ├── Features/             # executable feature specs
│   └── Architecture.md       # repo architecture map
├── AGENTS.md                 # root governance for humans and agents
└── DotPilot.slnx             # solution entry point
```

## Getting Started

### Prerequisites

- `.NET SDK 10.0.103`
- `Uno.Sdk 6.5.31`
- a supported desktop environment for `net10.0-desktop`

### Core Commands

```bash
dotnet build DotPilot.slnx
dotnet test DotPilot.slnx
dotnet format DotPilot.slnx --verify-no-changes
dotnet build DotPilot.slnx -warnaserror
dotnet publish DotPilot/DotPilot.csproj -c Release -f net10.0-desktop
```

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
- The current machine-local baseline may still hit a `Uno.Resizetizer` file-lock during `dotnet build`; that risk is documented in [agent-control-plane-backlog.plan.md](agent-control-plane-backlog.plan.md).
