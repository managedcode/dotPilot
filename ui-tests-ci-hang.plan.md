# UI Tests CI Hang Plan

## Goal

Fix the `DotPilot.UITests` GitHub Actions execution path so the required UI test job returns a real completed result instead of hanging or being canceled while `Run UI Tests` is still active.

## Scope

### In Scope

- `DotPilot.UITests` harness code and supporting configuration
- GitHub Actions validation behavior needed to expose or validate the harness fix
- Durable notes for any repo-level CI implication discovered during the fix

### Out Of Scope

- New product functionality unrelated to UI test stability
- Weakening, skipping, or conditionally bypassing the UI suite
- Release workflow changes unrelated to the UI test blocker

## Constraints And Risks

- The UI test suite is mandatory and must stay in the normal validation workflow.
- A hang, timeout, or canceled run counts as a failing harness outcome.
- Prefer a deterministic harness fix over workflow-level retries or skip logic.
- Keep the fix aligned with the current browserwasm test-launch path.

## Testing Methodology

- GitHub baseline:
  - inspect the active PR run and UI Tests job logs for the actual failure point
- Local validation:
  - `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
  - any focused repro command needed to exercise the harness shutdown path
- Broader validation:
  - `dotnet test DotPilot.slnx`
  - `dotnet format DotPilot.slnx --verify-no-changes`
- Quality bar:
  - UI tests must complete with a terminal pass/fail result locally
  - the fix must directly address the hang/cancel path instead of hiding it

## Ordered Plan

- [x] Step 1: Capture the current failing GitHub UI test job details and relevant local baseline.
  Verification:
  - inspect the active PR workflow run and UI Tests job
  - run the relevant local UI test command
  Done when: the concrete hang symptom and likely failing code path are documented below.

- [x] Step 2: Trace the harness launch and shutdown flow in `DotPilot.UITests` to isolate the blocking operation.
  Verification:
  - identify the exact code path used during CI teardown or cancellation
  - document the likely root cause before editing
  Done when: the intended fix path is clear.

- [x] Step 3: Implement the harness fix and any needed regression coverage.
  Verification:
  - changed code remains within repo maintainability limits
  - local UI test execution still returns a real result
  Done when: the blocking behavior is removed from the identified path.

- [ ] Step 4: Run final validation and record outcomes.
  Verification:
  - `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
  - `dotnet test DotPilot.slnx`
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - GitHub Actions `UI Test Suite` returns a real completed result with the instrumented harness logs available
  Done when: required checks are green locally and the GitHub UI test job returns a real completed result or a concrete failing signal.

## Full-Test Baseline Step

- [x] Run the relevant baseline commands after the plan is prepared:
  - inspect the active GitHub UI Tests job
  - `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
  Done when: the failing symptom is recorded below.

## Failing Tests And Checks Tracker

- [x] `GitHub Actions job: UI Tests`
  Failure symptom: the PR validation run enters `Run UI Tests`, then fails to produce a terminal result and may later surface `Attempting to cancel the build... Error: The operation was canceled.`
  Suspected cause: the UI test harness had unbounded teardown calls for `_browserApp.Dispose()` and `BrowserTestHost.Stop()`, so a Windows-specific cleanup hang could leave the job running until GitHub eventually canceled it.
  Intended fix path: bound teardown cleanup with explicit timeouts, add harness stage logging, bound Chrome version probing, and move the GitHub UI suite to the macOS browser environment that already ships Chrome and ChromeDriver.
  Status: bounded cleanup is fixed locally; the remaining patch now also removes an unbounded browser-version probe and switches CI off the hanging Windows runner path, but GitHub still needs a fresh run to confirm the terminal result.

## Baseline Notes

- PR `#10` currently points at GitHub Actions run `23041255695`, where `Build`, `Unit Tests`, and `Coverage` complete successfully but `UI Tests` stays in progress inside `Run UI Tests`.
- The affected job is `66920240879`, which started `Run UI Tests` at `2026-03-13T07:50:38Z` and did not produce a terminal result while the other validation jobs finished.
- A later PR validation run, `23043020124`, shows the same shape so far: `Quality Gate`, `Unit Test Suite`, and `Coverage Suite` completed while `UI Test Suite` job `66925873162` remained `in_progress` inside `Run UI Tests` after starting at `2026-03-13T08:47:49Z`.
- Local `dotnet test DotPilot.UITests/DotPilot.UITests.csproj` passed before the fix, which indicates the main failure mode is CI-specific hang behavior rather than a consistently failing test case.
- Local `dotnet test ./DotPilot.UITests/DotPilot.UITests.csproj --logger GitHubActions --blame-crash` also passed before the fix on macOS, which points further toward a Windows-specific teardown or process-cleanup issue.

## Validation Notes

- Added bounded cleanup execution in `DotPilot.UITests` so teardown now fails fast if app disposal or browser-host shutdown hangs.
- Added focused regression tests for the bounded-cleanup helper to prove success, exception, and timeout behavior.
- Added harness logging around browser binary resolution, ChromeDriver resolution, host startup, setup, and cleanup so the next GitHub run exposes the exact blocking stage instead of sitting silent inside `Run UI Tests`.
- Added a timeout around Chrome `--version` probing so browser bootstrap now fails fast instead of hanging the whole UI job when the version probe process stalls.
- Added driver-version mapping reuse by browser build/platform so the harness can reuse a cached matching ChromeDriver without re-querying the Chrome-for-Testing patch endpoint every time.
- Moved the GitHub Actions `UI Test Suite` job to `macos-latest` and injects the preinstalled Chrome and ChromeDriver paths through the existing Uno.UITest environment variables.
- `dotnet test DotPilot.UITests/DotPilot.UITests.csproj --filter BoundedCleanupTests` passed.
- `dotnet test DotPilot.UITests/DotPilot.UITests.csproj --filter BrowserAutomationBootstrapTests` passed.
- `dotnet test DotPilot.UITests/DotPilot.UITests.csproj` passed with `9` tests green.
- `dotnet test ./DotPilot.UITests/DotPilot.UITests.csproj --logger GitHubActions --blame-crash` passed with `9` tests green.
- `dotnet test DotPilot.slnx` passed.
- `dotnet format DotPilot.slnx --verify-no-changes` passed.

## Final Validation Skills

1. `gh-fix-ci`
Reason: inspect the failing GitHub Actions run and extract the real failure signal.

2. `mcaf-testing`
Reason: keep the UI suite mandatory and verified through real execution.

3. `mcaf-dotnet`
Reason: ensure the harness fix stays aligned with the repo’s .NET toolchain and quality path.
