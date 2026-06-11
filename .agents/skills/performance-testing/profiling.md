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
- **Line attribution stops at inlined boundaries** - a fully-inlined callee
  collapses onto its caller's call-site line. If the ranking piles onto one or
  two call-site lines, scope `--method` to the callee (`--method ExtGlobEngine`)
  or add a temporary `[MethodImpl(MethodImplOptions.NoInlining)]`.

## Capturing under ETW (net481, denser samples, native frames)

EventPipe is net10-only and managed-only. Reach for an ETW (`.etl`) capture in
three situations - the first is the one that bites hardest:

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
