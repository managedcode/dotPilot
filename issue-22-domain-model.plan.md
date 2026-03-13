# Issue 22 Domain Model Plan

## Goal

Define the first stable `dotPilot` control-plane domain contracts for agents, sessions, fleets, providers, runtimes, tools, artifacts, telemetry, approvals, and evaluations so later runtime and UI slices can build on versionable, serialization-safe shapes.

## Scope

### In Scope

- Introduce an issue-aligned domain slice in `DotPilot.Core` for the control-plane model behind issue `#22`
- Move typed identifiers and state enums out of the temporary runtime-foundation slice into the new domain slice
- Define serialization-safe records for agents, sessions, fleets, providers, runtimes, tool capabilities, approvals, artifacts, telemetry, and evaluations
- Update existing runtime-foundation contracts to depend on the new domain slice instead of owning domain primitives directly
- Update architecture and feature documentation so issue `#22` relationships are explicit and navigable
- Add automated tests for identifier semantics, DTO round-tripping, and the updated runtime-foundation composition path

### Out Of Scope

- Concrete Orleans grain implementations
- Microsoft Agent Framework runtime orchestration
- Live provider SDK adapters
- UI redesign beyond the minimum text or binding updates required by the new contract names

## Constraints And Risks

- Keep the Uno app presentation-only; the domain model must live outside `DotPilot`
- Preserve the already-reviewed runtime-foundation slice while extracting shared domain concepts from it
- Prefer versionable DTO shapes that remain safe for `System.Text.Json` serialization and future expansion
- Use modern stable `.NET 10` and `C#` features only when they improve clarity and maintainability
- Keep branch and type boundaries explicit so issue `#23`, `#24`, and `#25` can reference the new contracts directly
- Protect the deterministic CI baseline; external provider availability remains optional and self-gated

## Testing Methodology

- Baseline proof: run the full solution test command after this plan is created
- Contract proof: add focused tests for typed identifiers, default-safe DTO shapes, and JSON round-tripping of the new domain records
- Integration proof: rerun the runtime-foundation tests that consume the moved domain contracts
- UI proof: rerun the full UI suite to confirm the presentation host still renders the runtime foundation surfaces after the contract move
- Final proof: run repo-ordered validation with `format`, `build`, full `test`, and the coverage command
- Quality bar: domain contracts remain deterministic, future-facing, and serialization-safe; the full solution stays green; coverage remains at or above the prior baseline

## Ordered Plan

- [x] Step 1: Establish the full baseline for the new branch after the plan is prepared.
  Verification:
  - `dotnet test DotPilot.slnx`
  Done when: this file records whether any failures predate the issue `#22` changes.

- [x] Step 2: Introduce the `#22` control-plane domain slice in `DotPilot.Core`.
  Verification: typed identifiers, shared state enums, and the new domain DTOs live under an issue-aligned feature folder instead of the runtime-foundation slice.
  Done when: agents, sessions, fleets, providers, runtimes, tools, artifacts, telemetry, approvals, and evaluations all have stable contract shapes.

- [x] Step 3: Rewire the runtime-foundation slice to depend on the new domain contracts.
  Verification: `RuntimeFoundation` keeps its feature-specific contracts, but all reused domain primitives come from the `#22` domain slice.
  Done when: runtime-foundation no longer owns cross-cutting control-plane primitives.

- [x] Step 4: Update durable docs for issue `#22`.
  Verification: architecture and feature docs show the new domain slice, its relationships, and how later issues build on it.
  Done when: future agents can trace `#22` from docs without reverse-engineering the code.

- [x] Step 5: Add or update automated tests for the new domain model and moved runtime dependencies.
  Verification: tests cover identifier creation and formatting, JSON round-tripping, representative domain DTOs, and the runtime slice's continued behavior.
  Done when: the moved contracts are protected by meaningful assertions instead of compile-only confidence.

- [x] Step 6: Run final validation in repo order and record the results.
  Verification:
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - `dotnet build DotPilot.slnx -warnaserror`
  - `dotnet test DotPilot.slnx`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
  Done when: the commands are green or any remaining blocker is explicitly documented here.

- [ ] Step 7: Create the PR for issue `#22`, then continue from a fresh branch for the next slice.
  Verification: the branch and PR flow matches the enforced repo workflow after completing a slice.
  Done when: the issue `#22` PR exists and follow-up work no longer accumulates on its review branch.

## Validation Notes

- `dotnet test DotPilot.slnx` passed with `0` failed, `34` passed, and `0` skipped on the new `codex/issue-22-domain-model` branch baseline.
- `DotPilot.Core/Features/ControlPlaneDomain/*` now owns typed identifiers, shared state enums, and stable DTOs for workspaces, agents, fleets, providers, runtimes, approvals, artifacts, telemetry, and evaluations.
- `DotPilot.Core/Features/RuntimeFoundation/RuntimeFoundationContracts.cs` now consumes `ProviderDescriptor` and `ArtifactDescriptor` from the `#22` domain slice instead of owning those cross-cutting shapes locally.
- `DotPilot.Runtime/Features/RuntimeFoundation/*` now builds provider readiness and deterministic runtime artifacts on top of the shared control-plane domain contracts.
- `docs/Features/control-plane-domain-model.md` documents the issue `#22` relationships and downstream issue references with a Mermaid map.
- `DotPilot.Tests/ControlPlaneDomainContractsTests.cs` adds identifier, JSON round-trip, and mixed provider/local-runtime session coverage for the new contract set.
- `dotnet format DotPilot.slnx --verify-no-changes` passed.
- `dotnet build DotPilot.slnx -warnaserror` passed with `0` warnings and `0` errors.
- `dotnet test DotPilot.slnx` passed with `0` failed, `37` passed, and `0` skipped.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"` passed with `0` failed, `23` passed, and `0` skipped.
- Coverage from `DotPilot.Tests/TestResults/cb30b645-002f-45bf-af57-b224bd5073c1/coverage.cobertura.xml` is `99.43%` line coverage and `85.00%` branch coverage across the covered production surface.

## Failing Tests Tracker

- [x] Baseline solution verification
  Failure symptom: none.
  Root cause: baseline solution tests passed before the issue `#22` changes started.
  Fix path: preserve the green baseline while moving shared domain primitives into the new slice.
  Status: complete.

## Final Validation Skills

1. `mcaf-dotnet`
Reason: the change reshapes core contracts and must stay aligned with the repo .NET toolchain.

2. `mcaf-testing`
Reason: domain DTOs and moved runtime dependencies need durable regression coverage.

3. `mcaf-architecture-overview`
Reason: issue `#22` adds a new reusable slice that must appear in the architecture map.
