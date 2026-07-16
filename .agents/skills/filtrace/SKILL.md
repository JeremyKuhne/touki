---
name: filtrace
description: Analyze .NET CPU, allocation, exception, GC, JIT, and wall-clock (thread-time) data in .nettrace, .etl, and speedscope files with the filtrace CLI or MCP server. Use when a user asks where time or allocation volume goes in a trace or benchmark, which method or source line is hot, why a run regressed against a baseline, what a captured .nettrace / .etl contains, or to rank / drill / diff / export a profile - including profiling .NET Framework (net481) via ETW, where an EventPipe ranking would mislead. Also covers capturing the trace first - choosing EventPipe vs ETW, elevation, and the recording tool (dotnet-trace, BenchmarkDotNet, PerfView, wpr).
license: MIT
compatibility: Pairs with the filtrace MCP server (the KlutzyNinja.Filtrace.Mcp package, run via `dnx`) for in-agent tool calls; otherwise shells out to the filtrace CLI (the KlutzyNinja.Filtrace global tool). Both heads share the analysis core; capture, cache operations, and all-process ETW widening are CLI-only.
metadata:
   portability: repo-specific
   applicability: tool-shipped
   binding: optional-overlay
   risk: local-write
   maturity: stable
   requires: none
   related: performance-testing
---

# Analyzing .NET traces with filtrace

If `overlay.md` exists beside this file, read it before acting; it contains
consumer-specific bindings. This core remains usable without it.

filtrace ranks CPU / allocation / exception / contention / wait / activity /
thread-time data, reports GC / JIT / thread-pool / disk activity, and drills into,
diffs, or exports CPU profiles from `.nettrace`, `.etl`, and speedscope captures.
It reads both modern .NET and .NET Framework traces. It is a command-line tool and
an MCP server - there is no GUI. Output is dense text by default, or compact JSON
(`--format json`); the analyzer itself runs on .NET 10.

This skill is the *how*; the full reference is single-sourced in
[docs/workflow.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/workflow.md)
and [docs/traps.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/traps.md).

## Getting a trace to analyze

filtrace records ETW captures itself - the `collect` verb launches an executable and
records an `.etl` (Windows, Administrator) - and otherwise analyzes traces other tools
record; for an EventPipe `.nettrace`, that recorder is `dotnet-trace` (cross-platform).
Record or produce one, then point a verb - or `trace_info` - at the file. Pick the
capture by the question:

- **EventPipe** (`.nettrace`) - cross-platform, no elevation, single process. From
   `dotnet-trace collect` or BenchmarkDotNet `-p EP`. It can carry CPU,
   allocations, exceptions, contention, thread-pool, GC, and JIT data when the
   corresponding providers/keywords are enabled; activities require their application
   provider enabled. .NET 9+ wait-handle analysis needs a non-default capture keyword
   (recipes below).
   BenchmarkDotNet may also derive a CPU-only `.speedscope.json`; prefer the raw
   `.nettrace` when both exist.
- **ETW** (`.etl`) - **Windows only, needs Administrator** (kernel sampling),
  machine-wide. From `filtrace collect`, BenchmarkDotNet `-p ETW`, PerfView, or `wpr`.
  It is the *only* source for wall-clock (`threadtime`), the native GC / JIT / `memcpy` split
  (`--native-symbols` + `classify`), and multi-process scoping (`processes` +
  `--process`).

So "where's the time / what allocates" on one process -> EventPipe; "CPU-bound or
blocked?", "GC versus my code?", or a machine-wide capture -> ETW. Two bundled
scripts wrap the capture-then-analyze loop and print the scoped filtrace commands:
[scripts/Capture-BenchmarkTrace.ps1](scripts/Capture-BenchmarkTrace.ps1) profiles a
BenchmarkDotNet micro-benchmark in an isolated run directory, emits an all-case
manifest, verifies exact generated-child PDBs, and prints commands only for
known-enabled analyses. Each command uses the benchmark, process, method, or other
scope supported by its verb; structured reports and orientation commands keep their
own syntax. Disabled/unknown states become warnings;
full BenchmarkDotNet output stays in the run log. Use `-Format Json` for a compact
handoff or `-Quiet` for warnings only. On a non-fatal elevated wait timeout, text
modes emit a warning; `-Format Json` returns `status: "timeout"`, `runId`, `log`, and
`message` instead of empty stdout. JSON stdout stays under 20 KiB; when full case
detail would exceed that budget, a minimal completed result points to `manifest.json`;
every compact fallback includes `runDirectory`, using the canonical run-relative path
if an absolute path cannot fit.
Manifest cases carry explicit benchmark/parameter identity. Pass both
`-OperationCount` and `-OperationUnit` to add complete per-operation metadata, or
omit both.
Recorder-established command fallback is used only when filtrace is unavailable;
if `filtrace info` is present but cannot read a case, every analysis is unknown and
no command is emitted. Recorder fallback never fabricates an `eventCount`; only a
successful `filtrace info` result supplies an observed count, including zero.
Same-project/same-TFM overlap is rejected rather than sharing outputs. The
[scripts/Capture-ProjectTrace.ps1](scripts/Capture-ProjectTrace.ps1) builds an
executable project and traces its running output directly - never `dotnet run`,
whose build/run host is a different process (see the trap catalog).

Two more scripts open a filtrace `export` in a hosted viewer with the profile already
loaded, no manual upload:
[scripts/Open-SpeedscopeTrace.ps1](scripts/Open-SpeedscopeTrace.ps1) serves a
`--format speedscope` profile to speedscope.app (defaulting to the Left Heavy hotspot
view), and [scripts/Open-PerfettoTrace.ps1](scripts/Open-PerfettoTrace.ps1) serves a
`--format chromium` synthetic flame-graph trace to the Perfetto UI. Each hosts the
file on a one-shot loopback listener, so nothing is uploaded.

For `rank --metric wait`, capture a .NET 9+ process with the runtime's default
keywords plus `WaitHandle` (`0x40000000000`); the combined mask for the runtime
used here is `0x414C14FCCBD`:

```pwsh
dotnet-trace collect --profile cpu-sampling `
   --providers Microsoft-Windows-DotNETRuntime:0x414C14FCCBD:5 -- <app> <args>
```

A plain `dotnet-trace collect` can capture CPU, runtime contention, and structured
runtime reports depending on its selected profile, but `wait` needs the explicit
keyword above.

Activity ranking and `--activity` CPU scope need completed EventSource Start/Stop
pairs **and that application provider enabled during capture**. Use matching
`OperationStart` / `OperationStop` events (or explicit Start/Stop opcodes) and add
the provider alongside CPU sampling, for example:

```pwsh
dotnet-trace collect --profile cpu-sampling `
   --providers MyCompany-RequestSource:0xFFFFFFFFFFFFFFFF:5 -- <app> <args>
```

Replace the provider name; level `5` is Verbose and the mask enables all keywords.

## The workflow: orient -> rank -> drill -> compare

Almost every investigation is the same four moves:

1. **Orient.** Read the trace's format, sample count, and symbol-resolution rate
   first - `filtrace info <trace>` or the `trace_info` tool. A rate **below 0.8**
   fires a quality warning: inspect the unresolved rows before trusting frame names.
   Managed method names normally come from the capture's CLR rundown; `--symbols`
   supplies matching PDBs for source lines, not a replacement for missing rundown.
   Treat that rate as frame-name quality only. Before source-line analysis, inspect
   `sourceResolution`: require exact matches for the relevant modules, report mapped
   versus sampled managed frames, and use `highestUnmappedModules` plus
   `searchedDirectories` to diagnose the missing PDBs. If
   `pdbIdentityMismatchModules` names a module, the expected PDB filename exists but
   its GUID or age differs from the trace. For BenchmarkDotNet, point symbols at the
   generated child output retained with `--keepFiles`. Once the relevant module
   matches, compare `sourceMappedManagedMethodCount` with
   `sampledManagedMethodCount`; use `unmappedNamedManagedFrameCount` and
   `highestUnmappedMethods` to quantify and identify named frames that remain
   `<no source>`.
   Unresolved native ETW frames can depress the aggregate while managed-method
   rankings remain usable; use `--native-symbols` when the native runtime split matters.
   Check `availableAnalyses` before selecting a metric, then read
   `analyses.<name>`: `captureStatus` and `eventCount` distinguish enabled-zero,
   disabled, observed, and unknown provider state.
2. **Rank.** Find the hottest frames by the metric that matches the question -
   `cpu`, `alloc`, `exceptions`, or `threadtime` (or `rank --metric <m>`).
   Self-time finds the leaf that burns the resource; inclusive-time finds the
   subtree that drives it.
3. **Drill CPU.** For an unwindowed CPU ranking, follow the hot frame with
   `callers <frame>` (who calls it), `lines` / `heatmap <file>` (which source
   lines), or `tree` (what it calls). These tools read CPU stacks only. For alloc,
   exceptions, contention, wait, activity, or threadtime, compare self/inclusive
   rankings or refine `root` / `time` instead of crossing into a CPU drill.
4. **Compare.** `diff <before> <after>` accepts traces or capture manifests and
   reports absolute plus normalized changes. `batch <manifest>` runs one compact
   ranking query across every case; `export --format speedscope` hands a human a
   flame graph.

```pwsh
filtrace info app.nettrace                   # 1. orient: format, symbol rate, analyses
filtrace cpu app.nettrace                    # 2. rank self-time
filtrace callers app.nettrace MyApp.Parse    # 3. who drives the hot frame
filtrace lines app.nettrace --symbols bin/Release/net10.0   # 3. hot source lines
filtrace diff before.nettrace after.nettrace # 4. what changed
```

Choose the analysis from the symptom, confirm it appears in `availableAnalyses`,
then require `captureStatus: enabled` before interpreting a zero as an empty
workload:

| Symptom / question | Start with | What it establishes |
|---|---|---|
| CPU saturated or a hot loop | `cpu` self, then inclusive / callers | executing leaf, then the subtree or caller driving it |
| Slow with low CPU | `threadtime` (`.etl`), or `contention` / `wait` / `threadpool` (`.nettrace`) | broad on/off-CPU split, lock/handle waits, or pool starvation |
| High allocation rate or GC pauses | `alloc`, then `gcstats` | sampled allocation volume by site, then collection/pause cost |
| Startup or first-call delay | `jitstats` | JIT count and compile cost |
| Repeated exceptions | `exceptions` self, then inclusive | thrown types, then the paths that throw them |
| One captured request or job is slow | metric `activity`, then CPU scoped with `activity` | completed activity paths, then CPU inside the named operation |
| A spike occurs at an unknown time | `timeline`, then `rank --time` | the busy window, then its stacks |
| Physical disk pressure | `diskio` (`.etl` with disk keywords) | files ranked by physical disk service time |

<!-- filtrace:begin verbs -->
### CLI verbs

**Orient** - see what a capture holds before ranking:

| Verb | Shows |
|---|---|
| `info` | format, samples, frame-name and source/PDB quality, per-thread counts, per-analysis format/capture/event state, and quality warnings - the CLI counterpart of `trace_info` |

**Rank** - find the hottest frames by a metric:

| Verb | Ranks | Reads |
|---|---|---|
| `rank --metric <m>` | any metric (`cpu`, `alloc`, `exceptions`, `threadtime`, `contention`, `wait`, `activity`) | per metric |
| `cpu` | CPU self/inclusive time | `.nettrace`, `.etl`, `.speedscope.json` |
| `alloc` | bytes allocated, by site | `.nettrace` |
| `exceptions` | exception types, by count | `.nettrace` |
| `threadtime` | wall-clock (running + blocked) | `.etl` (Windows) |

**CPU drill** - follow a CPU ranking into detail:

| Verb | Shows |
|---|---|
| `callers <frame>` | immediate CPU callers of a frame, or a caller/callee view with `--callees` |
| `lines` | hottest CPU source lines of the scoped methods |
| `heatmap <file>` | per-line CPU heat for one source file |
| `tree` | top-down CPU call tree from the root |

**Inventory** - see what a (possibly machine-wide) capture holds:

| Verb | Shows |
|---|---|
| `processes` | processes by CPU-sample weight, to pick a `--process` target |
| `classify` | CPU time by runtime work category (zeroing / copying / GC / JIT) |

**Temporal** - see what happened when, to find the window to drill:

| Verb | Shows |
|---|---|
| `timeline` | per-bucket GC / CPU / exception / allocation / JIT activity across the trace |

**Compare and export:**

| Verb | Does |
|---|---|
| `diff <before> <after>` | absolute and normalized CPU changes; trace pairs or paired manifests |
| `batch <manifest>` | one compact metric ranking across every parameterized manifest case |
| `export --format <fmt>` | write a flame graph for a viewer - `speedscope` or `chromium` |

**Structured reports:**

| Verb | Reports |
|---|---|
| `gcstats` | GC counts, pauses, heap summary (`.nettrace`) |
| `jitstats` | JIT method count, compile time, sizes (`.nettrace`) |
| `threadpool` | worker-thread adjustments and starvation - slow under load, CPU idle (`.nettrace`) |
| `diskio` | physical disk I/O by file: bytes and disk service time (`.etl`, Windows) |
| `events --name <n>` | raw events, filtered by name / payload / pid / tid, paged (`.nettrace`, or `.etl` on Windows) |

**Capture** - record a Windows ETW `.etl` yourself (for an EventPipe `.nettrace`, use `dotnet-trace`):

| Verb | Does |
|---|---|
| `collect` | launch an executable and record a CPU / thread-time `.etl` (Windows, Administrator) |

**File ops** - manage the ETLX conversion cache TraceEvent keeps beside a trace:

| Verb | Does |
|---|---|
| `convert` | build the ETLX cache up front |
| `clean` | remove the ETLX cache to force a rebuild |

Same-trace conversions are coordinated by canonical path across threads and
processes. filtrace converts to a unique sibling temporary file and atomically
publishes the completed cache, so MCP calls against one trace may run in parallel;
different traces remain independent. `trace_info.etlxCacheState` and the `convert`
verb report `hit`, `waited`, `converted`, or `recovered` (`null` for speedscope).
`clean` waits for an active conversion before removing its cache.
<!-- filtrace:end verbs -->

Run `filtrace <verb> --help` for the full option set of any verb.

## Scope and symbols

<!-- filtrace:begin scopes -->
**Implemented scope inventory:**

- **Named process:** CLI `info`, `rank`, `cpu`, `threadtime`, `callers`, `lines`,
  `heatmap`, `tree`, `classify`, `timeline`, `diff`, `batch`, and `export`; MCP
  `trace_info`, `trace_rank`, `trace_callers`, `trace_lines`, `trace_heatmap`,
  `trace_tree`, `trace_classify`, `trace_timeline`, `trace_diff`, `trace_batch`, and
  `trace_export`. These auto-scope a multi-process `.etl` to the busiest process tree.
  Run `processes` / `trace_processes` first to inspect the capture, then set
  `--process <name>` / `process` to override. CLI verbs expose `--all-processes`
  where an aggregate is supported; MCP has no all-process aggregate.
- **Root subtree:** CLI `rank`, `cpu`, `alloc`, `exceptions`, `threadtime`, `callers`,
  `tree`, `classify`, `diff`, `batch`, and `export`; MCP `trace_rank`,
  `trace_callers`, `trace_tree`, `trace_classify`, `trace_diff`, `trace_batch`, and
  `trace_export`. Set `--root <frame>` / `root` to keep the subtree under a frame.
- **BenchmarkDotNet workload:** CLI `rank`, `cpu`, `alloc`, `exceptions`,
  `threadtime`, `callers`, `tree`, `classify`, `diff`, `batch`, and `export` accept
  `--benchmark`; MCP `trace_rank`, `trace_callers`, `trace_tree`, `trace_classify`,
  `trace_diff`, `trace_batch`, and `trace_export` accept `benchmark: true`. The
  preset isolates the `WorkloadAction` subtree from harness and overhead scaffolding;
  it is mutually exclusive with an explicit root. `lines` / `heatmap` are not
  root-aware, so narrow them by method/file and treat percentages as process-scoped
  whole-trace values.
<!-- filtrace:end scopes -->

- **Scope to a time window.** `rank --time <start>,<end>` (milliseconds relative to
  the trace start, either bound optional: `1000,5000`, `1000,`, or `,5000`) keeps
  only the samples anchored in the window. It applies to every metric, so it zooms
  a `.nettrace` / `.etl` ranking to the slice around a spike or one slow request
   (`.speedscope.json` is aggregate-only here and warns that the window was ignored).
- **Symbols.** Managed frames (including NGEN and ReadyToRun framework methods)
   resolve to method names from the trace's CLR rundown. `--symbols <dir>` supplies
   matching local PDBs for source-line attribution; do not assume it repairs missing
   rundown names or that a same-named PDB matches. Confirm exact PDB modules and
   sampled source mapping in `trace_info.sourceResolution`; for BenchmarkDotNet,
   prefer the retained generated child output over the outer project output.
   `--native-symbols` (CPU `.etl` only, opt-in, network) names the
   unmanaged GC / JIT / `memcpy` frames that otherwise show as a `?` leaf.

## Interpret and report the evidence

- Read `warnings` before the payload and use `hints` as candidate next steps. An
   empty or poorly resolved result is a reason to fix scope/symbols, not evidence
   that the behavior does not exist.
- State the trace format, selected process/root/time window, metric, and
   self-versus-inclusive measure with the finding. Percentages are relative to that
   scope; CPU milliseconds are sampled estimates, not exact elapsed duration.
- Keep counts separate from weight. `trace_info.sampleCount` describes the loaded
   whole trace after process/activity/time filters; it does not establish that a
   narrower query is well sampled. Rankings/callers expose
   `contributingRecordCount`; lines/heat maps expose attributed and unattributed
   record counts. `scopeWeight` remains metric weight, never a generic record count.
- Apply the default 200-record method and 1,000-record line guidance only to periodic
   CPU sampling. Evented speedscope records are duration intervals: report their count
   separately from weight, but do not apply periodic thresholds. A null count means
   the source cannot establish meaningful record semantics.
- `alloc` attributes `GCAllocationTick` volume to allocation sites. It does **not**
   report retained bytes, object reachability, or GC-root paths, so it cannot prove a
   memory leak; use a heap snapshot/dump tool for retention.
- `threadtime` aggregates running and blocked intervals across threads. Do not call
   its total a request's latency unless the scope isolates that request/thread.
- `contention`, `wait`, and `activity` pair Start/Stop events. An operation still
   open at trace end may be absent; an empty ranking does not rule out an active
   hang. Use ETW threadtime or a dump/current-state tool when the unfinished state
   itself is the question.
- `diff` reports absolute weight, scope shares, percentage-point change, normalized
   weight change, and appearing/disappearing frames. Scope direct traces consistently
   with root/process/benchmark. Manifest cases pair only by exact benchmark plus
   parameters; per-operation fields require complete count and matching unit on both
   sides.
- `batch` / `trace_batch` returns one compact top-frame row and case-specific warnings
   for each of at most 24 manifest cases. Use the returned path with `rank` for detail.
- Chromium export reconstructs one aggregate synthetic track whose widths preserve
   sample weight. Its axis is not the capture's original timestamps, thread
   concurrency, or idle gaps; use `timeline` / `--time` for temporal conclusions.
- Report observations separately from hypotheses. A hot frame, high allocation
   site, or positive diff identifies where recorded cost landed; it does not by itself
   establish root cause or prove that a code change caused the difference.

## Traps

The recurring ways a .NET trace investigation goes wrong:

<!-- filtrace:begin traps -->
## Trap catalog

1. **Profile .NET Framework with ETW, never extrapolate from an EventPipe trace.**
   EventPipe (`.nettrace`) is modern-.NET-only and managed-only. The net10
   EventPipe ranking actively *misleads* for `net481`: weaker Framework inlining
   relocates the hot frame, so a method that is 1.5% self-time on the EventPipe
   trace can be 56% on the ETW (`.etl`) capture of the same workload. Capture
   net481 under ETW (`threadtime` / `cpu` over an `.etl`) and rank that.

2. **Treat low symbol resolution as a quality gate, not an automatic rejection.**
   A rate below **0.8** (surfaced by `trace_info` / the load warning) means unresolved
   frames need inspection. Managed method names normally come from CLR rundown;
   `--symbols <build-output-dir>` supplies matching PDBs for source lines, not a
   replacement for missing rundown. The aggregate rate conflates managed and native
   frames, so a net481 ETW capture can read low while every *managed* leaf resolves
   correctly; in that case managed-method rankings remain usable, and
   `--native-symbols` is the relevant opt-in when the native runtime split matters.
   Conversely, 100% method-name resolution does not prove that any source line is
   available. Before `lines` or `heatmap`, inspect `trace_info.sourceResolution`:
   require the relevant module in `matchingPdbModules`, then report mapped versus
   sampled managed frames and `highestUnmappedModules`. When
   `pdbIdentityMismatchModules` names the module, the expected PDB filename exists
   but its GUID or age differs from the trace. For BenchmarkDotNet, use the generated
   child output retained with `--keepFiles`, not the outer project output. Once the
   relevant module appears in `matchingPdbModules`, compare
   `sourceMappedManagedMethodCount` with `sampledManagedMethodCount`; then use
   `unmappedNamedManagedFrameCount` and `highestUnmappedMethods` to quantify and
   identify named frames that still map to `<no source>`.

3. **On a machine-wide `.etl`, confirm the process before scoping.** filtrace
   auto-scopes to the busiest process tree ranked by **CPU-sample count** (a
   long-lived background service wins a wall-clock race but owns few samples), and
   that default is usually right - but run `processes` first to see what is in the
   capture, then pass `--process <name>` if the auto-pick is wrong.

4. **BenchmarkDotNet captures include the harness - scope with `--benchmark` by
   default, not as an afterthought.** A raw ranking (or export) of a BDN trace is
   mixed with orchestrator and overhead scaffolding outside your `[Benchmark]`.
   In the CLI, pass `--benchmark` to `rank`, `cpu`, `alloc`, `exceptions`,
   `threadtime`, `callers`, `tree`, `classify`, `diff`, `batch`, and `export`; in
   MCP, pass `benchmark: true` to `trace_rank`, `trace_callers`, `trace_tree`,
   `trace_classify`, `trace_diff`, `trace_batch`, and `trace_export`. The wrapper
   includes warmup and actual workload iterations; it excludes harness/overhead
   scaffolding, not warmup. This applies especially to export - a flame graph with
   the harness left in is not just noisy, its proportions are wrong. Do not
   substitute a benchmark method substring:
   if root/frame warnings report multiple definitions or depths, narrow the selector
   before trusting the result. `lines` / `heatmap` cannot preserve root scope; narrow
   them with their method/file filter and treat percentages as whole-trace.

5. **A healthy whole trace can still produce a statistically thin scoped result.**
   `trace_info.sampleCount` describes the loaded trace, while a root, focus method,
   method filter, or file filter may retain only a small subset. Read the query's
   `contributingRecordCount` or line-level attributed/unattributed counts separately
   from `scopeWeight`, which is metric weight rather than a record count. The default
   200-record method and 1,000-record line warnings apply only to periodic CPU
   sampling. Evented speedscope records are duration intervals: report their count,
   but do not treat it as a periodic sample confidence gate.

6. **A supported format does not prove the provider was enabled.**
   `availableAnalyses` is the format inventory only. Read
   `trace_info.analyses.<name>` before acting: observed events prove `enabled`;
   recorder metadata can prove `enabled` with zero events or `disabled`; without
   either, the status is `unknown`. Never relabel unknown as an empty workload.
   The bundled capture helpers write `<trace>.filtrace.json`; preserve that sidecar
   with the trace so enabled-zero stays distinguishable from disabled.

7. **Native runtime frames need `--native-symbols`.** Without it, the unmanaged
   share of a trace - GC, JIT, `memset` / `memcpy`, write barriers - shows as
   unresolved `?` leaves. Opt in (CPU `.etl` only; fetches PDBs from the Microsoft
   public symbol server, cached locally) to name them, then `classify` to get the
   zeroing-vs-copying-vs-GC-vs-JIT split. It is off by default so analysis stays
   offline and deterministic.

8. **Self-time and inclusive-time answer different questions.** Self-time finds
   the leaf that burns the resource; inclusive-time finds the subtree that drives
   it. Ranking by the wrong measure hides the frame you want - start with self for
   "what is hot", switch to inclusive for "what is responsible".

9. **Reading an `.etl` through filtrace is Windows-only.** The ETW -> ETLX
   conversion needs Windows, and direct `.etlx` input is not part of the current
   CLI or MCP surface. The `.etl` paths report a clean error off Windows. Do not
   serialize same-trace MCP calls as a workaround: filtrace now coordinates ETLX
   conversion across threads and processes, publishes atomically, and reports
   `hit`, `waited`, `converted`, or `recovered` in `trace_info.etlxCacheState`.

10. **The default fold list hides runtime leaves on purpose.** It folds
   `memmove`, write-barriers, and GC-poll helpers into their managed caller -
   right for "which method is hot", wrong for "what kind of work dominates". Use
   `--no-fold` (or `classify`) to let the native leaves rank on their own.

11. **Trace the built app, not `dotnet run`.** `dotnet run` builds and then forks
   your program into a separate child process, so a single-process EventPipe
   session launched with `dotnet-trace collect -- dotnet run ...` records the
   build/run host, not your code, and the hot frames never appear. Build first,
   then launch the built output directly (`dotnet-trace collect -- dotnet
   <app>.dll`, or `dotnet-trace collect -- <apphost>`); the bundled
   `Capture-ProjectTrace.ps1` resolves that run target for you.

12. **A machine-wide `.etl` can be huge - capture lean, then scope at analysis.**
   ETW kernel tracing is machine-wide, so the wrong keywords balloon the file: the
   File/Disk *name* rundowns enumerate every open file on the box (hundreds of
   thousands of events that dwarf the workload) no matter how short the window.
   `filtrace collect` avoids this by design - it enables only the CPU (and, for
   `threadtime`, context-switch) keywords and stacks just the sampled events, never the
   File/Disk rundown - so prefer it and bound open-ended runs with `--duration` or
   `--max-size-mb` (a circular buffer that keeps the last N MB). Only a `diskio` capture
   needs the File/Disk keywords, and `filtrace collect` has no switch for them: that
   capture comes from another recorder (PerfView, `wpr`, or a custom BenchmarkDotNet
   `EtwProfilerConfig` enabling `DiskIO` / `DiskFileIO`; plain `-p ETW` is CPU-only),
   so expect the system-wide rundown there and trim it down afterward. To focus a big
   capture on your code, scope at *analysis* time with `--process` (lossless - it keeps
   managed stacks); physically trimming the file by relogging is a transport-only
   optimization that currently drops JITted managed frames.
<!-- filtrace:end traps -->

## CLI or MCP

The two heads share one analysis core, with deliberately different operational
surfaces:

- **CLI** - `dotnet tool install -g KlutzyNinja.Filtrace`, then `filtrace <verb>`.
- **MCP server** - `dnx KlutzyNinja.Filtrace.Mcp` over stdio, exposing seventeen
  `trace_*` tools (`trace_info`, `trace_rank`, `trace_callers`, `trace_lines`,
  `trace_heatmap`, `trace_tree`, `trace_processes`, `trace_classify`,
   `trace_diff`, `trace_batch`, `trace_export`, `trace_timeline`, `trace_gc`, `trace_jit`,
  `trace_threadpool`, `trace_diskio`, `trace_query_events`).
  Each returns one envelope: a `schemaVersion`, a `warnings` list, next-step
   `hints`, and the typed result. MCP can auto-scope or select a named ETW process;
   use the CLI when the question requires `--all-processes`, capture, or ETLX cache
   operations.

See [docs/workflow.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/workflow.md)
for the full verb/tool reference and the MCP config snippet, and
[docs/traps.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/traps.md) for
the trap catalog.
