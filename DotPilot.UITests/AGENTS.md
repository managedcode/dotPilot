# AGENTS.md

Project: `DotPilot.UITests`
Stack: `.NET 10`, `NUnit`, `Uno.UITest`, browser-driven UI tests

## Purpose

- This project owns browser-driven UI coverage for `DotPilot` through the `Uno.UITest` harness.
- It is intended for app-launch and visible-flow verification once the external test prerequisites are satisfied.

## Entry Points

- `DotPilot.UITests.csproj`
- `Constants.cs`
- `TestBase.cs`
- `Given_MainPage.cs`

## Boundaries

- Keep this project focused on end-to-end browser verification only.
- Do not add app business logic or test-only production hooks here unless they are required for stable automation.
- Treat browser-driver setup and app-launch prerequisites as part of the harness, not as assumptions inside individual tests.
- The harness must make `dotnet test DotPilot.UITests/DotPilot.UITests.csproj` runnable without manual driver-path export and must fail loudly instead of silently skipping coverage.
- Keep the harness direct and minimal; prefer the smallest deterministic setup needed to run the suite and return a real result.
- UI tests must cover each feature's interactive elements, expected behaviors, and full operator flows instead of only a top-level smoke path.

## Local Commands

- `test-ui`: `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
- `test-ui-live`: `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-testing`
- `mcaf-ui-ux`

## Local Risks Or Protected Areas

- The harness targets a browser flow and auto-starts the `net10.0-browserwasm` head on a loopback URI resolved by the harness; any driver discovery or bootstrap logic must stay deterministic across local and agent environments.
- `Constants.cs` and `TestBase.cs` define environment assumptions for every UI test; update them carefully and only when the automation target actually changes.
- Every new UI capability should arrive with assertions for the visible controls it adds and at least one complete end-to-end flow through the affected surface.
