# UI Tests Mandatory Plan

## Goal

Make `DotPilot.UITests` runnable through the normal `dotnet test` command path without manual driver-path export or skip behavior, and keep the result honest: real pass or real fail.

## Scope

### In Scope

- `DotPilot.UITests` browser bootstrap and driver resolution
- `DotPilot` build issues that block the browser host from starting
- Governance and architecture docs that described the suite as manually configured or effectively optional
- Repo validation commands needed to prove the UI suite now runs in the normal flow

### Out Of Scope

- New product features unrelated to UI smoke execution
- New smoke scenarios beyond the existing browser coverage
- CI workflow redesign outside the current repo command path

## Constraints And Risks

- Keep the UI-test path direct and minimal.
- Do not reintroduce skip-on-missing-driver behavior.
- Keep `DotPilot.UITests` runnable with `NUnit` on `VSTest`.
- Preserve the `browserwasm` host bootstrap used by the current smoke suite.

## Testing Methodology

- Focused proof: `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
- Broader proof: `dotnet test DotPilot.slnx`
- Quality gates after the focused proof: `format`, `build`, `analyze`, and `coverage`
- Quality bar: zero skipped UI smoke tests caused by missing local driver setup, and green repo validation commands

## Ordered Plan

- [x] Step 1: Capture the baseline failure mode from the focused UI suite and the solution-wide test command.
  Verification: baseline showed the UI suite was being executed but skipped because `TestBase` ignored tests when `UNO_UITEST_DRIVER_PATH` was unset.
  Done when: the false-green skip behavior is reproduced and documented.

- [x] Step 2: Identify the smallest deterministic bootstrap path that keeps the user command simple.
  Verification: browser binary detection plus cached ChromeDriver download chosen as the direct path.
  Done when: the harness plan removes manual setup instead of layering more conditional setup around the tests.

- [x] Step 3: Implement the harness fix and resolve any product build blocker exposed by the now-real UI execution.
  Verification: focused UI suite now runs real browser tests with no skipped cases and passes locally.
  Done when: `dotnet test DotPilot.UITests/DotPilot.UITests.csproj` is green.

- [x] Step 4: Update durable docs to match the implemented UI-test workflow.
  Verification: governance and architecture docs describe automatic browser and driver resolution, not manual setup.
  Done when: stale manual-driver references are removed.

- [ ] Step 5: Run final validation in repo order and record the results.
  Verification:
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - `dotnet build DotPilot.slnx`
  - `dotnet build DotPilot.slnx -warnaserror`
  - `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
  - `dotnet test DotPilot.slnx`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --collect:"XPlat Code Coverage"`
  Done when: all commands are green and this checklist is complete.

## Validation Notes

- `dotnet format DotPilot.slnx --verify-no-changes` passed.
- `dotnet build DotPilot.slnx` passed.
- `dotnet build DotPilot.slnx -warnaserror` passed.
- `dotnet test DotPilot.UITests/DotPilot.UITests.csproj` passed with `0` failed, `6` passed, `0` skipped.
- `dotnet test DotPilot.slnx` passed and included the UI suite with `0` skipped UI tests.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --collect:"XPlat Code Coverage"` hung in the `coverlet.collector` data collector after the unit test process had started, so the coverage gate still needs separate follow-up if it is required for this task closeout.

## Failing Tests Tracker

- [x] `WhenNavigatingToAgentBuilderThenKeySectionsAreVisible`
  Failure symptom: initially skipped because `UNO_UITEST_DRIVER_PATH` was unset.
  Root cause: `TestBase` ignored browser tests instead of resolving driver prerequisites.
  Fix path: browser binary resolution plus cached ChromeDriver bootstrap.
  Status: passes in focused verification.

- [x] `WhenOpeningAgentBuilderThenDesktopSectionWidthIsPreserved`
  Failure symptom: initially skipped; once execution was real, it failed because the browser run did not preserve desktop width.
  Root cause: the browser session needed explicit window-size arguments in addition to the driver/bootstrap fix.
  Fix path: explicit browser window sizing in the UI harness.
  Status: passes in focused verification.

- [x] `WhenOpeningTheAppThenChatShellSectionsAreVisible`
  Failure symptom: initially skipped because `UNO_UITEST_DRIVER_PATH` was unset.
  Root cause: shared `TestBase` skip path.
  Fix path: shared browser bootstrap fix.
  Status: passes in focused verification.

- [x] `WhenReturningToChatFromAgentBuilderThenChatShellSectionsAreVisible`
  Failure symptom: initially skipped because `UNO_UITEST_DRIVER_PATH` was unset.
  Root cause: shared `TestBase` skip path.
  Fix path: shared browser bootstrap fix.
  Status: passes in focused verification.

## Final Validation Skills

1. `mcaf-solution-governance`
Reason: keep the durable rules aligned with the simplified UI-test workflow.

2. `mcaf-dotnet`
Reason: run the actual .NET verification pass and keep the solution build clean.

3. `mcaf-testing`
Reason: prove the browser flow now runs for real instead of skipping.

4. `mcaf-architecture-overview`
Reason: keep `docs/Architecture.md` aligned with the current harness contract.
