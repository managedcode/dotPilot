# Issue 23 Communication Contracts Plan

## Goal

Adopt `ManagedCode.Communication` for the first public runtime success and failure contracts so provider, runtime, orchestration, and policy slices can share one result language instead of inventing parallel abstractions.

## Scope

### In Scope

- Add `ManagedCode.Communication` as a centrally managed dependency for the control-plane contract layer
- Introduce an issue-aligned communication slice in `DotPilot.Core`
- Define the first runtime result and problem categories for provider, policy, orchestration, validation, and environment failures
- Rewire the current runtime-foundation public contracts to use the communication result model
- Update the deterministic runtime path and tests to prove success and failure flows
- Update architecture and feature docs so issue `#23` is explicit and referenceable

### Out Of Scope

- Full provider adapter implementation
- End-user copywriting for every error state
- Orleans host implementation
- Agent Framework orchestration implementation

## Constraints And Risks

- The Uno app must stay presentation-only; communication contracts belong outside the UI host
- Avoid inventing a second result abstraction next to `ManagedCode.Communication`
- Keep the first result surface small and focused on public runtime boundaries
- Preserve deterministic CI-safe runtime coverage while adding richer failure contracts
- Keep the package addition explicit and centrally managed in `Directory.Packages.props`

## Testing Methodology

- Baseline proof: run the full solution test command after the plan is created
- Contract proof: add focused tests for success and failure result flows through the communication slice
- Integration proof: rerun the deterministic runtime tests after rewiring the public contract
- UI proof: rerun the full UI suite to confirm the updated public contract does not break the presentation host
- Final proof: run repo-ordered `format`, `build`, full `test`, and the coverage command
- Quality bar: the runtime boundary uses one explicit success/failure contract language, deterministic flows remain green, and coverage does not regress

## Ordered Plan

- [x] Step 1: Establish the baseline on the new branch after the plan is prepared.
  Verification:
  - `dotnet test DotPilot.slnx`
  Done when: any pre-existing failure is tracked here before the issue `#23` changes begin.

- [x] Step 2: Add `ManagedCode.Communication` and create the issue `#23` communication slice in `DotPilot.Core`.
  Verification: the package is centrally managed and the new slice defines stable result/problem categories without leaking UI concerns.
  Done when: public runtime communication contracts live in a dedicated feature folder and compile cleanly.

- [x] Step 3: Rewire runtime-foundation public contracts and deterministic runtime flows to use the communication result model.
  Verification: success and failure paths for the deterministic runtime client travel through the new communication boundary instead of raw ad hoc records.
  Done when: runtime-foundation no longer exposes a custom parallel result abstraction.

- [x] Step 4: Add or update automated tests for communication success, validation failure, approval pause, and environment-unavailable paths.
  Verification: tests assert both success payloads and failure/problem categories through the public boundary.
  Done when: the new communication slice is protected by realistic runtime-flow tests.

- [x] Step 5: Update durable docs for issue `#23`.
  Verification: architecture and feature docs show where the communication slice sits between the domain model and later provider/runtime implementations.
  Done when: later slices can reference the result language directly from docs.

- [x] Step 6: Run final validation in repo order and record the results.
  Verification:
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - `dotnet build DotPilot.slnx -warnaserror`
  - `dotnet test DotPilot.slnx`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
  Done when: the commands are green or any remaining blocker is explicitly documented here.

- [ ] Step 7: Create the PR for issue `#23`, then continue from a fresh branch for the next slice.
  Verification: the branch and PR flow matches the enforced repo workflow.
  Done when: the issue `#23` PR exists and follow-up work no longer accumulates on its review branch.

## Validation Notes

- `dotnet test DotPilot.slnx` passed with `0` failed, `37` passed, and `0` skipped on the new `codex/issue-23-communication-contracts` branch baseline.
- `Directory.Packages.props` and `DotPilot.Core/DotPilot.Core.csproj` now add `ManagedCode.Communication` as the shared result/problem dependency for the core contract layer.
- `DotPilot.Core/Features/RuntimeCommunication/*` now owns typed communication problem codes and centralized `Problem` factories for validation, provider readiness, runtime-host availability, orchestration availability, and policy rejection.
- `DotPilot.Core/Features/RuntimeFoundation/IAgentRuntimeClient.cs` now exposes `ValueTask<Result<AgentTurnResult>>`, so runtime public boundaries use `ManagedCode.Communication` instead of a parallel ad hoc result abstraction.
- `DotPilot.Runtime/Features/RuntimeFoundation/DeterministicAgentRuntimeClient.cs` now returns explicit success and failure results for plan, execute, review, approval-pause, blank-prompt, and provider-unavailable paths.
- `DotPilot.Tests/RuntimeFoundationCatalogTests.cs` and `DotPilot.Tests/RuntimeCommunicationProblemsTests.cs` now cover success results, validation failures, provider-environment failures, and direct problem-code mappings.
- `docs/Features/runtime-communication-contracts.md` documents the issue `#23` result/problem language with a Mermaid flow.
- `dotnet format DotPilot.slnx --verify-no-changes` passed.
- `dotnet build DotPilot.slnx -warnaserror` passed with `0` warnings and `0` errors.
- `dotnet test DotPilot.slnx` passed with `0` failed, `49` passed, and `0` skipped.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"` passed with `0` failed, `35` passed, and `0` skipped.
- Coverage from `DotPilot.Tests/TestResults/57d524aa-22ea-4a88-acb2-3757c3eb671c/coverage.cobertura.xml` is `99.20%` line coverage and `86.66%` branch coverage across the covered production surface.

## Failing Tests Tracker

- [x] Baseline solution verification
  Failure symptom: none.
  Root cause: baseline solution tests passed before the issue `#23` changes started.
  Fix path: preserve the green baseline while introducing the communication result layer.
  Status: complete.

## Final Validation Skills

1. `mcaf-dotnet`
Reason: the task changes shared contracts and package references in the .NET solution.

2. `mcaf-testing`
Reason: communication boundaries need explicit regression coverage for success and failure flows.

3. `mcaf-architecture-overview`
Reason: issue `#23` adds a new reusable contract slice that must appear in the architecture map.
