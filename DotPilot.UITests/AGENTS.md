# AGENTS.md

Project: `DotPilot.UITests`
Stack: `.NET 10`, `NUnit`, `Uno.UITest`, browser-driven smoke tests

## Purpose

- This project owns UI smoke coverage for `DotPilot` through the `Uno.UITest` harness.
- It is intended for app-launch and visible-flow verification once the external test prerequisites are satisfied.

## Entry Points

- `DotPilot.UITests.csproj`
- `Constants.cs`
- `TestBase.cs`
- `Given_MainPage.cs`

## Boundaries

- Keep this project focused on end-to-end or smoke-level verification only.
- Do not add app business logic or test-only production hooks here unless they are required for stable automation.
- Treat browser-driver setup and app-launch prerequisites as part of the harness, not as assumptions inside individual tests.

## Local Commands

- `test-ui`: `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
- `test-ui-live`: `UNO_UITEST_DRIVER_PATH=/absolute/path/to/chromedriver dotnet test DotPilot.UITests/DotPilot.UITests.csproj`

## Applicable Skills

- `mcaf-dotnet`
- `mcaf-testing`
- `mcaf-ui-ux`

## Local Risks Or Protected Areas

- The current harness targets a browser flow and requires `UNO_UITEST_DRIVER_PATH`; when the driver is configured it auto-starts the `net10.0-browserwasm` head on a loopback URI resolved by the harness.
- `Constants.cs` and `TestBase.cs` define environment assumptions for every UI test; update them carefully and only when the automation target actually changes.
