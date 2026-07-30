# Reproducible performance investigations

Detail for the [performance-testing](SKILL.md) skill. Use this workflow when a
performance question spans multiple phases, compares an external implementation,
iterates through several candidate optimizations, or must remain reproducible from
a dirty worktree.

For a simple A/B benchmark over one pure operation, use [authoring.md](authoring.md),
[running.md](running.md), and [interpreting-results.md](interpreting-results.md)
directly. Do not add phase harnesses or source bundles when the ordinary benchmark
already answers the question.

The expected outputs are a trustworthy benchmark or profile, a compact experiment
ledger, and enough source and run provenance to reconstruct any result that is kept.
Creating those local artifacts does not authorize committing, uploading, or
publishing them.

## Measure phases with fresh state

A mutable or consumable intermediate representation cannot be reused across
measurements. Reuse can make a later phase observe already-mutated state, shared
arrays, warmed caches, or an object graph that no longer represents a fresh
operation.

For phase latency:

1. Prepare a bounded batch of fresh inputs in `[IterationSetup]`.
2. Consume every item exactly once in the `[Benchmark]` method.
3. Set `OperationsPerInvoke` to the number of items consumed so BenchmarkDotNet
   reports per-operation time and allocation.
4. Release the batch in `[IterationCleanup]` so one iteration's live set does not
   leak into the next.
5. Run separate configurations for one item, an intermediate batch, and a larger
   batch. Keep `OperationsPerInvoke` accurate for each configuration.

The one-item run exposes timer and harness overhead. The larger run exposes
retained-live-set and GC distortion. Keep the smallest batch that amortizes the
harness without changing the workload's allocation or latency shape.

A consumable-state benchmark may need one invocation per iteration. BenchmarkDotNet
can then warn that the minimum iteration time was not reached. Document why the
invocation count must remain one; do not silence the warning by consuming the same
state repeatedly.

### Use a different harness for CPU profiling

The one-shot measurement harness is often too sparse for a useful periodic CPU
profile. Prefer an adaptive end-to-end benchmark that BenchmarkDotNet can repeat,
then use the repository's trace analyzer to scope the profile to the phase method.
Work before that phase call remains outside the selected subtree while the repeated
end-to-end operation supplies enough observations.

Profile the one-shot harness only when the selected phase query has enough
contributing records under the analyzer's sample-quality contract. A whole-trace
sample count does not establish quality for a narrow phase.

Before trusting a decomposition, compare it with an independently measured
end-to-end operation:

- phase allocations should add to the end-to-end allocation at the report's
  precision;
- phase means should approximately add to the end-to-end mean;
- each phase should receive fresh, semantically equivalent state.

A large gap usually means setup leaked into a measurement, state was reused, or
the batch changed GC and live-set behavior.

## Keep an experiment ledger

Start the ledger before the first edit and add one row per candidate. Record
rejected variants as carefully as retained ones.

| Hypothesis | Small edit | Discriminating check | Time | Allocation | Target frame | Decision |
| --- | --- | --- | ---: | ---: | --- | --- |
| Example claim | One-variable change | Same filtered benchmark | Result | Result | Before -> after | Keep or reject, with reason |

Change one material variable at a time. Use the same scenario, filter, target
framework, job, and profiler scope before and after. Preserve both target
frameworks when the production code serves both. A rejection is useful evidence:
it prevents a later investigation from retrying an attractive idea that already
lost on throughput, allocation, another runtime, or the intended target frame.

The final report should explain why the retained implementation beat at least the
most plausible alternative, not merely state that the retained row was faster than
the original baseline.

## Compare an exact-source oracle

When the baseline lives in another repository or revision, compare against exact
source rather than an unpinned package or a mutable checkout:

1. Create a clean detached checkout at an exact commit SHA.
2. Build it in Release with the subject repository's pinned SDK. Record the SDK
   version and any compatibility override.
3. Verify the built assembly's informational version and configuration when that
   metadata is available, and retain its SHA-256 hash.
4. Isolate namespace and type collisions with an `extern alias` rather than
   renaming either implementation.
5. Make the oracle reference opt-in. Set an environment variable whose name is
   also an optional MSBuild property before launching BenchmarkDotNet; the
   generated child build inherits the environment, while an outer `/p:` argument
   alone may not reach that child build.
6. Include the oracle reference and benchmark methods only when that property is
   present. An ordinary build must contain neither.
7. Validate semantic parity before measuring: equivalent inputs, outputs,
   exceptions, options, and fresh mutable state for every operation.
8. Remove the temporary checkout after the retained artifacts record its commit
   and assembly hash.

Do not assume a parsed model or decoded object graph can be reused safely. Prove
that repeated materialization is independent, or reconstruct the intermediate
state per operation. Shared mutable arrays or caches can make an oracle appear
faster while changing the work being measured.

As a build check, inspect the generated BenchmarkDotNet child project for an
opt-in run and confirm that it contains the exact oracle reference. Then build
without the environment property and confirm that no oracle reference or
oracle-only benchmark method remains.

## Preserve a reconstructable run

Give every retained run a unique directory or output stem, for example:

```text
<subject>-<phase>-<variant>-<tfm>-<job>-<timestamp>
```

Retain the compact report and raw result, exact command line, non-secret
environment settings, runtime and JIT identity, OS and architecture, base commit,
clean/dirty state, experiment-ledger row, and any trace manifest. Keep separate
run directories so a later BenchmarkDotNet invocation cannot overwrite the
baseline used in a claim.

### Dirty-source bundle contract

A commit SHA plus hashes is insufficient when tracked, untracked, binary, or
ignored inputs affected the run. For a dirty worktree, retain one of these:

- a complete source snapshot; or
- a base commit plus all of the following reconstruction artifacts.

The reconstruction artifacts are:

1. A binary-capable full-index patch of every tracked difference from the recorded
   base commit, including staged and unstaged changes. Store that commit in
   `$baseCommit`, then use `git diff --binary --full-index $baseCommit --` so the
   patch and restore point cannot disagree.
2. An archive containing the bytes of every relevant untracked or ignored source,
   generated input, and benchmark data file, stored at its repository-relative
   path. Build the archive from an explicit allowlist after reviewing each file
   for credentials, signing material, personal data, proprietary inputs, and
   unrelated local configuration. Include required file metadata when it affects
   the build or run.
3. A manifest containing the base commit, repository-relative path, tracked /
   untracked / ignored classification, byte length, and SHA-256 hash for every
   archived input, plus hashes of the patch and archive themselves. Record who or
   what performed the allowlist review and list any non-secret external
   provisioning requirements.
4. The exact restore procedure: detach the base commit, apply the binary patch,
   extract the archive at the repository root, and verify the manifest hashes.

Hashes prove integrity; they do not replace missing content. Never archive a file
merely because it is ignored or untracked. Do not archive credentials, signing
material, package caches, unrelated build output, or data whose redistribution is
not authorized. When a secret influences setup, record a non-secret provisioning
requirement rather than the secret. If the run cannot be reconstructed without
retaining sensitive bytes, do not publish or share the bundle; keep the result
local or redesign the input so a safe equivalent can be retained.

For a result that will support a durable claim, test the restore procedure in a
temporary clean checkout before discarding the original worktree. The restored
checkout must reproduce the recorded source hashes and include every build or
benchmark input that was not obtainable from the base commit.

## Acceptance checks

The workflow is complete when all applicable checks pass:

- A consumable parse/materialize split uses fresh-state batches for measurement
  and an adaptive end-to-end path for profiling unless the one-shot phase has
  enough query-level evidence.
- The ledger preserves rejected variants and explains the final decision.
- An opt-in BenchmarkDotNet child build references the assembly built from the
   recorded oracle commit and hash, semantic-parity checks pass on fresh state, and
   an ordinary build has no oracle surface.
- A clean checkout plus the retained dirty-source bundle reconstructs and verifies
   every source and input byte needed for the run; its explicit allowlist contains
   no secret, unauthorized, or unrelated content.
- The retained command, non-secret environment, runtime/JIT, OS/architecture,
   target framework, job, and profiler scope identify the execution environment
   closely enough to rerun the same experiment.
