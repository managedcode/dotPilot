# AGENTS.md

Project: `DotPilot`
Stack: `.NET 10`, `Uno Platform`, `Uno.Extensions.Navigation`, `Uno Toolkit`, desktop-first XAML UI

## Purpose

- This project contains the production `Uno Platform` application shell and presentation layer.
- It owns app startup, route registration, desktop window behavior, shared styling resources, and the current static desktop screens.
- It is evolving into the desktop control plane for local-first agent operations across coding, research, orchestration, and operator workflows.
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
- Reuse the current desktop workbench direction: left navigation, central session surface, right-side inspector, and the agent-builder flow should evolve into real runtime-backed features instead of being replaced with a different shell concept.
- Prefer declarative `Uno.Extensions.Navigation` in XAML via `uen:Navigation.Request` over page code-behind navigation calls.
- Keep business logic, persistence, networking workflows, and non-UI orchestration out of page code-behind.
- Build presentation with `MVVM`-friendly view models and separate reusable XAML components instead of large monolithic pages.
- Organize non-UI work by feature-aligned vertical slices so each slice can evolve and ship without creating a shared dump of cross-cutting services in the app project.
- Replace scaffold sample data with real runtime-backed state as product features arrive; do not throw away the shell structure unless a later documented decision explicitly requires it.
- Reuse shared resources and small XAML components instead of duplicating large visual sections across pages.
- Treat desktop window sizing and positioning as an app-startup responsibility in `App.xaml.cs`.
- Prefer `Microsoft Agent Framework` for orchestration, sessions, workflows, HITL, MCP-aware runtime features, and OpenTelemetry-based observability hooks.
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
- The current screens already imply the future product IA, so backlog and implementation work should map onto the existing shell concepts instead of inventing unrelated pages.
