## Goal

Consolidate the user-requested local branches into one new branch, keep the merged runtime and UI fixes, restore a green validation baseline, open one replacement PR, and leave only `main` plus the new consolidated branch in the local repo.

## Scope

In scope:
- Merge the requested branch content into `codex/consolidated-13-15-76`
- Fix any integration regressions introduced by the consolidation
- Re-run full repo validation, including `DotPilot.UITests`
- Push the consolidated branch and open a single PR
- Remove extra local branches and extra local worktrees so only `main` and the consolidated branch remain

Out of scope:
- New backlog feature work outside the merged branches
- Any dependency additions
- Human merge/approval actions on GitHub

## Constraints And Risks

- The repo requires `-warnaserror` builds.
- UI tests must run through the real `DotPilot.UITests` harness; no manual app launch outside the harness.
- The consolidated branch must preserve the startup responsiveness fixes from the PR 76 review follow-up.
- The local branch cleanup must not delete `main` or the new consolidated branch.

## Testing Methodology

- Validate the compile baseline with the repo `build` command.
- Validate end-to-end UI behavior only through `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`.
- Validate the full repo through the solution test command after focused fixes are green.
- Validate coverage with the repo collector command and confirm no regression versus the pre-consolidation baseline.

## Ordered Plan

- [x] Confirm the active branch/worktree state and identify the consolidated branch target.
- [x] Reproduce the consolidated-branch regression through the real `DotPilot.UITests` harness.
- [x] Capture the root cause of the harness failure instead of treating it as a generic host timeout.
- [x] Restore the missing shared build input and any other merge fallout required to make the browser host buildable again.
- [x] Run focused UI verification to prove the browser host starts and the failing settings/workbench flow passes again.
- [x] Run the full required validation sequence:
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - `dotnet test DotPilot.slnx`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
- [ ] Commit the consolidation fixes on `codex/consolidated-13-15-76`.
- [ ] Push the consolidated branch and open one replacement PR to `main`.
- [ ] Delete extra local branches and extra local worktrees so only `main` and `codex/consolidated-13-15-76` remain locally.

## Full-Test Baseline

- `dotnet test DotPilot.UITests/DotPilot.UITests.csproj --filter FullyQualifiedName~WhenNavigatingToSettingsThenCategoriesAndEntriesAreVisible -v minimal`
  - Failed before test execution with `CSC` errors because `/Users/ksemenenko/Developer/dotPilot/CodeMetricsConfig.txt` was missing during the `net10.0-browserwasm` host build.

## Tracked Failing Tests

- [x] `WhenNavigatingToSettingsThenCategoriesAndEntriesAreVisible`
  - Symptom: browser host exits before reachable
  - Root cause: `CodeMetricsConfig.txt` missing from repo root, so the browserwasm compile inside the harness fails
  - Intended fix: restore `CodeMetricsConfig.txt` with the shared analyzer config content and rerun the harness

## Verification Results

- `dotnet test DotPilot.UITests/DotPilot.UITests.csproj --filter FullyQualifiedName~WhenNavigatingToSettingsThenCategoriesAndEntriesAreVisible -v minimal`
  - Passed: `1`
- `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - Passed with `0` warnings and `0` errors
- `dotnet test DotPilot.slnx`
  - Passed: `60` unit tests and `22` UI tests
- `dotnet format DotPilot.slnx --verify-no-changes`
  - Passed
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
  - Passed: `60` tests
  - Coverage artifact: `DotPilot.Tests/TestResults/9a4b4ba7-ae2c-4a23-9eab-0af4d4e30730/coverage.cobertura.xml`

## Done Criteria

- The consolidated branch contains the requested merged work plus the follow-up fixes.
- Full repo validation is green.
- One PR exists for the consolidated branch.
- Only `main` and `codex/consolidated-13-15-76` remain as local branches.
