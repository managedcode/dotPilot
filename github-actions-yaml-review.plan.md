# GitHub Actions YAML Review Plan

## Goal

Review the current GitHub Actions validation and release workflows, record concrete risks with line-level evidence, and capture any durable CI policy that emerged from the user conversation, including mandatory `-warnaserror` enforcement for local and CI builds, keeping formatting as a local pre-push concern instead of a CI gate, and preferring analyzer-backed quality gates for overloaded methods.

## Scope

### In Scope

- `.github/workflows/build-validation.yml`
- `.github/workflows/release-publish.yml`
- shared workflow assumptions from `.github/steps/install_dependencies/action.yml`
- root governance updates needed to capture durable CI runner policy, mandatory `-warnaserror` build usage, local-only formatting policy, and analyzer-backed maintainability gates

### Out Of Scope

- application code changes unrelated to CI/CD
- release-note content changes
- speculative platform changes without a concrete workflow finding

## Constraints And Risks

- The review must focus on real failure or operability risks, not styling-only nits.
- Findings must use exact file references and line numbers.
- Desktop builds must stay on native OS runners for the target artifact.

## Testing Methodology

- Static review of workflow logic, trigger model, runner selection, packaging steps, and rerun behavior
- Validation commands for any touched YAML or policy file:
  - `dotnet build DotPilot.slnx -warnaserror`
  - `actionlint .github/workflows/build-validation.yml`
  - `actionlint .github/workflows/release-publish.yml`
- Quality bar:
  - every reported finding must describe a user-visible or operator-visible failure mode
  - no finding should rely on vague “could be cleaner” arguments

## Ordered Plan

- [x] Step 1: Read the current workflows, shared composite action, and relevant governance/docs.
  Verification:
  - inspect the workflow YAML and shared action with line numbers
  - inspect the nearest architecture/ADR references for the intended CI split
  Done when: the current workflow intent and boundaries are clear.

- [x] Step 2: Record durable policy from the latest user instruction.
  Verification:
  - update `AGENTS.md` with the native-runner rule for desktop build/publish jobs
  - update `AGENTS.md` with mandatory `-warnaserror` usage for local and CI builds
  Done when: the rules are present in root governance.

- [x] Step 3: Apply the explicitly requested CI warning-policy fix.
  Verification:
  - `build-validation.yml` runs the build step with `-warnaserror`
  - no duplicate non-value build step remains after the change
  Done when: CI and local build policy both require `-warnaserror`.

- [x] Step 4: Apply the explicitly requested formatting-gate policy.
  Verification:
  - `build-validation.yml` no longer runs `dotnet format`
  - `AGENTS.md` keeps format as a local pre-push check instead of a CI job step
  Done when: formatting is enforced locally but not re-run as a GitHub Actions validation gate.

- [x] Step 5: Produce the workflow review findings.
  Verification:
  - findings reference exact files and lines
  - findings are ordered by severity
  Done when: the user can act on the review without needing another pass to discover the real problems.

- [x] Step 6: Add analyzer-backed maintainability gating for overloaded methods.
  Verification:
  - enable `CA1502` in `.editorconfig`
  - attach a repo-level `CodeMetricsConfig.txt` threshold through `Directory.Build.props`
  - `dotnet build DotPilot.slnx -warnaserror` stays green
  Done when: excessive method complexity is enforced by the normal build gate instead of a standalone CI helper step.

- [x] Step 7: Validate touched YAML/policy files.
  Verification:
  - `dotnet build DotPilot.slnx -warnaserror`
  - `dotnet test DotPilot.slnx`
  - `actionlint .github/workflows/build-validation.yml`
  - `actionlint .github/workflows/release-publish.yml`
  Done when: touched workflow files still parse cleanly.

- [x] Step 8: Remove deprecated Node 20 JavaScript action usage from GitHub workflows.
  Verification:
  - upgrade `actions/checkout` usages to the current stable major
  - upgrade `actions/setup-dotnet` usage in the shared composite action to the current stable major
  - `actionlint` still passes for both workflows
  Done when: the workflows no longer pin the deprecated Node 20 action majors reported by GitHub Actions.

## Full-Test Baseline Step

- [x] Capture the current workflow state before proposing changes.
  Done when: the review is grounded in the current checked-in YAML, not stale assumptions.

## Failing Tests And Checks Tracker

- [x] `Build Validation #23045490754 -> Quality Gate -> Build`
  Failure symptom: Windows CI build fails with `Uno.Dsp.Tasks.targets(20,3): error : Unable to find uno.themes.winui.markup in the Nuget cache.`
  Suspected cause: the `Dsp` Uno feature activates build-time DSP generation in CI even though the generated `Styles/ColorPaletteOverride.xaml` file is already checked into the repo; on GitHub Actions Windows the DSP task looks for a package identity that is not present in the restored NuGet cache.
  Intended fix path: keep `Dsp` enabled for local design-time regeneration only and disable it in CI by conditioning the feature on the `CI` environment property.
  Status: fixed locally; `CI=true dotnet build DotPilot.slnx -warnaserror` and `CI=true dotnet test DotPilot.slnx` both pass.

## Final Validation Skills

1. `mcaf-ci-cd`
Reason: review CI/release workflow structure and operator risks.

2. `mcaf-testing`
Reason: ensure the review respects the repo verification model and required test gates.
