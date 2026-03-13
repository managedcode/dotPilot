## Goal

Implement issue `#24` by embedding a local-first Orleans silo into the Uno desktop host, using `UseLocalhostClustering` plus in-memory grain storage and reminders, while keeping the browser/UI-test path isolated from server-only Orleans dependencies.

## Scope

In scope:
- Add the minimum Orleans contracts and grain interfaces for the initial runtime host cut
- Add a dedicated runtime host class library for the embedded Orleans implementation
- Register the initial Session, Workspace, Fleet, Policy, and Artifact grains
- Integrate the embedded Orleans silo into the Uno desktop startup path only
- Expose enough runtime-host status to validate startup, shutdown, and configuration through tests and docs
- Update architecture/docs for the new runtime host boundary

Out of scope:
- Agent Framework orchestration
- Remote clustering
- External durable storage providers
- Full UI work beyond existing runtime/readiness presentation needs

## Constraints And Risks

- The first Orleans cut must use `UseLocalhostClustering` and in-memory storage/reminders only.
- The Uno app must remain presentation-only; Orleans implementation must live in a separate DLL.
- Browserwasm and UI-test paths must stay green; server-only Orleans packages must not leak into the browser build.
- All validation must pass with `-warnaserror`.
- No mocks, fakes, or stubs in verification.

## Testing Methodology

- Add contract and runtime tests for Orleans host configuration, host lifecycle, and initial grain registration.
- Verify the app composition path through real DI/build boundaries rather than isolated helper tests only.
- Keep `DotPilot.UITests` in the final validation because browser builds must remain unaffected by the Orleans addition.
- Prove the host uses localhost clustering plus in-memory storage/reminders through caller-visible configuration or startup behavior, not just private constants.

## Ordered Plan

- [x] Confirm the correct backlog item and architecture boundary for Orleans hosting.
- [x] Record the Orleans local-host policy in governance before implementation.
- [x] Inspect current runtime contracts, startup composition, and test seams for the Orleans host insertion point.
- [x] Add or update the runtime-host feature contracts in `DotPilot.Core`.
- [x] Add a dedicated Orleans runtime host project with the minimum official Orleans package set and a local `AGENTS.md`.
- [x] Implement the embedded Orleans silo configuration with localhost clustering and in-memory storage/reminders.
- [x] Register the initial Session, Workspace, Fleet, Policy, and Artifact grains.
- [x] Integrate the Orleans host into the Uno desktop startup/composition path without affecting browserwasm.
- [x] Add or update automated tests for contracts, lifecycle, and composition.
- [x] Update `docs/Architecture.md` and the relevant feature/runtime docs with Mermaid diagrams and the runtime-host boundary.
- [x] Run the full repo validation sequence:
  - `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - `dotnet test DotPilot.slnx`
  - `dotnet format DotPilot.slnx --verify-no-changes`
  - `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
- [ ] Commit the implementation and open a PR that uses GitHub closing references for `#24`.

## Full-Test Baseline

- [x] `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - Passed with `0` warnings and `0` errors.
- [x] `dotnet test DotPilot.slnx`
  - Passed with `60` unit tests and `22` UI tests.

## Tracked Failing Tests

- [x] `InitialGrainsReturnNullBeforeTheirFirstWrite`
  - Symptom: Orleans `CodecNotFoundException` for `SessionDescriptor`
  - Root cause: control-plane runtime DTOs were not annotated for Orleans serialization/code generation
  - Fix status: resolved by adding Orleans serializer metadata to the domain contracts
- [x] `InitialGrainsRoundTripTheirDescriptorState`
  - Symptom: Orleans `CodecNotFoundException` for compiler-generated `<>z__ReadOnlyArray<AgentProfileId>`
  - Root cause: collection-expression values stored in `IReadOnlyList<T>` produced a compiler-internal runtime type that Orleans could not deep-copy
  - Fix status: resolved by changing runtime-bound collection properties to array-backed contract fields
- [x] `SessionGrainRejectsDescriptorIdsThatDoNotMatchThePrimaryKey`
  - Symptom: the same collection-copy failure masked the intended `ArgumentException`
  - Root cause: serialization failed before the grain method body executed
  - Fix status: resolved after the array-backed contract change
- [x] `SessionStateDoesNotSurviveHostRestartWhenUsingInMemoryStorage`
  - Symptom: the same collection-copy failure blocked the in-memory restart assertion
  - Root cause: serialization failed before persistence behavior could be exercised
  - Fix status: resolved after the array-backed contract change

## Final Validation Notes

- `dotnet build DotPilot.slnx -warnaserror -m:1 -p:BuildInParallel=false`
  - Passed with `0` warnings and `0` errors after the final regression-test update.
- `dotnet test DotPilot.slnx`
  - Passed with `67` unit tests and `22` UI tests.
- `dotnet format DotPilot.slnx --verify-no-changes`
  - Passed with no formatting drift.
- `dotnet test DotPilot.Tests/DotPilot.Tests.csproj --settings DotPilot.Tests/coverlet.runsettings --collect:"XPlat Code Coverage"`
  - Passed with a non-zero report after changing `ExcludeAssembliesWithoutSources` from `MissingAny` to `MissingAll`, which keeps mixed-source Orleans-generated assemblies measurable instead of dropping the whole report to zero.
  - Latest report: `82.55%` line coverage and `50.39%` branch coverage overall, with `DotPilot.Runtime.Host` at `100%` line and `100%` branch coverage.

## Done Criteria

- Orleans hosting is implemented through a dedicated non-UI DLL and integrated into the desktop host.
- The host uses `UseLocalhostClustering` plus in-memory storage/reminders.
- The initial core grains are registered and reachable through real runtime tests.
- Browser/UI-test validation remains green.
- A PR is open with `Closes #24`.
