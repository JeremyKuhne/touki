# traceq

A small, agent-shaped CLI and MCP server for analyzing .NET CPU/memory/wall-clock
traces - the productized successor to `touki.mcp`. Built on the
`Microsoft.Diagnostics.Tracing.TraceEvent` library; reads EventPipe
(`.nettrace` / `.speedscope.json`) and ETW (`.etl`) captures from both .NET and
.NET Framework runs.

> **Incubation status.** This is the M0 scaffold of a self-contained subtree
> inside the `touki` repository. It is promoted to its own repository at the
> M3.5 gate. The full plan, surface area, and milestones live in
> [docs/traceq-implementation-plan.md](../docs/traceq-implementation-plan.md)
> (in the parent repo during incubation).

## Using traceq

Every verb takes a trace path and prints a dense text report (or compact JSON
with `--format json`). The canonical investigation is **rank -> drill -> compare**:
find the hot frames, drill into one, then diff against a baseline.

```pwsh
# Workflow: rank the hottest frames, drill into one, then diff two runs.
traceq cpu app.nettrace                      # 1. what's hot (self-time)
traceq callers app.nettrace MyApp.Parse      # 2. who calls the hot frame
traceq lines app.nettrace --symbols bin/Release/net10.0   # 3. hot source lines
traceq diff before.nettrace after.nettrace   # 4. what changed between runs
```

Install it as a local .NET tool from a `dotnet pack` output:

```pwsh
dotnet pack src/TraceQ/TraceQ.csproj -c Release
dotnet tool install --global --add-source ./artifacts/packages TraceQ.Tool
```

### Verbs

**Ranking** - rank stacks by a metric (`--metric` on `rank`, or a shortcut verb):

| Verb | What it ranks | Example |
|---|---|---|
| `rank` | Any metric (`--metric cpu\|alloc\|exceptions\|threadtime`) | `traceq rank app.nettrace --metric alloc` |
| `cpu` | CPU self-/inclusive-time | `traceq cpu app.nettrace --measure inclusive` |
| `alloc` | Bytes allocated, by site | `traceq alloc app.nettrace --top 10` |
| `exceptions` | Throw sites, by count | `traceq exceptions app.nettrace` |
| `threadtime` | Wall-clock (running + blocked), Windows `.etl` | `traceq threadtime app.etl` |

Every ranking verb accepts `--root` (scope to a frame subtree) and `--benchmark`
(scope a BenchmarkDotNet capture to the measured workload, past the harness). The
verbs that can read a multi-process ETW `.etl` - `cpu`, `threadtime`, and `rank`,
plus the drill-down `callers`, `lines`, and `heatmap` - also accept `--process` /
`--all-processes` (the busiest process tree, ranked by CPU sample count, is
auto-scoped by default); `alloc` and `exceptions` read single-process
`.nettrace` only, so they have no process options. To see what is in a
multi-process capture before scoping, run `traceq processes` (below).

```pwsh
traceq cpu bdn.nettrace --benchmark          # just the [Benchmark] code
traceq alloc bdn.nettrace --benchmark        # allocations under the workload
traceq processes machinewide.etl             # list every process by weight
traceq cpu machinewide.etl --process MyApp   # one process tree
```

**Native runtime symbols.** Managed frames (including NGEN and ReadyToRun
framework methods) resolve for free from the trace's CLR rundown. The *unmanaged*
runtime frames - the GC, the JIT, `memset` / `memcpy`, write barriers - need PDBs
from the Microsoft public symbol server, which `cpu` / `rank` fetch only when you
opt in with `--native-symbols` (cached under `--symbol-cache`, default in the temp
path). It is off by default so analysis stays offline and deterministic; the first
run downloads, later runs hit the cache.

```pwsh
traceq cpu app.etl --process MyApp --native-symbols   # name the GC/JIT/memcpy frames
```

**Drill-down** - follow a ranking into detail:

| Verb | Purpose | Example |
|---|---|---|
| `callers` | Immediate callers of a frame | `traceq callers app.nettrace MyApp.Parse` |
| `lines` | Hottest source lines of scoped methods | `traceq lines app.nettrace --symbols bin/Release/net10.0` |
| `heatmap` | Per-line heat for one source file | `traceq heatmap app.nettrace Parser.cs` |
| `tree` | Top-down call tree from the root | `traceq tree app.nettrace --max-depth 5` |

**Inventory** - see what a (possibly machine-wide) capture contains:

| Verb | Purpose | Example |
|---|---|---|
| `processes` | List processes by CPU-sample weight, to pick a `--process` target | `traceq processes machinewide.etl` |
| `classify` | Summarize CPU time by runtime work category (zeroing / copying / GC / ...) | `traceq classify app.etl --native-symbols` |

**Compare and export:**

| Verb | Purpose | Example |
|---|---|---|
| `diff` | What got slower/faster between two traces | `traceq diff before.nettrace after.nettrace` |
| `export` | Write a flame graph (speedscope / chromium) | `traceq export app.nettrace --format speedscope -o app.json` |

**Structured reports** (EventPipe `.nettrace`):

| Verb | Purpose | Example |
|---|---|---|
| `gcstats` | GC counts, pauses, heap summary | `traceq gcstats app.nettrace` |
| `jitstats` | JIT method count, compile time, sizes | `traceq jitstats app.nettrace` |
| `events` | Query raw events by name, paged | `traceq events app.nettrace --name GC/AllocationTick` |

**File ops** - manage the ETLX conversion cache TraceEvent keeps beside a trace:

| Verb | Purpose | Example |
|---|---|---|
| `convert` | Build the ETLX cache up front | `traceq convert app.nettrace` |
| `clean` | Remove the ETLX cache to force a rebuild | `traceq clean app.nettrace` |

Run `traceq <verb> --help` for the full option set of any verb.

## Layout

| Path | Purpose |
|---|---|
| `src/TraceQ.Core/` | Analysis core: trace readers, stack-source providers, the provider-agnostic question-service engine. The only place logic lives. |
| `src/TraceQ/` | CLI host (`traceq`); the `traceq mcp` verb hosts the server. |
| `src/TraceQ.Mcp/` | Thin shim package over the same core assembly. |
| `tests/TraceQ.Core.Tests/` | Unit + golden-file contract tests. |
| `tests/TraceQ.Parity.Tests/` | Numeric parity against the frozen legacy oracles. |
| `eval/` | Headless-agent eval harness, tasks, baselines (M5). |
| `docs/` | Single-source workflow text for the skill / README / help (M4). |
| `skills/traceq/` | The shipped agent skill. |

## Self-containment

The subtree carries its own `Directory.Build.props`, `Directory.Build.targets`,
`Directory.Packages.props`, `global.json`, and `.editorconfig` (`root = true`),
none of which inherit from the parent `touki` repo. Nothing outside `traceq/`
references in, and nothing inside references a `touki` project. Promotion is a
plain file copy.

## Build and test (standalone)

```pwsh
cd traceq
dotnet build traceq.slnx
dotnet test traceq.slnx
```
