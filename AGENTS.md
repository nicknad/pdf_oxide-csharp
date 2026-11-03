## Trunk-Based Development

- Treat `main` as the single integration branch.
- Start every task from the latest `main`.
- Use short-lived branches only when required for review or isolation.
- Keep branches small and merge-ready; do not accumulate unrelated work.
- Break large features into independently buildable and testable increments.
- Keep every increment compatible with the current production behavior.
- Do not merge code that breaks the build or required tests.
- Do not bypass CI, required checks, or code review.

## Feature Flags

- Use feature flags for incomplete user-facing behavior that must be
  integrated before it is ready for release.
- Do not add a flag to every change. Complete bug fixes and small
  refactorings should normally integrate directly.
- New release flags must default to OFF in production.
- Keep flag checks at meaningful behavioral boundaries.
- Test both enabled and disabled behavior.
- Preserve the existing behavior when a flag is disabled.
- Do not use flags to conceal broken builds, failing tests, or
  incompatible database migrations.
- Document each temporary flag's purpose, owner, and removal condition.
- Remove temporary flags and obsolete code paths once the feature
  is fully released.

## Required Workflow

1. Inspect the current branch, working tree, and repository instructions.
2. Identify the smallest useful, independently mergeable change.
3. Explain whether a feature flag is needed and why.
4. Implement the increment.
5. Run the relevant tests and build.
6. Verify both flag states if a flag was introduced or modified.
7. Review the diff for unrelated changes and compatibility issues.
8. Report the validation results and any remaining work.
