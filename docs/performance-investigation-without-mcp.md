# Profiling without filtrace: the PowerShell fallback scripts

The primary way to turn a captured benchmark trace into ranked hotspots and
line-level attribution is the standalone
[filtrace](https://github.com/JeremyKuhne/filtrace) analyzer - its MCP tools for
an agent, or the `filtrace` CLI (fresh install:
`dotnet tool install -g KlutzyNinja.Filtrace --version 0.6.0`; existing install:
`dotnet tool update -g KlutzyNinja.Filtrace --version 0.6.0`) when the MCP server
is unavailable - see
[performance-investigation.md](performance-investigation.md) sections 3a, 3f,
and 6.

This document is the **last-resort fallback**: the committed PowerShell scripts
that do the same trace aggregation with no filtrace install at all (for an
environment where neither the filtrace CLI nor its MCP server is available, or
when you want a flame-graph SVG, which filtrace does not render). The
*conceptual* guidance - the JIT-helper folding traps and scope ambiguity -
lives in
[performance-investigation.md](performance-investigation.md) section 3a and
applies identically here; this doc only covers the script invocations.

## What each script does

| Script | Role | MCP equivalent |
| --- | --- | --- |
| `tools/Profile-Benchmark.ps1` | Run a benchmark under EventPipe **and** print folded self/inclusive rankings in one command (net10.0-only; refuses net481). | capture with `dotnet run ... -p EP` + the `cpu` verb / `trace_rank` |
| `tools/Get-TraceHotspots.ps1` | Aggregate an existing `.speedscope.json` into folded self/inclusive rankings; `-CallersOf <frame>` reports a frame's callers. | `trace_rank` (self/inclusive) / `trace_callers` |
| `tools/speedscope-to-flamegraph.ps1` | Render an inclusive flame-graph SVG. | none - use this, or drag the speedscope into <https://www.speedscope.app/> |

## One command: run + profile + ranked hotspots

```powershell
# Run the benchmark under EventPipe, then print accurate hotspot rankings.
./tools/Profile-Benchmark.ps1 `
    -Filter '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingleWithRoot' `
    -RootFrame 'RecordedDirectoryEnumerator.MoveNext' `
    -OutSvg scratch/extglob.svg          # SVG is optional

# Already have a fresh trace? Re-aggregate without re-running:
./tools/Profile-Benchmark.ps1 -Filter '*GlobEnumeratorExtGlobSingleWithRoot' `
    -RootFrame 'RecordedDirectoryEnumerator.MoveNext' -SkipRun

# Or analyze a specific trace directly:
./tools/Get-TraceHotspots.ps1 `
    -Path BenchmarkDotNet.Artifacts/<trace>.speedscope.json `
    -RootFrame 'RecordedDirectoryEnumerator.MoveNext'

# Confirm what a folded JIT-helper artifact is attributable to:
./tools/Get-TraceHotspots.ps1 `
    -Path BenchmarkDotNet.Artifacts/<trace>.speedscope.json `
    -RootFrame 'RecordedDirectoryEnumerator.MoveNext' `
    -CallersOf 'BulkMoveWithWriteBarrier'
```

`Get-TraceHotspots.ps1` folds the JIT-helper sampling artifacts (default
patterns: `CPU_TIME`, `UNMANAGED_CODE_TIME`, `BulkMoveWithWriteBarrier`,
`PollGC`, `Memmove`, `WriteBarrier`, `JIT_`) into the nearest non-folded
ancestor on each sample's stack - the PerfView `/FoldPats` operation done
headless. See section 3a Trap 2 in
[performance-investigation.md](performance-investigation.md) for why this is
mandatory before trusting any self-time number.

## Line-level attribution

The scripts stop at the method. For line-level (`file:line`) attribution there
is **no script fallback** - it requires the `filtrace` analyzer reading a
`.nettrace`/`.etl` with the matching PDB. See
[performance-investigation.md](performance-investigation.md) section 3f.
