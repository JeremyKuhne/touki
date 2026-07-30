# Model GitHub Actions cost

Use current official billing rules and observed job durations. Do not preserve a
copied price table as timeless policy: runner SKUs and rates change.

## Sources

Record the access date and use GitHub's current documentation:

- [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing)
  for standard and larger runner rates and billing granularity;
- [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions)
  for plan allowances, public-repository treatment, and artifact/cache storage;
- [GitHub-hosted runners](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)
  for visibility-specific CPU, memory, architecture, and image specifications;
- [Actions usage metrics](https://docs.github.com/en/enterprise-cloud@latest/organizations/collaborating-with-groups-in-organizations/viewing-usage-metrics-for-github-actions)
  for organization-level job and workflow evidence.

At the time of analysis, verify rather than assume these rules:

- whether each job's partial minute is rounded up independently;
- whether the runner is standard, larger, or self-hosted;
- whether repository visibility makes standard hosted minutes free;
- whether plan allowances make the next minute non-billable but still consume a
  finite quota;
- current artifact, cache, and custom-image storage treatment.

## Two cost views

Always distinguish:

1. **Actual invoiced cost.** What the owner is expected to pay under current
   visibility, plan allowance, budgets, runner class, and storage usage.
2. **Normalized list-price cost.** A synthetic rate-weighted measure: observed
  job duration multiplied by current published runner rates before
  public-repository or plan allowances.

For a public repository on standard hosted runners, actual minute cost may be
zero while normalized cost is nonzero. Report both. Normalized cost makes runner
choices comparable, exposes resource use, and remains useful if visibility or
billing policy changes.

Runner labels do not always identify equivalent hardware across visibility. For
example, GitHub can assign more CPU and memory to a standard public
`ubuntu-latest` or `windows-latest` job than to the same label in a private
repository. In that case:

- call the normalized result a **synthetic rate-only comparison**;
- record both hardware specifications beside the rate;
- do not present the number as what making the repository private would cost;
- forecast a visibility change only from durations measured on equivalent paid
  hardware, or report a runtime range that accounts for the hardware change.

Comparing alternatives measured on the same public runner remains useful, but
the rate-weighted result ranks their observed usage rather than predicting a
different runner's elapsed time.

Do not label a self-hosted runner free. GitHub may not charge hosted-runner
minutes, but infrastructure, maintenance, scaling, and incident response still
have a cost. Model those separately when they matter.

## Job-level calculation

When current GitHub policy rounds each job up to a whole minute:

```text
billedMinutesPerJob = ceiling(observedRunnerSeconds / 60)
normalizedJobCost = invocations * billedMinutesPerJob * currentRunnerRate
```

Apply the rounding to every matrix leg and aggregate job independently. Do not
round the sum of raw seconds once at workflow level. This is why six 15-second
jobs can cost six billed minutes while one sequential 90-second job costs two.

Use runtime after a runner starts; queue time affects feedback latency but is not
runner execution. Setup, checkout, restore, and cleanup run on the runner and do
count. Canceled and failed jobs consume the time used before they stop.

If GitHub changes billing granularity, replace the `ceiling` function with the
current rule and cite it in the report.

## Lifecycle calculation

Calculate at least three scopes:

```text
perPullRequest = sum(PR update and rerun job costs)
perMergedChange = perPullRequest + merge-queue + default-branch + deployment jobs
monthlyPeriodic = scheduled frequency * cost per scheduled run
```

State invocation assumptions explicitly. Useful variables include:

- average pull-request updates before merge;
- percentage of pull requests merged;
- bot/dependency pull-request volume;
- flaky rerun rate;
- cancellation delay for superseded runs;
- releases and manual full-matrix runs per month.

Report both a typical estimate (median durations and ordinary update count) and a
conservative estimate (high-percentile durations plus observed reruns). Do not
claim precision beyond the quality of the history.

## Storage calculation

Runner minutes and retained storage are separate. Inventory:

- uploaded artifacts and their `retention-days`;
- repository-level log/artifact retention;
- cache size, key cardinality, churn, and eviction;
- custom images used by larger runners;
- package storage if the workflow publishes artifacts externally.

GitHub bills applicable storage over time, commonly in GB-hours converted to
GB-months. Deleting an artifact stops future accrual but does not erase storage
already accrued. Use the current billing page for allowances and rates.

Caching is an optimization only when its time savings exceed restore/upload
overhead and storage pressure. Compare cache-hit, cache-miss, and no-cache runs.
Key caches on dependency inputs and runner compatibility; a cache pointed at a
different directory than the tool uses has zero benefit.

## Reporting table

Use one row per expanded job:

| Workflow / job | Trigger | Runner / hardware / SKU | Runs per scope | p50 / p75 | Billed min | Rate | Normalized cost |
| -------------- | ------- | ----------------------- | -------------- | --------- | ---------- | ---- | --------------- |
| Example | PR | current label and specs | measured | measured | calculated | dated source | calculated |

Then summarize:

| Scope | Current actual | Current normalized | Proposed actual | Proposed normalized | Change |
| ----- | -------------- | ------------------ | --------------- | ------------------- | ------ |
| Pull request | | | | | |
| Merged change | | | | | |
| Monthly periodic | | | | | |

Calculate reduction as:

```text
reductionPercent = (currentNormalized - proposedNormalized) / currentNormalized * 100
```

If current normalized cost is zero, report the absolute duration/job reduction
instead of an undefined percentage.

## Sanity checks

- The sum includes every matrix leg and every tiny aggregate job.
- PR and default-branch triggers are modeled separately.
- Public-repository free usage is not presented as zero resource consumption.
- Visibility-specific runner hardware is recorded; a synthetic public-duration
  by private-rate number is not presented as a private-repository forecast.
- Larger runners are not assumed free for public repositories.
- Canceled, failed, scheduled, release, and manual runs are either modeled or
  explicitly excluded.
- Rates have a source and access date.
- The proposed estimate uses the same assumptions as the baseline.
