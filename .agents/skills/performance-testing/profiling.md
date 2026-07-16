# Profiling a benchmark: from operation to method to line

Detail for the [performance-testing](SKILL.md) skill. For the full filtrace
verb / tool reference and trap catalog, see the [`filtrace`](../filtrace/SKILL.md)
skill; this page is the touki capture recipe and how to read the result.

To find where a benchmark spends its time - optimizing a hot path or chasing a
regression - capture an EventPipe CPU trace on `net10.0`, then drill it with the
standalone [filtrace](https://github.com/JeremyKuhne/filtrace) analyzer. It reads
the trace through TraceEvent, folds the JIT-helper sampling artifacts, and ranks
by method or by source `file:line`. filtrace is registered as an MCP server in
[.vscode/mcp.json](../../../.vscode/mcp.json), so **an agent calls its tools
directly** - `trace_info` first, then `trace_rank` / `trace_callers` /
`trace_lines` / `trace_heatmap`. The equivalent CLI verbs (below) are the manual
fallback. Prefer the bundled isolated capture helper; one run emits every
parameterized case, exact child symbols, provider-aware commands, and a manifest
that `batch` and `diff` can consume:

```powershell
$filtraceVersion = (& filtrace --version | Select-Object -First 1).Trim()
if ($filtraceVersion -ne '0.6.1') {
  throw "filtrace 0.6.1 is required; found '$filtraceVersion'."
}

$handoff = & ./.agents/skills/filtrace/scripts/Capture-BenchmarkTrace.ps1 `
  -Project touki.perf/touki.perf.csproj `
  -Filter '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingleWithRoot' `
  -Tfm net10.0 -Format Json | ConvertFrom-Json

$manifest = Get-Content $handoff.manifest -Raw | ConvertFrom-Json
$manifest.cases | Select-Object benchmark, parameters, trace, symbolsDirectory, warnings

# Rank all parameterized cases compactly, then drill one case with rank/callers/lines.
filtrace batch $handoff.manifest --metric cpu --benchmark
$manifest.cases[0].commands
```

The MCP tools map onto those verbs one-to-one: `trace_rank` (with
`measure: self|inclusive`) for the method ranking, `trace_lines` for the line
ranking, `trace_callers` for the caller breakdown, and `trace_info` for the
load-and-summarize the other tools do implicitly.

## Agent protocol for phase investigations

**Use different harnesses for measurement and profiling.** A mutable or
consumable intermediate representation needs fresh state for every measured
operation: prepare a bounded batch in `IterationSetup`, consume every item once,
normalize with `OperationsPerInvoke`, and release the batch in
`IterationCleanup`. Sweep one item, an intermediate batch, and a larger batch:
the first exposes timer overhead, while the last exposes retained-live-set
distortion. Keep the smallest batch that amortizes the harness without changing
the workload.

That one-shot shape is often a poor CPU-profiling harness. For profiling, prefer
an adaptive end-to-end benchmark and root-scope filtrace to the phase method.
Work before that call remains outside the selected subtree, while BenchmarkDotNet
can execute enough operations to produce a denser profile. Profile the one-shot
benchmark only when a compatible periodic CPU capture has enough contributing
samples in the selected query.

Validate a phase split before trusting it: phase allocations should add to the
independently measured end-to-end allocation at reported precision, and phase
means should approximately add to the end-to-end mean. A large gap usually means
setup leaked into one measurement, mutable state was reused, or the batch changed
GC/live-set behavior.

**Use the filtrace 0.6.1 evidence contracts:**

- Read `trace_info.analyses.<name>` before interpreting a metric.
  `captureStatus: enabled` plus `eventCount: 0` is a valid empty analysis;
  `disabled` is unavailable; `unknown` remains unknown. Preserve the capture
  helper's `<trace>.filtrace.json` sidecar with the trace.
- For Touki's BenchmarkDotNet 0.16.0-preview.1 captures, the helper verifies hashed
  parameterized artifacts against the benchmark identity embedded in each trace.
  Ordinary filenames may use exact benchmark name plus execution order only when
  case counts and distinct capture timestamps align. Any case left unidentified
  stays out of manifest-aware `batch`/`diff` pairing; analyze that trace directly
  rather than inferring an identity. Durable manifests have a 16 MiB safety limit;
  only the agent-facing JSON handoff is capped at 20 KiB. Treat `activity` according
  to its reported provider state; the default capture leaves it unknown without
  provider evidence.
- Read `sourceResolution` separately from managed frame-name resolution. Require
  the relevant module in `matchingPdbModules`; use `pdbIdentityMismatchModules`,
  `highestUnmappedModules`, and `highestUnmappedMethods` to diagnose `<no source>`.
  The capture manifest's `symbolsDirectory` is the verified generated-child path.
- Root-aware tools accept either the BenchmarkDotNet preset (`benchmark: true` /
  `--benchmark`) or an explicit frame root (`root` / `--root`), never both. Use
  the preset for the whole measured workload and replace it with the phase method
  root for phase-specific analysis. Read ambiguity warnings and selected frame
  definitions instead of guessing a benchmark-method substring. `lines`/`heatmap`
  remain whole-trace views narrowed by method/file rather than a stack root.
- Rankings/callers expose `contributingRecordCount`; lines/heat maps expose
  attributed and unattributed record counts. Keep those counts separate from
  `scopeWeight`. Apply the 200/1,000 quality guidance only to periodic CPU samples,
  never evented speedscope records.
- Same-trace MCP calls may run in parallel: ETLX conversion is coordinated across
  threads/processes and `trace_info.etlxCacheState` reports `hit`, `waited`,
  `converted`, or `recovered`.
- Use `trace_batch`/`batch` for parameter matrices and manifest-aware
  `trace_diff`/`diff` for before/after scope shares. Per-operation values require
  matching operation count and unit metadata in both manifests.

Keep a compact experiment ledger while iterating:

| Hypothesis | Small edit | Check | Time | Allocation | Target frame | Decision |
| --- | --- | --- | ---: | ---: | --- | --- |

Record rejected variants as well as retained changes. Rejections prevent a later
agent from repeating attractive experiments that already lost on another TFM or
on allocation.

Things that bite, kept short - full rationale in
[docs/performance-investigation.md](../../../docs/performance-investigation.md)
sections 3a (methods) and 3f (lines):

- **EventPipe is net10.0-only** - net481 has no EventPipe, so profile it under
  ETW instead (see [Capturing under ETW](#capturing-under-etw-net481-inlining-accurate-attribution-denser-samples-native-frames)
  and [tools/Capture-EtwTrace.ps1](../../../tools/Capture-EtwTrace.ps1)); never
  read a net481 hotspot off a net10 trace. EventPipe's CPU sampler is also fixed
  at **~100 Hz** and **net10 cannot raise it**
  (`DOTNET_EventPipeThreadSamplingRate` is .NET 11+, and only makes sampling
  *coarser*); when that resolution is too sparse for line-level work, escalate to
  ETW (see below).
- **Fold the artifacts.** A raw self-time view shows `0 ms` per method (the leaf
  is a synthetic `CPU_TIME` marker), and the managed-only walker mislabels
  JIT-helper thunks (`BulkMoveWithWriteBarrier`, `Thread.PollGCWorker`,
  `Buffer.Memmove`) as the hotspot. The analyzer folds both by default; a
  `BulkMoveWithWriteBarrier` over a GC-ref-free struct is always an artifact.
- **Scope root-aware analysis with `--benchmark` / `benchmark: true`.** For a
  phase inside the benchmark, use its explicit library root *instead of* the
  benchmark preset after inspecting ambiguity diagnostics. Do not substitute the
  benchmark method name: it can also match an activity/harness wrapper. `lines`
  and `heatmap` do not preserve stack-root scope; narrow them by method/file.
- **Line ranking needs the exact generated-child symbols.** Use the capture
  manifest's verified `symbolsDirectory` and inspect `trace_info.sourceResolution`.
  A wrong/same-named PDB can leave `<no source>` even when frame-name resolution
  is 1.0; `pdbIdentityMismatchModules` identifies an exact identity mismatch.
- **Inlining attribution differs by profiler, and it corrupts the *method*
  ranking - not just the line view.** EventPipe's managed walker credits a
  fully-inlined callee's self-time to its *physical host* method (and, in the
  line view, collapses it onto the host's call-site line); BDN's ETW
  `EtwProfiler` resolves it back to the inlinee. So a fully-inlined callee can
  read near-zero on the EventPipe *method* ranking while its host tops it. If the
  top self-frames are thin drivers/wrappers, suspect this and cross-check under
  ETW (reason 4 below). Scoping `--method` to the callee or a temporary
  `[MethodImpl(MethodImplOptions.NoInlining)]` also exposes it, but NoInlining
  changes codegen (it can force a ref-struct engine to materialize per call), so
  treat it as a probe, not a measurement.

## Capturing under ETW (net481, inlining-accurate attribution, denser samples, native frames)

EventPipe is net10-only and managed-only. Reach for an ETW (`.etl`) capture in
four situations - the first two bite hardest:

1. **Profiling net481 at all.** The Framework target has no EventPipe, so ETW is
   the only profiler - and the net481 hotspot can differ *completely* from net10,
   so never extrapolate a Framework ranking from a net10 EventPipe trace. Worked
   example (the `!(bin|obj)/**/*.cs` glob enumeration): `ExtGlobEngine.ProduceAlternative`
   was **~1.5 %** of self-time on the net10 EventPipe trace and **56 %** on the
   net481 ETW trace, because .NET Framework 4.8.1 RyuJIT inlines far less and
   leaves that method standing as its own frame. The net10 profile actively
   misleads about where net481 spends its time.
2. **Native frames are the suspect** - ETW resolves coreclr / clrjit / ntdll and
   the GC, which the managed-only EventPipe walker cannot see.
3. **Line attribution is too sparse** - kernel CPU sampling is ~1 kHz, ~10x
   EventPipe's fixed ~100 Hz, so a short hot path spreads across more source lines
   (escalation detail below).
4. **Method attribution under heavy inlining - even on net10.** EventPipe's
   managed walker credits a fully-inlined callee's self-time to its *physical
   host* method; BDN's ETW `EtwProfiler` (TraceEvent + the CLR inline map)
   resolves it to the real inlinee. This is *not* a net481-only effect and *not*
   a density effect - it reorders the net10 method ranking on the same binary.
   Proven A/B (same PDB GUID, same net10 scenario, only the capture method
   differs): `ExtGlobEngine.ProduceAlternative` collected just **98** leaf
   samples in the EventPipe trace versus **11,516** in the ETW trace of the same
   op - a ~117x attribution gap - while EventPipe parked 99.76 % of the engine's
   self-time on two host call/setup lines (`RunEngine` :385 and
   `RunEngineDirectory` :410) that ETW spread across `ProduceAlternative`'s real
   body. **Signal in the `.nettrace`:** a thin
   driver/wrapper method tops the self-time ranking while the inner loop you
   expect to be hot reads near-zero. **Duration cannot fix this** - more samples
   give a more confident wrong answer; only ETW (or reading the host's source /
   a temporary NoInlining probe) corrects it. When you escalate, keep both
   captures: the EventPipe-to-ETW flip *is* the evidence.

**Capture it with one command.** [tools/Capture-EtwTrace.ps1](../../../tools/Capture-EtwTrace.ps1)
self-elevates (one UAC prompt), shows the benchmark's **live** progress in the
elevated window (output is teed, never redirected with `*>`, so it never looks
hung), checks that the `BenchmarkDotNet.Diagnostics.Windows` package is
referenced - without it `-p ETW` silently no-ops - and prints the exact,
process-scoped next-step filtrace commands:

```powershell
./tools/Capture-EtwTrace.ps1 -Filter '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingle'
```

**An `.etl` is a machine-wide capture - scope it to your process.** filtrace
auto-scopes to the busiest process *by CPU sample count* (the quantity the
rankings consume), which is normally the benchmark; but a noisy background app
(antivirus, a VPN client) can own enough samples to win, so pass `--process <name>`
to pin it. **Every analysis verb takes it** - `cpu`, `rank`, `threadtime`, `lines`,
`callers`, `heatmap`, `export` (and the matching `trace_*` MCP tools). `filtrace
processes <etl>` lists every process by weight so you can choose the right
target. Quiesce other CPU consumers before capturing, or scope explicitly.

**Default every root-aware BenchmarkDotNet analysis to `--benchmark`, not just
`--process`, unless you are explicitly scoping to a phase root.**
Process scoping only narrows *which OS process* the samples come from; a BDN
process itself still interleaves the harness bootstrap, JIT/overhead warmup
iterations, and the measured `[Benchmark]` workload in one call tree. `--benchmark`
(preset root = the generated `WorkloadAction*` wrapper) is what isolates the
measured code from that scaffolding, and it is **not optional for a BDN trace** -
apply it by default to every verb that offers it, including `export`; pass
`benchmark: true` to the corresponding MCP tools. For a phase-specific query,
replace the preset with `--root <phase-frame>` / `root: <phase-frame>`; the two
scope selectors are mutually exclusive. An
unscoped export is not just noisy, its *proportions* are wrong: a flame graph or
line ranking that still includes warmup materially understates the workload's own
share of time. `lines` and `heatmap` cannot preserve root scope, so filter those
by method/file and report their percentages as whole-trace. Forgetting benchmark
scope on `export` specifically is an easy miss because the
verb writes a file instead of rendering a ranking, so there is no immediate
"scoped to X" line to notice is missing - check the command before running it,
not the output after.

**`threadtime` vs `cpu`.** `threadtime` (ETL-only) is wall-clock per thread -
running *and* blocked - while `cpu` is on-CPU time. When they agree closely the
work is CPU-bound (the glob example: 56.45 % cpu vs 56.46 % threadtime - no
blocking); when `threadtime` is much larger the frame is waiting on I/O or a lock.

### Is EventPipe enough, or escalate to ETW?

EventPipe is the right *first* capture on net10: unelevated, one command, and its
method ranking locates the hot *region*. Treat it as triage, then decide whether
to trust it or escalate. Three cases, two different fixes:

- **Density - are there enough samples?** EventPipe samples each managed thread at
  a fixed **~100 Hz**, so coverage is a function of *total* capture time (BDN
  repeats the op across many iterations), not single-op latency. Rules of thumb
  from this machine: a frame needs roughly **>=200-300 periodic CPU samples** for
  a trustworthy self-%, and **>=1,000 samples** - about **~10 s of cumulative time
  in that frame** at 100 Hz - before its *line* view spreads usefully. Read
  `contributingRecordCount` from rank/callers and attributed/unattributed counts
  from lines/heat maps; never reinterpret `scopeWeight` as a count. Filtrace emits
  thin-scope warnings. These thresholds do not apply to evented speedscope records.
- **Sparse but *correct* - the tree points at the right method, just thin.**
  Lengthen the measured work (a larger input or the long-running scenario) or
  capture ETW for ~10x density. **Lengthening the scenario is the right lever
  here**, and the cheapest.
- **Misattributed - a driver/wrapper tops the ranking (reason 4 above).**
  Lengthening does **nothing**; it only sharpens a wrong answer. ETW is the fix.
  This is the distinction that matters: **duration cures sparsity, never
  misattribution.** Tell them apart by reading the top self-frame's source - if
  its body is mostly a call into a loop, its self-time is borrowed from an
  inlinee, and only ETW (or a NoInlining probe) will hand it back.

The line-level escalation ladder below refines the first two cases.

### When line attribution is too sparse

EventPipe's CPU sampler is fixed at **~100 Hz** - one sample per managed thread
roughly every 10 ms - and on **net10 you cannot raise it**
(`DOTNET_EventPipeThreadSamplingRate` is .NET 11+, and only makes sampling
*coarser*, never finer). A `lines` or `heatmap` over a short benchmark therefore
lands too few samples in the hot code to spread across source lines: you get a
handful of rows, not a dense per-line picture. **When the resolution is not
granular enough for the attribution you need, say so and escalate** - cheapest
first:

1. **Rule out inlining collapse first** (the bullet above). A fully-inlined
   callee piles its heat onto one call-site line at *any* sample rate, so a finer
   capture will not help until you split it. Cheap to check, so check it first.
2. **Lengthen the measured work** so more 10 ms ticks fall inside the hot path -
   profile a larger input or the long-running scenario rather than a
   microbenchmark. The fixed rate over a multi-second workload still accumulates
   thousands of samples and a stable per-line tree.
3. **Capture under ETW** (the one-command recipe above). Kernel CPU sampling
   defaults to **~1 ms (~1 kHz)** - roughly 10x denser than EventPipe's fixed
   ~100 Hz - and is true on-CPU time that also resolves native frames, so a
   genuinely short hot path spreads across far more source lines. This is the real
   lever for line-level depth. See [docs/performance-investigation.md](../../../docs/performance-investigation.md)
   sections 3b (`EtwProfiler`) and 3e (sample density).

The older `Profile-Benchmark.ps1` / `Get-TraceHotspots.ps1` scripts predate
filtrace and remain as a no-filtrace fallback (when the MCP server is unavailable
the `filtrace` CLI is the better path; plus `speedscope-to-flamegraph.ps1` for
SVG) in
[docs/performance-investigation-without-mcp.md](../../../docs/performance-investigation-without-mcp.md).

## Reading the line ranking - what the heat *shape* means

A correct, dense line ranking (a method's own `trace_lines`, or `trace_heatmap`
over its file) still has to be *interpreted*. Two shapes recur and point at
opposite levers - say which one you see, not just the top line:

- **The method's entry/prologue line dominates its own ranking -> it is called
  too often, not doing too much per call.** A method's first source line carries
  the prologue and the first (bounds-checked) field/argument access, so when it
  tops the method's *own* line ranking - and the next few field-bind lines pile in
  behind it - the cost is *invocation count*, not any one computation. Worked
  example: in `ProduceAlternative` (the negation backtracker) line 1006
  (`ref Frame frame = ref _frames[frameIdx]`, the prologue) was **39 %** of the
  method and the field binds on the next lines brought the entry preamble to
  **~50 %**. The lever is calling it fewer times (prune dead candidates earlier),
  not shaving its body. A *body*-line hotspot points the opposite way - optimize
  that computation.
- **A callee that recurs hot across several branches beats any single top line.**
  Scan the ranking for the same helper at multiple `file:line`s and sum them - one
  fix lands on every site. Same example: `CopyRanges` (the per-frame range
  snapshot) appeared at three call sites summing above most single lines, so a
  cheaper snapshot/restore is a better target than the single hottest line.

A `:0` or `<no source>` row is self-time the PDB could not map to a line (a
compiler-synthesized region, or a missing symbol), not a real line 0 - treat it as
unattributed, not a hotspot.

**Before trusting an *existing* trace, confirm the source has not moved since the
capture.** Compare the hot file's last-commit date
(`git log -1 --format=%ci -- <file>`) to the trace's timestamp; if the source
changed after the capture the line numbers are stale - recapture rather than read
them. (Reusing a still-matching trace is the right call - it skips a recapture -
but only after this check.)

## Handing off an interactive flame graph (optional)

The drills above - `trace_rank`, `trace_lines`, `trace_heatmap` - are the
practical answer to "where is the time" and should be offered first: they are
line-precise, fold the sampling artifacts, and need no viewer literacy. When the
*shape* of the call tree is the insight, or a human wants to explore the trace
interactively (or see the EventPipe-vs-ETW attribution flip side by side),
`trace_export` writes a flame graph for speedscope or Perfetto, and
[Open-SpeedscopeTrace.ps1](../filtrace/scripts/Open-SpeedscopeTrace.ps1) /
[Open-PerfettoTrace.ps1](../filtrace/scripts/Open-PerfettoTrace.ps1) open it in
the browser hands-free with the right view already active. See
[graphical-viewers.md](graphical-viewers.md) for when it is worth offering, which
viewer to pick, and how to guide the user once it is open. Scope a machine-wide
`.etl` on export with `--process <name>` (or the `process` MCP argument), the
same as the ranking verbs.
