# AGENTS.md

Project: `DotPilot`
Stack: `.NET 10`, `Uno Platform`, `Uno.Extensions.Navigation`, `Uno Toolkit`, desktop-first XAML UI

## Purpose

- This project contains the production `Uno Platform` application shell and presentation layer.
- It owns app startup, route registration, desktop window behavior, shared styling resources, and the current static desktop screens.

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
- Prefer declarative `Uno.Extensions.Navigation` in XAML via `uen:Navigation.Request` over page code-behind navigation calls.
- Keep business logic, persistence, networking workflows, and non-UI orchestration out of page code-behind.
- Build presentation with `MVVM`-friendly view models and separate reusable XAML components instead of large monolithic pages.
- Replace template or sample screens completely when real product screens arrive; do not layer new design work on top of scaffold UI.
- Reuse shared resources and small XAML components instead of duplicating large visual sections across pages.
- Treat desktop window sizing and positioning as an app-startup responsibility in `App.xaml.cs`.

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

## Local Risks Or Protected Areas

- `App.xaml.cs` controls route registration and desktop window startup; changes there affect every screen.
- `App.xaml` and `Styles/*` are shared styling roots; careless edits can regress the whole app.
- `Presentation/*Page.xaml` files can grow quickly; split repeated sections before they violate maintainability limits.
- This project is currently the visible product surface, so every visual change should preserve desktop responsiveness and accessibility-minded structure.
