# Vertical Slice Runtime Foundation Plan

## Goal

Reshape the solution so `DotPilot` stays a presentation-only Uno app, while the first runtime foundation for epic `#12` moves into separate vertical-slice class libraries with isolated contracts and composition points.

## Scope

### In Scope

- Record durable governance rules for vertical slices, presentation-only Uno UI, and branch-after-PR workflow
- Update the architecture map to show the new solution boundaries and feature-slice direction
- Introduce a `DotPilot.Core` class library for non-UI contracts and feature-aligned slices that support issue `#12`
- Introduce any additional class library needed to keep runtime bootstrapping and service registration out of the Uno UI project
- Move the first runtime/domain contracts out of `DotPilot` and into slice-owned folders aligned with issues `#22`, `#23`, `#24`, and `#25`
- Rewire the Uno app to reference the new DLL boundaries through composition only
- Add or update automated tests that verify the moved contracts and composition surface

### Out Of Scope

- Full Orleans host implementation
- Full Microsoft Agent Framework integration
- Full provider adapters, persistence, telemetry, or Git tooling implementation
- Closing every open GitHub issue in one slice
- Replacing the current Uno shell layout

## Constraints And Risks

- Keep the existing `README.md` modification untouched because it predates this task.
- Respect the new architecture rule: each feature slice owns its contracts and orchestration; no new shared horizontal dump folders.
- Use the newest stable `.NET 10` and `C#` features supported by the pinned SDK when they improve clarity and reduce boilerplate.
- Keep `DotPilot` as the presentation host only; avoid reintroducing runtime/domain logic there during the refactor.
- Preserve the current Uno startup and navigation behavior while moving composition and contracts behind separate DLL boundaries.
- Keep changes small enough that the first PR is reviewable and can become the branch point for the next slice.
- UI tests remain mandatory even if this slice mostly changes structure.
- CI cannot rely on Codex, Claude Code, or GitHub Copilot being installed or authenticated, so provider-independent agent flows need a deterministic in-repo test AI client.
- Provider-specific tests must self-gate on real toolchain availability without weakening the provider-independent baseline.
- API-level and UI-level flow coverage are mandatory deliverables for this slice, not optional follow-up work.

## Testing Methodology

- Baseline proof: run the full solution test command after the plan is prepared to capture the real starting point.
- Focused proof for the refactor: run targeted contract and API-style tests for the new core/runtime composition surface and any updated app-startup tests.
- Provider-independent agent-flow proof: cover the new contracts and composition path with the in-repo test AI client so CI can validate the behavior without external CLIs.
- Provider-dependent proof: any tests that need real Codex, Claude Code, or GitHub Copilot must detect tool availability and execute only when the dependency is present.
- UI proof: add or extend browser tests so the changed surfaces verify each introduced interactive element and at least one complete end-to-end flow.
- Broader proof: run the solution build and test commands once the slice is wired through.
- Quality gates: `dotnet format DotPilot.slnx --verify-no-changes`, `dotnet build DotPilot.slnx -warnaserror`, `dotnet test DotPilot.slnx`, and the repo coverage command.
- Quality bar: the new class-library split compiles cleanly, the Uno app still starts through the normal composition path, provider-independent flows stay testable in CI, UI flows are exercised end to end, and no existing automated coverage regresses without an explicit documented reason.

## Ordered Plan

- [x] Step 1: Capture the durable workflow and architecture rules in root and local `AGENTS.md`.
  Verification: root governance now requires vertical slices, presentation-only Uno UI, and a fresh branch after each PR; local `DotPilot/AGENTS.md` narrows the app boundary to presentation.
  Done when: future agents can infer the intended architecture from governance alone.

- [x] Step 2: Run the full relevant baseline to establish the real starting state before deeper refactoring.
  Verification:
  - `dotnet test DotPilot.slnx`
  Done when: the plan records every pre-existing failure or risk exposed by the baseline.

- [x] Step 3: Update `docs/Architecture.md` so the architecture map matches the new vertical-slice and multi-DLL direction for epic `#12`.
  Verification: the architecture overview contains real module and contract diagrams for the Uno UI host, the new core/runtime libraries, and the first issue-aligned slices.
  Done when: a new agent can scope `#12`, `#22`, `#23`, `#24`, and `#25` from the architecture map without reading the whole repo.

- [x] Step 4: Create the new solution projects and wire them into `DotPilot.slnx`.
  Verification: the solution contains a presentation-only `DotPilot` app plus separate class libraries for shared non-UI foundations.
  Done when: the app project references the new libraries instead of owning non-UI contracts directly.

- [x] Step 5: Move the first issue-aligned contracts into isolated vertical slices and add the minimal composition surface.
  Verification: issue `#12` foundation types are grouped by feature slice, not by horizontal layer, and the Uno app consumes them through service registration or typed composition only.
  Done when: the first runtime/domain contracts needed for `#22` to `#25` live outside the UI project with clear ownership.

- [x] Step 6: Add or update automated API-style tests for the extracted contracts, app composition path, and provider-independent agent flows.
  Verification: focused tests cover the new slice boundaries, identifiers/contracts, composition wiring, and the in-repo test AI client path with meaningful positive, negative, and edge-case assertions.
  Done when: the refactor is protected by realistic non-UI flow tests instead of only compile success.

- [x] Step 7: Add or extend UI tests for the affected surfaces.
  Verification: browser tests assert the visible interactive elements introduced or rewired by this slice and exercise at least one full end-to-end operator flow through the changed area.
  Done when: UI coverage proves the new slice shape from the operator surface, not just from non-UI tests.

- [x] Step 8: Run final validation in repo order and record the results.
  Verification:
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - `dotnet build DotPilot.slnx -warnaserror`
  - `dotnet test DotPilot.slnx`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
  Done when: the commands are green or any remaining blocker is explicitly documented in this file.

- [ ] Step 9: Create the PR for this slice, then open a fresh working branch for the next slice before continuing.
  Verification: the branch and PR flow match the new repo rule.
  Done when: the PR exists and follow-up work is no longer accumulating on the reviewed branch.

## Validation Notes

- `dotnet test DotPilot.slnx` passed with `0` failed, `14` passed, and `0` skipped.
- `docs/Architecture.md` now maps the Uno presentation host, `DotPilot.Core`, `DotPilot.Runtime`, and the first issue-aligned runtime foundation slice for epic `#12`.
- `DotPilot.slnx` now includes `DotPilot.Core` and `DotPilot.Runtime`, with `DotPilot` consuming runtime/domain behavior through project references and DI only.
- Runtime foundation contracts, typed identifiers, provider-probe logic, and the deterministic in-repo agent client now live outside the Uno project in slice-owned folders.
- UI coverage now includes the runtime foundation panel, slice/provider counts, the agent-builder runtime banner, and an end-to-end runtime foundation flow.
- UI harness verification found and fixed the browser-host bootstrap path so the suite produces a real result instead of stalling on nested browser setup.
- `dotnet format DotPilot.slnx --verify-no-changes` passed.
- `dotnet build DotPilot.slnx -warnaserror` passed with `0` warnings and `0` errors.
- `dotnet test DotPilot.slnx` passed with `0` failed, `34` passed, and `0` skipped.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"` passed with `0` failed, `20` passed, and `0` skipped.
- Coverage from `DotPilot.Tests/TestResults/67ca7188-7eca-4fae-aa3e-893fd99a8e3c/coverage.cobertura.xml` is `99.29%` line coverage and `85.00%` branch coverage across the changed production surface.

## Failing Tests Tracker

- [x] Baseline solution verification
  Failure symptom: none.
  Root cause: baseline solution tests passed before the refactor started.
  Fix path: keep the baseline green while introducing the new projects and slices.
  Status: complete.

## Final Validation Skills

1. `mcaf-solution-governance`
Reason: the root and local agent rules must explicitly match the enforced architecture and branch workflow.

2. `mcaf-architecture-overview`
Reason: the solution map must reflect the new DLL and slice boundaries.

3. `mcaf-dotnet`
Reason: this slice changes project structure, app composition, and solution verification commands.

4. `mcaf-testing`
Reason: moved contracts and composition boundaries need durable automated verification.
