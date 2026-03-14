## Goal

Implement epic `#11` on a dedicated branch by fully covering its direct child issues `#22` and `#23` with code, docs, and automated tests, then open one PR that closes the epic and both child issues automatically.

## Scope

In scope:
- issue `#22`: finalize the control-plane domain model for agents, sessions, fleets, tools, artifacts, telemetry, and evaluations
- issue `#23`: finalize `ManagedCode.Communication` usage for public runtime result and problem contracts
- fix any remaining gaps on `main` that keep the epic from being honestly closeable, including stale docs, issue references, and missing automated verification coverage
- keep the work inside `DotPilot.Core`, `DotPilot.Runtime`, `DotPilot.Tests`, and docs that describe these slices

Out of scope:
- runtime host or orchestration implementation changes beyond what is strictly needed to prove the issue `#23` contract surface
- UI redesign or workbench behavior
- provider-specific adapter work from later epics

## Constraints And Risks

- The app remains presentation-only; this epic is contract and foundation work, not UI-first behavior.
- Do not claim the epic is complete unless both direct child issues are covered by real implementation and automated tests.
- Tests must stay realistic and exercise caller-visible flows through public contracts.
- Existing open issue state on GitHub may reflect missing PR closing refs rather than missing code; the branch must still produce real repository improvements before opening a new PR.
- Avoid user-specific local paths and workflow-specific branch names in durable test data and user-facing docs; task-local plan notes may still reference the active branch and PR.

## Testing Methodology

- Validate issue `#22` through serialization-safe contract round-trips, identifier behavior, and cross-record relationship assertions.
- Validate issue `#23` through deterministic runtime client success and failure flows that surface `ManagedCode.Communication` results and problems at the public runtime boundary.
- Keep verification layered:
  - focused issue `#22/#23` tests
  - full `DotPilot.Tests`
  - full solution tests including `DotPilot.UITests`
  - coverage for `DotPilot.Tests`
- Require changed production files to stay at or above the repo coverage bar.

## Ordered Plan

- [x] Confirm epic `#11` scope and direct child issues from GitHub.
- [x] Create a dedicated branch from clean `main`.
- [x] Audit `main` for remaining gaps in issue `#22/#23` implementation, docs, and tests.
- [x] Correct stale architecture and feature docs so epic `#11`, issue `#22`, and issue `#23` are referenced accurately.
- [x] Add or tighten automated tests for issue `#22` and issue `#23` in slice-aligned locations, including deterministic runtime result/problem coverage.
- [x] Run focused verification for the changed slice tests.
- [x] Run the full repo validation sequence:
  - `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - `dotnet test DotPilot.slnx`
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
- [x] Commit the epic `#11` work and open one PR with correct GitHub closing refs.

## Full-Test Baseline

- [x] `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - Passed with `0` warnings and `0` errors.
- [x] `dotnet test DotPilot.slnx`
  - Passed with `61` unit tests and `22` UI tests.

## Tracked Failing Tests

- [x] No baseline failures in the repository state under serial execution.
- [x] Baseline note: a parallel local `build` + `test` attempt caused a self-inflicted file-lock on `DotPilot.Core/obj`; this was not a repository failure and was resolved by rerunning the required commands serially per root `AGENTS.md`.

## Done Criteria

- Epic `#11` has a real implementation close-out branch, not only issue closure metadata.
- Issue `#22` contracts are documented, serialization-safe, and covered by automated tests.
- Issue `#23` result/problem contracts are documented, exercised through public runtime flows, and covered by automated tests.
- Architecture and feature docs no longer misattribute issue `#22/#23` to epic `#12`.
- The final PR closes `#11`, `#22`, and `#23` automatically after merge.

## Audit Notes

- `main` already contained the bulk of the issue `#22/#23` implementation, but the close-out was incomplete:
  - `docs/Features/control-plane-domain-model.md` incorrectly listed epic `#12` as the parent instead of epic `#11`
  - `docs/Architecture.md` and `ADR-0003` treated issues `#22` and `#23` as if they belonged to epic `#12`
  - domain-contract tests still embedded a user-specific local filesystem path and stale branch name
  - issue `#23` lacked focused automated coverage that exercised `ManagedCode.Communication` through the public deterministic runtime client boundary

## Final Validation Results

- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter "FullyQualifiedName~ControlPlaneDomain|FullyQualifiedName~RuntimeCommunication"`
  - Passed with `23` tests.
- `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - Passed with `0` warnings and `0` errors.
- `dotnet test DotPilot.slnx`
  - Passed with `61` unit tests and `22` UI tests.
- `dotnet format DotPilot.slnx --verify-no-changes`
  - Passed with no formatting drift.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
  - Passed with overall coverage `91.66%` line and `61.66%` branch.
  - Changed production files met the repo bar:
    - `RuntimeFoundationCatalog`: `100.00%` line / `100.00%` branch
- Pull request
  - Opened [PR #82](https://github.com/managedcode/dotPilot/pull/82) from `codex/epic-11-foundation-contracts` to `main` with `Closes #11`, `Closes #22`, and `Closes #23`.
