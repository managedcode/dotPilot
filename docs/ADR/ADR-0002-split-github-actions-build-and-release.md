# ADR-0002: Split GitHub Actions Build Validation and Desktop Release Automation

## Status

Accepted

## Date

2026-03-13

## Context

`dotPilot` previously used a single GitHub Actions workflow file, `.github/workflows/ci.yml`, for every automation concern: formatting, build, analysis, tests, coverage, desktop publishing, and artifact uploads.

That shape no longer matches the repository workflow:

- normal validation should stay focused on build and test feedback
- release publishing has different permissions, side effects, and operator intent
- the release path now needs version bumping in `DotPilot/DotPilot.csproj`
- desktop releases must publish platform artifacts and create a GitHub Release with feature-oriented notes

Keeping all of that in one catch-all workflow makes the automation harder to reason about, harder to secure, and harder to operate safely.

## Decision

We will split GitHub Actions into two explicit workflows:

1. `build-validation.yml`
   - owns formatting, build, analysis, unit tests, coverage, and UI tests
   - runs on normal integration events
   - does not publish desktop artifacts or create releases
2. `release-publish.yml`
   - runs only from explicit release intent through `workflow_dispatch`
   - may run only from `main` or `release/*`
   - bumps `ApplicationDisplayVersion` and `ApplicationVersion` in `DotPilot/DotPilot.csproj`
   - tags the release, publishes desktop outputs for macOS, Windows, and Linux, and creates the GitHub Release
   - prepends repo-owned feature summaries and feature-doc links to GitHub-generated release notes

## Decision Diagram

```mermaid
flowchart LR
  Change["Push or pull request"]
  ReleaseIntent["Manual release dispatch"]
  Validation["build-validation.yml"]
  Release["release-publish.yml"]
  Quality["Format + build + analyze"]
  Tests["Unit + coverage + UI tests"]
  Version["Version bump in DotPilot.csproj"]
  Publish["Desktop publish matrix"]
  GitHubRelease["GitHub Release with feature notes"]

  Change --> Validation
  Validation --> Quality
  Validation --> Tests

  ReleaseIntent --> Release
  Release --> Version
  Release --> Publish
  Release --> GitHubRelease
```

## Alternatives Considered

### 1. Keep a single `ci.yml` for validation and release

Rejected.

This keeps unrelated concerns coupled and makes ordinary CI runs carry release-specific complexity, permissions, and naming.

### 2. Release only from manually edited tags with no version bump in the repository

Rejected.

The repository needs the release version recorded in `DotPilot/DotPilot.csproj`, not only in Git tags or GitHub Release metadata.

### 3. Store release notes entirely as manual workflow input

Rejected.

That makes release quality depend on operator memory instead of repo-owned history and docs. The release flow should be able to generate a meaningful baseline summary from commits and `docs/Features/`.

## Consequences

### Positive

- Validation runs are easier to understand and remain side-effect free.
- Release automation has a clear permission boundary and operator trigger.
- Desktop publish artifacts move to the workflow that actually needs them.
- Release notes now combine GitHub-generated notes with repo-owned feature context.

### Negative

- Release automation now depends on branch write permissions for the workflow token or an equivalent release credential strategy.
- The repository gains dedicated helper scripts for version bumping and release-note generation that must stay aligned with `DotPilot.csproj`.

## Implementation Impact

- Rename the old validation workflow to `build-validation.yml`.
- Add `release-publish.yml` plus repo-owned scripts for version bumping and release-summary generation.
- Update `docs/Architecture.md` and root governance rules to reference the split workflow model.

## References

- [Architecture Overview](../Architecture.md)
- [GitHub Actions Build And Release Split Plan](../../github-actions-build-release-split.plan.md)
