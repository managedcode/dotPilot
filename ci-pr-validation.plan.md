# CI PR Validation Plan

## Goal

Fix the GitHub Actions CI path used by `managedcode/dotPilot` so it builds with the current `.NET 10` toolchain, runs the real repository verification flow, includes the mandatory `DotPilot.UITests` suite, publishes desktop app artifacts for macOS, Windows, and Linux, and blocks pull requests when those checks fail.

## Scope

### In Scope

- GitHub Actions workflow definitions under `.github/workflows/`
- GitHub repository branch protection or ruleset configuration needed to make CI mandatory for pull requests
- Test and coverage execution issues that prevent the repo-defined validation flow from running in CI
- Cross-platform desktop publish jobs and artifact uploads for the `DotPilot` app
- Durable docs and governance notes that must reflect the enforced CI policy

### Out Of Scope

- New product features unrelated to CI and validation
- New test scenarios beyond what is needed to make the existing verification path reliable
- Release automation unrelated to pull-request validation beyond producing CI desktop publish artifacts

## Constraints And Risks

- Keep the CI commands aligned with the repo-root `AGENTS.md` commands instead of inventing a separate build path.
- Do not weaken or skip UI tests to make CI green.
- Keep the workflow deterministic across GitHub-hosted runners.
- If coverage remains broken with `coverlet.collector`, fix the root cause instead of removing coverage from the quality path.
- Protect both `main` and any release-target pull requests with required CI checks where practical.
- Prefer the official Uno desktop publish command path over custom packaging logic in CI.

## Testing Methodology

- CI baseline: inspect the failing GitHub Actions run `23005673864` and map each failed job to a concrete local or workflow-level cause.
- Local focused validation:
  - `dotnet build DotPilot.slnx`
  - `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
  - `dotnet publish DotPilot/DotPilot.csproj -c Release -f net10.0-desktop`
- Workflow validation:
  - run the updated GitHub Actions workflow locally where possible through matching `dotnet` commands
  - push or dispatch only after the local command path is green
- Quality bar:
  - CI must use `dotnet`-based build and test commands compatible with the pinned SDK
  - UI tests must run as real tests in CI and locally
  - CI must publish downloadable desktop artifacts for macOS, Windows, and Linux on every PR and mainline run
  - required status checks must block pull requests until the workflow is green

## Ordered Plan

- [x] Step 1: Capture the full baseline for the failing GitHub Actions run and the current local validation path.
  Verification:
  - inspect jobs and logs for run `23005673864`
  - run the relevant local commands that mirror CI, including the coverage command
  Done when: the exact failing workflow steps and any local reproduction gaps are documented below.

- [x] Step 2: Replace the outdated CI build path with a `dotnet`-native workflow that matches repo policy.
  Verification:
  - updated workflow uses `dotnet build` and `dotnet test` instead of `msbuild`
  - workflow includes the mandatory UI test suite
  Done when: the workflow definition reflects the real repo command path and no longer depends on incompatible Visual Studio MSBuild.

- [x] Step 3: Fix any remaining test or coverage blocker exposed by the corrected workflow path.
  Verification:
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"` finishes with a result instead of hanging
  - `dotnet test DotPilot.slnx` remains green with the UI suite included
  Done when: the repo quality path needed by CI is stable locally.

- [x] Step 4: Add desktop publish artifact jobs for macOS, Windows, and Linux.
  Verification:
  - workflow publishes `DotPilot` with `dotnet publish DotPilot/DotPilot.csproj -c Release -f net10.0-desktop`
  - workflow uploads a separate desktop artifact for each supported GitHub-hosted runner OS
  Done when: each platform has a stable required job name and downloadable artifact path in CI.

- [x] Step 5: Enforce CI as mandatory for pull requests at the repository level.
  Verification:
  - branch protection or rulesets require the CI status checks for protected pull-request targets
  - configuration applies to `main` and the intended release branch pattern
  - required checks include the desktop artifact jobs as well as `Quality`, `Unit Tests`, `Coverage`, and `UI Tests`
  Done when: a failing CI workflow would block merge for protected PR targets and artifact publishing is part of that gate.

- [x] Step 6: Update durable docs and governance notes to reflect the enforced CI contract.
  Verification:
  - relevant docs describe CI as required for pull requests, the validation path as `dotnet`-based, and desktop artifacts as mandatory outputs
  Done when: the durable docs no longer describe outdated or optional CI behavior.

- [x] Step 7: Run final validation and record the outcomes.
  Verification:
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - `dotnet build DotPilot.slnx`
  - `dotnet build DotPilot.slnx -warnaserror`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
  - `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
  - `dotnet test DotPilot.slnx`
  - `dotnet publish DotPilot/DotPilot.csproj -c Release -f net10.0-desktop`
  Done when: all required commands are green and this checklist is complete.

## Baseline Notes

- GitHub Actions run `23005673864` failed before tests ran because both jobs used `msbuild`, and the hosted runner's Visual Studio `MSBuild 17.14` could not resolve the `.NET 10.0.200` SDK selected by the old workflow path.
- Local `dotnet build DotPilot.slnx` and `dotnet test DotPilot.Tests/DotPilot.Tests.csproj` already passed, which confirmed the primary CI break was the workflow toolchain path rather than product code.
- Local `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --collect:"XPlat Code Coverage"` reproduced a second blocker: coverage did not crash, but `coverlet.collector` spent minutes instrumenting generated Uno artifacts before test execution began, which was not acceptable for PR validation.
- The workflow also lacked any desktop publish stage, so pull requests produced no downloadable app artifacts for human verification across macOS, Windows, and Linux.

## Failing Tests And Checks Tracker

- [x] `CI job: Smoke Test (Debug Build of DotPilot)`
  Failure symptom: GitHub Actions run `23005673864` fails in the build step before tests run.
  Suspected cause: the workflow uses `msbuild`, which cannot resolve the pinned `.NET 10.0.200` SDK because the hosted Visual Studio MSBuild version is `17.14`, below the required `18.0`.
  Intended fix path: move the workflow off `msbuild` and onto `dotnet build` or `dotnet test` with the pinned SDK installed by `actions/setup-dotnet`.
  Status: replaced by the `UI Tests` job in the corrected workflow.

- [x] `CI job: Unit Tests`
  Failure symptom: GitHub Actions run `23005673864` fails in the build step and never reaches the test runner.
  Suspected cause: same incompatible `msbuild` path as the old build-only UI job.
  Intended fix path: use `dotnet` restore, build, and test commands that match the repo-root commands.
  Status: fixed by the `dotnet`-native workflow.

- [x] `Coverage command: dotnet test DotPilot.Tests/DotPilot.Tests.csproj --collect:"XPlat Code Coverage"`
  Failure symptom: local run previously hung in the `coverlet.collector` data collector after test execution started.
  Suspected cause: the collector was instrumenting generated Uno `obj` and hot-reload artifacts before test execution, which made the command effectively unusable for the repo gate.
  Intended fix path: keep `coverlet.collector`, but move the coverage command to a repo-owned runsettings file that targets the product assembly and excludes generated sources.
  Status: fixed by `DotPilot.Tests/coverlet.runsettings`.

- [x] `CI capability: Desktop publish artifacts for macOS, Windows, and Linux`
  Failure symptom: pull requests and mainline runs did not produce downloadable application outputs for desktop reviewers.
  Suspected cause: the workflow only ran quality and test jobs, with no publish stage or artifact upload.
  Intended fix path: add a stable matrix job that publishes `net10.0-desktop` on `macos-latest`, `windows-latest`, and `ubuntu-latest`, then uploads the publish directories as artifacts.
  Status: fixed by the `Desktop Artifact` matrix job in `.github/workflows/ci.yml`.

## Validation Notes

- `dotnet format DotPilot.slnx --verify-no-changes` passed.
- `dotnet build DotPilot.slnx` passed.
- `dotnet build DotPilot.slnx -warnaserror` passed.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj` passed with `3` tests green.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"` passed and produced `coverage.cobertura.xml`.
- `dotnet test DotPilot.UITests/DotPilot.UITests.csproj` passed with `6` UI tests green and `0` skipped.
- `dotnet test DotPilot.slnx` passed and included both the unit and UI suites.
- `dotnet publish DotPilot/DotPilot.csproj -c Release -f net10.0-desktop` passed locally on macOS and produced a publish directory under `artifacts/local-macos-publish`.
- GitHub repository ruleset `Require Full CI Validation` was created in active mode and initially required `Quality`, `Unit Tests`, `Coverage`, and `UI Tests` on the default branch and `refs/heads/release/*`; it now also needs the new desktop artifact checks after the workflow is pushed and verified.

## Final Validation Skills

1. `mcaf-ci-cd`
Reason: align the GitHub Actions workflow and repository enforcement with the intended PR quality gate.

2. `mcaf-dotnet`
Reason: keep the workflow and local command path correct for the pinned `.NET 10` toolchain.

3. `mcaf-testing`
Reason: prove the required unit, coverage, and UI test flows run for real.

4. `mcaf-solution-governance`
Reason: keep durable repo rules and enforcement notes aligned with the new CI contract.
