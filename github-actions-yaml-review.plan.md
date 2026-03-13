# GitHub Actions YAML Review Plan

## Goal

Review the current GitHub Actions validation and release workflows, record concrete risks with line-level evidence, and capture any durable CI policy that emerged from the user conversation, including mandatory `-warnaserror` enforcement for local and CI builds.

## Scope

### In Scope

- `.github/workflows/build-validation.yml`
- `.github/workflows/release-publish.yml`
- shared workflow assumptions from `.github/steps/install_dependencies/action.yml`
- root governance updates needed to capture durable CI runner policy and mandatory `-warnaserror` build usage

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

- [x] Step 4: Produce the workflow review findings.
  Verification:
  - findings reference exact files and lines
  - findings are ordered by severity
  Done when: the user can act on the review without needing another pass to discover the real problems.

- [x] Step 5: Validate touched YAML/policy files.
  Verification:
  - `dotnet build DotPilot.slnx -warnaserror`
  - `actionlint .github/workflows/build-validation.yml`
  - `actionlint .github/workflows/release-publish.yml`
  Done when: touched workflow files still parse cleanly.

## Full-Test Baseline Step

- [x] Capture the current workflow state before proposing changes.
  Done when: the review is grounded in the current checked-in YAML, not stale assumptions.

## Failing Tests And Checks Tracker

- [x] None currently tracked for this review-only pass.

## Final Validation Skills

1. `mcaf-ci-cd`
Reason: review CI/release workflow structure and operator risks.

2. `mcaf-testing`
Reason: ensure the review respects the repo verification model and required test gates.
