# AGENTS.md

Project: `DotPilot`
Stack: `.NET 10`, `Uno Platform`, `Uno.Extensions.Navigation`, `Uno Toolkit`, desktop-first XAML UI

## Purpose

- This project contains the production `Uno Platform` application shell and presentation layer.
- It owns app startup, route registration, desktop window behavior, shared styling resources, and the desktop chat experience for agent sessions.
- It is evolving into a chat-first desktop control plane for local-first agent operations across coding, research, orchestration, and operator workflows.
- It must remain the presentation host for the product, while feature logic lives in separate vertical-slice class libraries.

## Entry Points

- `DotPilot.csproj`
- `App.xaml`
- `App.xaml.cs`
- `Platforms/Desktop/Program.cs`
- `Presentation/Shell.xaml`
- `Presentation/MainPage.xaml`
- `Presentation/SecondPage.xaml`
- `Styles/ColorPaletteOverride.xaml`

## Boundaries

- Keep this project focused on app composition, presentation, routing, and platform startup concerns.
- Keep feature/domain/runtime code out of this project; reference it through slice-owned contracts and application services from separate DLLs.
- Build the visible product around a desktop chat shell: session list, active transcript, terminal-like activity pane, agent/profile controls, and provider settings are the primary surfaces.
- Do not use workbench, issue-center, domain-browser, or other backlog-driven IA labels as the product shell.
- Do not preserve legacy prototype pages or controls once the replacement chat/session surface is underway; remove obsolete UI paths instead of carrying both shells.
- Prefer declarative `Uno.Extensions.Navigation` in XAML via `uen:Navigation.Request` over page code-behind navigation calls.
- Keep business logic, persistence, networking workflows, and non-UI orchestration out of page code-behind.
- Build presentation with `MVVM`-friendly view models and separate reusable XAML components instead of large monolithic pages.
- Organize non-UI work by feature-aligned vertical slices so each slice can evolve and ship without creating a shared dump of cross-cutting services in the app project.
- Replace scaffold sample data with real runtime-backed state as product features arrive; the shell should converge on the real chat/session workflow instead of preserving prototype-only concepts.
- Reuse shared resources and small XAML components instead of duplicating large visual sections across pages.
- Treat desktop window sizing and positioning as an app-startup responsibility in `App.xaml.cs`.
- For local UI debugging on this machine, run the real desktop head and prefer local `Uno` app tooling or MCP inspection over `browserwasm` reproduction unless the task is specifically about `DotPilot.UITests`.
- Prefer `Microsoft Agent Framework` for orchestration, sessions, workflows, HITL, MCP-aware runtime features, and OpenTelemetry-based observability hooks.
- Persist durable chat/session/operator state outside the UI layer, using `EF Core` with `SQLite` for the local desktop store when data must survive restarts.
- Prefer official `.NET` AI evaluation libraries under `Microsoft.Extensions.AI.Evaluation*` for quality and safety evaluation features.
- Do not plan or wire `MLXSharp` into the first product wave for this project.

## Local Commands

- `build-app`: `dotnet build DotPilot/DotPilot.csproj`
- `publish-desktop`: `dotnet publish DotPilot/DotPilot.csproj -c Release -f net10.0-desktop`
- `run-desktop`: `dotnet run --project DotPilot/DotPilot.csproj -f net10.0-desktop`
- `run-wasm`: `dotnet run --project DotPilot/DotPilot.csproj -f net10.0-browserwasm`
- `test-unit`: `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-ui-ux`
- `mcaf-architecture-overview`
- `mcaf-testing`
- `figma-implement-design`
- `mcaf-feature-spec`
- `mcaf-solution-governance`

## Local Risks Or Protected Areas

- `App.xaml.cs` controls route registration and desktop window startup; changes there affect every screen.
- `App.xaml` and `Styles/*` are shared styling roots; careless edits can regress the whole app.
- `Presentation/*Page.xaml` files can grow quickly; split repeated sections before they violate maintainability limits.
- This project is currently the visible product surface, so every visual change should preserve desktop responsiveness and accessibility-minded structure.
- `DotPilot.csproj` keeps `GenerateDocumentationFile=true` with `CS1591` suppressed so Roslyn `IDE0005` stays active in CI across desktop, core, and browserwasm targets; do not remove that exception unless full XML documentation becomes part of the enforced quality bar.
- Product wording and navigation here set the real user expectation; avoid leaking architecture slice names, issue numbers, or backlog jargon into the visible shell.
