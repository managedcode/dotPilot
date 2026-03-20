<div align="center">

# dotPilot

### Local Agent Orchestrator

**Run AI agents locally. Build workflows. Own your data.**

*Intent becomes the interface* 🎯

[![Download](https://img.shields.io/github/v/release/managedcode/dotPilot?label=Download&style=for-the-badge)](https://github.com/managedcode/dotPilot/releases/latest)
[![License](https://img.shields.io/github/license/managedcode/dotPilot?style=for-the-badge)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-purple?style=for-the-badge)](https://dotnet.microsoft.com/)
[![YouTube](https://img.shields.io/badge/YouTube-Subscribe-red?style=for-the-badge&logo=youtube)](https://www.youtube.com/@ManagedCode)

[Website](https://dotpilot.managed-code.com) · [Downloads](#downloads) · [Getting Started](#getting-started) · [YouTube](https://www.youtube.com/@ManagedCode) · [Issues](https://github.com/managedcode/dotPilot/issues)

---

</div>

## What is dotPilot?

dotPilot is an **open source desktop app** for running AI agents locally on your machine.

- 🤖 **Run multiple agents** — Launch and manage several AI agents at once
- 🔄 **Build workflows** — Create multi-agent pipelines with Microsoft Agent Framework
- 🔌 **Any provider** — Codex, Claude Code, GitHub Copilot, Gemini, or local models
- 🏠 **100% local** — Your data stays on your device, no cloud required
- 💬 **Natural language** — Create agents by describing what you need

Built with **C#**, **.NET 10**, and **Uno Platform**.

---

## Downloads

| Platform | Architecture | Link |
|:--------:|:------------:|:----:|
| 🍎 **macOS** | Apple Silicon | [Download .dmg](https://github.com/managedcode/dotPilot/releases/latest) |
| 🪟 **Windows** | x64 | [Download .exe](https://github.com/managedcode/dotPilot/releases/latest) |
| 🐧 **Linux** | x64 | [Download .snap](https://github.com/managedcode/dotPilot/releases/latest) |

---

## Supported Providers

| Provider | Type | Status |
|:---------|:-----|:------:|
| **Codex CLI** | OpenAI coding agent | ✅ |
| **Claude Code** | Anthropic assistant | ✅ |
| **GitHub Copilot** | Microsoft AI | ✅ |
| **Gemini** | Google AI | ✅ |
| **OpenAI API** | Direct API | ✅ |
| **Azure OpenAI** | Enterprise API | ✅ |
| **LLamaSharp** | Local models | ✅ |
| **ONNX Runtime** | Local inference | ✅ |

---

## Getting Started

### Download & Run

1. Download the latest release for your platform from [Releases](https://github.com/managedcode/dotPilot/releases/latest)
2. Install and launch dotPilot
3. Configure your preferred AI provider in Settings
4. Create your first agent and start chatting

### Build from Source

```bash
git clone https://github.com/managedcode/dotPilot.git
cd dotPilot
dotnet build DotPilot.slnx
dotnet run --project DotPilot/DotPilot.csproj -f net10.0-desktop
```

**Requirements:** `.NET SDK 10.0.103`, `Uno.Sdk 6.5.31`

---

## Features

### 🤖 Run Multiple Agents Simultaneously

Launch as many AI agents as you need, all running in parallel on your local machine. Each agent operates with its own isolated context, memory, and tool set. The **Fleet Board** gives you a real-time dashboard to monitor all active sessions — see which agents are working, what they're doing, and their current status. Switch between agents instantly, pause or resume sessions, and manage your entire agent fleet from one unified interface.

### 🔄 Build Agentic Workflows with Microsoft Agent Framework

dotPilot integrates with [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) to enable sophisticated multi-agent orchestration. Design workflows where agents collaborate — one agent researches, another analyzes, a third generates code. Use **sequential pipelines** for step-by-step tasks, **parallel execution** for independent workloads, or **handoff patterns** where agents pass context to each other. All workflows support streaming output, checkpoints for long-running tasks, and human-in-the-loop approvals when you need to stay in control.

### 🔌 Connect Any Provider — Cloud or Local

Freedom to choose your AI backend. Connect to **Codex CLI** for OpenAI's coding agent, **Claude Code** for Anthropic's assistant, **GitHub Copilot** for Microsoft's AI pair programmer, or **Gemini** for Google's models. Need direct API access? Use **OpenAI API** or **Azure OpenAI** for enterprise scenarios. Want full privacy? Run models entirely on your hardware with **LLamaSharp** or **ONNX Runtime** — zero data leaves your machine. Mix and match providers across different agents based on what each task needs.

### 💬 Intent-Driven Agent Creation

Stop configuring, start describing. Tell dotPilot what kind of agent you need in plain language: *"I need an agent that reviews pull requests and suggests improvements"* or *"Create a research agent that summarizes technical papers"*. The system generates the complete agent profile — system prompt, tool configuration, provider selection, and behavioral settings. Refine with follow-up instructions or dive into manual config when you need fine-grained control. **Intent becomes the interface.**

### 📊 Full Observability with OpenTelemetry

Every agent action is traceable. dotPilot integrates **OpenTelemetry** to capture detailed telemetry for each session — every prompt sent, every response received, every tool invocation, every workflow step. Visualize agent reasoning flows, identify performance bottlenecks, debug unexpected behaviors. Export traces to your preferred observability backend or analyze locally. When something goes wrong, you'll know exactly where and why.

### 🔒 100% Local, 100% Private

Your data never leaves your device unless you explicitly choose a cloud provider. All session history, agent configurations, and conversation transcripts are stored locally in **SQLite**. No telemetry sent to external servers. No account required to use local models. Run completely air-gapped if needed. You own your data, your workflows, and your AI infrastructure.

---

## Tech Stack

| | |
|:--|:--|
| **Language** | C# |
| **Runtime** | .NET 10 |
| **UI** | Uno Platform |
| **Orchestration** | Microsoft Agent Framework |
| **Database** | SQLite + EF Core |
| **Observability** | OpenTelemetry |

---

## Building in Public

We're developing dotPilot in the open.

- 📺 **YouTube** — [@Managed-Code](https://www.youtube.com/@managed-code)
- 💬 **Issues** — [GitHub Issues](https://github.com/managedcode/dotPilot/issues)
- 🌐 **Website** — [dotpilot.managed-code.com](https://dotpilot.managed-code.com)

---

## Documentation

- [Architecture](docs/Architecture.md)
- [Contributing](AGENTS.md)

---

## License

Open source. See [LICENSE](LICENSE) for details.

---

<div align="center">

**[dotpilot.managed-code.com](https://dotpilot.managed-code.com)**

Made by [ManagedCode](https://dotpilot.managed-code.com)

</div>
