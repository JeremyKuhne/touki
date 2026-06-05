# Profiling a benchmark: from operation to method to line

Detail for the [performance-testing](SKILL.md) skill.

To find where a benchmark spends its time - optimizing a hot path or chasing a
regression - capture an EventPipe CPU trace on `net10.0`, then drill it with the
in-workspace [touki.mcp](../../../touki.mcp/touki.mcp.csproj) analyzer. It reads
the trace through TraceEvent, folds the JIT-helper sampling artifacts, and ranks
by method or by source `file:line`. One capture serves both:

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
dotnet run --project touki.mcp -c Release -- analyze $trace --root 'RecordedDirectoryEnumerator.MoveNext' --top 25

# Line ranking inside the dominant method (which lines of its hot loop dominate).
dotnet run --project touki.mcp -c Release -- analyze $trace --lines RunEngine --symbols $sym --top 30

# Who calls a folded JIT-helper artifact, to confirm what it's attributable to.
dotnet run --project touki.mcp -c Release -- analyze $trace --callers 'BulkMoveWithWriteBarrier'
```

An agent that speaks MCP calls the equivalent tools directly (`hotspots_self`,
`hotspots_inclusive`, `hot_lines`, `callers_of`, `load_trace`, `list_threads`).

Things that bite, kept short - full rationale in
[docs/performance-investigation.md](../../../docs/performance-investigation.md)
sections 3a (methods) and 3f (lines):

- **EventPipe is net10.0-only** - net481 needs `[EtwProfiler]` + admin.
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
  two call-site lines, scope `--lines` to the callee (`--lines ExtGlobEngine`)
  or add a temporary `[MethodImpl(MethodImplOptions.NoInlining)]`.

The older `Profile-Benchmark.ps1` / `Get-TraceHotspots.ps1` scripts predate the
analyzer and remain as a no-MCP fallback (plus `speedscope-to-flamegraph.ps1`
for SVG) in
[docs/performance-investigation-without-mcp.md](../../../docs/performance-investigation-without-mcp.md).
