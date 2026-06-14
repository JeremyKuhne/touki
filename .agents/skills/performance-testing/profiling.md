# Profiling a benchmark: from operation to method to line

Detail for the [performance-testing](SKILL.md) skill.

To find where a benchmark spends its time - optimizing a hot path or chasing a
regression - capture an EventPipe CPU trace on `net10.0`, then drill it with the
in-workspace [traceq](../../../traceq/README.md) analyzer. It reads the trace
through TraceEvent, folds the JIT-helper sampling artifacts, and ranks by method
or by source `file:line`. traceq is registered as an MCP server in
[.vscode/mcp.json](../../../.vscode/mcp.json), so **an agent calls its tools
directly** - `trace_info` first, then `trace_rank` / `trace_callers` /
`trace_lines` / `trace_heatmap`. The equivalent CLI verbs (below) are the manual
fallback. One capture serves every drill:

```powershell
# Capture once. --keepFiles preserves BDN's build so its PDB GUID survives for
# the line ranking below.
dotnet run -c Release -f net10.0 --project touki.perf -- `
    --filter '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingleWithRoot' -p EP --keepFiles

$trace = (Get-ChildItem BenchmarkDotNet.Artifacts `
    -Filter '*GlobEnumeratorExtGlobSingleWithRoot*.nettrace' |
    Sort-Object LastWriteTime | Select-Object -Last 1).FullName
# The exact build BDN profiled - its PDB GUID matches the trace:
$sym = 'artifacts/x64/Release/touki.perf/net10.0/touki.perf-DefaultJob-1/bin/Release/net10.0'

# Method ranking, scoped to a workload frame (which method owns the self-time).
dotnet run --project traceq/src/TraceQ -c Release -- cpu $trace --root 'RecordedDirectoryEnumerator.MoveNext' --top 25

# Line ranking inside the dominant method (which lines of its hot loop dominate).
dotnet run --project traceq/src/TraceQ -c Release -- lines $trace --method RunEngine --symbols $sym --top 30

# Who calls a folded JIT-helper artifact, to confirm what it's attributable to.
dotnet run --project traceq/src/TraceQ -c Release -- callers $trace 'BulkMoveWithWriteBarrier'
```

The MCP tools map onto those verbs one-to-one: `trace_rank` (with
`measure: self|inclusive`) for the method ranking, `trace_lines` for the line
ranking, `trace_callers` for the caller breakdown, and `trace_info` for the
load-and-summarize the other tools do implicitly.

Things that bite, kept short - full rationale in
[docs/performance-investigation.md](../../../docs/performance-investigation.md)
sections 3a (methods) and 3f (lines):

- **EventPipe is net10.0-only** - net481 has no EventPipe, so profile it under
  ETW instead (see [Capturing under ETW](#capturing-under-etw-net481-denser-samples-native-frames)
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
- **`--root` must be a frame inside the workload**, not the benchmark method
  name (that also matches BDN's `Activity Benchmark(...)` wrapper and pulls in
  idle threadpool threads).
- **Line ranking needs `--symbols` pointing at BDN's `...-DefaultJob-N/bin/...`
  build** - `touki.dll` ships its PDB embedded, and the symbols build's GUID
  must match the trace (hence `--keepFiles`). A wrong dir resolves frames to
  `<no source>` or is rejected with a GUID mismatch.
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
process-scoped next-step traceq commands:

```powershell
./tools/Capture-EtwTrace.ps1 -Filter '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingle'
```

**An `.etl` is a machine-wide capture - scope it to your process.** traceq
auto-scopes to the busiest process *by CPU sample count* (the quantity the
rankings consume), which is normally the benchmark; but a noisy background app
(antivirus, a VPN client) can own enough samples to win, so pass `--process <name>`
to pin it. **Every CPU verb takes it** - `cpu`, `rank`, `threadtime`, `lines`,
`callers`, `heatmap` (and the matching `trace_*` MCP tools). `traceq processes
<etl>` lists every process by weight so you can choose the right target. Quiesce
other CPU consumers before capturing, or scope explicitly.

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
  from this machine: a frame needs roughly **>=200-300 samples** for a trustworthy
  self-%, and **>=1,000 samples** - about **~10 s of cumulative time in that
  frame** at 100 Hz - before its *line* view spreads usefully. `trace_info`
  reports the total count; multiply by the frame's self-% to see whether it clears
  the bar. Under BDN's repetition you almost always clear the *method* bar; for a
  short hot path you usually miss the *line* bar.
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

The older `Profile-Benchmark.ps1` / `Get-TraceHotspots.ps1` scripts predate the
analyzer and remain as a no-MCP fallback (plus `speedscope-to-flamegraph.ps1`
for SVG) in
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
[tools/Open-SpeedscopeTrace.ps1](../../../tools/Open-SpeedscopeTrace.ps1) /
[tools/Open-PerfettoTrace.ps1](../../../tools/Open-PerfettoTrace.ps1) open it in
the browser hands-free with the right view already active. See
[graphical-viewers.md](graphical-viewers.md) for when it is worth offering, which
viewer to pick, and how to guide the user once it is open. Scope a machine-wide
`.etl` on export with `--process <name>` (or the `process` MCP argument), the
same as the ranking verbs.
