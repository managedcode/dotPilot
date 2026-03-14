## Goal

Implement epic `#12` on one delivery branch by covering its direct child issues `#24`, `#25`, `#26`, and `#27` in a single tested runtime slice, while keeping the Uno app presentation-only and keeping the first Orleans host cut on localhost clustering with in-memory Orleans storage/reminders.

## Scope

In scope:
- issue `#24`: embedded Orleans silo inside the desktop host with the initial core grains
- issue `#25`: Microsoft Agent Framework integration as the orchestration runtime on top of the embedded host
- issue `#26`: explicit grain traffic policy and visibility, with package-compatible graphing kept honest
- issue `#27`: session persistence, replay, checkpointing, and resume for local-first runtime flows
- runtime-facing contracts, deterministic orchestration seams, docs, and tests needed to prove the full epic behavior

Out of scope:
- related but non-child issues such as `#50`, `#69`, and `#77`
- provider-specific live adapters beyond the existing deterministic or environment-gated paths
- remote Orleans clustering or external durable storage providers
- replacing the current Uno shell with a different UI model

## Constraints And Risks

- The app project must stay presentation-only; runtime hosting, orchestration, graph policy, and persistence logic belong in separate DLLs.
- The first Orleans host cut must use `UseLocalhostClustering`, in-memory grain storage, and in-memory reminders.
- Durable session replay and resume for `#27` must not force a remote or durable Orleans cluster; if needed, it must persist serialized session/checkpoint data outside Orleans storage.
- All added behavior must be covered by automated tests and the full repo validation sequence must stay green.
- Any new dependencies must be the minimum official set needed for the runtime slice and must remain compatible with the pinned SDK and current `LangVersion`.
- `ManagedCode.Orleans.Graph` currently targets Orleans `9.x`; this branch must not lie about graph enforcement if the package cannot coexist with Orleans `10.0.1`.

## Testing Methodology

- Cover host lifecycle, grain registration, traffic policy, orchestration execution, session serialization, checkpoint persistence, replay, and resume through real runtime boundaries.
- Keep deterministic in-repo orchestration available for CI so the epic remains testable without external provider CLIs or auth.
- Add regression tests for both happy-path and negative-path flows:
  - invalid runtime requests
  - traffic-policy violations
  - missing or corrupt persisted session state
  - restart/resume behavior
- Keep `DotPilot.UITests` in the final pass because browser and app composition must remain green even when runtime hosting expands.
- Require every direct child issue in scope to map to at least one explicit automated test flow.

## Ordered Plan

- [x] Confirm the exact direct-child issue set for epic `#12` and keep unrelated issues out of the PR scope.
- [x] Add or restore the embedded Orleans host slice from the cleanest available implementation path for issue `#24`.
- [x] Add the minimum runtime dependencies and contracts for Microsoft Agent Framework orchestration for issue `#25`.
- [x] Implement the first orchestration runtime path on top of the deterministic runtime flow and Orleans-backed runtime boundaries.
- [x] Add explicit grain traffic policy modeling and enforcement for issue `#26`, including runtime-visible policy information and denial behavior.
- [x] Add local-first session persistence, replay, checkpointing, and resume for issue `#27` without changing Orleans clustering/storage topology.
- [x] Update runtime docs, feature docs, ADR references, and architecture diagrams so the epic boundaries and flows are explicit.
- [x] Add or update automated tests for every covered issue:
  - host lifecycle and grain registration
  - orchestration execution and session serialization
  - traffic-policy allow and deny flows
  - checkpoint persistence, replay, and resume
- [x] Run the full repo validation sequence:
  - `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - `dotnet test DotPilot.slnx`
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
- [ ] Commit the epic branch implementation and open one PR that closes epic `#12` and its covered child issues correctly.

## Full-Test Baseline

- [x] `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - Passed with `0` warnings and `0` errors.
- [x] `dotnet test DotPilot.slnx`
  - Passed with `52` unit tests and `22` UI tests.

## Tracked Failing Tests

- [x] No baseline failures before epic implementation.
- [x] `ExecuteAsyncPausesForApprovalAndResumeAsyncCompletesAfterHostRestart`
  - Failure symptom: paused archive was persisted before Agent Framework checkpoint files had materialized, so `CheckpointId` was null.
  - Root cause: `RunAsync()` returns on `RequestHaltEvent` before checkpoint metadata is always observable through `Run.LastCheckpoint`.
  - Fix path: wait for checkpoint materialization and resolve checkpoint metadata from run state plus persisted checkpoint files.
- [x] `ResumeAsyncPersistsRejectedApprovalAsFailedReplay`
  - Failure symptom: resume on the same runtime client threw workflow ownership errors.
  - Root cause: `Run` handles were not disposed, so the workflow remained owned by the previous runner.
  - Fix path: dispose Agent Framework `Run` handles with `await using`.

## Done Criteria

- The branch covers direct child issues `#24`, `#25`, `#26`, and `#27` with real implementation, not only planning artifacts.
- The Uno app remains presentation-only and browser-safe.
- Orleans stays on localhost clustering and in-memory storage/reminders.
- Orchestration, traffic policy, and session persistence flows are automated and green.
- The final PR references the epic and child issues with correct GitHub closing semantics.

## Final Validation Results

- `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - Passed with `0` warnings and `0` errors.
- `dotnet test DotPilot.slnx`
  - Passed with `72` unit tests and `22` UI tests.
- `dotnet format DotPilot.slnx --verify-no-changes`
  - Passed with no formatting drift.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
  - Passed with overall coverage `84.26%` line and `50.93%` branch.
  - Changed runtime files met the repo bar:
    - `AgentFrameworkRuntimeClient`: `100.00%` line / `90.00%` branch
    - `RuntimeSessionArchiveStore`: `100.00%` line / `100.00%` branch
    - `EmbeddedRuntimeTrafficPolicy`: `100.00%` line / `83.33%` branch
    - `EmbeddedRuntimeTrafficPolicyCatalog`: `100.00%` line / `100.00%` branch
