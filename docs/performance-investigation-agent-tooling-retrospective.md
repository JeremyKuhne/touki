# Performance investigation agent/tooling retrospective

This document reflects on the July 2026 `BinaryFormattedObject` performance
investigation and identifies changes that would help a future coding agent reach
trustworthy results faster. It covers the repository's performance-testing guidance,
the vendored filtrace skill and capture scripts, and the filtrace analyzer itself.

The investigation succeeded: it established reproducible cross-runtime baselines,
separated decoding from materialization, used profiles to select bounded changes,
rejected regressions, and produced an upstream API proposal. The recommendations below
focus on places where the agent had to discover missing workflow rules or compensate for
tool behavior manually.

Filtrace release [0.6.0](https://github.com/JeremyKuhne/filtrace/releases/tag/v0.6.0)
delivered the analyzer, MCP, capture-script, and tool-shipped-skill work tracked by
[JeremyKuhne/filtrace#42](https://github.com/JeremyKuhne/filtrace/issues/42), which
is closed as completed. Touki now consumes release
[0.6.1](https://github.com/JeremyKuhne/filtrace/releases/tag/v0.6.1), which hardens
that workflow for BenchmarkDotNet 0.16 captures. The portable performance-workflow
guidance is still being validated in Touki's local performance-testing overlay before
it is upleveled to `JeremyKuhne/agent-skills`.

## Executive summary

Filtrace 0.6.0 delivered:

1. Explicit BenchmarkDotNet/root selection with ambiguity diagnostics.
2. Query-specific contributing-record counts and thin-periodic-sample warnings.
3. Concurrent same-trace ETLX conversion across MCP calls and processes.
4. Separate format support, provider enablement, and observed event counts.
5. Source/PDB resolution separate from managed frame-name resolution.
6. Isolated BenchmarkDotNet captures with every case, its exact child-build symbols,
   and machine-readable manifests.
7. Normalized manifest-aware diff and compact batch analysis.

Separately, Touki's local performance-testing overlay now distinguishes one-shot phase
measurement from adaptive phase profiling and requires an experiment ledger. Those are
workflow guidance, not filtrace product features. The current workflow can produce correct
answers without the manual filtrace workarounds this investigation needed. The remaining
work is to uplevel the local guidance, exact-source comparison, and reconstructable
dirty-source provenance to `agent-skills`.

## What worked well

Several parts of the existing guidance materially improved the investigation and should
remain load-bearing:

- `trace_info` was called before rankings, and the 0.8 managed-symbol threshold prevented
  trusting traces with unresolved frame names.
- Self-time, inclusive-time, callers, and source-line views were treated as different
  questions rather than interchangeable rankings.
- The before/after loop required the targeted frame to move, not just the benchmark mean.
  This caught successful dictionary/stack changes and rejected a pooled-stack experiment.
- Performance conclusions named the runtime and JIT. The modern .NET and .NET Framework
  results diverged enough that a single-runtime conclusion would have been wrong.
- Allocation was a first-class result. Eager capacity reservation and realistic
  `PopulateObjectMembers` batching were rejected because their allocation cost outweighed
  or erased throughput gains.
- Exact-source comparison used a clean detached upstream checkout, Release configuration,
  the repository's pinned SDK, assembly provenance checks, and a separate stream over the
  same bytes.
- Mutable one-shot semantics were validated. Parsed NRBF graphs were materialized exactly
  once, preventing a fast but invalid benchmark that reused mutable record state.

The retrospective should extend these practices rather than replace them.

## Historical friction addressed by filtrace 0.6.0

The subsections below record what happened during the 0.4 investigation. They are retained
as rationale and regression context; they are not current 0.6 limitations.

### One-shot measurement was a poor profiling harness

The materialization-only benchmark correctly used `IterationSetup` to parse fresh record
graphs outside the measured region. BenchmarkDotNet consequently forced one invocation
per iteration. A one-object batch hit timer quantization; a 256-object batch retained
15-25 MiB of decoded state and distorted tree/cycle timing; a 64-object batch was the
best measurement compromise.

That compromise still produced sparse EventPipe profiles. A 512-object dedicated field
benchmark returned `scopeWeight: 32` inside
`ClassRecordFieldInfoDeserializer.Continue`; for this CPU `.nettrace`, unit sample weights
made that equivalent to 32 sampled ticks. The global trace held more than 6,000 samples,
so `trace_info` looked healthy while the selected scope was statistically thin.

For profiling, the better harness already existed: run the adaptive end-to-end benchmark
and root-scope analysis to `BinaryFormattedObject.Deserialize`. Parsing remains outside
the selected call subtree, while BenchmarkDotNet can execute enough operations to produce
a dense phase profile without retaining a large batch of parsed graphs.

### Parameterized captures produced multiple traces without a manifest

One `BinaryFormattedObjectPerf` capture produced six `.nettrace` files, one per scenario.
The capture script selected and printed commands for only the newest trace. The agent had
to enumerate files, parse scenario names from filenames, and manually pair before/after
captures.

This is easy to get wrong when another capture exists in the artifacts directory or when
two runs overlap. It also makes automated scenario-by-scenario comparison unnecessarily
expensive.

### The printed symbol path did not identify the traced build

The capture script derives symbols as `<project>/bin/Release/<tfm>`. This repository sets
custom `BaseOutputPath` and `OutputPath` values, and BenchmarkDotNet profiles a generated
child build under a path such as:

```text
artifacts/x64/Release/touki.perf/net10.0/
  touki.perf-DefaultJob-1/bin/Release/net10.0
```

Passing the outer perf output allowed method names to resolve but returned `<no source>`
for Touki lines. Passing the preserved child output resolved exact file/line locations.
The aggregate symbol-resolution rate remained `1.0` in both cases because it measured
managed frame names, not portable-PDB source mapping. The agent therefore had a green
quality signal while line attribution was unusable.

### `trace_info` advertised allocation analysis without allocation events

The raw BenchmarkDotNet EventPipe traces were `.nettrace` files, so filtrace 0.4
`trace_info` listed
`alloc` under `availableAnalyses`. `trace_rank(metric: alloc)` then reported that no
allocation events existed and asked whether allocation sampling had been enabled. The
tool did not distinguish an enabled provider that observed zero events from a provider
that was not enabled.

The 0.4 capture script similarly stated that a raw EventPipe trace carried allocation
events and printed an `alloc` command unconditionally. File format alone does not
establish that the required provider/events were captured. The then-vendored 0.4 core
skill made the same unconditional claim for EventPipe allocation and exception data.

### Parallel MCP reads raced on the ETLX cache

Parallel `trace_info`, `trace_rank`, and `trace_lines` calls against the same `.nettrace`
failed with combinations of:

```text
Could not find ...nettrace.etlx.new
The process cannot access ...nettrace.etlx.new because it is being used
Access to ...nettrace.etlx.new is denied
```

Calls against different traces were safe. Retrying sequentially succeeded. This is a
product correctness issue because agent guidance generally encourages parallel read-only
tool calls, and the MCP surface does not communicate that conversion is a single-writer
operation.

### Whole-trace quality hid low-quality scopes

Rankings over narrow scopes returned `scopeWeight` values of 2, 10, 20, or 32 and precise
percentages without a quality warning. For these CPU `.nettrace` inputs, the unit-weight
samples made those values numerically equal to sampled ticks, but that is not the filtrace
contract: `scopeWeight` is metric weight, and evented speedscope inputs use
event-duration deltas.
The 0.4 tool exposed no distinct scoped sample count. The skill documented rough count
thresholds (`>=200-300` samples for a method percentage, `>=1,000` for useful line
distribution), but neither the agent nor the tool can apply them generically from
`scopeWeight`.

The agent compensated by building wider benchmarks and confirming hypotheses with
BenchmarkDotNet microbenchmarks. A tool warning would have prompted that escalation
immediately.

### Before/after trace comparison was manual

The investigation recaptured tree and cycle traces after each retained optimization, then
manually compared percent-of-scope rows to verify that `Dictionary.Resize`, stack growth,
and duplicate type resolution moved. Raw sample counts differed between captures, so
absolute-weight comparison would have been misleading.

A normalized, root-scoped diff would have made this both faster and less error-prone.

### Benchmark output and artifacts needed manual retention

Long adaptive runs produced large terminal output, were sometimes moved to background,
and reused BenchmarkDotNet's standard result filenames. The investigation used explicit
`Tee-Object` files to preserve each matrix. Without that discipline, a later run would
replace the compact report needed for documentation.

The generic skill correctly says to read artifacts rather than scrollback, but it does not
provide a standard run identifier or result manifest for a multi-run investigation.

## Skill changes and remaining agent-skills work

### 1. Separate measurement harnesses from profiling harnesses

**Owner:** Touki-owned
[`profiling.md`](../.agents/skills/performance-testing/profiling.md) immediately;
propose a portable version upstream to `JeremyKuhne/agent-skills` and re-vendor rather
than editing the pinned performance-testing core.

The local "phase measurement versus phase profiling" section requires:

- For a mutable or consumable intermediate representation, measure phase latency with
  fresh state in `IterationSetup`, a bounded batch, `OperationsPerInvoke`, and one
  consumption per item.
- Sweep at least three batch sizes: one item to expose timer overhead, an intermediate
  batch, and a larger batch to expose retained-live-set distortion.
- Treat BenchmarkDotNet's minimum-iteration warning as expected only after documenting why
  invocation count must remain one.
- For CPU profiling, prefer an adaptive end-to-end benchmark and root-scope the trace to
  the phase method. Do not profile the one-shot `IterationSetup` benchmark unless the
  selected scope has enough samples.
- Validate phase decomposition by checking that phase allocations add to end-to-end
  allocation and that phase means approximately add to end-to-end latency.

**Acceptance check:** a future agent facing a parse/materialize split should choose the
one-shot batch for measurement and the adaptive end-to-end path for profiling without
first trying 1/256-item profiling batches.

**Status:** implemented in Touki's owned profiling page; candidate for promotion to
`agent-skills`.

### 2. Make scoped sample sufficiency a mandatory gate

**Owner:** delivered in filtrace 0.6.0 and its tool-shipped skill; Touki's profiling page
consumes the result.

Use query-specific `contributingRecordCount` (or line-level attributed/unattributed
counts), never `trace_info.sampleCount`, self-percent, or `scopeWeight`. When capture
metadata establishes periodic CPU sampling semantics, apply these gates:

- `<200` CPU samples: directional hypothesis only; no percentage claim.
- `200-999`: method ranking may be usable, but line claims need escalation.
- `>=1,000`: line attribution may be useful if source resolution is healthy.

The skill should explicitly say that `trace_info.sampleCount` is a whole-trace number and
cannot establish quality for a narrow root or method. The required count depends on the
query: records surviving root/process/activity/time filters for rank, records containing
the focus frame for callers, and records actually attributed to the requested methods and
source locations for lines.

**Acceptance check:** a 32-sample field-assignment scope from a periodic CPU profile should
automatically trigger a larger/adaptive harness or ETW recommendation. Evented profiles do
not use these gates.

**Status:** delivered in filtrace 0.6.0 (#44).

### 3. Correct provider-dependent EventPipe wording

**Owner:** filtrace core skill, vendored into Touki with release 0.6.0.

Change statements that EventPipe "carries" analysis data to say that `.nettrace` can
carry CPU, allocation, exception, GC, JIT, contention, wait, activity, and thread-pool
events when the recorder enables each analysis's required providers/keywords. Route based
on capture metadata and observed events, not the extension.

**Acceptance check:** manual and BenchmarkDotNet capture guidance no longer promises an
allocation ranking merely because the output is `.nettrace`.

**Status:** delivered in filtrace 0.6.0 (#46 and the shipped skill).

### 4. Allow parallel same-trace MCP analysis

**Owner:** filtrace core and tool-shipped skill.

Coordinate ETLX conversion by canonical path across threads and processes, publish the
cache atomically, and report cache state rather than requiring consumer serialization.

**Acceptance check:** an agent may run `trace_info`, `trace_rank`, and `trace_lines`
concurrently against one trace without sidecar races.

**Status:** delivered in filtrace 0.6.0 (#45). The temporary sequential-call workaround
has been removed from Touki guidance.

### 5. Add an experiment ledger to the performance workflow

**Owner:** Touki-owned `docs/performance-investigation.md` immediately; propose the
portable workflow upstream to `JeremyKuhne/agent-skills` and re-vendor it.

The local workflow requires a compact ledger for each experiment:

| Hypothesis | Small edit | Discriminating check | Time | Allocation | Target frame | Decision |
| --- | --- | --- | ---: | ---: | --- | --- |

Record rejected variants as well as retained changes. This investigation rejected eager
dictionary reservation, pooled parser storage, and realistic batched field assignment.
Keeping those results prevented revisiting attractive but losing ideas and made the final
document more defensible.

**Acceptance check:** every optimization pass leaves enough evidence to explain why the
chosen implementation beat at least the most plausible alternative.

**Status:** implemented in Touki's owned profiling page; candidate for promotion to
`agent-skills`.

### 6. Add an exact-source oracle recipe

**Owner:** Touki-owned performance investigation documentation immediately; propose the
portable recipe upstream to `JeremyKuhne/agent-skills` and re-vendor it instead of
editing pinned authoring/running pages.

For comparisons against another repository:

- use a clean detached checkout at an exact SHA;
- build Release with the subject repository's pinned SDK;
- verify assembly informational version/configuration where available;
- isolate namespace collisions with `extern alias`;
- pass optional references to BenchmarkDotNet child builds through an environment-backed
  MSBuild property, not only an outer `/p:` argument;
- validate semantic parity and fresh mutable state before measuring;
- remove the temporary checkout afterward.

**Acceptance check:** BenchmarkDotNet's generated child project must include the oracle in
opt-in runs and ordinary builds must contain no oracle reference or benchmark methods.

**Status:** implemented by this investigation's runner and retained as a portable
`agent-skills` candidate; it is not a filtrace feature.

### 7. Standardize run IDs and artifact retention

**Owner:** Touki-owned performance investigation documentation immediately; upstream
filtrace capture scripts for automation; upstream `agent-skills` for portable running
guidance.

Recommend a unique artifact directory or output stem containing:

```text
<subject>-<phase>-<variant>-<tfm>-<job>-<timestamp>
```

A run should retain the compact report, command line, runtime banner, commit SHA, clean or
dirty status, and trace manifest. For a dirty worktree, retain a reconstructable run-source
bundle: a binary-capable tracked-file patch plus an archive containing relevant untracked
and ignored input content and its manifest, or an equivalent complete source snapshot.
Hashes establish integrity but do not replace the content. Avoid chaining several long
adaptive runs in one persistent terminal; run one TFM at a time and wait for completion
before scheduling the next command.

**Acceptance check:** a later benchmark cannot silently overwrite the baseline report used
for before/after or documentation. A clean checkout at the recorded commit plus the
run-source bundle reconstructs the hashed source inputs, including untracked and binary
files, for a dirty experimental run.

**Status:** filtrace 0.6.0 capture manifests deliver isolated run IDs, logs, traces, exact
child symbols, runtime/source identity, and optional operation metadata (#48/#49). The
reconstructable dirty-source bundle remains a portable `agent-skills` candidate.

## Capture-script changes delivered in filtrace 0.6.0

These requirements are implemented in filtrace's bundled
[`Capture-BenchmarkTrace.ps1`](../.agents/skills/filtrace/scripts/Capture-BenchmarkTrace.ps1),
first delivered in release 0.6.0 and retained in the current vendored helper. Keep
the acceptance checks as regression contracts.

### 1. Isolated runs and complete case manifests

The helper generates a unique run ID, isolates its log and BenchmarkDotNet artifacts, and
holds a same-project/same-TFM capture lock around the shared project output/intermediate
paths. The generated-child files live under the isolated BenchmarkDotNet artifacts path.
It enumerates only that run directory and emits JSON as well as concise console output.
This abridged example matches manifest schema v1:

```json
{
  "schemaVersion": 1,
  "runId": "20260712-012345-5d9f1234",
  "startedUtc": "2026-07-12T01:23:45.0000000+00:00",
  "completedUtc": "2026-07-12T01:24:45.0000000+00:00",
  "command": {
    "executable": "dotnet",
    "arguments": ["run", "-c", "Release", "..."]
  },
  "project": "touki.perf/touki.perf.csproj",
  "tfm": "net10.0",
  "filter": "*BinaryFormattedObjectPerf*",
  "profiler": "EP",
  "source": {
    "repository": ".../touki-binary-formatted-object",
    "commit": "0123456789abcdef0123456789abcdef01234567"
  },
  "paths": {
    "runDirectory": ".../BenchmarkDotNet.Artifacts/filtrace-runs/20260712-012345-5d9f1234",
    "artifactsDirectory": ".../filtrace-runs/20260712-012345-5d9f1234/artifacts",
    "log": ".../filtrace-runs/20260712-012345-5d9f1234/capture.log"
  },
  "cases": [
    {
      "benchmark": "BinaryFormattedObject_DeserializeRecords",
      "parameters": "Scenario=ObjectTree_127",
      "trace": "...ObjectTree_127....nettrace",
      "speedscope": "...ObjectTree_127....speedscope.json",
      "symbolsDirectory": "...touki.perf-DefaultJob-1/bin/Release/net10.0",
      "analyses": {},
      "commands": [],
      "warnings": []
    }
  ]
}
```

Do not select only the globally newest trace. Include every parameterized case and its
paired files.

**Regression check:** one six-scenario capture returns six case entries. Stale traces are
never selected. Two simultaneous same-project/same-TFM captures either write disjoint
logs, artifacts, outer/child outputs, intermediates, binaries, and PDBs without
cross-contamination, or the second fails immediately with a clear capture-lock message.

### 2. Exact BenchmarkDotNet child output

The helper preserves generated files for EventPipe and ETW, discovers child output
candidates from the isolated run, and asks filtrace to select the directory whose
module/PDB identity matches the trace. It respects custom output paths, architecture,
BenchmarkDotNet artifacts directories, and job names.

**Regression check:** the printed `filtrace lines` command resolves Touki file/line data in
this repository without the user replacing `<project>/bin/Release/<tfm>` manually.

### 3. Provider-aware next-step commands

After capture, the helper inspects the trace and prints analysis commands only when capture
metadata says the required provider was enabled. An enabled provider with zero observed
events supports a valid empty analysis; disabled is unavailable; unknown remains unknown
rather than being inferred from the extension or a zero event count.

**Regression check:** test one trace with allocation capture enabled and zero allocations,
one with allocation capture enabled and events, one with allocation capture disabled, and
one whose provider enablement cannot be determined. The first two print a valid `alloc`
command; the disabled trace explains how to recapture; the unknown trace reports that
availability cannot be established and does not present a command as known-valid.

### 4. Agent-friendly output mode

`-Format Text|Json` and `-Quiet` keep full BenchmarkDotNet output in the capture log while
JSON/stdout carries compact run status, manifest location, warnings, and next steps. This
avoids routing tens of kilobytes through agent context merely to learn the trace path.

**Regression check:** a successful parameterized capture keeps the agent-facing JSON
handoff under 20 KiB regardless of BenchmarkDotNet's iteration log size, while the
complete durable manifest remains available under its separate 16 MiB safety limit.

## Filtrace product changes delivered in 0.6.0

All product sections below shipped in release 0.6.0. Their acceptance checks remain useful
for future regression testing.

### P0: First-class BenchmarkDotNet scoping and root/frame diagnostics

Root-aware CLI and MCP queries accept either the BenchmarkDotNet workload preset
(`--benchmark` / `benchmark: true`) or an explicit frame root, never both. The preset
selects the generated `WorkloadAction` wrapper without requiring the caller to know its
name. Every substring-based root/frame selection reports:

- the number of matching frame definitions;
- selected frames with module and full signature;
- matches at multiple stack depths;
- the exact BenchmarkDotNet workload root selected;
- a warning when a substring also matches an activity/harness wrapper.

**Regression check:** an MCP caller can isolate measured workload without knowing generated
`WorkloadAction*` frame names. A benchmark-method substring that matches both an activity
wrapper and actual method emits ambiguity diagnostics and identifies the deterministic
outermost root or deepest focus selection, so the caller can narrow it before trusting the
result.

### P0: Query-specific scoped record counts and quality warnings

Results keep `scopeWeight` as metric weight and expose count fields whose contributing-
record population matches each query:

- rank: CPU records surviving all process/activity/time/root filters;
- callers: filtered CPU records containing the focus frame;
- lines/heatmap: filtered CPU records attributed to the requested method/file and source
  location, plus a separate unattributed count.

For periodic CPU sample profiles, warnings and hints use the requested
granularity:

- CPU method scope under 200 samples: directional only.
- CPU line scope under 1,000 samples: sparse; recommend adaptive/larger harness or ETW.
- Percentages from very small scopes should be marked low confidence in text and JSON.

Thresholds remain capture-aware because periodic sources use different sample rates.
For evented speedscope profiles, report contributing-record count separately but do not
apply periodic-sample thresholds: those records are duration intervals driven by stack
transitions, not independent samples. For formats where a meaningful record count or
compatible periodic-sampling semantics are unavailable, omit the count or thresholds.

**Regression check:** a CPU rank with 32 contributing records returns a low-sample warning
even though the whole periodic-sample trace has more than 6,000 samples. A weighted
evented speedscope scope with four contributing records and `scopeWeight: 32 ms` reports
record count `4`, not `32`, and does not apply the 200/1,000 periodic-sample gates. A
format/profile for which a contributing-record count cannot be defined reports the count
as unavailable and does not apply count thresholds.

### P0: Concurrency-safe ETLX conversion

**Observed failure:** parallel MCP calls against one trace raced over `.etlx.new`.

The core coordinates conversion by canonical trace path across threads and processes:

1. Check for a valid completed cache.
2. Acquire the per-trace conversion lock.
3. Recheck after acquiring it.
4. Convert to a uniquely named temporary file.
5. Atomically replace/move to the final cache.
6. Clean temporary files on failure.
7. Let waiters reuse the completed cache.

Transient file-not-found/access-denied conversion races no longer reach callers. Diagnostic
output includes cache state (`hit`, `waited`, `converted`, `recovered`).

**Regression check:** run `trace_info`, two `trace_rank` calls, and `trace_lines` concurrently
against a trace with no preexisting ETLX. All calls succeed and only one conversion occurs.
Also test two processes, cancellation during conversion, and a stale `.new` file.

### P0: Format support, capture enablement, and observed events

`trace_info.analyses` reports per-analysis state instead of inferring availability from
the trace extension. `availableAnalyses` separately lists what the loaded format supports.
For example:

```json
{
  "analyses": {
    "cpu": {
      "captureStatus": "enabled",
      "eventCount": 6673
    },
    "alloc": {
      "captureStatus": "disabled",
      "eventCount": null
    },
    "exceptions": {
      "captureStatus": "enabled",
      "eventCount": 0
    }
  }
}
```

`captureStatus` is `enabled`, `disabled`, or `unknown`. Zero events with enabled
capture means a valid empty analysis; disabled means unavailable; unknown must remain
unknown. Report the distinction whenever trace metadata permits it.

**Regression check:** cover enabled-zero, enabled-nonzero, disabled, and unknown provider
states. The investigation's EventPipe trace must not claim that allocation analysis is
available solely because its format is `.nettrace`.

### P0: Separate source-resolution quality

`trace_info.sourceResolution` reports portable-PDB/source quality independently of managed
frame-name resolution:

- modules with matching PDBs;
- modules with GUID/age mismatch;
- methods with sequence points;
- sampled managed frames mapped to source;
- sampled managed frames named but mapped to `<no source>`;
- symbol directories searched.

A `symbolResolutionRate` of `1.0` must not imply that `trace_lines` will work.

**Regression check:** passing the outer perf output reports full frame-name resolution but
poor Touki source resolution and identifies the missing/mismatched module. Passing the
BenchmarkDotNet child output reports usable Touki sequence points.

### P1: Normalized, scope-aware trace diff

`trace_diff` compares percent-of-scope and normalized capture weights in addition to raw
metric weight. Direct-trace inputs use consistent root or BenchmarkDotNet preset, process,
and metric scope and report:

- before/after scope weights;
- before/after percent of scope;
- percentage-point change;
- normalized weight change;
- frames appearing/disappearing;
- quality warnings when either scope is thin.

Manifest inputs pair cases by exact benchmark plus parameters. Per-operation normalization
is offered only when both manifests include an explicit
operation count and unit; otherwise the tool must not imply per-operation data.

**Regression check:** the modern .NET RyuJIT dictionary optimization should show
`Dictionary.Resize` moving from roughly 38% to 2% in the tree scenario without manual
table comparison. Test manifests with absent operation metadata, count without unit, and
complete count+unit metadata; per-operation output must appear only for the complete case.

### P2: Manifest batch analysis

`batch` / `trace_batch` runs one compact ranking across all manifest cases:

```text
filtrace batch capture.json --metric cpu --root BinaryFormattedObject.Deserialize
```

Return a table keyed by benchmark and parameters. This is especially useful for agents,
which otherwise spend calls discovering and opening each parameterized trace.

**Regression check:** one call ranks custom/tree/callback/cycle traces and flags only the
cases whose selected scope is sample-starved.

## Delivery order and status

| Order | Change | Outcome | Status |
| ---: | --- | --- | --- |
| 1 | BenchmarkDotNet scoping + root/frame diagnostics | Prevents plausible percentages over the wrong subtree | Delivered (#43) |
| 2 | Query-specific scoped counts and warnings | Prevents plausible quantitative claims from thin scopes | Delivered (#44) |
| 3 | Measurement-versus-profiling skill guidance | Avoids low-sample one-shot traces immediately | Local; pending `agent-skills` |
| 4 | ETLX per-trace locking and atomic cache writes | Eliminates tool failures and unsafe parallelism | Delivered (#45) |
| 5 | Capture enablement/event reporting | Prevents impossible analysis workflows | Delivered (#46/#49) |
| 6 | Source/PDB resolution diagnostics | Prevents false confidence in line attribution | Delivered (#47/#51) |
| 7 | Isolated capture manifest + exact child symbols | Removes artifact discovery and overlap hazards | Delivered (#48) |
| 8 | Normalized root-scoped diff | Speeds before/after validation | Delivered (#52) |
| 9 | Manifest batch analysis | Reduces calls for parameter matrices | Delivered (#52) |

Filtrace 0.6.0 completed every product/capture item. The remaining promotion work is the
portable performance-testing guidance and dirty-source provenance discipline.

## 0.6.0 outcome and remaining success metrics

With filtrace 0.6.0, an agent can:

- go from a benchmark filter to a trustworthy scoped method ranking in two tool calls after
  capture (`trace_info`, then `trace_rank`);
- get exact source lines without manually searching BenchmarkDotNet output directories;
- analyze parameterized cases without filesystem enumeration;
- run independent read-only analysis calls in parallel without cache races;
- receive a warning before interpreting a CPU scope with fewer than 200 scoped samples,
  without treating generic metric weight as a sample count;
- know from `trace_info` whether allocation capture was enabled, disabled, or unknown and
  how many events were observed;
- compare a targeted frame before/after without manually normalizing sample counts;
- preserve capture command, runtime/JIT, commit SHA, run ID, manifest, trace, exact child
  symbols, and artifact identity.

The remaining `agent-skills` goal is to preserve a reconstructable dirty-state source
bundle when a benchmark is run against uncommitted tracked, untracked, or binary inputs.

Completing the remaining provenance work will make the workflow more reconstructable. The
0.6 contracts already reduce plausible-looking conclusions built on the wrong scope, wrong
PDB, absent events, or statistically thin samples.
