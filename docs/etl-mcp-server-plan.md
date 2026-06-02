# ETL/trace analysis MCP server plan for `touki`

> **Workflow rule (non-negotiable).** No commits, pushes, or pull requests
> are to be made while executing this plan without **explicit user consent**
> for that specific action. Local edits and local build/test runs are fine;
> publishing of any kind (`git commit`, `git push`, opening or updating a PR)
> requires an explicit publishing verb from the user first. This mirrors the
> repository's [AGENTS.md](../AGENTS.md) "Working with the user on changes"
> contract.

A design plan for a Model Context Protocol (MCP) server that lets an AI coding
agent read a profiling trace produced by `touki`'s benchmarks and get back
structured, accurate "where did the time go" answers - **headless, no GUI, on
both `net10.0` and `net481`**.

This is a plan, not an implementation. It captures every requirement the server
must satisfy so the build can proceed without re-deriving the investigation
needs. Read [performance-investigation.md](performance-investigation.md) first
for the profiling workflow this server slots into.

---

## 1. Why build this

### 1.1 The gap this closes

`touki` already has an agent-readable profiling path **on net10 only**:

- BenchmarkDotNet's `[EventPipeProfiler]` / `-p EP` writes a
  `.speedscope.json` (and `.nettrace`) to `BenchmarkDotNet.Artifacts/`.
- [tools/Get-TraceHotspots.ps1](../tools/Get-TraceHotspots.ps1) aggregates that
  speedscope into accurate self-time + inclusive rankings, **folding the
  JIT-helper sampling artifacts** back into the real methods (see §1.2).
- [tools/Profile-Benchmark.ps1](../tools/Profile-Benchmark.ps1) wraps the run +
  aggregation into one command, and
  [tools/speedscope-to-flamegraph.ps1](../tools/speedscope-to-flamegraph.ps1)
  renders an SVG.

The **net481 path has no equivalent.** `[EventPipeProfiler]` resolves to
`UnresolvedDiagnoser` on Framework; the only sampled-CPU option is
`[EtwProfiler]`, which:

- requires **administrator** (kernel `SampledProfile` needs
  `SeSystemProfilePrivilege`),
- emits a `.etl` that **`dotnet-trace` cannot read**, and
- today can only be opened in the **PerfView GUI** - not agent-readable.

So the asymmetry is: an agent can self-serve net10 hotspots but must hand a
human the net481 `.etl`. This server's primary job is to **make `.etl` as
agent-readable as `.speedscope.json` already is**, closing the net481 gap.

### 1.2 The folding requirement is domain-specific

The non-negotiable behavior that makes our trace readings *correct* is folding
two classes of frame into the nearest real method on each sample's stack:

1. The synthetic `CPU_TIME` / `UNMANAGED_CODE_TIME` leaf markers BenchmarkDotNet
   writes (an unfolded self-time reading reports ~100% `CPU_TIME`).
2. **JIT-helper thunks** the managed-only stack walker mis-resolves to:
   `System.Buffer.BulkMoveWithWriteBarrier`, `Thread.PollGCWorker`,
   `Buffer.Memmove`, write barriers, GC-poll thunks at loop back-edges. These
   appear to dominate but are really attributable to the hot loop that invoked
   them. This trap is documented in
   [dotnet-perf-discoveries.md](dotnet-perf-discoveries.md) §8 and is the whole
   reason `Get-TraceHotspots.ps1` exists.

No off-the-shelf trace tool folds these the way we need (PerfView can with
`/FoldPats`, but only via the GUI or a bespoke CLI invocation). **The folding
algorithm and its default fold list are the core IP this server must carry
over from the PowerShell script.**

### 1.3 What this is for

This is internal optimization tooling for the `touki` library. It is *not* a
shipped product, not part of the `KlutzyNinja.Touki` NuGet package, and not on
the public API surface. It exists to let agents (and humans) answer "which
`touki` method dominates this benchmark on this TFM" without a GUI.

---

## 2. Why not just adopt an existing server

Microsoft ships **no** trace-analysis MCP server (their first-party
telemetry servers - Sentinel, Fabric RTI, Clarity, App Insights via the Azure
server - are all cloud/KQL surfaces, not local-file analyzers).

`wpa-mcp` is well-built and, notably, runs on the **same
`Microsoft.Diagnostics.Tracing.TraceEvent` library** we would use. It is a
strong reference implementation and proves the approach. But it does not fit our
need as-is:

| Need | `wpa-mcp` | Our requirement |
| --- | --- | --- |
| Fold JIT-helper thunks into real methods | No | **Mandatory** (§1.2) |
| Read `.nettrace` / `.speedscope.json` (net10 EventPipe) | No (`.etl` only) | Required for one tool across both TFMs |
| BenchmarkDotNet-aware root-frame scoping | No | Required (the `Activity Benchmark(...)` wrapper gotcha, §4) |
| Surface shape | Broad kernel ETW (waits, I/O, registry, ALPC, DPC) | Narrow: managed CPU self/inclusive + caller/callee |
| Distribution | Per-machine install, external repo | In-repo tool, versioned with `touki` |

**Decision options (confirm with user before building):**

- **A. Build our own narrow server** reusing `Get-TraceHotspots.ps1`'s folding
  logic, on TraceEvent, covering both `.etl` and `.nettrace`. *Recommended* -
  smallest surface, exactly our semantics, in-repo.
- **B. Adopt `wpa-mcp` and contribute folding upstream.** Lower build cost, but
  pulls a broad external dependency and a feature we would have to land in
  someone else's PoC, and still does not read `.nettrace`.
- **C. Keep the PowerShell scripts, skip MCP.** Zero build, but leaves the
  net481 `.etl` gap open and is not a structured tool surface.

The rest of this plan assumes **Option A**.

---

## 3. Investigation needs the server MUST satisfy

A concrete acceptance checklist. The server is "done enough" for our purposes
when an agent can, with no GUI:

1. **Load a trace** in any of these formats and get back metadata + a
   capability/quality summary:
   - `.etl` (net481 `[EtwProfiler]`, Windows + admin to *capture*, not to read),
   - `.nettrace` (net10 `[EventPipeProfiler]`),
   - `.speedscope.json` (what `Get-TraceHotspots.ps1` reads today).
2. **Top self-time ranking**, folded per §1.2, crediting time to the real
   method. Must reproduce `Get-TraceHotspots.ps1`'s validated numbers on the
   existing trace (RunEngine ~50.9%, RunEngineDirectory ~42.2%, etc.).
3. **Inclusive-time ranking**, skipping folded frames.
4. **Caller/callee drill** on a focus frame (the `-CallersOf` mode: confirm what
   a `BulkMoveWithWriteBarrier`-style artifact is really attributable to).
5. **Root-frame scoping** to a substring, attributing time only to the subtree
   rooted at the first matching frame - with the **BenchmarkDotNet wrapper
   caveat baked in**: the workload is wrapped in an
   `Activity Benchmark(...benchmarkName=Foo...)` frame whose name *contains* the
   method name, so naive scoping pulls in idle threadpool threads. Scope to a
   frame inside the workload (e.g. an enumerator `MoveNext`).
6. **Configurable fold list**, defaulting to the same patterns as the script
   (`CPU_TIME`, `UNMANAGED_CODE_TIME`, `BulkMoveWithWriteBarrier`, `PollGC`,
   `Memmove`, `WriteBarrier`, `JIT_`), extensible per call.
7. **Symbol resolution** good enough that managed `touki` frames resolve to
   `Namespace.Type.Method` (not `module!?`).
8. Output **structured JSON** (ms + percent-of-scope rows), token-bounded by a
   `Top` parameter (default 25).

If all eight hold on both a net10 `.nettrace` and a net481 `.etl` of the same
benchmark, the net481 gap is closed.

---

## 4. Scope and non-goals

**In scope**

- Sampled-CPU analysis of managed code: self-time, inclusive, caller/callee.
- The three input formats in §3.1.
- JIT-helper + synthetic-marker folding.
- BenchmarkDotNet-aware root scoping.
- Symbol path configuration.

**Out of scope (at least initially)**

- Kernel ETW domains `wpa-mcp` covers: scheduler waits, file/disk/mmap I/O,
  registry, ALPC, DPC/ISR, hard faults, virtual/heap alloc. Not what `touki`
  optimization work needs.
- CLR GC/allocation/exception/contention event analysis. (Possible Phase 4 -
  allocation hotspots would complement `[MemoryDiagnoser]`.)
- Trace *capture*. The server reads traces; capture stays with BenchmarkDotNet
  (`-p EP` / `[EtwProfiler]`) and `Profile-Benchmark.ps1`.
- Linux `perf` / jitdump. Covered by the WSL path in
  [performance-investigation.md](performance-investigation.md); the MCP server
  is a Windows-first `.etl`/`.nettrace` reader.

---

## 5. Architecture

### 5.1 Stack

- **Language/runtime:** C# on `net10.0`. Self-contained Windows executable
  (`win-x64`) so no SDK is required on the machine that runs it.
- **Trace parsing:** `Microsoft.Diagnostics.Tracing.TraceEvent` for `.etl` and
  `.nettrace` (the library PerfView is built on; `.etl` kernel parsers are
  Windows-only, `.nettrace` parsing is portable). `.speedscope.json` is parsed
  directly (it is the JSON the script already reads) so the existing validated
  path keeps working without a TraceEvent round-trip.
- **MCP transport:** stdio MCP server using the .NET MCP SDK
  (`ModelContextProtocol` packages, the same surface `microsoft/mcp` servers
  use).
- **Folding core:** port the algorithm from
  [tools/Get-TraceHotspots.ps1](../tools/Get-TraceHotspots.ps1) - the
  self-time loop that walks the leaf past folded frames, the inclusive loop that
  skips them, and the caller-credit logic - into a small, unit-tested C# class
  shared by all three input adapters.

### 5.2 Project layout

- New project, e.g. `tools/EtlMcp/EtlMcp.csproj` (or `touki.mcp/`), **excluded
  from `touki.slnx` packaging** and from `InternalsVisibleTo`. It depends on
  nothing in `touki` and `touki` depends on nothing in it.
- Follows repo MSBuild conventions
  ([.github/instructions/msbuild.instructions.md](../.github/instructions/msbuild.instructions.md))
  for any `.csproj`/`.props` it adds.
- C# files carry the standard MIT header; the folding core gets real unit tests
  (xUnit, matching `touki.tests` conventions) that pin the validated numbers
  from §3.2 as a regression oracle.

### 5.3 Data flow

```
.etl  ──(TraceEvent, Windows)──┐
.nettrace ─(TraceEvent)────────┼──► normalized sample-stack model ──► FoldingAggregator ──► JSON rows
.speedscope.json ─(direct)─────┘                                          ▲
                                                                  fold list + root frame
```

All three adapters produce the same normalized "stack of frames per weighted
sample" model; the folding aggregator and the MCP tool layer are
format-agnostic.

---

## 6. MCP tool surface

Mirror `Get-TraceHotspots.ps1`'s modes, plus a load/orient step. Names and
parameters are a starting proposal.

| Tool | Purpose | Key params |
| --- | --- | --- |
| `load_trace` | Open `.etl`/`.nettrace`/`.speedscope.json`; cache parsed model; return format, duration, thread/process list, sample count, symbol-resolution rate, quality warnings. | `path` |
| `hotspots_self` | Top-N self-time, folded. | `path`, `rootFrame`, `fold[]`, `top` |
| `hotspots_inclusive` | Top-N inclusive, folded frames skipped. | `path`, `rootFrame`, `fold[]`, `top` |
| `callers_of` | Immediate callers of a focus frame, time each contributes (the `-CallersOf` mode). | `path`, `frame`, `rootFrame`, `top` |
| `callees_of` | Inverse drill (frames the focus calls). Phase 2. | `path`, `frame`, `rootFrame`, `top` |
| `list_threads` | Per-thread sample counts, to pick a `rootFrame` or spot idle threadpool noise. | `path` |
| `set_symbol_path` / `diagnose_symbols` | Configure `_NT_SYMBOL_PATH`; report per-module resolution status. | `path` (symbols) |

Design rules (borrowed from the `wpa-mcp` design philosophy, which works well):

- `load_trace` exposes capabilities/quality up front so the agent picks the next
  call from real signals, not from empty results.
- Every ranking tool returns rows as `{ frame, selfMs|inclusiveMs, pctOfScope }`
  bounded by `top`, so output stays token-cheap.
- No synthesized "root cause" field - return the evidence, let the agent reason.

---

## 7. Trace acquisition (the capture side, for reference)

The server reads; these produce what it reads. Captured in
[performance-investigation.md](performance-investigation.md), summarized here:

- **net10:** `dotnet run -c Release -f net10.0 --project touki.perf -- --filter
  <Filter> -p EP` writes `.speedscope.json` + `.nettrace`. Already wrapped by
  [tools/Profile-Benchmark.ps1](../tools/Profile-Benchmark.ps1).
- **net481:** `[EtwProfiler]` on the benchmark, run from an **elevated**
  shell, writes a `.etl`. There is **no non-admin path** to ETW kernel
  CPU-sampled stacks (the Performance Log Users group does not grant the kernel
  sampled profile). This admin requirement is a capture-time constraint only;
  reading the resulting `.etl` needs no elevation.

A follow-up may extend `Profile-Benchmark.ps1` to drive the net481
`[EtwProfiler]` capture and then hand the `.etl` to the server, giving one
command for both TFMs.

---

## 8. Symbols

- Managed `touki` frames need portable PDBs on the symbol path. Ensure Release
  builds keep `<DebugType>portable</DebugType>` + `<DebugSymbols>true</DebugSymbols>`
  and that PDB/DLL signatures match the profiled build.
- `_NT_SYMBOL_PATH` (or a per-call symbol arg) for BCL/OS modules:
  `SRV*<cache>*https://msdl.microsoft.com/download/symbols`.
- `diagnose_symbols` should flag a low resolution rate; treat
  `ResolutionRate < 0.8` as "results are garbage, fix symbols first" - the same
  threshold `wpa-mcp` uses and the single biggest source of misleading output.
- The `.speedscope.json` path already carries resolved names, so it needs no
  symbol setup - a useful fallback when symbol resolution on `.etl`/`.nettrace`
  is troublesome.

---

## 9. Phased rollout

**Phase 0 - Port and pin the folding core.**
Extract the folding algorithm + default fold list from
`Get-TraceHotspots.ps1` into a tested C# class. Feed it the existing
`.speedscope.json` and assert it reproduces the validated rankings (§3.2).
No MCP yet. This de-risks the only novel logic.

**Phase 1 - Speedscope MCP server.**
Wrap the core in a stdio MCP server with `load_trace`, `hotspots_self`,
`hotspots_inclusive`, `callers_of`, scoped to `.speedscope.json`. At this point
the server matches the PowerShell script's capability, as a structured tool.

**Phase 2 - `.nettrace` via TraceEvent.**
Add the TraceEvent adapter for `.nettrace`; normalize to the same sample model.
Verify net10 `.nettrace` and its sibling `.speedscope.json` agree.

**Phase 3 - `.etl` (closes the net481 gap).**
Add the TraceEvent `.etl` adapter and symbol tooling. Capture a net481
`[EtwProfiler]` trace of a benchmark and confirm the eight acceptance criteria
(§3) hold. **This is the milestone that delivers the primary value.**

**Phase 4 (optional) - CLR allocation hotspots.**
`GCAllocationTick`-based allocation-by-type/stack, complementing
`[MemoryDiagnoser]`. Only if a real need appears.

---

## 10. Risks and open questions

- **net481 `.etl` stack quality.** `[EtwProfiler]` stacks must be complete
  enough (kernel stackwalk + managed symbol resolution) to attribute to `touki`
  methods. Validate early in Phase 3; if attribution is poor, the net10
  EventPipe path may remain the better signal and the `.etl` reader becomes a
  secondary cross-check rather than the primary net481 answer.
- **Folding parity across formats.** The fold list was tuned against
  EventPipe/speedscope frame names. ETW frame names (module-qualified, kernel
  frames present) may need an expanded default list. Keep the list per-call
  overridable and document the ETW additions as they are found.
- **MCP SDK choice/stability.** Confirm the .NET MCP SDK version and packaging
  story (self-contained exe + client config) before Phase 1.
- **Maintenance cost vs. `wpa-mcp`.** Revisit Option B (§2) if our narrow
  server's TraceEvent/symbol plumbing grows to rival what `wpa-mcp` already
  solved. The folding feature could in principle be contributed upstream.
- **Decision to confirm with the user:** Option A vs B vs C (§2), the project
  name/location, and whether `.etl` capture should be folded into
  `Profile-Benchmark.ps1`.

---

## 11. Reference material

- [tools/Get-TraceHotspots.ps1](../tools/Get-TraceHotspots.ps1) - the folding
  algorithm and default fold list to port.
- [tools/Profile-Benchmark.ps1](../tools/Profile-Benchmark.ps1) - the net10
  capture+aggregate wrapper to mirror/extend.
- [performance-investigation.md](performance-investigation.md) - the profiling
  workflow this server slots into (EventPipe vs ETW, the net481 gap, symbols).
- [dotnet-perf-discoveries.md](dotnet-perf-discoveries.md) §8 - why the
  JIT-helper folding is mandatory.
- `Microsoft.Diagnostics.Tracing.TraceEvent` - the parsing library for
  `.etl`/`.nettrace`.
