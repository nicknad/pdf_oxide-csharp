## Multi-Agent Development

### Workspace Isolation

- Each agent MUST use its own Git worktree or clone.
- Agents MUST NOT modify files in another agent's working directory.
- Agents MUST NOT share a working directory or Git index.
- Each agent MUST have a clearly defined task and file ownership.
- Before starting, inspect the current branch, working tree, and repository instructions.

### Branching

- `main` is the single integration branch and MUST remain releasable.
- Each agent MUST work on a short-lived branch based on the latest `main`.
- Agents MUST NOT independently create competing integration branches.
- Agents MUST NOT force-push or rewrite shared branch history.
- Agents MUST NOT commit another agent's changes.

### Task Decomposition

- Prefer independent tasks with minimal file overlap.
- Split large features into small, independently buildable and testable increments.
- Define interfaces and shared contracts before parallelizing dependent implementation work.
- If two agents need to modify the same files, coordinate ownership or serialize those changes.
- Do not create parallel work merely to increase agent count.

### Integration

- Integrate completed increments into `main` as soon as they are independently safe.
- Do not wait for every agent working on a larger feature to finish.
- Before integration, fetch the latest `main` and reconcile any conflicts.
- Rebase or merge the agent branch onto the latest `main` before integration.
- Run the required build, tests, and validation against the integrated result.
- Never bypass required CI checks or code review.
- After integration, agents MUST synchronize with the latest `main` before continuing.
- If integration fails, resolve the failure before accepting additional dependent changes.

### Feature Flags

- Use feature flags to allow incomplete user-facing functionality to be integrated safely.
- New release flags MUST default to OFF in production.
- Test both enabled and disabled behavior.
- Keep flag checks at meaningful behavioral boundaries.
- Document the flag's purpose, owner, and removal condition.
- Remove temporary flags and obsolete code paths after release.

### Completion Report

For each completed task, report:

1. The implemented change and its integration status.
2. The commit or pull request containing the change.
3. Build and test results.
4. Any conflicts, remaining work, or known risks.

## Required Workflow

1. Inspect the current branch, working tree, and repository instructions.
2. Identify the smallest useful, independently mergeable change.
3. Explain whether a feature flag is needed and why.
4. Implement the increment.
5. Run the relevant tests and build.
6. Verify both flag states if a flag was introduced or modified.
7. Review the diff for unrelated changes and compatibility issues.
8. Report the validation results and any remaining work.
