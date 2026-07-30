# Optimize GitHub Actions safely

Apply changes in this order. Earlier steps remove unnecessary work and usually
carry less validation risk than deleting matrix dimensions.

## 1. Stop runs that should not start

- Cancel superseded pull-request runs with a concurrency key stable for that
  pull request. A run id is unique and therefore cannot cancel anything.
- Remove the default-branch `push` trigger only when rulesets prove every path to
  the branch validates an equivalent merge candidate. Keep it when direct or
  bypass pushes remain possible.
- Avoid overlapping branch, tag, and release workflows.
- Use path filters for optional workflows whose absence cannot block a required
  status. For required checks, ensure the context is always emitted.
- Group dependency updates when repository policy permits so one compatible
  batch pays one CI lifecycle instead of many.
- Add `merge_group` when a merge queue requires testing the synthetic merge
  candidate; do not count a PR-head test as equivalent.

## 2. Remove repeated setup and rounding overhead

Every job pays for runner startup, checkout, SDK/tool setup, restore, and current
job-level billing granularity. Consolidate sequential work that needs the same
runner and artifacts when the saved setup and rounded minutes outweigh the lost
parallelism.

Good consolidation candidates include build plus tests, multiple target
framework test invocations on one compatible host, and performance gates that
can reuse a verified Release build. Preserve separate jobs when they provide
material wall-clock savings, isolation, permissions boundaries, or genuinely
different runners.

Do not split tiny script checks into many jobs merely for presentation. A cheap
aggregate required check should be one job when one stable context is enough.

## 3. Choose the cheapest compatible runner

- Use a slim one-core Linux runner for short scripts and aggregate status jobs
  when its toolset and speed are sufficient.
- Prefer standard Linux for portable build/test work.
- Consider Linux ARM64 only after restore, toolchain, native dependency, and test
  compatibility are demonstrated. Architecture substitution is a test-coverage
  decision as well as a price decision.
- Keep Windows or macOS where native APIs, legacy frameworks, packaging, signing,
  filesystem semantics, or supported-platform behavior requires them.
- Compare larger runners by total job cost, not minute rate alone. A faster,
  more expensive runner can be cheaper if measured runtime falls enough.

Cross-compiling proves compilation for a target; it does not replace executing
native behavior on that target.

## 4. Make the automatic matrix load-bearing

Run Release tests automatically because Release code generation is what ships.
Keep Debug automatic only when it detects a distinct class of regressions that
the project has chosen to gate.

For operating-system, architecture, target-framework, runtime-identifier, AOT,
and SDK matrices:

1. Map every leg to a stated invariant.
2. Keep merge-blocking legs for common or high-risk behavior.
3. Move expensive breadth to a named scheduled/manual workflow or a release gate
   only when delayed detection is acceptable.
4. Give periodic breadth a cadence and owner so it cannot silently rot.

Do not call a reduced automatic matrix "equivalent" to the full matrix. Report
what remains automatic and what becomes delayed.

## 5. Fix restore and cache behavior

- Restore once per job and use `--no-restore` or the tool's equivalent only when
  the following command truly supports it.
- Point the cache at the actual configured package directory. If an environment
  variable redirects that directory, cache the redirected path.
- Include operating system, architecture, lock files, SDK/tool versions, and
  other compatibility inputs in the key when they affect cache validity.
- Use restore keys only when a partial match is safe.
- Remove caches whose hit-rate or measured savings do not justify upload,
  download, and storage churn.

Never share build output across jobs without proving it cannot become stale or
mix configurations. Rebuilding is cheaper than trusting the wrong binary.

## 6. Control artifacts and logs

- Upload only outputs needed by downstream jobs, diagnostics, or release.
- Set the shortest practical `retention-days` on large transient artifacts.
- Avoid uploading duplicate binaries from every matrix leg.
- Keep test logs and failure diagnostics long enough to debug ordinary failures.
- Review repository retention and cache limits as remote settings; changing them
  requires approval.

## 7. Treat security scans as security decisions

Moving CodeQL or another scanner from every pull request to a schedule lowers
runner cost but delays detection. Before doing so, record:

- whether the repository handles untrusted input or sensitive data;
- whether language/compiler analyzers cover part of the same risk;
- whether branch protection currently requires the scan;
- scheduled cadence, alert ownership, and response expectations;
- whether release gates need a fresh scan.

Use a security-review workflow when this decision is material. Never run
untrusted pull-request code with write permissions or repository secrets to save
the setup cost of a separate trusted job.

## 8. Preserve required-check behavior

When branch protection depends on historical check names, a temporary aggregate
job can preserve them while the underlying topology changes. The aggregate must:

- use `if: always()` so it runs after failed, canceled, or skipped dependencies;
- inspect every dependency result;
- succeed only when all required dependencies succeeded;
- use a cheap compatible runner and a short timeout.

An aggregate that runs only after success can be skipped on failure and leave an
ambiguous required context. An aggregate that accepts `skipped` without a
deliberate policy can turn missing validation green.

Update or remove legacy contexts only with explicit approval to change the
remote ruleset. Required-check changes and workflow changes should be sequenced
so merges are never accidentally unguarded.

## 9. Validate in layers

After each coherent edit:

1. Run an Actions-aware linter and YAML/editor diagnostics.
2. Run whitespace and repository policy checks.
3. Execute the local build, Release tests, and any perf/AOT/package commands the
   changed automatic jobs will use.
4. Confirm cache paths and generated artifact locations statically.
5. Push only with approval and observe one hosted run on every retained native
   platform and architecture.
6. Confirm required contexts appear and fail closed for a deliberately failed or
   canceled dependency when practical.
7. Replace estimated durations with hosted measurements and recalculate cost.

## Common traps

- A path-filtered required workflow never starts and leaves the PR pending.
- A concurrency group includes a unique run id, so no prior run is canceled.
- A cache saves the default directory while the build uses a redirected one.
- A shallow checkout changes git-derived versioning such as MinVer.
- `--no-build` reuses output from a different configuration or target.
- A manual full matrix has no cadence or owner and quietly stops being useful.
- Removing post-merge CI ignores an administrator or automation bypass path.
- Replacing native execution with cross-compilation drops behavior coverage.
- Many tiny jobs look parallel but multiply setup and rounded billing minutes.
