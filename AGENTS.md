# AGENTS.md

Project: dotPilot
Stack: .NET 10 `Uno Platform` desktop app with central package management, `NUnit` unit tests, and `Uno.UITest` browser UI coverage

Follows [MCAF](https://mcaf.managed-code.com/)

---

## Purpose

This file defines how AI agents work in this solution.

- Root `AGENTS.md` holds the global workflow, shared commands, cross-cutting rules, and global skill catalog.
- In multi-project solutions, each project or module root MUST have its own local `AGENTS.md`.
- Local `AGENTS.md` files add project-specific entry points, boundaries, commands, risks, and applicable skills.
- `dotPilot` is a desktop control plane for agents in general. Coding agents are first-class, but repository governance, architecture, and planning must also support research, analysis, orchestration, operator, and mixed-provider agent flows.

## Solution Topology

- Solution root: `.` (`DotPilot.slnx`)
- Projects or modules with local `AGENTS.md` files:
  - `DotPilot`
  - `DotPilot.Core`
  - `DotPilot.Tests`
  - `DotPilot.UITests`
- Shared solution artifacts:
  - `.editorconfig`
  - `Directory.Build.props`
  - `Directory.Packages.props`
  - `global.json`
  - `docs/Architecture.md`
  - `.codex/skills/mcaf-*`

## Rule Precedence

1. Read the solution-root `AGENTS.md` first.
2. Read the nearest local `AGENTS.md` for the area you will edit.
3. Apply the stricter rule when both files speak to the same topic.
4. Local `AGENTS.md` files may refine or tighten root rules, but they must not silently weaken them.
5. If a local rule needs an exception, document it explicitly in the nearest local `AGENTS.md`, ADR, or feature doc.

## Conversations (Self-Learning)

Learn the user's stable habits, preferences, and corrections. Record durable rules here instead of relying on chat history.

Before doing any non-trivial task, evaluate the latest user message.
If it contains a durable rule, correction, preference, or workflow change, update `AGENTS.md` first.
If it is only task-local scope, do not turn it into a lasting rule.

Update this file when the user gives:

- a repeated correction
- a permanent requirement
- a lasting preference
- a workflow change
- a high-signal frustration that indicates a rule was missed

Extract rules aggressively when the user says things equivalent to:

- "never", "don't", "stop", "avoid"
- "always", "must", "make sure", "should"
- "remember", "keep in mind", "note that"
- "from now on", "going forward"
- "the workflow is", "we do it like this"

Preferences belong in `## Preferences`:

- positive preferences go under `Likes`
- negative preferences go under `Dislikes`
- comparisons should become explicit rules or preferences

Corrections should update an existing rule when possible instead of creating duplicates.

Treat these as strong signals and record them immediately:

- anger, swearing, sarcasm, or explicit frustration
- ALL CAPS, repeated punctuation, or "don't do this again"
- the same mistake happening twice
- the user manually undoing or rejecting a recurring pattern

Do not record:

- one-off instructions for the current task
- temporary exceptions
- requirements that are already captured elsewhere without change

Rule format:

- one instruction per bullet
- place it in the right section
- capture the why, not only the literal wording
- remove obsolete rules when a better one replaces them

## Global Skills

List only the skills this solution actually uses.

- `mcaf-dotnet` — primary entry skill for normal C# and .NET work in this solution.
- `mcaf-dotnet-features` — decide which modern C# and .NET 10 features are safe for the active project.
- `mcaf-solution-governance` — create or refine the root and project-local `AGENTS.md` files.
- `mcaf-testing` — plan test scope, layering, and regression coverage.
- `mcaf-dotnet-quality-ci` — align `.editorconfig`, analyzers, formatting, and CI-quality gates.
- `mcaf-dotnet-complexity` — review or tighten complexity limits and complexity tooling.
- `mcaf-solid-maintainability` — enforce SOLID, SRP, maintainability limits, and exception handling.
- `mcaf-architecture-overview` — create or maintain `docs/Architecture.md`.
- `mcaf-ci-cd` — design or review CI and deployment gates.
- `mcaf-ui-ux` — handle UI architecture, accessibility, and design-handoff rules for the agent-facing UI.
- `figma-implement-design` — translate Figma handoff into `Uno Platform` desktop XAML without drifting into web-specific implementation patterns.

Skill-management rules for this `.NET` solution:

- `mcaf-dotnet` is the entry skill and routes to specialized `.NET` skills.
- Route test planning through `mcaf-testing`; the current repo uses `NUnit` on the `VSTest` runner, so do not apply `TUnit` or `Microsoft.Testing.Platform` assumptions.
- Add tool-specific `.NET` skills only when the repository actually uses those tools in CI or local verification.
- Keep only `mcaf-*` skills in agent skill directories.
- When upgrading skills, recheck `build`, `test`, `format`, `analyze`, and `coverage` commands against the repo toolchain.

## Rules to Follow (Mandatory)

### Commands

- `build`: `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
- `test`: `dotnet test DotPilot.slnx`
- `format`: `dotnet format DotPilot.slnx --verify-no-changes`
- `analyze`: `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
- `coverage`: `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
- `publish-desktop`: `dotnet publish DotPilot/DotPilot.csproj -c Release -f net10.0-desktop`

For this app:

- unit tests currently use `NUnit` through the default `VSTest` runner
- UI tests live in `DotPilot.UITests` and are a mandatory part of normal verification; the harness must provision or resolve browser-driver prerequisites automatically instead of skipping when local setup is missing
- a canceled, timed-out, or hanging `DotPilot.UITests` run is a harness failure to fix, not an acceptable substitute for a real pass or fail result in CI
- when debugging or validating the browser UI path, do not launch the app manually outside `DotPilot.UITests`; reproduce and diagnose only through the real UI-test harness so failures match the enforced verification path
- `format` uses `dotnet format --verify-no-changes` as a local pre-push check; GitHub Actions validation should not spend CI time rechecking formatting drift that must already be fixed before push
- coverage uses the `coverlet.collector` integration on `DotPilot.Tests` with the repo runsettings file to keep generated Uno artifacts out of the coverage path
- desktop release publishing uses `dotnet publish DotPilot/DotPilot.csproj -c Release -f net10.0-desktop`; the validation workflow stays focused on build and automated tests, while the release workflow owns desktop publish outputs for macOS, Windows, and Linux
- `LangVersion` is pinned to `14` at the root
- prefer the newest stable `.NET 10` and `C#` language features that are supported by the pinned SDK and do not weaken readability, determinism, or analyzability
- the repo-root lowercase `.editorconfig` is the source of truth for formatting, naming, style, and analyzer severity
- local and CI build commands must pass `-warnaserror`; warnings are not an acceptable "green" build state in this repository
- do not run unit tests, UI tests, or coverage commands unless the user explicitly asks for test execution in the current turn; structural refactors and exploratory work should stop at code organization until the operator requests verification
- do not run parallel `dotnet` or `MSBuild` work that shares the same checkout, target outputs, or NuGet package cache; the multi-target Uno app must build serially in CI to avoid `Uno.Resizetizer` file-lock failures
- do not commit user-specific local paths, usernames, or machine-specific identifiers in tests, docs, snapshots, or fixtures; use neutral synthetic values so the repo stays portable and does not leak personal machine details
- keep local planning artifacts and analyzer scratch files out of git history: ignore `*.plan.md` and `CodeMetricsConfig.txt`, and do not commit or re-stage them because they are operator-local working files
- quality gates should prefer analyzer-backed build failures over separate one-off CI tools; for overloaded methods and maintainability drift, enable build-time analyzers such as `CA1502` instead of adding a formatting-only gate
- `Directory.Build.props` owns the shared analyzer and warning policy for future projects
- `Directory.Packages.props` owns centrally managed package versions
- `global.json` pins the .NET SDK and Uno SDK version used by the app and tests
- `DotPilot/DotPilot.csproj` keeps `GenerateDocumentationFile=true` with `CS1591` suppressed so `IDE0005` stays enforceable in CI across all target frameworks without inventing command-line-only build flags
- solution folders in `DotPilot.slnx` are allowed when they provide stable project categories such as `Libraries` and `Tests`; use them to keep the IDE readable, but keep project directory names, `.csproj` names, and namespaces honest to the real extracted subsystem
- project extraction must stay structurally honest: once a subsystem becomes its own DLL, keep its files, namespaces, local `AGENTS.md`, and direct app references inside that subsystem instead of leaving half of it behind in `DotPilot.Core`
- when a library already names the subsystem, do not add a duplicate same-name root folder inside it; expose its real slices directly from the project root instead of nesting them again under another copy of the subsystem name
- architecture work must keep a vertical-slice shape: each feature owns its contracts, orchestration, and tests behind clear boundaries instead of growing a shared horizontal service layer
- `DotPilot.Core` is the default home for non-UI code, but once a feature becomes large enough to deserve an architectural boundary, extract it into its own DLL instead of bloating `DotPilot.Core`
- do not create or reintroduce generic project, folder, namespace, or product language named `Runtime` unless the user explicitly asks for that exact boundary; the default non-UI home is `DotPilot.Core`, and vague runtime naming is considered architectural noise in this repo
- do not create or keep project, folder, or namespace names like `ControlPlaneDomain`; shared identifiers, contracts, models, and policies must live under explicit roots such as `Identifiers`, `Contracts`, `Models`, and `Policies` instead of behind a vague umbrella name
- every new large feature DLL must reference `DotPilot.Core` for shared abstractions and contracts, and the desktop app should reference that feature DLL explicitly instead of dragging the feature back into the UI project
- when a feature slice grows beyond a few files, split it into responsibility-based subfolders that mirror the slice's real concerns such as chat, drafting, providers, persistence, settings, or tests; do not leave large flat file dumps that force unrelated code to coexist in one directory
- do not hide multiple real features under one umbrella folder such as `AgentSessions` when the code actually belongs to distinct features like `Chat`, `AgentBuilder`, `Settings`, `Providers`, or `Workspace`; use explicit feature roots and keep logs, models, services, and tests under the feature that owns them
- inside each feature root, keep structural subfolders explicit: models go under `Models`, configuration and defaults under `Configuration` or `Composition`, views under `Views`, view-models under `ViewModels`, diagnostics under `Diagnostics`, and service/runtime types under a responsibility-specific folder; do not leave those file kinds mixed together at the feature root
- when a feature exposes commands, public interfaces, or specialized runtime contracts, put them in visible folders such as `Commands`, `Interfaces`, and other role-specific folders; do not bury them inside a generic `Contracts` folder where their role disappears from the tree
- keep the Uno app project presentation-only; domain, host, orchestration, integrations, and persistence code must live outside the UI project in class-library code so UI composition does not mix with feature implementation
- UI-facing shell and application-configuration types belong in `DotPilot`; `DotPilot.Core` stays the shared non-UI contract/application layer, while any future large subsystem should move into its own DLL only when it earns a clear architectural boundary
- UI-only preferences, keyboard behavior options, and other shell interaction settings belong in `DotPilot`, not in `DotPilot.Core`; `Core` must not own models or contracts that exist only to drive presentation behavior
- when the user asks to implement an epic, the delivery branch and PR must cover all of that epic's direct child issues that belong to the requested scope, not just one child issue with a partial close-out
- epic implementation PRs must include automated tests for every direct child issue they claim to cover, plus the broader runtime and UI regressions required by the touched flows
- do not claim an epic is implemented unless every direct child issue in the requested scope is both realized in code and covered by automated tests; partial coverage is not an acceptable close-out
- structure both `DotPilot.Tests` and `DotPilot.UITests` by vertical slice and explicit harness boundaries; do not keep test files in one flat project-root pile
- GitHub is the backlog, not the product: use issues and PRs only to drive task scope and traceability, and never copy GitHub issue text, labels, workflow language, or tracker metadata into production code, runtime snapshots, or user-facing UI
- never claim an epic is complete until its current GitHub scope is verified against the live issue graph; check which issues are real children versus issues that merely depend on the epic or belong to a different parent epic
- Desktop responsiveness is a product requirement: avoid synchronous probe, filesystem, network, or process work on UI-facing construction and navigation paths so the app stays fast and immediately reactive
- Prefer a thin desktop presentation layer over UI-owned orchestration: long-running work, background coordination, and durable session state should live in `DotPilot.Core` services and persistence boundaries, while the Uno UI mainly renders state and forwards operator commands
- Uno controls and page code-behind must not cast `DataContext` to concrete view-model/model types or invoke orchestration methods directly; route framework events through bindable commands, attached behaviors, dependency properties, or other presentation-safe seams so the view stays decoupled from runtime logic
- Do not invent a repo-specific product framing such as "workbench" unless the active issue or feature spec explicitly uses it; implement the app features described in the backlog instead of turning internal implementation language into the product narrative
- The primary product IA is a desktop chat client for local agents: session list, active session transcript, terminal-like streaming activity, agent management, and provider settings must be the default mental model instead of workbench, issue-tracking, domain-browser, or toolchain-center concepts
- Agent creation must be prompt-first: the default operator flow is to describe the desired agent in natural language and let the product generate a draft agent definition, prompt, description, and tool set instead of forcing low-level manual configuration first
- When the product creates or configures agents, workflows, or similar runtime assets from operator intent, route that intent through a built-in system agent or equivalent orchestration tool that understands the available providers, tools, and policies instead of scattering that decision logic across UI forms
- The product must always have a sensible default system agent path: a fresh app state should not leave the operator without an obvious usable agent, and runtime defaults should pick an available provider/model combination or degrade to the deterministic debug agent without blocking the UI
- The deterministic debug provider is an internal fallback, not an operator-facing authoring choice: do not surface it as a selectable provider or suggested model in the `New agent` creation flow; if no real provider is enabled or installed, send the operator to provider settings instead of defaulting the form to debug
- Do not invent agent roles, tool catalogs, skill catalogs, or capability tags in code or UI unless the product has a real backing registry and runtime path for them; absent a real implementation, leave those selections out of the product surface.
- User-facing UI must not expose backlog numbers, issue labels, workstream labels, "workbench", "domain", or similar internal planning and architecture language unless a feature explicitly exists to show source-control metadata
- Provider integrations must stay SDK-first: when Codex, Claude Code, GitHub Copilot, or debug/test providers already expose a `Microsoft Agent Framework` or `Microsoft.Extensions.AI` path, compose agent orchestration directly on that official surface instead of inventing parallel request/result abstractions.
- Do not add or keep provider-specific wrapper chat clients, compatibility shims, or extra adapter layers for `Codex`, `Claude Code`, or `GitHub Copilot`; use the provider SDK and `Microsoft Agent Framework` integration path directly.
- Do not use `AgentSessionProviderCatalog` or `AgentSessionCommandProbe` as provider-runtime indirection layers; provider registration, readiness, and session creation must come from the actual `Microsoft Agent Framework` plus provider SDK composition path.
- For `Codex` and `Claude Code`, prefer `ManagedCode.CodexSharpSDK.Extensions.AgentFramework`, `ManagedCode.CodexSharpSDK.Extensions.AI`, `ManagedCode.ClaudeCodeSharpSDK.Extensions.AgentFramework`, and `ManagedCode.ClaudeCodeSharpSDK.Extensions.AI` when those packages are available in the repo, and use them as the primary integration path instead of building repo-local wrappers; remove `AI.Fluent.Assertions` usage instead of layering it beside the Agent Framework path.
- Do not leave Uno binding on reflection fallback: when the shell binds to view models or option models, annotate or shape those types so the generated metadata provider can resolve them without runtime reflection warnings or performance loss
- Persist app models and durable session state through `SQLite` plus `EF Core` when the data must survive restarts; do not keep the core chat/session experience trapped in seed data or transient in-memory catalogs
- When agent conversations must survive restarts, persist the full `AgentSession` plus chat history through an Agent Framework history/storage provider backed by a local desktop folder; do not reduce durable conversation state to transcript text rows only
- Do not add cache layers for provider CLIs, model catalogs, workspace projections, or similar environment state unless the user explicitly asks for caching; prefer direct reads from the current source of truth
- Current repository policy is stricter than the default: do not keep provider-status caches, workspace/session in-memory mirrors, or app-preference caches at all unless the user explicitly asks to bring a specific cache back
- The current explicit exception is startup readiness hydration: the app may show a splash/loading state at launch, probe installed provider CLIs and related metadata once during that startup window, and then reuse that startup snapshot until an explicit refresh or provider-setting change invalidates it
- Provider CLI probing must not rerun as a side effect of ordinary screen binding or MVUX state re-evaluation; normal shell loads should share one in-flight probe and only reprobe on explicit refresh or provider-setting changes
- Expected cancellation from state re-evaluation or navigation must not be logged as a product failure; reserve failure logs for real errors, not superseded async loads
- Runtime and orchestration flows must emit structured `ILogger` logs for provider readiness, agent creation, session creation, send execution, and failure paths; ad hoc console-only startup traces are not enough to debug the product
- When the runtime uses `Microsoft Agent Framework`, prefer agent or run-scope middleware for detailed lifecycle logging and correlation instead of scattering ad hoc logging around UI callbacks or provider shims
- UI-facing view models must stay projection-only: do not keep orchestration, provider probing, session loading pipelines, or other runtime coordination in the Uno presentation layer when the same work can live in `DotPilot.Core` services
- Desktop navigation and tab/menu switching must stay structurally simple: do not introduce background refresh loops or in-memory cache projections as a default optimization path
- Agent-management UX must be proven end-to-end: prompt-first creation, default-agent availability, provider enable/disable or readiness changes, and starting a chat with an agent all require real `DotPilot.UITests` coverage instead of unit-only verification
- The desktop app is one shell, not three unrelated page layouts: keep one stable left navigation rail/app chrome across chat, agents, and providers, and switch the main content state instead of rebuilding different sidebars per screen
- Shell geometry must stay visually stable across primary screens: do not let the left rail width, footer block, nav button sizing, or page chrome jump between `Chat`, `Agents`, and `Providers`
- Do not fill the shell with duplicated explanatory copy or hardcoded decorative text blocks that restate the same information in multiple panes; prefer concise, task-oriented labels and let the main content carry the active workflow
- Do not ship placeholder-looking UI chrome such as ASCII pseudo-icons, swollen capsule buttons, or duplicated provider/agent cards just to make state visible; use consistent desktop controls and one clear source of truth per surface
- Do not keep legacy product slices alive during a rewrite: when `Workbench`, `ToolchainCenter`, legacy runtime demos, or similar prototype surfaces are being replaced, remove them instead of leaving a parallel legacy path in the codebase
- GitHub Actions workflows must use descriptive names and filenames that reflect their purpose; do not use a generic `ci.yml` catch-all because build validation and release automation are separate operator flows
- GitHub Actions must be split into at least one validation workflow for normal builds/tests and one release workflow for CI-driven version resolution, release-note generation, desktop publishing, and GitHub Release publication
- meaningful GitHub review comments must be evaluated and fixed when they still apply even if the original PR was closed; closed review threads are not a reason to ignore valid engineering feedback
- PR bodies for issue-backed work must use GitHub closing references such as `Closes #14` so merged work closes the tracked issue automatically
- the release workflow must run automatically on pushes to `main`, build desktop apps, and publish the GitHub Release without requiring a manual dispatch
- repository rules for `main` must keep an explicit org-admin bypass path for required status checks so repository administrators can perform direct emergency or operator-owned pushes without deadlocking the branch policy
- after changing GitHub rulesets, workflows, or release packaging, verify against the specific live blocked operation or failing run instead of assuming the policy or YAML change solved the issue
- desktop app build or publish jobs must use native runners for their target OS: macOS artifacts on macOS runners, Windows artifacts on Windows runners, and Linux artifacts on Linux runners
- desktop release assets must be native installable or directly executable outputs for each OS, not archives of raw publish folders; package the real `.exe`, `.snap`, `.dmg`, `.pkg`, `Setup.exe`, or equivalent runnable installer/app artifact instead of zipping intermediate publish directories
- desktop release versions must use the `ApplicationDisplayVersion` value in `DotPilot/DotPilot.csproj` as a manually maintained two-segment prefix, with CI appending the final segment from the build number (for example `0.0.<build-number>`)
- until the user explicitly changes the versioning policy, the manually maintained `ApplicationDisplayVersion` prefix for desktop releases must stay `0.0`, not `1.0`
- the release workflow must not take ownership of the first two version segments; those remain manually edited in source, while CI supplies only the last numeric segment and matching release tag/application version values
- for CI and release automation in this solution, prefer existing `dotnet` and `MSBuild` capabilities plus small workflow-native steps over Python or adding a separate helper project for simple versioning and release-note tasks
- prefer MIT-licensed GitHub and NuGet dependencies when they materially accelerate delivery and align with the current architecture
- prefer official `.NET` AI evaluation libraries under `Microsoft.Extensions.AI.Evaluation*` for response-quality, tool-usage, and safety evaluation instead of custom or third-party evaluation stacks by default
- prefer `Microsoft Agent Framework` telemetry and observability patterns with OpenTelemetry-first instrumentation and optional Azure Monitor or Foundry export later
- Treat the built-in MCP server as the canonical capability surface of `dotPilot`: operator-visible actions and automations should be exposed as properly defined MCP tools on the app-owned server, and agents must discover and invoke those tools through the shared MCP gateway instead of bypassing it with ad hoc internal calls

### Project AGENTS Policy

- Multi-project solutions MUST keep one root `AGENTS.md` plus one local `AGENTS.md` in each project or module root.
- Do not add `Owned by:` metadata to root or local `AGENTS.md` files.
- Each local `AGENTS.md` MUST document:
  - project purpose
  - entry points
  - boundaries
  - project-local commands
  - applicable skills
  - local risks or protected areas
- If a project grows enough that the root file becomes vague, add or tighten the local `AGENTS.md` before continuing implementation.

### Maintainability Limits

These limits are repo-configured policy values. They live here so the solution can tune them over time.

- `file_max_loc`: `400`
- `type_max_loc`: `200`
- `function_max_loc`: `50`
- `max_nesting_depth`: `3`
- `exception_policy`: `Document any justified exception in the nearest ADR, feature doc, or local AGENTS.md with the reason, scope, and removal/refactor plan.`

Local `AGENTS.md` files may tighten these values, but they must not loosen them without an explicit root-level exception.

### Task Delivery

- Start from `docs/Architecture.md` and the nearest local `AGENTS.md`.
- Treat `docs/Architecture.md` as the architecture map for every non-trivial task.
- If the overview is missing, stale, or diagram-free, update it before implementation.
- Define scope before coding:
  - in scope
  - out of scope
- Keep context tight. Do not read the whole repo if the architecture map and local docs are enough.
- If the task matches a skill, use the skill instead of improvising.
- Analyze first:
  - current state
  - required change
  - constraints and risks
- For non-trivial work, create a root-level `<slug>.plan.md` file before making code or doc changes.
- Keep the `<slug>.plan.md` file as the working plan for the task until completion.
- The plan file MUST contain:
  - task goal and scope
  - a detailed implementation plan with detailed ordered steps
  - constraints and risks
  - explicit test steps as part of the ordered plan, not as a later add-on
  - the test and verification strategy for each planned step
  - the testing methodology for the task: what flows will be tested, how they will be tested, and what quality bar the tests must meet
  - an explicit full-test baseline step after the plan is prepared
  - a tracked list of already failing tests, with one checklist item per failing test
  - root-cause notes and intended fix path for each failing test that must be addressed
  - a checklist with explicit done criteria for each step
  - ordered final validation skills and commands, with reason for each
- Use the Ralph Loop for every non-trivial task:
  - plan in detail in `<slug>.plan.md` before coding or document edits
  - include test creation, test updates, and verification work in the ordered steps from the start
  - once the initial plan is ready, run the full relevant test suite to establish the real baseline
  - if tests are already failing, add each failing test back into `<slug>.plan.md` as a tracked item with its failure symptom, suspected cause, and fix status
  - work through failing tests one by one: reproduce, find the root cause, apply the fix, rerun, and update the plan file
  - include ordered final validation skills in the plan file, with reason for each skill
  - require each selected skill to produce a concrete action, artifact, or verification outcome
  - execute one planned step at a time
  - mark checklist items in `<slug>.plan.md` as work progresses
  - review findings, apply fixes, and rerun relevant verification
  - update the plan file and repeat until done criteria are met or an explicit exception is documented
- Implement code and tests together.
- Run verification in layers:
  - changed tests
  - related suite
  - broader required regressions
- If `build` is separate from `test`, run `build` before `test`.
- After tests pass, run `format`, then the final required verification commands.
- The task is complete only when every planned checklist item is done and all relevant tests are green.
- Summarize the change, risks, and verification before marking the task complete.

### Documentation

- All durable docs live in `docs/`.
- `docs/Architecture.md` is the required global map and the first stop for agents.
- `docs/Architecture.md` MUST contain Mermaid diagrams for:
  - system or module boundaries
  - interfaces or contracts between boundaries
  - key classes or types for the changed area
- Keep one canonical source for each important fact. Link instead of duplicating.
- Public bootstrap templates are limited to root-level agent files. Authoring scaffolds for architecture, features, ADRs, and other workflows live in skills.
- Update feature docs when behaviour changes.
- Update ADRs when architecture, boundaries, or standards change.
- For non-trivial work, the plan file, feature doc, or ADR MUST document the testing methodology:
  - what flows are covered
  - how they are tested
  - which commands prove them
  - what quality and coverage requirements must hold
- Every feature doc under `docs/Features/` MUST contain at least one Mermaid diagram for the main behaviour or flow.
- Every ADR under `docs/ADR/` MUST contain at least one Mermaid diagram for the decision, boundaries, or interactions.
- Mermaid diagrams are mandatory in architecture docs, feature docs, and ADRs.
- Mermaid diagrams must render. Simplify them until they do.

### Testing

- TDD is the default for new behaviour and bug fixes: write the failing test first, make it pass, then refactor.
- Bug fixes start with a failing regression test that reproduces the issue.
- Every behaviour change needs new or updated automated tests with meaningful assertions. New tests are mandatory for new behaviour and bug fixes.
- Tests must prove the real user flow or caller-visible system flow, not only internal implementation details.
- Tests should be as realistic as possible and exercise the system through real flows, contracts, and dependencies.
- Tests must cover positive flows, negative flows, edge cases, and unexpected paths from multiple relevant angles when the behaviour can fail in different ways.
- All caller-visible feature flows must have API or integration-style automated coverage through public contracts; structure-only unit tests are not enough for this repository.
- Prefer integration, API, and UI tests over isolated unit tests when behaviour crosses boundaries.
- Do not use mocks, fakes, stubs, or service doubles in verification.
- Exercise internal and external dependencies through real containers, test instances, or sandbox environments that match the real contract.
- Flaky tests are failures. Fix the cause.
- Because CI does not guarantee Codex, Claude Code, or GitHub Copilot availability, keep a deterministic test AI client in-repo so core agent flows stay testable without external provider CLIs.
- Tests that require real Codex, Claude Code, or GitHub Copilot toolchains must run only when the corresponding toolchain and auth are available; their absence is an environment condition, not a reason to block the provider-independent test baseline.
- Changed production code MUST reach at least 80% line coverage, and at least 70% branch coverage where branch coverage is available.
- Critical flows and public contracts MUST reach at least 90% line coverage with explicit success and failure assertions.
- Repository or module coverage must not decrease without an explicit written exception. Coverage after the change must stay at least at the previous baseline or improve.
- Coverage is for finding gaps, not gaming a number. Coverage numbers do not replace scenario coverage or user-flow verification.
- The task is not done until the full relevant test suite is green, not only the newly added tests.
- UI tests are mandatory for this repository and must run in normal agent verification; missing local browser-driver setup is a harness bug to fix, not a reason to skip the suite.
- UI coverage must validate complete end-to-end operator flows and also assert the presence and behavior of each interactive element introduced by a feature.
- For `Uno` UI-test changes, use the official `Uno` MCP documentation as the source of truth and align browser assertions with the documented WebAssembly automation mapping before changing the harness shape.
- When debugging local product behavior on this machine, prefer the real desktop `Uno` app head plus local `Uno` app tooling or MCP over ad hoc `browserwasm` runs; keep `browserwasm` for the dedicated `DotPilot.UITests` verification path.
- GitHub Actions PR validation is mandatory for every PR and must enforce the real repo verification path so test failures are caught in CI, not only locally.
- GitHub Actions PR validation must run full automated test verification, especially the real UI suite; build-only or smoke-only checks are not an acceptable substitute for pull-request gating.
- GitHub Actions validation must also produce downloadable app artifacts for macOS, Windows, and Linux so every PR and mainline run has test results plus installable build outputs.
- For `.NET`, keep the active framework and runner model explicit so agents do not mix `TUnit`, `Microsoft.Testing.Platform`, and legacy `VSTest` assumptions.
- After changing production code, run the repo-defined quality pass: format, build, analyze, focused tests, broader tests, coverage, and any configured extra gates.

### Code And Design

- Everything in this solution MUST follow SOLID principles by default.
- Every class, object, module, and service MUST have a clear single responsibility and explicit boundaries.
- SOLID is mandatory.
- SRP and strong cohesion are mandatory for files, types, and functions.
- project structure must also follow SOLID and cohesion rules: keep related code together, keep unrelated responsibilities apart, and introduce a project boundary when a feature has become too large or too independent to stay clean inside `DotPilot.Core`
- Prefer composition over inheritance unless inheritance is explicitly justified.
- Large files, types, functions, and deep nesting are design smells. Split them or document a justified exception under `exception_policy`.
- Hardcoded values are forbidden.
- String literals are forbidden in implementation code. Declare them once as named constants, enums, configuration entries, or dedicated value objects, then reuse those symbols.
- Avoid magic literals. Extract shared values into constants, enums, configuration, or dedicated types.
- Backlog metadata does not belong in product code: issue numbers, PR numbers, review language, and planning terminology must never appear in production runtime models, diagnostics, or user-facing text unless the feature explicitly exposes source-control metadata.
- Design boundaries so real behaviour can be tested through public interfaces.
- For `.NET`, the repo-root `.editorconfig` is the source of truth for formatting, naming, style, and analyzer severity.
- Use nested `.editorconfig` files when they serve a clear subtree-specific purpose. Do not let IDE defaults, pipeline flags, and repo config disagree.

### Critical

- Never commit secrets, keys, or connection strings.
- Never skip tests to make a branch green.
- Never weaken a test or analyzer without explicit justification.
- Do not remove the `DotPilot/DotPilot.csproj` XML-doc and `CS1591` configuration unless the repo adopts full public API documentation coverage or a different documented fix for Roslyn `IDE0005`.
- Never introduce mocks, fakes, stubs, or service doubles to hide real behaviour in tests or local flows.
- Never introduce a non-SOLID design unless the exception is explicitly documented under `exception_policy`.
- Never force-push to `main`.
- Never approve or merge on behalf of a human maintainer.

### Boundaries

Always:

- Read root and local `AGENTS.md` files before editing code.
- Read the relevant docs before changing behaviour or architecture.
- Run the required verification commands yourself.

Ask first:

- changing public API contracts
- adding new dependencies
- modifying database schema
- deleting code files

## Preferences

### Likes

- When the user asks to fix CI or release automation, push the workflow/code changes yourself and keep iterating on live runs until the targeted pipeline is green, instead of stopping at a local patch.
- Verify `Uno Platform`, packaging, and CI-fix decisions against the latest official docs and current web sources when working on release or tooling failures, so the repo does not keep stale workflow assumptions.
- Keep regression coverage tied to real operator flows: when agent creation changes, tests should cover creating an agent, choosing a valid provider model, and sending at least one message through the resulting session path.
- Keep the first provider baseline deliberately small: the operator-visible provider list should stay focused on the three real console providers, and each one needs automated create-agent plus `hello -> hello reply` smoke coverage before extra provider features are added.
- Keep operator-visible provider models unseeded: supported and suggested model lists for real providers must come from live CLI metadata or explicit operator input, not from hardcoded fallback catalogs.
- Keep provider, model, and runtime state honest: when a value should come from a live provider, workspace, or operator choice, do not hardcode it into production paths.
- Follow the canonical MCAF tutorial when bootstrapping or upgrading the agent workflow.
- Commit cohesive code-change batches promptly while debugging, especially before switching focus or starting long verification runs, so the branch state stays inspectable and pushable.
- After opening or updating a PR, create a fresh working branch before continuing with the next slice of work so follow-up changes do not pile onto the already-reviewed branch.
- When one requested slice is complete and verified, commit it before switching to the next GitHub issue so each backlog step stays isolated and reviewable.
- Keep `DotPilot` feeling like a fast desktop control plane: startup, navigation, and visible UI reactions should be prompt, and agents should remove unnecessary waits instead of normalizing slow web-style loading behavior.
- Keep the root `AGENTS.md` at the repository root.
- Keep the repo-local agent skill directory limited to current `mcaf-*` skills.
- Keep the solution file name cased as `DotPilot.slnx`.
- Treat `DotPilot` UI implementation as `Uno Platform` desktop XAML work, especially for Figma handoff, instead of translating designs into web stacks.
- Use central package management for shared test and tooling packages.
- Keep one `.NET` test framework active in the solution at a time unless a documented migration is in progress.
- Validate UI changes through runnable `DotPilot.UITests` on every relevant verification pass, instead of relying only on manual browser inspection or conditional local setup.
- Keep the UI-test execution path minimal: one normal test command should produce a real result without extra harness indirection or side-effect-heavy setup.
- Keep validation and release GitHub Actions separate, with descriptive names and filenames instead of a generic `ci.yml`.
- Keep the validation workflow focused on build and automated test feedback, and keep release responsibilities in a dedicated workflow that bumps versioning, publishes desktop artifacts, and creates the GitHub Release with feature notes.
- Keep `dotPilot` positioned as a general agent control plane, not a coding-only shell.
- Keep the visible product direction aligned with desktop chat apps such as Codex and Claude: sessions first, chat first, streaming first, with repo and git actions as optional utilities inside a session instead of the primary navigation model.
- Keep provider integrations SDK-first where good typed SDKs already exist.
- Prefer `ManagedCode` provider SDK bridges for `Codex` and `Claude Code` when they already expose `Microsoft Agent Framework` and `Microsoft.Extensions.AI` integration points, instead of keeping parallel custom adapters or `AI.Fluent.Assertions` glue.
- Keep evaluation and observability aligned with official Microsoft `.NET` AI guidance when building agent-quality and trust features.

### Dislikes

- Installing stale, non-canonical, or non-`mcaf-*` skills into the repo-local agent skill directory.
- Shipping fake, mock, stub, pretend, or synthetic runtime paths where the product or verification is supposed to exercise the real contract.
- Moving root governance out of the repository root.
- Mixing multiple `.NET` test frameworks in the active solution without a documented migration plan.
- Creating auxiliary `git worktree` directories for normal PR follow-up when straightforward branch switching in the main checkout is enough.
- Running build, test, or verification commands for file-only structural reorganizations when the user explicitly asked for folder cleanup without behavior changes.
- Adding fallback paths or alternate harnesses that only make failures disappear in tests while the primary product path remains broken.
- Switching desktop Uno pages into stacked or mobile-style responsive layouts during resize work unless the user explicitly asks for a different composition; desktop pages must stay desktop-first and protect geometry through sizing constraints instead.
- Adding extra UI-test orchestration complexity when the actual goal is simply to run the tests and get an honest pass or fail result.
- Planning `MLXSharp` into the first product wave before it is ready for real use.
- Keeping `AI.Fluent.Assertions` in the provider/chat stack after an official `Microsoft Agent Framework` plus `ManagedCode` integration path is available.
- Reintroducing `AgentSessionProviderCatalog`, `AgentSessionCommandProbe`, or provider-specific wrapper chat clients after the repo has official `Microsoft Agent Framework` plus provider SDK integration packages available.
- Letting internal implementation labels such as `Workbench`, issue numbers, or architecture slice names leak into the visible product wording or navigation when the app should behave like a clean desktop chat client.
- Leaving deprecated product slices, pages, view models, or contracts in place "for later cleanup" after the replacement direction is already chosen.
