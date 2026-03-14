# PR 76 Review Follow-up Plan

## Goal

Address the meaningful review comments on `PR #76`, remove backlog-specific text that leaked into production `ToolchainCenter` runtime metadata, and update the PR body so merge closes every relevant open issue included in the stacked change set.

## Scope

- In scope:
  - `DotPilot.Runtime` fixes for `ToolchainCenterCatalog`, `ToolchainCommandProbe`, `ToolchainProviderSnapshotFactory`, and `RuntimeFoundationCatalog`
  - regression and behavior tests in `DotPilot.Tests`
  - PR `#76` body update with GitHub closing references for the open issue stack included in the branch history
- Out of scope:
  - new product features outside existing `PR #76`
  - dependency changes
  - release workflow changes

## Constraints And Risks

- Build and test must run with `-warnaserror`.
- Do not run parallel `dotnet` or `MSBuild` work in the same checkout.
- `DotPilot.UITests` remains mandatory final verification.
- Review fixes must not keep GitHub backlog text inside production runtime snapshots or user-facing summaries.
- PR body should only close issues actually delivered by this stacked branch.

## Testing Methodology

- Runtime snapshot and probe behavior will be tested through `DotPilot.Tests` using real subprocess execution paths rather than mocks.
- Catalog lifecycle fixes will be covered with deterministic tests that validate disposal, snapshot stability, and provider caching behavior.
- Final validation must prove both the focused runtime slice and the broader repo verification path.

## Ordered Plan

- [x] Step 1. Establish the real baseline for this PR branch.
  - Verification:
    - `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
    - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter Toolchain`
    - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter RuntimeFoundationCatalog`
- [x] Step 2. Remove backlog-specific text from `ToolchainCenterCatalog` and make snapshot polling/disposal thread-safe.
  - Verification:
    - targeted `ToolchainCenterCatalogTests`
- [x] Step 3. Fix `ToolchainCommandProbe` launch-failure and redirected-stream handling.
  - Verification:
    - targeted `ToolchainCommandProbeTests`
- [x] Step 4. Fix provider-summary/status logic in `ToolchainProviderSnapshotFactory`.
  - Verification:
    - targeted `ToolchainProviderSnapshotFactoryTests`
- [x] Step 5. Fix `RuntimeFoundationCatalog` provider caching so UI-thread snapshot reads do not re-probe subprocesses.
  - Verification:
    - targeted `RuntimeFoundationCatalogTests`
- [x] Step 6. Update PR `#76` body with GitHub closing references for all relevant open issues merged through this stack.
  - Verification:
    - `gh pr view 76 --repo managedcode/dotPilot --json body`
- [x] Step 7. Run final verification and record outcomes.
  - Verification:
    - `dotnet format DotPilot.slnx --verify-no-changes`
    - `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
    - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj`
    - `dotnet test DotPilot.UITests/DotPilot.UITests.csproj`
    - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`

## Baseline Results

- [x] `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
- [x] `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter Toolchain`
- [x] `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --filter RuntimeFoundationCatalog`

## Known Failing Tests

- None. The focused baseline and final repo validation passed.

## Results

- `dotnet format DotPilot.slnx --verify-no-changes` passed.
- `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false` passed.
- `dotnet test DotPilot.slnx` passed with `57` unit tests and `22` UI tests green.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"` passed with overall collector result `91.09%` line / `63.66%` branch.
- `PR #76` body now uses `Closes #13`, `Closes #14`, and `Closes #28-#39`, so those issues will auto-close on merge.

## Final Validation Skills

- `mcaf-dotnet`
  - Run build and test verification with the repo-defined commands.
- `mcaf-testing`
  - Confirm new regressions cover the review-comment failure modes.
- `gh-address-comments`
  - Verify the review comments are resolved and the PR body closes the correct issues on merge.
