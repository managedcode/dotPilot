# Issue 14 Toolchain Center Plan

## Goal

Implement epic `#14` in one coherent vertical slice so `dotPilot` gains a first-class Toolchain Center for `Codex`, `Claude Code`, and `GitHub Copilot`, while keeping the `Uno` app presentation-focused and all non-UI logic in separate DLLs.

## Scope

### In scope

- Issue `#33`: Toolchain Center UI
- Issue `#34`: Codex detection, version, auth, update, and operator actions
- Issue `#35`: Claude Code detection, version, auth, update, and operator actions
- Issue `#36`: GitHub Copilot readiness, CLI or server visibility, SDK prerequisite visibility, and operator actions
- Issue `#37`: provider connection-test and health-diagnostics model
- Issue `#38`: provider secrets and environment configuration management model and UI
- Issue `#39`: background polling model and surfaced stale-state warnings
- Core contracts in `DotPilot.Core`
- Runtime probing, diagnostics, polling, and configuration composition in `DotPilot.Runtime`
- Desktop-first `Uno` presentation and navigation in `DotPilot`
- Automated coverage in `DotPilot.Tests` and `DotPilot.UITests`
- Architecture and feature documentation updates required by the new slice

### Out of scope

- Epic `#15` provider adapter issues `#40`, `#41`, `#42`
- Real live session execution for external providers
- New external package dependencies unless explicitly approved
- Non-provider local runtime setup outside Toolchain Center scope

## Constraints And Risks

- Keep the `Uno` app cleanly UI-only; non-UI toolchain behavior must live in `DotPilot.Core` and `DotPilot.Runtime`.
- Do not add new NuGet dependencies without explicit user approval.
- Do not hide readiness problems behind fallback behavior; missing, stale, or broken provider state must remain visible and attributable.
- Provider-specific tests that require real `Codex`, `Claude Code`, or `GitHub Copilot` toolchains must be environment-gated, while provider-independent coverage must still stay green in CI.
- UI tests must continue to run through `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`; no manual app launch path is allowed for UI verification.
- Local and CI validation must use `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`.
- Existing workbench navigation issues may already be present in the UI baseline and must be tracked explicitly if reproduced in this clean worktree.

## Testing Methodology

- Unit and integration-style tests in `DotPilot.Tests` will verify:
  - Toolchain Center contracts and snapshot shape
  - provider readiness probes for success, missing toolchain, partial readiness, and stale-state warnings
  - provider diagnostics and secrets or environment modeling
  - background polling metadata and surfaced warning summaries
- UI tests in `DotPilot.UITests` will verify:
  - navigation into the Toolchain Center
  - provider summary visibility
  - provider detail visibility for each supported provider
  - secrets and environment sections
  - diagnostics and polling-state visibility
  - at least one end-to-end operator flow through settings to Toolchain Center and back to the broader shell
- Real-toolchain tests will run only when the corresponding executable and auth prerequisites are available.
- The task is not complete until the changed tests, related suites, broader solution verification, and coverage run are all green or any pre-existing blockers are documented with root cause and explicit non-regression evidence.

## Ordered Plan

- [x] Step 1. Capture the clean-worktree baseline.
  - Run the mandatory build and relevant test suites before code changes.
  - Update the failing-test tracker below with every reproduced baseline failure.
- [x] Step 2. Define the Toolchain Center slice contracts in `DotPilot.Core`.
  - Add explicit provider readiness, version, auth, diagnostics, secrets, environment, action, and polling models.
  - Keep contracts provider-agnostic where possible and provider-specific only where required by the epic.
- [x] Step 3. Implement runtime probing and composition in `DotPilot.Runtime`.
  - Build provider-specific readiness snapshots for `Codex`, `Claude Code`, and `GitHub Copilot`.
  - Add operator-action models, diagnostics summaries, secrets or environment metadata, and polling-state summaries.
  - Keep probing side-effect free except for tightly bounded metadata or command checks.
- [x] Step 4. Integrate the Toolchain Center into the desktop settings surface in `DotPilot`.
  - Add a first-class Toolchain Center entry and detail surface.
  - Keep the layout desktop-first, fast to scan, and aligned with the current shell.
  - Surface errors and warnings directly instead of masking them.
- [x] Step 5. Add or update automated tests in parallel with the production slice work.
  - Start with failing regression or feature tests where new behavior is introduced.
  - Cover provider-independent flows broadly and gated real-provider flows conditionally.
- [x] Step 6. Update durable docs.
  - Update `docs/Architecture.md` with the new slice and diagrams.
  - Add or update a feature doc in `docs/Features/` for Toolchain Center behavior and verification.
  - Correct any stale root guidance discovered during the task, including `LangVersion` wording if still inconsistent with source.
- [x] Step 7. Run final validation and prepare the PR.
  - Run format, build, focused tests, broader tests, UI tests, and coverage.
  - Create a PR that uses GitHub closing references for the implemented issues.

## Full-Test Baseline Step

- [x] Run `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
- [x] Run `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`
- [x] Run `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`

## Already Failing Tests Tracker

- [x] `GivenMainPage.WhenFilteringTheRepositoryThenTheMatchingFileOpens`
  - Failure symptom: `Uno.UITest` target selection was unstable when the repository list used DOM-expanded item content instead of one stable tappable target.
  - Root-cause notes: the sidebar repository flow mixed a `ListView` selection surface with a nested text-only automation target, which made follow-up navigation flows brittle after document-open actions.
  - Resolution: the tests now open the document through one canonical search-and-open helper, assert the opened title explicitly, and the repository list remains unique under `Uno` automation mapping.
- [x] `dotnet test DotPilot.UITests/DotPilot.UITests.csproj` run completion
  - Failure symptom: the suite previously stalled around the first failing workbench navigation flow and left the browser harness in an unclear state.
  - Root-cause notes: multiple broken navigation paths and stale diagnostics made the harness look hung even though the real issue was route resolution and ambiguous navigation controls.
  - Resolution: page-specific sidebar automation ids, route fixes for `-/Main`, and improved DOM or hit-test diagnostics now leave the suite green and terminating normally.

## Final Results

- `dotnet format DotPilot.slnx --verify-no-changes`
- `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`
- `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
- `dotnet test DotPilot.slnx`
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`

Final green baselines after the slice landed:

- `DotPilot.Tests`: `52` passed
- `DotPilot.UITests`: `22` passed
- Coverage collector overall: `91.58%` line / `61.33%` branch
- Key changed runtime files:
  - `ToolchainCenterCatalog`: `95.00%` line / `100.00%` branch
  - `ToolchainCommandProbe`: `89.23%` line / `87.50%` branch
  - `ToolchainProviderSnapshotFactory`: `98.05%` line / `78.43%` branch
  - `ProviderToolchainProbe`: `95.12%` line / `85.71%` branch

## Final Validation Skills

- `mcaf-dotnet`
  - Reason: enforce repo-specific `.NET` commands, analyzer policy, language-version compatibility, and final validation order.
- `mcaf-testing`
  - Reason: keep test layering explicit and prove user-visible flows instead of only internal wiring.
- `mcaf-architecture-overview`
  - Reason: update the cross-project architecture map and diagrams after the new slice boundaries are introduced.

## Final Validation Commands

1. `dotnet format DotPilot.slnx --verify-no-changes`
   - Reason: repo-required formatting and analyzer drift check.
2. `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
   - Reason: mandatory warning-free build for local and CI parity.
3. `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`
   - Reason: unit and integration-style validation for the non-UI slice.
4. `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
   - Reason: mandatory end-to-end UI verification through the real harness.
5. `dotnet test DotPilot.slnx`
   - Reason: broader solution regression pass across all test projects.
6. `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
   - Reason: prove coverage expectations for the changed production code.
