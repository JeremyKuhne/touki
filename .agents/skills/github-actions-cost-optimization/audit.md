# Audit GitHub Actions usage

Build an evidence-backed picture before editing a workflow. The audit has two
outputs: the current job topology and the validation invariants that topology is
supposed to enforce.

## 1. Inventory local workflow topology

Read every workflow under `.github/workflows/`, including reusable and release
workflows. Record one row per job, after expanding matrix legs conceptually:

| Field | What to record |
| ----- | -------------- |
| Workflow and job | Display name, job id, and matrix leg names |
| Trigger | `pull_request`, `push`, `merge_group`, tag, schedule, dispatch, or caller |
| Filters | Branch, tag, and path filters plus job-level `if` expressions |
| Runner | Exact label, architecture, operating system, and standard/larger/self-hosted class |
| Dependencies | `needs`, reusable workflow calls, and aggregate status jobs |
| Work | Restore, build, tests, coverage, analysis, perf, AOT, pack, publish, or scripts |
| Repetition | Checkout, SDK setup, restore, cache, and artifact transfer repeated in sibling jobs |
| Storage | Cache keys/paths and uploaded artifact size and retention |
| Controls | Concurrency group, cancellation policy, permissions, and timeout |

Check for trigger overlap. A pull request that runs once before merge and again
on the resulting default-branch push consumes two workflow lifecycles. Tag
pushes can match both branch and release workflows. A reusable workflow is billed
to its caller, so include it in the caller's topology rather than treating it as
free shared infrastructure.

Do not infer behavior from names. Confirm what each command compiles or runs,
whether `--no-build` actually reuses prior output, and whether a cache path
matches the tool's configured package/cache directory.

## 2. Inventory remote merge and governance paths

Read the repository rulesets or branch protection when access permits. Otherwise
mark each item unverified and ask for the missing facts:

- required status-check names and whether they are tied to a GitHub App;
- pull-request and review requirements;
- merge queue use and whether workflows handle `merge_group`;
- administrator, team, automation, or deploy-key bypass paths;
- whether direct pushes, merge commits, or branch updates can reach the default
  branch without testing the exact merge candidate;
- release environments, protected tags, and publishing approvals;
- Actions budgets, artifact/log retention, and cache limits.

This determines whether a default-branch `push` run is duplicate confirmation or
the only validation for a bypass path. Do not remove it based only on the presence
of a `pull_request` trigger.

Required checks and path filters need special care. If an entire required
workflow is skipped because no filtered path matched, GitHub can leave its check
pending and block the merge. Prefer an always-created aggregate check, job-level
conditional work, or a ruleset design that does not require a path-filtered
workflow.

## 3. State validation invariants

List what must remain true before merge and release. Derive this from supported
platforms, packaging promises, security posture, and repository guidance, not
from the current matrix alone. Common invariants include:

- Release configuration builds and tests the merge candidate;
- legacy target frameworks compile or execute on a compatible host;
- platform-specific source and native interop execute on each supported native
  operating system or architecture that cannot be represented elsewhere;
- coverage, API compatibility, formatting, generated-file, and lock-file gates
  continue to fail the required aggregate check;
- performance gates run in a stable environment and cannot reuse stale output;
- trim and Native AOT paths publish and execute for the support contract they
  protect;
- package creation and release authentication are tested before publishing;
- untrusted pull-request code never receives write tokens or secrets;
- code scanning runs at the cadence and merge boundary required by the security
  model.

Classify each invariant:

| Class | Meaning |
| ----- | ------- |
| Automatic blocking | Every merge candidate must pass it |
| Scheduled/manual breadth | Important regression coverage with a named cadence or operator |
| Release-only | Must pass before a release, but not every change |

Record the owner and trigger for every non-automatic invariant. "Available
manually" without an owner or occasion is deferred indefinitely, not preserved
coverage.

## 4. Collect representative execution evidence

Use Actions usage/performance metrics when available. They expose workflow and
job minutes, runner type, average runtime, queue time, and failure rate. The
displayed usage metrics do not apply billing multipliers, so join them to current
runner rates in the cost model.

Without organization metrics, inspect recent runs with GitHub CLI:

```shell
gh run list --workflow WORKFLOW --limit 20
gh run view RUN_ID --json event,startedAt,updatedAt,jobs
```

For each material job, collect successful, failed, and canceled samples over a
representative period. Prefer at least 10 recent ordinary runs when the history
exists. Report the median for a typical estimate and a high percentile (for
example p75) for a conservative estimate. Keep queue time separate from runner
execution time.

Include:

- reruns after flaky or infrastructure failures;
- time consumed before superseded runs are canceled;
- cache-hit and cache-miss samples;
- pull-request updates per merged change;
- scheduled, bot, merge-queue, and post-merge invocations;
- jobs created only to emit required status contexts.

Do not substitute workflow wall-clock duration for runner usage. Parallel jobs
reduce elapsed feedback time while still consuming and billing each runner.

## 5. Produce the baseline topology

End the audit with:

1. A diagram or table showing which logical change causes each workflow and job.
2. The automatic, periodic, and release validation invariants.
3. Unverified remote assumptions that could make an optimization unsafe.
4. The duration samples and invocation frequencies used by the cost model.
5. Obvious waste hypotheses, each paired with a check that could disprove it.

Examples of falsifiable hypotheses:

- "The default-branch run duplicates PR validation" is disproved by an allowed
  direct-push or bypass path.
- "This Linux job can move to ARM64" is disproved by an unavailable toolchain,
  native dependency, or architecture-sensitive test.
- "The cache saves restore time" is disproved when cache-hit and no-cache
  durations are equivalent or the configured path is never populated.
