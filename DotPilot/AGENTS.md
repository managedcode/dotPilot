# AGENTS.md

Project: `DotPilot`
Stack: `.NET 10`, `Uno Platform`, `Uno.Extensions.Navigation`, `Uno Toolkit`, desktop-first XAML UI

## Purpose

- This project contains the production `Uno Platform` application shell and presentation layer.
- It owns app startup, route registration, desktop window behavior, shared styling resources, and the desktop chat experience for agent sessions.
- It also owns app-host and application-configuration types that are specific to the desktop shell.
- It is evolving into a chat-first desktop control plane for local-first agent operations across coding, research, orchestration, and operator workflows.
- It must remain the presentation host for the product, while feature logic lives in separate vertical-slice class libraries.

## Entry Points

- `DotPilot.csproj`
- `App.xaml`
- `App.xaml.cs`
- `Platforms/Desktop/Program.cs`
- `Presentation/Shell/Views/Shell.xaml`
- `Presentation/Chat/Views/ChatPage.xaml`
- `Presentation/AgentBuilder/Views/AgentBuilderPage.xaml`
- `Presentation/Settings/Views/SettingsPage.xaml`
- `Styles/ColorPaletteOverride.xaml`

## Boundaries

- Keep this project focused on app composition, presentation, routing, and platform startup concerns.
- Keep feature/domain/runtime code out of this project; reference it through slice-owned contracts and application services from separate DLLs.
- Keep the Uno UI as a thin representation layer: background orchestration, long-running commands, and durable session updates should come from `DotPilot.Core` services instead of page-owned workflows.
- Build the visible product around a desktop chat shell: session list, active transcript, terminal-like activity pane, agent/profile controls, and provider settings are the primary surfaces.
- Keep agent creation prompt-first in the UI: the default `New agent` experience should start from a natural-language description that generates a draft agent, while manual field-by-field configuration stays a secondary fallback path.
- Keep a visible default-agent path in the shell: the app should surface a usable system/default agent by default and must not make the operator build everything manually before they can start a session.
- Starting a chat from an agent card or immediately after creating an agent must open/select the resulting chat session directly; do not leave the operator on `Agents` with a message telling them to switch to `Chat` manually.
- In `New agent`, the selected provider must be the single source of truth for the provider status card and model dropdown; switching from `Codex` to `Claude Code` or `GitHub Copilot` must immediately replace the shown suggested/supported models and provider summary instead of leaving stale values from the previous provider.
- Keep debug fallback out of the operator-facing authoring surface: `New agent` must only present real provider/model choices, and when no real provider is enabled or installed the screen should direct the operator to `Providers` instead of selecting debug defaults.
- Treat agent creation as profile authoring, not role assignment: do not expose role pickers or role-derived copy in the operator flow because an agent is created from its prompt, instructions, provider, model, and system prompt.
- Do not fabricate tool lists, skill lists, role taxonomies, or capability chips in the operator flow when the app does not have a real backing registry or runtime implementation for them; if a capability is not real, leave it out of the UI and saved draft.
- Do not present a live provider like Codex in the desktop shell as selected or runnable when the local runtime still cannot execute through its installed CLI; the visible session path must match the actual provider that will answer.
- Do not use workbench, issue-center, domain-browser, or other backlog-driven IA labels as the product shell.
- Do not preserve legacy prototype pages or controls once the replacement chat/session surface is underway; remove obsolete UI paths instead of carrying both shells.
- Keep one consistent desktop app chrome across all primary routes: the left rail, branding, and operator footer should stay structurally stable while the main content region changes.
- Treat primary navigation as a state switch inside one desktop shell, not as a chance to rebuild page-specific chrome; `Chat`, `Agents`, and `Providers` should share the same shell rhythm and only replace the main working surface.
- Keep shell geometry and control sizing stable across route changes; the left rail, nav rows, footer profile block, and main page header rhythm must not jump or reflow between `Chat`, `Agents`, and `Providers`.
- Do not leave placeholder screen names in the presentation layer: page, model, and generated view-model names must describe the actual feature surface such as `ChatPage` or `AgentBuilderModel`, not scaffolding leftovers like `MainPage` or `SecondModel`.
- Avoid duplicated side-panel content and oversized decorative copy; prefer compact navigation, clear current-task headers, and content-first layouts.
- Avoid placeholder-looking XAML chrome such as ASCII pseudo-icons, duplicate provider lists, inflated pill buttons, or decorative labels that repeat the same state in multiple panes.
- Do not let the desktop shell collapse into an unstructured visual mash: keep reusable styles, brushes, templates, and spacing rules in dedicated XAML resources or focused controls instead of burying ad hoc visual decisions inline across pages.
- Do not hide distinct product features under one presentation umbrella directory such as `Presentation/AgentSessions`; keep `Chat`, `AgentBuilder`, `Settings`, `Shell`, and shared infrastructure in explicit feature roots.
- Inside each presentation feature root, keep `Models`, `Views`, `ViewModels`, `Controls`, and `Configuration` explicit instead of mixing page, view-model, model, and policy files together at the top level.
- The chat composer must expose an operator setting for send behavior with exactly two modes: `Enter` sends while `Enter` with modifiers inserts a new line, or `Enter` inserts a new line while `Enter` with modifiers sends; do not hardcode only one behavior.
- Prefer declarative `Uno.Extensions.Navigation` in XAML via `uen:Navigation.Request` over page code-behind navigation calls.
- Keep business logic, persistence, networking workflows, and non-UI orchestration out of page code-behind.
- Do not cast `DataContext` to concrete screen models or call their methods from control/page code-behind; if a framework event needs bridging, expose a bindable command or presentation-safe abstraction instead of coupling the view to a specific view-model type.
- Build presentation with projection-only `MVVM`/`MVUX`-friendly models and separate reusable XAML components instead of large monolithic pages; runtime coordination, provider probes, session-loading pipelines, and other orchestration must stay outside the UI layer.
- Organize non-UI work by feature-aligned vertical slices so each slice can evolve and ship without creating a shared dump of cross-cutting services in the app project.
- Replace scaffold sample data with real runtime-backed state as product features arrive; the shell should converge on the real chat/session workflow instead of preserving prototype-only concepts.
- Reuse shared resources and small XAML components instead of duplicating large visual sections across pages.
- Treat desktop window sizing and positioning as an app-startup responsibility in `App.xaml.cs`.
- For local UI debugging on this machine, run the real desktop head and prefer local `Uno` app tooling or MCP inspection over `browserwasm` reproduction unless the task is specifically about `DotPilot.UITests`.
- Do not let ordinary view-model binding or section switching trigger duplicate provider CLI probes or log expected async cancellation as failures; the shell should stay quiet and reactive during normal navigation.
- Prefer `Microsoft Agent Framework` for orchestration, sessions, workflows, HITL, MCP-aware runtime features, and OpenTelemetry-based observability hooks.
- Keep the prompt-to-agent interpreter outside the page layer: the Uno shell should collect the user prompt and render the generated draft, while the runtime or a dedicated system-agent orchestration service decides agent name, description, tools, providers, and policy-compliant defaults.
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
- Screen switches, tab changes, and menu navigation in this project must reuse already-available in-memory projections; avoid view-model constructors or activation hooks that trigger cold runtime work during ordinary navigation.
- `DotPilot.csproj` keeps `GenerateDocumentationFile=true` with `CS1591` suppressed so Roslyn `IDE0005` stays active in CI across desktop, core, and browserwasm targets; do not remove that exception unless full XML documentation becomes part of the enforced quality bar.
- Product wording and navigation here set the real user expectation; avoid leaking architecture slice names, issue numbers, or backlog jargon into the visible shell.
