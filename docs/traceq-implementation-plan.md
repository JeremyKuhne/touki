# `traceq` implementation plan

**Status:** Active — Q1–Q3 resolved (§6)
**Progress (2026-06-09):** M0 complete (PR #182). M1's analysis core is complete and M2 (the CLI head) is the active milestone. M1 landings - core relocation merged (PR #183); provider/engine seam laid (`StackSampleSource` + `MetricInfo`); output-contract envelope landed (`AnalysisResult<T>` + compact, rounded, deterministic JSON with golden tests); symbol gate landed (`SymbolGate`; `--strict`/exit-3 deferred to M2); tier-2 LRU landed (`LruCache`); token budget landed (`OutputBudget`; truncation + text renderer deferred to M2); fixture corpus + parity harness landed for the net10 EventPipe half (PR #186); allocation provider landed (`AllocationProvider` reads `GCAllocationTick` into byte-weighted stacks ranked by the same metric-generic engine - `SampleStack.Weight`); GC-stats provider landed (`GcStatsProvider` reads `TraceGC` structured records, reusing the GC-verbose fixture). **ThreadTime landed (ETW):** the earlier EventPipe spike was a strictly worse CPU view (no `BLOCKED_TIME`, since EventPipe samples only running threads), but the net481 ETW capture carries context switches, so `ThreadTimeProvider` reconstructs each thread's running and blocked intervals into elapsed-millisecond stacks (`MetricInfo.ThreadTime`) the metric-generic engine ranks unchanged. Export engine verb landed (`SpeedscopeExporter` writes any provider's stacks to the speedscope sampled format, metric-aware unit). Filter/scope grammar subset landed (`ScopeFilter` include/exclude on frame names; sample-level time/process scoping deferred for model + fixture reasons). Diff engine verb landed (`RankingDiff` compares two rankings - provider-agnostic regression/improvement deltas). Group transform landed (`GroupTransform` collapses a matched module's frames into a `module!` box). Chromium export landed (`ChromiumExporter` writes any provider's stacks to the Chrome Trace Event Format). EventQuery provider landed (`EventQueryProvider` paginated raw-event query with payload cap). JitStats provider landed (`JitStatsProvider` per-method JIT compile-time / size / tier records, with a tuned `JitLoop` smoke fixture). Steering-hint taxonomy landed (`SteeringHints` turns a ranking, callers, or diff result into the canonical next-step nudge). The net481 ETW (`.etl`) corpus half landed: an `EtwLoop` benchmark captured under the ETW profiler with context-switch keywords, a process-tree relog `trim` (native-only; the managed-JIT limit and the physical-trim follow-up are captured in [traceq-etl-trimming.md](traceq-etl-trimming.md)), and a committed ~1 MB multi-process scenario fixture. Process-tree scoping landed (`ProcessScope` + read-time filtering in the ETL/EventPipe reader): a machine-wide capture is scoped losslessly to the workload process tree and its children at analysis time, resolving the workload's managed frames. The tier-1 ETLX disk cache is deferred with reason (TraceEvent already caches `.nettrace`/`.etl` -> `.etlx` on both paths; a custom cache adds nothing measurable without the elevated `.etl` half). ThreadTime landed over the ETW capture (`ThreadTimeProvider`: running + blocked intervals into elapsed-millisecond stacks). The O1 cross-machine hand-off spike passed: a Windows-converted `.etlx` resolves managed frames byte-identically on Ubuntu 26.04, so the hand-off is advertised (only the `.etl` -> `.etlx` conversion stays Windows-only). Exceptions provider landed (`ExceptionsProvider` reads `Exception/Start` events into count-weighted throw-site stacks). The text renderer (landed in M2) and the grouping altitude (`GroupTransform`, landed in M1) are no longer outstanding, so the only deferred M1 work is the sample-level time- and process-filter altitudes (M1 step 5, parked for want of fixture data - distinct from the read-time `ProcessScope`, which is already built). **M2 (CLI head):** the engine verbs `rank`/`cpu`/`callers`/`lines`/`heatmap`/`diff`/`tree`/`export` have merged (PRs #195-198); the fifth slice has wired the provider-selection seam (a `TraceMetric` selector threaded through `TraceStore` -> `TraceLoader`, which dispatches to each provider and synthesizes the `TraceInfo` for the non-CPU families) and lit up the three stack-source family shortcuts - `alloc`, `exceptions`, and `threadtime` - each selectable as `rank --metric <name>` or its own verb (PR #199), and added the three report verbs `gcstats` / `jitstats` / `events` (structured records rather than rankable stacks, each with its own executor and renderer), and wired the `--process` / `--all-processes` scope options onto the stack-ranking verbs (a `ScopeRequest` intent the loader resolves to a process tree, defaulting to the busiest process automatically). M2's verb surface is complete, and the CLI head is closed out: the `--benchmark` scope option (frame-based, presets the root to the BenchmarkDotNet workload wrapper) and the `convert` / `clean` file-op verbs landed, the CLI packages as a local `dotnet tool` (an exit criterion), and a CI help-lint guards the help/README contract. `heap` stays unbuilt (no provider yet, Addendum A capture work); `trim` stays parked (the relog rewrite resolves native frames only - see traceq-etl-trimming.md); the eval-task exit criterion rides the M5 harness. See the M2 section for detail.
**Date:** 2026-06-04
**Basis:** *Agentic access to TraceEvent — design document* (2026-06-04). Section references (§) and decisions (D#) below point at that document.
**Resolved path:** incubate in Touki under a self-contained `traceq/` subtree and **promote at the M3½ gate** · **private GitHub Packages feed until v1.0** (public NuGet + MCP registry at 1.0) · **retire `touki.mcp` at parity**, Touki references `TraceQ.Core`.

**Revision 2026-06-06 (this copy):** relocated into `touki/docs/` (version-controlled with the repo it incubates in). Two changes fold in: (1) **the `touki.mcp` git history is not worth preserving** - the move is a plain file copy, not a `git mv`/`filter-repo` history carry, which simplifies M1 and M3-and-a-half; (2) a **thorough surface-area pass** informed by the PerfView field guide and its automation companion ([documentation/perfview-trace-analysis-guide.md](https://github.com/microsoft/perfview), [documentation/perfview-automation-guide.md](https://github.com/microsoft/perfview)), widening the plan from the CPU-stack slice `touki.mcp` ships today to the full TraceEvent analysis surface, the scenario-trimming workflows the user asked for, and the export/visualization bridges. New material: the surface-area section below, expanded M1/M2 verb sets, and Addenda A-B.

---

## 0. Approach

This is an **extraction-and-hardening project, not a greenfield build.** The correctness core the design protects — the folding aggregator, the three trace readers over `TraceLog`, the keyed trace store, embedded-PDB extraction, the source annotator — already exists and works in `touki.mcp` (~1.6k LOC, MIT, yours). The reference semantics it must reproduce exist twice more (`Get-TraceHotspots.ps1`, the touki.mcp tool outputs), which makes a *numeric parity harness* the cheapest possible safety net for the move.

**The surface is much larger than CPU stacks, and this pass widens the plan to match it.** PerfView's whole design reduces to one idea (its field guide's section 1.4): nearly every investigation is a list of **{time, metric, stack}** samples rolled into a tree, where only *what the metric means* and *what the stack frames are* change. That makes traceq's natural architecture a set of interchangeable **stack-source providers** - CPU, wall-clock / blocked time (Thread Time), allocation, net surviving heap, retention (heap snapshots), exceptions, arbitrary events - feeding **one** rank / callers / call-tree / hot-lines / filter / diff / export engine. `touki.mcp` ships only the CPU provider today; the same `TraceLog` foundation reaches every other family through TraceEvent's public computers and stack writers, with two genuine capture-side gaps (net-mem simulation, heap-snapshot capture) catalogued in Addendum A. The surface-area section below enumerates the families, the engine, and the workflows; M1 and M2 grow the service and verb sets to cover them.

**The consumer is an agent mid-investigation, and that intent shapes every verb.** It wants the *smallest relevant slice*, deterministic compact output, and a nudge to the next step - not a machine-wide firehose. Two corollaries this pass makes first-class. First, **scenario scoping is the default, not an option**: every analysis verb auto-scopes to one process and the workload subtree and drops the Idle process and unrelated activity, and accepts the full filter / group / fold / time-range grammar to tighten further (the field guide's section 6 "turn millions of samples into an answer"). Second, a **`trim` verb physically rewrites** an oversized machine-wide capture into a small scenario-scoped trace - one process, one time window, the relevant providers - that is far cheaper to re-analyze and to share. Both are the user-requested "trim captures to the target scenario, filter out irrelevant process information" workflow, expressed at the two altitudes (virtual at analysis time, physical as a derived file).

The plan is therefore organized as: extract the core under the new identity → wrap it in the output contract → grow the CLI head to the full verb set → re-cut the MCP facade to the curated eight → ship the knowledge layer and packages → stand up the eval harness and tune against it → migrate Touki onto the result. Milestones gate on **exit criteria, not dates**; every milestone ends with something an agent can actually use.

One sequencing principle throughout: **the eval harness is the dev loop.** The same headless-agent runs that will guard regressions in §10 of the design are how descriptions, help text, and the skill get written in the first place — drafted by agents, scored by the harness, reviewed by you.

---

## 1. Assets in hand

| Asset | Location today | Feeds | Reuse mode |
|---|---|---|---|
| `FoldingAggregator` (self/inclusive/callers/lines/heatmap + default fold list) | `touki/touki.mcp/Tracing` | Core §4.1–4.2 | Plain file copy into `TraceQ.Core` (history not preserved - deliberate) |
| Readers: `NetTraceReader`, `EtlReader`, `SpeedscopeReader`, `TraceLogReader` base | `touki/touki.mcp/Tracing/Readers` | Trace access layer | Copy as-is; this is the `TraceLog` gateway every new family reuses |
| `TraceStore` (keyed, case-sensitivity-aware) | `touki/touki.mcp/Server` | Tier-2 cache §4.3 | Move; add LRU eviction |
| `EmbeddedPdbExtractor`, `SourceAnnotator` | `touki/touki.mcp/Tracing` | Symbol pipeline §4.2 | Copy; extend with `SymbolReader` lookup + SourceLink (surface-area section) |
| 7 engineered tool descriptions (sequencing, gates, BDN trap) | `touki/touki.mcp/Server/TraceTools.cs` | MCP facade §5.2 | Port text; re-cut to 8-tool surface (D5 consolidation) |
| Reference semantics for parity | `tools/Get-TraceHotspots.ps1`, touki.mcp `analyze` | §3 parity harness | Oracle only |
| Workflow + trap knowledge | `docs/performance-investigation*.md`, `docs/etl-mcp-server-plan.md`, `.agents/skills/performance-testing/SKILL.md` | Knowledge layer §6 | Rewrite, single-sourced |
| PerfView field guide + automation companion (workflows, the TraceEvent API map, the PerfView-only gaps) | `perfview/documentation/perfview-trace-analysis-guide.md`, `…/perfview-automation-guide.md` | Surface-area section; M1/M2 verbs; Addendum A | Reference only - the API-to-workflow map and gap inventory |
| Repo engineering infrastructure | Touki's AGENTS.md / copilot-instructions + CI enforcement, Trusted Publishing setup | M0 scaffold | Seed and trim |
| `pvanalyze` patterns (disk ETLX cache w/ locking; help/README stance; bounded verbs) | external repo | §4.3, §5.1 | **Pattern reimplementation only — the repo carries no LICENSE file, so its code is all-rights-reserved by default. Nothing is copied; the patterns are trivial to re-derive and ours differ anyway.** |
| Eval methodology | Zechner harness write-up; mcp-builder eval guide | M5 | Methodology only |

---

## 1A. The analysis surface: families, the engine, and scenario-scoping

> The map the rest of the plan builds to, informed by the PerfView field guide (the *why* of each workflow) and its automation companion (the TraceEvent class behind each one). traceq is a thin, agent-shaped re-expression of that surface over the `TraceLog` core already in `touki.mcp`.

### One engine, many providers

Every traceq analysis is the same pipeline: **build a `StackSource`, roll it into a `CallTree`, then rank / drill / filter / diff / export.** Only the *metric* and the *stack* differ per investigation - the field guide's {time, metric, stack} insight. So the service layer is two layers: **stack-source providers** (one per family) and a **provider-agnostic engine** every verb runs on.

### Stack-source providers (the investigation families)

| Family (field-guide section) | One sample = | Metric | Stack / leaf | TraceEvent API | traceq status |
|---|---|---|---|---|---|
| **CPU** (5, 7) | a 1 ms CPU sample | ms | call stack | `TraceEventStackSource` over `SampledProfileTraceData`; `CallTree` | **shipping** in touki.mcp |
| **Wall-clock / blocked** (9) | a slice of a thread's existence | ms | call stack + `CPU_TIME` / `BLOCKED_TIME` / `DISK_TIME` / `NETWORK_TIME` / `READIED_TIME` leaf | `ThreadTimeStackComputer` (ETW) / `SampleProfilerThreadTimeComputer` (EventPipe) + `StartStopActivityComputer` | **new - biggest missing family** |
| **Allocation rate** (8.2) | crossing a 100 KB / ~10 KB alloc threshold | bytes | alloc site + `Type <name>` leaf | `GCAllocationTick` -> `MutableTraceEventStackSource` | **new** (design lists `Alloc`) |
| **Net surviving heap** (8.2) | a surviving object | bytes | alloc site + `Type <name>` | `GCHeapSimulator` - PerfView-only, **gap** (Addendum A) | **new, gated on Addendum A** |
| **Retention / leak** (8.4-8.5) | one live object | bytes | referencer chain to a GC root | `GCHeapDump` + `MemoryGraphStackSource`; diff two snapshots | **new** (analysis public; capture is gap 2) |
| **GC behavior** (8.6) | a GC | per-GC record | structured records | `NeedLoadedDotNetRuntimes()` -> `TraceGC` / `GCStats` | **new** (design lists `GcStats`) |
| **JIT** (8) | a compiled method | ms / count | structured records | `runtime.JIT.Methods` / `JITStats` | **new** (design lists `JitStats`) |
| **Exceptions** (4) | a throw | count | throw-site stack | `source.Clr.ExceptionStart` over `MutableTraceEventStackSource` | **done** (`ExceptionsProvider`) |
| **Any event** (4, 10 Q9) | one event | count / payload | event stack (optional) | `traceLog.Events` filter; \"Any Stacks\" | partial (`EventQuery`) |
| **CPU counters / PMC** (10 Q8) | a hardware-counter overflow | retired instr / cache miss / branch | call stack | `TraceEventProfileSources` + kernel `PMCProfile`; `PMCSample` | backlog (ETW capture-side) |

The headline: **the analysis side of almost every family is a public TraceEvent call already reachable from the same `TraceLog`** the CPU provider uses - the work is wiring providers into the engine and writing agent-shaped output, not new trace parsing. The two real gaps (net-mem simulation, heap-snapshot capture) are isolated in Addendum A with their factoring cost.

### The engine (verbs that run on any provider's stack source)

| Verb | Field-guide view | TraceEvent primitive | Notes |
|---|---|---|---|
| `rank` | ByName (Exc/Inc) | `CallTree.ByIDSortedExclusiveMetric()` | existing `hotspots_self`/`_inclusive`, generalised to any provider via a `--metric` selector (design D5) |
| `callers` | Caller-Callee | `CallTree.CallerCallee(name)` | existing `callers_of` |
| `tree` | CallTree (top-down) | `CallTree.Root` walk | who-runs-at-all attribution |
| `lines` | Goto Source per line | `SourceAnnotator` + `SymbolReader` | existing `hot_lines` / `source_heatmap` |
| `diff` | Diff -> With Baseline | `InternStackSource.Diff(after, before)` | regression + leak-growth; works on *any* provider (CPU, alloc, heap-retention) |
| `export` | Save / external viewers | `SpeedScopeStackSourceWriter`, `ChromiumStackSourceWriter` | visualization bridge (below) |

Because the engine is provider-agnostic, every verb composes with every family and with the filter grammar: \"diff the *allocation* stacks of two runs, scoped to my process, exported to speedscope\" is one pipeline, not a special case.

### Scenario scoping and trimming (the user-requested workflow)

An agent wants the smallest relevant slice. traceq scopes at two altitudes.

**1. Virtual scope (analysis time) - the filter grammar applied before the CallTree is built.** PerfView's filter bar is `FilterParams` + `FilterStackSource` (automation guide section 5.3); every analysis verb accepts the same controls and *defaults them to scenario scope*:

| Intent | Flag | `FilterParams` field |
|---|---|---|
| keep only one process (drop the machine-wide rest) | `--process <name-or-pid>` (default: busiest, or the `--benchmark` workload) | `IncludeRegExs` |
| drop Idle / known noise | (on by default) | `ExcludeRegExs` |
| scope to one operation / request | `--start <ms> --end <ms>` (or `--activity <name>`) | `StartTimeRelativeMSec` / `EndTimeRelativeMSec` |
| treat other code as a labelled black box | `--group \"{%}!=>module $1\"` (preset: `[group module entries]`) | `GroupRegExs` |
| dissolve noise frames into callers | `--fold <regex>` / `--fold-pct <n>` | `FoldRegExs` / `MinInclusiveTimePercent` |

The defaults matter more than the knobs: an unscoped machine-wide ranking is the most common way an agent burns tokens on irrelevant processes, so scenario scope is *on* unless explicitly widened (`--all-processes`). Time-scoping to one request is the field guide's \"select two events -> Set Time Range\" bridge (4.2, 9.3): the `events` verb finds the bracketing events, the engine scopes every later verb to that window.

**Process-tree scope - landed.** The `--process` altitude is implemented as a lossless read-time scope: `ProcessScope` (a name substring plus, by default, all descendants) is resolved against the trace's process tree, and the reader keeps only the samples belonging to that tree. Following children is the default because it is what the capture shapes require - BenchmarkDotNet runs each workload in a child process the orchestrating host launches, and "profile my app" work runs in launched children too - so scoping to a host name without its children would miss the measured code. It is lossless: an ETW capture is fully symbol-resolved by `TraceLog` (managed and native frames alike) before any sample is dropped, so it both avoids physically rewriting the trace and resolves the workload's JITted managed frames - precisely the resolution the physical relog trim cannot preserve. `SampleStack` now carries its owning `Process`, so a multi-process capture is reasoned about per process. The remaining altitudes (time window, group, fold) and the `--all-processes` / default-on wiring are M2.

**2. Physical trim (a derived file) - the `trim` verb.** A machine-wide `.etl` is expensive to re-open and awkward to share. `trim` rewrites it to a smaller scenario trace - one process *and its descendants* (BenchmarkDotNet runs each workload in a child of the orchestrating host, and "profile my app" work often runs in children too, so following the process tree is the default), an optional time window, only the providers a family needs - written back as `.etl` / `.nettrace` (or straight to `.etlx`). This is the literal "trim the capture to the target scenario, filter out irrelevant process information" the user asked for, and it makes every later verb and every hand-off cheaper. It extends the design's existing `convert` / `clean` verbs (M2). **Status: parked.** A working process-tree relog trim exists in the fixture tool and shrinks a 111 MB machine-wide capture to ~1 MB, but the raw ETW relogger preserves native module resolution and *not* the JITted managed-method map, so the trimmed file resolves native frames only. The lossless capability the user actually needs - scope a capture to the workload tree at analysis time - is delivered by the ETL reader over the full trace instead; physical trimming is deferred with its full state, the five relogger layers already solved, and the follow-up options captured in [docs/traceq-etl-trimming.md](traceq-etl-trimming.md).

### Symbols

Frame and line resolution is a TraceEvent service (`SymbolReader`; automation guide section 8), already partly wired via `EmbeddedPdbExtractor`. Full surface: `LookupSymbolsForModule` (resolve a module), `LookupWarmSymbols` (resolve only frames that carry samples - the cheap default for an agent), `_NT_SYMBOL_PATH` honouring, and **SourceLink** for `lines` / `heatmap` to fetch the exact source revision. The resolution rate already feeds the `< 0.8` warning in the output contract (M1).

### Visualization and export (graphical / interactive views)

Output is dense text by default (agent-friendly), but the *same* `StackSource` the engine builds is exportable to interactive viewers with no PerfView dependency - both writers are public TraceEvent (automation guide section 9):

| `export --format` | Opens in | Writer |
|---|---|---|
| `speedscope` | [speedscope.app](https://speedscope.app) (web flame graph; already the format `Get-TraceHotspots.ps1` and `tools/speedscope-to-flamegraph.ps1` consume) | `SpeedScopeStackSourceWriter` |
| `chromium` | `chrome://tracing` / the [Perfetto](https://ui.perfetto.dev) UI | `ChromiumStackSourceWriter` |
| `perfview` | PerfView itself (re-open for the full GUI) | `XmlStackSourceWriter` - PerfView-side, Addendum A |
| `flamegraph` (svg) | any browser, inline in a PR | the existing `tools/speedscope-to-flamegraph.ps1` path |

Because export takes whatever `StackSource` the pipeline produced, a *scoped, filtered, diffed* view exports as easily as a raw one - the agent can hand a human a one-click flame graph of exactly the slice it was reasoning about. Inline/interactive rendering inside the agent surface (MCP resources, or an MCP Apps flame-graph view) is the richer end and stays on the post-1.0 backlog.

---

## 2. Milestones

### M0 — Scaffold inside Touki, fixtures strategy

**Status: complete (PR #182).** The scaffold, the subtree CI (both OSes), and the extraction rehearsal landed; the plan itself ships in the same commit.

Stand up the incubation subtree — one contained directory, so promotion is a single `--path` argument:

```text
traceq/
  Directory.Build.props      # net10-only; severs Touki's multi-targeting + root props
  Directory.Packages.props   # own central-package versions (does NOT inherit Touki's)
  global.json                # pins the SDK independently
  .editorconfig              # root = true; severs Touki's style inheritance
  src/TraceQ.Core/           # analysis core + trace access (the only place logic lives)
  src/TraceQ/                # CLI host; `traceq mcp` verb hosts the server (D2)
  src/TraceQ.Mcp/            # thin dnx shim package over the same assembly (§9)
  tests/TraceQ.Core.Tests/   # unit + golden-file contract tests
  tests/TraceQ.Parity.Tests/ # numeric parity vs. legacy oracles (§3 below)
  eval/                      # harness, tasks, baselines (M5)
  docs/                      # single-source for skill/README/help workflow text (§6 design)
  skills/traceq/             # shipped skill (moves to .github/skills/ at promotion)
```

`traceq/Directory.Build.props` declares independence on day one: net10-only (none of Touki's multi-targeting machinery), `IsPackable=false` until promotion, Touki's style analyzers *kept* so promotion never triggers a reformat. It must **not** re-import Touki's root props via `GetPathOfFileAbove` (that would re-couple it); a sibling `Directory.Packages.props` holds its own central-package versions (TraceEvent pinned here, *not* inherited from Touki's list), a `root = true` `.editorconfig` severs style inheritance, and an own `global.json` pins the SDK. **Self-containment rules:** nothing outside `traceq/` references into it, and nothing inside references a Touki project — the parity tests treat `touki.mcp analyze` and `Get-TraceHotspots.ps1` as *processes whose output is compared*, not as project references, which keeps the coupling graph empty by construction.

Two CI additions to Touki's pipeline: subtree-scoped build + test jobs on `windows-latest` **and** `ubuntu-latest` (the §8.1 capability matrix is a test plan, not prose), and the **extraction rehearsal** - on traceq-touching PRs, a plain recursive copy of `traceq/` into a temp directory (history is not preserved, so no `filter-repo` is needed) followed by a standalone `dotnet build && dotnet test`. The rehearsal is the enforcement mechanism for the self-containment rules; a failure means coupling crept in, and it gets fixed before merge, not at promotion.

MIT license (inherited). Trusted Publishing: nothing wired during incubation — it belongs to the promoted repo (M3½). The naming *availability pass* happens now (§6, D-N) even though the decision gate is promotion.

**Exit:** subtree CI green on both OSes; rehearsal green on the scaffold; fixture strategy chosen (§3); design decision log updated with Q1–Q3 (done — D15–D17).

### M1 — Core extraction and the output contract

**Status: in progress.** Step 1 (relocation + provider/engine seam), the output-contract envelope, the symbol gate, the tier-2 LRU cache, and the output token budget have landed, the parity harness is green for the net10 EventPipe corpus half (§3), the net481 ETW corpus half has landed (capture, lossless process-tree scoping, thread time), and the **O1 cross-machine hand-off spike has passed** (a Windows-converted `.etlx` resolves managed frames on Linux); the text renderer and the remaining filter altitudes remain, and the tier-1 ETLX disk cache is deferred to land with the `.etl` fixture (TraceEvent already caches the conversion on both paths).

The move itself, then the two things touki.mcp doesn't yet have: the contract and breadth.

1. **Relocate** the §1 assets into `TraceQ.Core` (plain copy, no history) — **done (PR #183)**: re-namespaced `Touki.Mcp` → `TraceQ` and ported the core suites. The analysis **object model is public**: the CLI head (`TraceQ`) and the MCP facade (`TraceQ.Mcp`) consume it through the public surface, and only the implementation details (the format readers behind `TraceLoader`, the LRU cache, and a few test-only members) stay internal, surfaced to the test assemblies via `InternalsVisibleTo`. Then lay the **provider/engine seam** from section 1A — **done**: a `StackSampleSource` pairs a family's weighted stacks with a `MetricInfo` (name + unit; CPU = `ms` today), threaded through the aggregator so the metric is carried rather than assumed to be milliseconds. The *formal* `StackSourceProvider` interface, and generalizing the engine verbs (`Rank`, `Callers`, `Tree`, `HotLines`, `SourceHeatmap`, plus the new `Diff` and `Export`) to run on *any* provider, are **deferred to step 5** — designing the abstraction against the lone CPU provider would be speculative; a real second family (thread-time, allocation) pins its shape. The CPU provider is the existing aggregator, whose fold list (`CPU_TIME`, `WriteBarrier`, …) is itself CPU-specific; `TraceInfo` fronts it for the at-a-glance summary.
2. **Output contract (§4.4)** as a core type every service returns through. **Envelope landed**: `AnalysisResult<T>` carries a `schemaVersion`, a warnings channel, a steering-hints channel, and the typed payload; `OutputJson` serializes it compact (no `WriteIndented`), camel-cased, with doubles rounded to a fixed precision and the relaxed encoder so frame names keep their literal `<>&`; the rank builders break weight ties by ordinal name for **deterministic ordering**; golden-file tests pin the byte-exact output (compact JSON is single-line, so the goldens compare cleanly on both OSes). **Token budget - done**: `OutputBudget` estimates a serialized result's token cost (the rough four-characters-per-token heuristic) against a 25k ceiling and emits a remediation warning over it; `OutputBudget.TryGetBudgetWarning` is the building block the verbs consume, while the actual row-dropping truncation is a per-verb concern deferred to M2. **Steering-hint taxonomy - done**: `SteeringHints` turns a ranking, callers, or diff result into the canonical next-step nudge (a ranking points at the hottest frame's callers, a callers report points further up the stack, a diff points at the frame that changed most; an empty scope steers toward widening instead), matching the hint the output-contract golden pins. **Deferred within the contract**: the dense fixed-width text renderer (deferred to M2, where the ported `ConsoleAnalyzer` becomes its consumer and pins the format - building it now would be a renderer with no caller). The symbol-resolution `< 0.8` warning is wired in step 3.
3. **Symbol gate** — **done** for the analysis side: `SymbolGate` centralizes the `0.8` threshold (previously a magic number duplicated in the reader) and emits one standardized warning *with remediation* (`pass --symbols <build-output-dir>`), replacing the reader's ad-hoc text (which had hardcoded a `touki` module name through the relocation); the percentages are formatted whole-number and culture-invariant so the text is deterministic, and the gate suppresses on a zero-sample trace (a separate warning covers that). `SymbolGate.IsBelowThreshold` is the predicate the **`--strict` → exit 3** CLI gate will consume; that exit-code behavior is **deferred to M2** with the rest of the head.
4. **Caching.** **Tier-2 LRU — done**: `LruCache<TKey, TValue>` (thread-safe, bounded, value factory run outside the lock) replaces the trace store's unbounded dictionary, so a long agent session retains only the most-recently-used traces (default 16) instead of growing without limit; it preserves the store's OS-aware path comparer and single-instance identity. **Tier-1 disk ETLX cache — deferred (with reason)**: the clean-room reimplementation of the pvanalyze pattern (sidecar `<name>.traceq.etlx`, mtime validation, lock file, atomic publish) lands with the `.etl` fixture, not as a non-elevation increment. Two reinforcing reasons. First, the plan already scopes it to the `.etl` -> `.etlx` conversion, which needs a real elevated capture to validate end-to-end. Second, and decisively, TraceEvent *already* does sidecar-`.etlx` disk caching with mtime validation on **both** trace paths: the EventPipe readers call `TraceLog.CreateFromEventPipeDataFile` (which writes and reuses a sibling `.etlx` - the file the subtree gitignores) and `EtlReader` calls `TraceLog.OpenOrConvert` (which checks/writes the same sidecar). A custom tier-1 cache on the non-elevation path would duplicate the library's built-in behavior with nothing to measure, since the committed fixtures are sub-megabyte and convert in milliseconds. Its only distinctive value - a cache directory separate from a read-only or networked source, plus lock-file coordination for concurrent machine-wide conversions - only manifests with a large `.etl` under elevation, which is exactly what cannot be exercised here. Building it now would be the disk-cache equivalent of a renderer with no caller.
5. **New providers and engine verbs**, in eval-task order. **Allocation provider - done**: `AllocationProvider` reads `GCAllocationTick` events from a `.nettrace` into byte-weighted call stacks and returns a `StackSampleSource` carrying `MetricInfo.Allocations`, which the existing `FoldingAggregator` ranks unchanged - the first proof that the engine is provider-agnostic. To make it honest, `SampleStack.Weight` (was `WeightMs`) and the aggregator are now metric-generic, so the weight is the source metric's unit (ms for CPU, bytes for allocation). **GC-stats provider - done**: `GcStatsProvider` assembles the runtime's `TraceGC` records (via `NeedLoadedDotNetRuntimes`) into a `GcStatsResult` - per-collection rows plus aggregate counts/pause summary; unlike the stack families this is structured data, so it returns its own result, not a stack source. It reuses the GC-verbose allocation fixture (same capture carries both alloc ticks and GC events). The capture side grew too: a GC-verbose `AllocLoop` benchmark and an `inspect` verb in `HotLoopBench`, with `make-fixtures` committing a tuned sub-1 MB allocation smoke `.nettrace`. **ThreadTime - done (ETW)**: the earlier spike (`SampleProfilerThreadTimeComputer` over the BDN CpuSampling `.nettrace`) reconstructed only `CPU_TIME`/`UNMANAGED_CODE_TIME` leaves, never `BLOCKED_TIME`, because EventPipe samples only running threads - so it was deferred to the ETW half. That half now exists: `ThreadTimeProvider` runs the `ThreadTimeStackComputer` over the net481 ETW capture (whose context-switch keywords carry the blocked intervals) into a `MetricInfo.ThreadTime` stack source - each stack rooted at its process and thread, leafed at `CPU_TIME` / `BLOCKED_TIME` - so the engine ranks where wall-clock time went, not just where the CPU was busy. It scopes to a workload tree the same way the CPU reader does (shared `ProcessTree.ResolvePids`); input-event filtering is avoided because it would break the blocked-time simulation, so the scope is applied on the output by the process the stack is rooted at. **Export - done** (the first provider-agnostic engine verb): `SpeedscopeExporter` writes any `StackSampleSource` to the speedscope "sampled" format - one aggregate profile, a shared frame table, the profile `unit` mapped from the source `MetricInfo` (milliseconds for CPU, bytes for allocation) - so any family's stacks open as an interactive flame graph with no PerfView dependency. **Filter / scope grammar - subset done**: `ScopeFilter` keeps or drops samples by include / exclude regex on frame names (exclude wins), returning a narrower `StackSampleSource` of the same metric that every engine verb composes with unchanged. The grammar's other altitudes are deferred for want of model data the normalized `SampleStack` does not carry: time-windowing (`--start`/`--end`) needs a per-sample timestamp (and a multi-operation fixture to test). Process scoping (`--process` / `--all-processes`) is **done**, but via the read-time path rather than this frame-name filter: a `ScopeRequest` intent (an explicit name, the automatic busiest-process default, or the all-processes opt-out) is resolved against the trace's process tree in the loader and the matched pids drive the reader, so the samples are narrowed before the model is built (where `SampleStack` would carry no process to filter on). The default is on - a multi-process ETW capture auto-scopes to the busiest process tree and surfaces an applied-scope notice when it actually narrows. The **grouping altitude is done**: `GroupTransform` collapses every frame of a matched module into a single `module!` box (PerfView's `[group module entries]` preset), removing the consecutive duplicates that collapse produces so inclusive ranking still counts a group once per sample - another same-metric transform the engine composes with unchanged. **Diff - done** (task 4): `RankingDiff` compares a baseline ranking against a current one, matching frames by name and ordering by the size of the change (regressions and improvements alike), so an agent sees what got slower / faster - or allocated more / less - between two runs. It is purely a comparison of two rankings, so it is provider-agnostic and composes with scoping and filtering (diff two filtered, scoped rankings). **Chromium export - done**: `ChromiumExporter` writes any `StackSampleSource` to the Chrome Trace Event Format, reconstructing an evented begin/end timeline from the weighted samples (the inverse of the speedscope reader) so the stacks open as a flame graph in `chrome://tracing` / Perfetto; the time axis carries the metric magnitude (microseconds for CPU, raw bytes for allocation). **EventQuery - done** (task 7): `EventQueryProvider` queries a trace's raw events by name with pagination (`skip`/`take`) and a per-event payload cap, returning its own structured result, so an agent can inspect arbitrary events without a machine-wide firehose. **JitStats - done** (task 5): `JitStatsProvider` reads the runtime's per-method JIT records (compile time, IL / native size, optimization tier) from a JIT-profile capture into its own structured result, like the GC-stats report, so an agent can judge JIT cost and startup pressure. **Exceptions - done** (task 4): `ExceptionsProvider` reads the runtime's `Exception/Start` events into count-weighted throw-site stacks (`MetricInfo.Exceptions`), which the existing `FoldingAggregator` ranks unchanged - a frame that throws often rises to the top - captured by an `ExceptionLoop` benchmark that throws at two named sites in a fixed ratio. *Still to come* - *Engine* (provider-agnostic): the deferred filter altitudes (time / process scoping). *Providers*: `Datas`. *Retention* (`GCHeapDump` + `MemoryGraphStackSource`) and *net-mem* (`GCHeapSimulator`, gated on the Addendum A factoring) land when an eval task demands them.
6. **The `trim` verb** (section 1A): rewrite a machine-wide capture to a scenario-scoped trace (one process, optional time window, only the needed providers). Its output feeds the same engine, so a parity test asserts `rank` over a trimmed trace matches `rank` over the parent within tolerance.
7. **O1 spike - done; answer: YES, advertise the hand-off.** The spike converted the ETW fixture `.etl` to `.etlx` on Windows, then opened that same `.etlx` directly (`new TraceLog(etlx)`, no symbol reader) on **Ubuntu 26.04** and asserted managed frame names resolve. They do, **byte-identically to the Windows control**: 31,659 CPU samples, 230 resolved frames, and the JITted managed leaf `hotloopbench!TraceQ.Fixtures.HotLoopBench.EtwLoop.BuildLabel(int32)` resolves on both OSes. So managed JIT names are baked into the `.etlx` (they come from the trace's own JIT rundown, not local PDBs) and survive the cross-machine hop. The one asymmetry: the `.etl` -> `.etlx` *conversion* is Windows-only, so the workflow is "convert on Windows, analyze anywhere" rather than "analyze the raw `.etl` anywhere". A *permanent* CI guard (Windows leg converts, Linux leg asserts) is a follow-up gated on a managed-resolving fixture: the committed scenario trim resolves native frames only (the relogger limitation in [traceq-etl-trimming.md](traceq-etl-trimming.md)), so the guard needs either the full capture as a release asset or a long-lived standalone capture. The spike's deliverable is this answer; the permanent guard rides the release-asset work.

**Exit:** parity harness green (§3); contract golden files green on both OSes; the scenario-scope default verified (a machine-wide fixture ranks to the workload process without `--all-processes`); O1 answered (**yes** - a Windows-converted `.etlx` resolves managed frames on Linux, so the cross-machine hand-off is advertised).

### M2 — CLI head

> **Progress:** the CLI head has begun. The first vertical slice - the `rank` engine verb and the `cpu` family shortcut - is built, green, and merged in PR #195. It runs over a testable `RankingExecutor` seam (its input is a plain `RankRequest`, its output goes to injected writers), so the verbs, the parser, and the rendering are exercised independently; both text and compact JSON render through the M1 output-contract envelope, the symbol gate is wired to `--strict` -> exit 3 (the rest of the §4.4.6 exit codes ride the remaining verbs), and the dense fixed-width **text renderer deferred from M1** landed here as its first consumer. **Second slice (merged in PR #196): the drill-down engine verbs `callers`, `lines`, and `heatmap`** - the single-trace reads that complete the rank -> callers -> lines/heatmap workflow. Each follows the same shape (request record -> executor -> text/JSON renderer), and the shared load-and-validate plumbing (the broadened trace-load exception mapping, fold-pattern validation, symbol-gate warning, and `--strict` exit) is factored into one `TraceExecution` helper that every executor - `rank` included - now shares. `heatmap`'s text mode overlays the heat onto the on-disk source via the ported `SourceAnnotator`. **Third slice (merged in PR #197): `diff` and `export`.** `diff` compares two traces - it ranks both with no row cap (so a frame hot on only one side is not misreported as a full regression) then diffs the top changes, steering toward the frame that moved most; `export` writes a `StackSampleSource` to a speedscope or Chrome-trace flame-graph file (its `--format` selects the flame-graph format rather than text/JSON, matching the M2 exit criterion `export --format speedscope`), to a file or stdout, with symbol warnings routed to stderr so the written JSON stays clean. **Fourth slice (merged in PR #198): `tree`, the last engine verb, and its new core.** Unlike the others it needed analysis core: `FoldingAggregator.CallTree` builds a top-down, path-based call tree (the inverse of `callers`), skipping folded frames like inclusive-time and bounding the result two ways so it stays within an agent's token budget - a maximum depth and a minimum per-node share of scope (`--max-depth` / `--min-pct`, the `--fold-pct` idea from §5.1). The `tree` verb renders it as an indented text view or nested JSON. With `tree` the engine-verb set (`rank`, `callers`, `tree`, `lines`, `heatmap`, `diff`, `export`) is complete. At that point none of these verbs took a `--metric` selector (only CPU existed); the fifth slice below adds it to `rank` and the new family shortcuts, while the drill-down verbs (`callers`/`tree`/`lines`/`heatmap`/`diff`/`export`) gain it uniformly once an eval task needs a non-CPU drill-down. **Fifth slice: the provider-selection seam, the family shortcuts, the report verbs, and process scoping.** The three stack-source families - `alloc`, `exceptions`, and `threadtime` - the three report verbs - `gcstats`, `jitstats`, `events` - and the `--process` / `--all-processes` scope options have all landed, completing M2's verb surface. A read-only investigation pinned the blocker to a single line - `LoadedTrace`'s constructor hardcodes `new StackSampleSource(MetricInfo.Cpu, samples)`; everything downstream (`TraceStore` -> `LoadedTrace` -> `FoldingAggregator` -> the seven engine verbs) is already provider-agnostic and runs on whatever source it is handed. The existing providers split two ways. The **stack-source** providers (`AllocationProvider`, `ThreadTimeProvider`, `ExceptionsProvider`) each return a `StackSampleSource` with their own `MetricInfo` (bytes, ms, counts), so the whole engine ranks, drills, trees, and diffs them unchanged; the **report** providers (`GcStatsProvider`, `JitStatsProvider`, `EventQueryProvider`) return their own structured records and need their own verb/executor/renderer, never touching the aggregator. The one real design wrinkle is the `TraceInfo` gap: the CPU reader computes a full `TraceInfo` (duration, per-thread breakdown, symbol-resolution rate, warnings) that the stack-source providers do not produce, so the provider seam must synthesize a `TraceInfo` from the provider's samples and accept that the symbol-resolution rate (and the `--strict` gate it feeds) is a CPU-reader signal not available for the other families. Sequencing was one provider at a time, all three now landed: **`alloc` first** (it proved the `LoadedTrace` provider seam end-to-end against the committed `alloc.nettrace` fixture, made the `cpu`-only shortcut pattern general via a new `TraceMetric` enum, and lit up `rank --metric alloc` - the selector the verbs had rejected), **then `exceptions`** (the same `.nettrace` shape, count-weighted throw sites), **then `threadtime`** (the `.etl` capture and process scoping, whose Windows-only read is `[OSCondition]`-guarded while its format guardrail stays cross-platform). Each lights up both `rank --metric <name>` and a family shortcut verb. **The report verbs `gcstats` / `jitstats` / `events` have also landed:** unlike the stack families these return structured records (per-collection GC rows, per-method JIT rows, paged raw events), so each has its own request/executor/renderer rather than flowing through the folding aggregator, and each shares one `TraceExecution.TryReadNetTraceReport` helper that applies the EventPipe-only format guardrail and maps provider failures to a clean exit code. `gcstats` and `jitstats` keep the full aggregate summary but cap their detail rows (ranked by pause / compile time) with a truncation warning; `events` is paged by the provider (`--skip` / `--take` with a payload cap) and steers toward the next page. `heap` stays unbuilt - it has no provider yet (Addendum A capture work). Each stack family adds a one-line format guardrail (`threadtime` rejects a `.nettrace`, `alloc`/`exceptions` reject an `.etl`) so a wrong-format input is a clean usage error. **Parser decision - ConsoleAppFramework v5** (`5.7.13`): chosen over a hand-rolled parser and over System.CommandLine (perennially preview) because it is a C# source generator with *zero runtime dependency* (referenced as an analyzer, `PrivateAssets="all"`), AOT-safe, and so preserves the subtree's "promotion is a plain file copy" property. Two consequences fold back into the plan below. First, the "uniform global option grammar (§5.1)" is now CAF's - `--lower-kebab-case`, `--opt value` / `--opt=value`, case-insensitive enums, single-pass parse - which differs from a fully custom grammar in two visible ways: array options such as `--fold` are **comma-separated** (or JSON), not repeatable, and a bare verb prints its own help. Second, help is generated by CAF from each verb's XML doc comments (Usage / Arguments / Options, with `-x|--alias, desc` aliases drawn from `<param>`), so the **examples-first help-lint** becomes a CAF-customization task (custom help text), not a hand-written template - flagged for when the lint is built.

Full verb set over the services with the uniform global option grammar (§5.1). The verbs fall into three groups: **engine** (`rank`, `callers`, `tree`, `lines`, `heatmap`, `diff`, `export`), each taking a `--metric`/provider selector and the filter flags (`--process`, `--start`/`--end`, `--group`, `--fold`, `--fold-pct`, `--all-processes`); **family** shortcuts (`cpu`, `threadtime`, `alloc`, `gcstats`, `jitstats`, `exceptions`, `events`, `heap`) that preset the provider; and **file** ops (`convert`, `clean`, and the deferred **`trim`**). **Status:** the engine, family, and `convert`/`clean` verbs have landed (`heap` excepted - no provider; `trim` parked, since the relog rewrite resolves native frames only). The **`--benchmark` resolver landed** as a frame-based scope: rather than the time-based `Activity Benchmark(...)` window (which needs the deferred per-sample timestamp), it presets the root scope to the BDN workload wrapper frame (`Runnable_N.WorkloadAction*`), isolating the measured code from the harness and the overhead iterations - replacing the documented-trap approach (D6), with unit tests against the committed `exceptions.nettrace` BDN fixture. `--benchmark` and `--root` are mutually exclusive. Exit codes per §4.4.6. Scenario scope is the default; `--all-processes` opts out.

Help is treated as a build artifact (**landed**): the README carries an examples-first usage section documenting every verb with a runnable example plus the canonical rank -> drill -> compare workflow, and a CI **help lint** (`tools/Test-CliHelp.ps1`) enforces that every verb is listed in top-level help, each verb's `--help` succeeds with a Usage line within a 60-line budget, and the README documents every verb with an example and the workflow. (Examples live in the README rather than per-verb `--help` because ConsoleAppFramework generates `--help` from XML docs and has no examples section.) `dotnet pack` produces a locally installable tool (`PackAsTool`/`ToolCommandName=traceq`), verified by an install -> run -> uninstall round-trip.

**Exit:** a human completes eval tasks 1–7 using only the CLI against fixtures (rides the M5 harness); help lint green (**done**); local `dotnet tool install` round-trips on both OSes (**done** - verified install -> run -> uninstall); an `export --format speedscope` opens in speedscope.app (the exporter emits valid speedscope JSON; opening it is a manual check); a `trim`med trace's `rank` matches the parent within tolerance (**deferred with `trim`**).

### M3 — MCP facade

> **Progress:** the facade has begun. The first slice stands up the **standalone
> `TraceQ.Mcp` stdio server** (stderr-only logging, a singleton `TraceStore`, the
> server `instructions` field carrying the workflow summary, `readOnlyHint` /
> `openWorldHint` / `idempotentHint` annotations) plus the **read-only query
> tools**: `trace_info` (folding the old `load_trace` and `list_threads` into one
> metadata call), `trace_rank` (the D5 consolidation - one tool with a
> `metric: cpu|threadtime|alloc|exceptions` selector and a `measure: self|inclusive`
> switch), `trace_callers`, `trace_lines`, and `trace_heatmap`. Each returns the
> same `AnalysisResult` envelope through `OutputJson`, byte-identical to the CLI's
> `--format json`, with the warnings forwarded whole and the ranking/callers steering
> hints reused. The selector vocabulary moved to a canonical
> `TraceMetricSelector` in the core so the CLI's `RankRequestFactory` and the tool
> share one mapping. `TraceQ.Mcp.Tests` exercises every tool, the metric and measure
> guards, the `top >= 1` boundary, and the load-failure path; a manual stdio
> handshake confirmed `tools/list` registration, the instructions field, and stdout
> purity. **Deferred to the next slice:** `trace_diff`, `trace_export`, and a
> `trace_gc` / `trace_query_events` report tool; the **stdout-purity** and
> **schema-budget** CI checks; the MCP Inspector smoke and scripted client
> round-trip; and `outputSchema` / `structuredContent`. `trace_trim` stays parked
> with the `trim` verb.
>
> **Second slice:** the AOT-safe tool registration and three more tools. The
> reflection-based `WithToolsFromAssembly()` (IL2026) became the generic
> `WithTools<TraceTools>()` - `TraceTools` is now a non-static class (its methods
> stay static) so it works as a type argument; a stdio handshake confirmed all
> seven tools still register. Added `trace_diff` (compare two CPU traces, ranking
> both fully before diffing, warnings prefixed `baseline:` / `current:`) and
> `trace_gc` (the GC report over a `.nettrace`, capped to the hottest pauses).
> Both CI checks landed as a single stdio harness, `tools/Test-McpServer.ps1`,
> wired into `traceq.yml` next to the help lint: it drives `initialize` ->
> `tools/list` with `Logging__LogLevel__Default=Trace` forced and asserts (1) every
> stdout line parses as JSON-RPC even under chatty logging, and (2) the serialized
> tool list stays within the token budget. The measured surface is ~2.2k tokens for
> seven tools; the budget is set at 4000 to accommodate the full planned curated set
> while still catching genuine bloat. **Still deferred:** `trace_export` (it writes
> a file rather than returning an envelope - a distinct tool category needing a new
> result type and write annotations), `trace_query_events`, the MCP Inspector smoke
> and scripted client round-trip, and `outputSchema` / `structuredContent`.
> `trace_trim` stays parked with the `trim` verb.
>
> **Third slice:** `trace_export`, the first write tool. It exports a trace's CPU
> sample source to a speedscope or Chrome-trace flame-graph file, taking a required
> `output` path (there is no stdout to write to under the protocol) and a `format`
> selector, and is annotated `readOnlyHint=false` (the others are read-only). Its
> product is a file, so it returns an `ExportResult` receipt (format, absolute path,
> byte count, profile name) through the same envelope, with a viewer hint
> (speedscope.app or the Perfetto UI) in the hints channel; `ExportResult` joins the
> source-gen JSON context so the head stays AOT-clean. Write failures map to a clean
> `McpException`. The surface is now eight tools at ~2.6k tokens, still under budget.
> **Still deferred:** `trace_query_events`, the MCP Inspector smoke and scripted
> client round-trip, and `outputSchema` / `structuredContent`. `trace_trim` stays
> parked with the `trim` verb.

The eight tools (§5.2) re-cut over the same services: port touki.mcp's description text, apply the D5 consolidation (`trace_rank` with `metric`), fold `list_threads` into `trace_info`, add `trace_gc`/`trace_diff`/`trace_query_events`. The broadened surface (section 1A) raises the consolidation stakes, not the tool count: the **engine** tools take a `metric`/provider parameter, so `trace_rank(metric: cpu|threadtime|alloc|…)` is *one* tool spanning every family rather than one tool per family - the token budget below is what forces this. `trace_export` and `trace_trim` join the curated set (they are how an agent hands a human a flame graph or shrinks a trace); the rich family reports (`alloc`/`jit`/`exceptions`/`heap`) stay CLI-first and are promoted to MCP only when an eval task demands it (backlog O4). Annotations (`readOnlyHint` et al.), `outputSchema` + `structuredContent` where the C# SDK supports them, the server `instructions` field carrying the workflow summary, and `traceq mcp` hosting with stderr-only logging.

Two CI checks born here and kept forever: the **stdout-purity test** (run the server under load, including a deliberately chatty logging provider, and assert nothing but JSON-RPC reaches stdout) and the **schema budget check** (serialize the tool list, fail above a token budget - measured at ~2.2k tokens for the seven-tool surface, with the ceiling set at 4000 to fit the full curated set). Both landed in `tools/Test-McpServer.ps1`. MCP Inspector smoke plus one scripted client round-trip test.

**Exit:** eval tasks 1–5 completed through the facade in Claude Code and VS Code agent mode (manual runs; the harness arrives in M5); both CI checks green.

### M3½ — Promotion

With the facade demonstrably standing alone, execute the rehearsed extraction:

1. **Name finalized** (§6, D-N) — the repo needs it now, and the first private packages at M4 need final IDs. The availability pass was an M0 task, so this is a decision, not research.
2. Copy `traceq/`'s files into a fresh `JeremyKuhne/<name>` repo at the root (a plain copy plus an initial commit; history is intentionally not carried - the parity harness, not git lineage, is the correctness guarantee, and the `touki.mcp` history was judged not worth preserving).
3. Stand up the promoted repo: port the subtree CI jobs verbatim; seed AGENTS.md / copilot-instructions trimmed from Touki's (deliberately lighter — this is a tool product, not a public API surface); enable the GitHub Packages NuGet feed; wire Trusted Publishing, dormant until the v1.0 public push.
4. **Delete the subtree from Touki** in a single commit. `touki.mcp` itself remains untouched and working — it stays the daily driver until M6 flips the switch.

**Exit:** promoted repo CI green standalone; the rehearsal check retires (it has done its job); Touki's tree clean of `traceq/`.

### M4 — Knowledge layer and distribution

Write the SKILL.md, trap catalog, and AGENTS.md snippet from `docs/` single-sourcing with the drift check (§6 of the design); generate `server.json`; self-contained publish profiles (interim non-AOT - full Native AOT is the goal, but TraceEvent blocks it today; this reverses D13, see the AOT-goal note in section 6); **first packages (Core, tool, Mcp shim) land on the promoted repo's GitHub Packages feed — private until v1.0.** Feed-auth realities are an M4 deliverable because they bite every consumer: CI uses `GITHUB_TOKEN`; local installs need a PAT-backed `nuget.config` source; the Copilot cloud agent needs the credential supplied through `copilot-setup-steps.yml` env/secrets — ship that snippet alongside the §8.2 one. The NuGet MCP package-type metadata rides along now so nothing changes shape at 1.0; install badges and the MCP registry entry wait for the public publish (M6).

**Exit:** cold-discovery (eval task 8) passes from a fresh clone carrying only the AGENTS.md pointer; `dotnet tool install --add-source <feed>` and `dnx <Pkg>@<ver>` against the feed both work on clean, feed-authenticated Windows and Linux machines.

### M5 — Eval harness and tuning

Build the §10 harness: headless Claude Code + Copilot CLI runners, the four arms, N = 10, metrics capture (success / tokens / calls / wall time), the ten tasks, and the mcp-builder-style QA file for the MCP arm. Record baselines, then run the tuning loop on descriptions, help, and the skill — agent-drafted revisions, harness-scored, human-reviewed. Wire the smoke subset (tasks 1, 4, 9) into CI with the regression budget (any success drop fails; > 15% token growth on a task fails).

**Exit:** baselines committed; design goals G1 (≤ 6 calls) and G2 (token budgets) met or variances documented with rationale; smoke subset gating CI.

### M6 — Touki migration and v1.0

`touki.mcp` retires. Touki adds the feed source and references `TraceQ.Core` — fold-list additions and BDN conventions survive as a thin config + skill layer (design D-table); the performance-testing skill and `docs/performance-investigation*.md` are rewritten to route through `traceq`, now covering the wall-clock, allocation, and retention families and the `trim` / `export` workflows rather than CPU alone; `.vscode/mcp.json` gains the server; the `touki.mcp` project is deleted. Decide and record whether `Get-TraceHotspots.ps1` stays as a no-dependency fallback or goes too. Resolve any O1 residue. Then the cadence flip: **v1.0 is the first public NuGet publish** — the same IDs the private feed proved out — with install badges and the MCP registry entry, after which Touki's private-feed source becomes unnecessary.

**Exit:** a full Touki perf investigation (benchmark → rank → callers → lines → diff) runs end-to-end on `traceq` with no PowerShell in the loop; v1.0 live on NuGet and the MCP registry.

### Post-1.0 backlog (eval- and demand-gated, per design §13)

`collect` verb (CLI-only, Windows-only, elevation-guarded — D11) · promote `alloc`/`jit`/`exceptions` to MCP when an eval task demands it (O4) · MCP Tasks for large-trace first load (O2) · MCP Apps flame-graph view (O3) · kernel ETW surface as a bounded expansion (O7) · Linux `perf`/LTTng ingestion · **net-mem provider** by factoring `GCHeapSimulator` into TraceEvent (Addendum A) · **heap-snapshot capture** via the HeapDump assembly or `dotnet-gcdump` (Addendum A) · **trigger-based + circular-buffer collection** (Addendum A) · **PMC / CPU-counter** capture and ranking (section 1A) · promote the retention / leak-diff family to MCP · richer interactive views (Perfetto deep-link, MCP Apps).

---

## 3. Fixtures and the numeric parity harness

> **Progress:** the net10 EventPipe half has landed - `traceq/fixtures/HotLoopBench` (a dedicated hot string-building loop) captured via BenchmarkDotNet's `[EventPipeProfiler]`, `traceq/fixtures/make-fixtures.ps1` copies the speedscope export into the parity fixtures and freezes the oracle's rankings, and `TraceQ.Parity.Tests` asserts `traceq`'s self-time matches the frozen `Get-TraceHotspots.ps1` golden within tolerance and identical top-N order (green). The net481 `[EtwProfiler]` `.etl`/`.etlx` half (also the O1 fixture) has its **capture tooling in place** - `HotLoopBench` multi-targets `net10.0;net481`, an `EtwLoop` benchmark runs under an `EtwProfiler` configured (`EtwCaptureConfig`) with the `ContextSwitch`/`Dispatcher`/`Thread` kernel keywords so one capture serves both the CPU rankings and the ThreadTime view, a `convert` verb produces the `.etlx`, and `make-fixtures.ps1` captures the `.etl` + `.etlx` in an elevated block (skipped with a warning when unelevated). The committed `.etl`/`.etlx` fixture itself awaits an elevated capture run (ETW kernel tracing needs administrator rights, which CI does not have). **Update: captured.** An elevated capture produced the full machine-wide `.etl`; a process-tree relog trim reduces it to a committed ~1 MB multi-process scenario fixture ([traceq/tests/TraceQ.Core.Tests/Fixtures/etw.etl](../traceq/tests/TraceQ.Core.Tests/Fixtures/etw.etl)) that the process-scope tests read. Reading an `.etl` is Windows-only (the ETW conversion), so those tests are `[OSCondition(OperatingSystems.Windows)]`-guarded and skip cleanly on the Linux CI leg; managed-frame resolution over the full capture is validated locally and the `.nettrace` half covers it cross-platform.

The extraction's safety net is a fixed corpus of traces with known-good answers:

- **Corpus:** one small dedicated benchmark (not touki.perf itself — something tiny and stable, e.g. a deliberately hot string loop) captured three ways: net10 `[EventPipeProfiler]` `.nettrace` + `.speedscope.json`, and net481 `[EtwProfiler]` `.etl`, plus the `.etlx` conversion of the latter (which is also the O1 fixture). Each fixture ships with its build output so `--symbols` line-level tests run.
- **Storage:** a tiny smoke `.nettrace` (< ~5 MB) committed in-repo; the full corpus attached to a GitHub release and pulled by tests on demand (avoids LFS, which the Copilot cloud agent needs extra setup for, and keeps clones light). A `make-fixtures` script regenerates the corpus on a Windows machine when TraceEvent or BDN versions move.
- **Parity assertions:** `traceq` self/inclusive/callers rankings vs. `Get-TraceHotspots.ps1` and touki.mcp `analyze` on identical inputs, within a small relative tolerance and identical row ordering for the top N. Once M1 exits green, the legacy oracles are frozen — drift after that is a `traceq` bug by definition.

---

## 4. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| O1 fails (ETLX not cleanly readable off-Windows) | Cross-machine hand-off story collapses to "analyze where collected" | **Resolved (spike, 2026-06-07): O1 passes** - a Windows-converted `.etlx` resolves managed frames byte-identically on Ubuntu 26.04, so the hand-off is advertised; only the `.etl` -> `.etlx` conversion stays Windows-only |
| Fixture traces too large / unstable across TraceEvent versions | Flaky parity, heavy repo | Release-attached corpus + tiny in-repo smoke trace; regeneration script; pin TraceEvent and record its version in fixture metadata |
| pvanalyze code reuse | License exposure — repo has **no LICENSE file** | Already mitigated: patterns only, asserted in §1; nothing copied |
| Description/help tuning overfits one host | Looks great in Claude Code, mediocre in Copilot CLI | Two-host eval arms from the first baseline (M5) |
| Eval flakiness and cost (N = 10 × arms × tasks) | Noisy gates, slow CI | Full matrix only on description/skill/help changes; CI runs the 3-task smoke; budgets compare medians |
| Schema-token counting is approximate | Budget check drifts from real tokenizers | Use a real tokenizer package for the check; treat 1.5k as soft with a hard 2k fail |
| Single binary serving CLI + MCP confuses packaging | `dnx` shim vs. tool install ambiguity | §9 of the design already splits identities: `TraceQ` tool, `TraceQ.Mcp` shim invoking the same assembly; document the equivalence in both READMEs |
| Scope creep toward PerfView parity | v1 never ships | Non-goals restated at every milestone review; backlog is the pressure valve |
| Solo-maintainer stall mid-extraction | Half-moved core, two sources of truth | M1 is deliberately small and gated by parity; touki.mcp stays untouched and working until M6 flips the switch |
| Coupling creep during incubation | Promotion becomes archaeology instead of mechanics | Self-containment rules (M0) enforced by the extraction-rehearsal CI check on every traceq-touching PR |
| Private-feed auth friction (CI, local, cloud agent) | Silent install failures in exactly the environments evals run in | Feed-auth snippets are M4 deliverables; the eval harness authenticates the same way consumers do; the feed's lifespan is bounded — it dies at v1.0 |

---

## 5. Development workflow

**Inner loop.** Through M3, everything is a plain `ProjectReference` inside the Touki solution — the tightest possible loop, and the reason incubation won. After promotion the repo is self-sufficient (builds from source), and Touki touches `traceq` exactly once more, at M6, via the private feed. The only cross-repo coordination in the entire plan is that one migration PR.

**Agents build it.** The repo's own AGENTS.md/copilot-instructions are seeded at M0 precisely so Claude Code / Copilot CLI / the cloud agent can take milestone tasks; from M5 the harness scores their output on the surfaces that matter (descriptions, help, skill). The pre-PR self-review convention from Touki — every perf claim carries a benchmark or an explicit "not measured" — applies here as "every surface-text change carries an eval delta or an explicit 'not measured.'"

**Definition of done, globally:** both-OS CI green, golden files updated deliberately (never regenerated blind), parity green, and — once M5 lands — eval smoke green.

---

## 6. Resolved decisions (2026-06-04)

| # | Question | Decision | Consequences baked into this plan |
|---|---|---|---|
| Q1 | Where does this live? | **Incubate in Touki; promote at the M3½ gate** | Self-contained `traceq/` subtree with its own build props (M0); extraction-rehearsal CI check enforcing zero coupling; promotion is an explicit milestone (M3½) with the naming gate attached; discovery/identity deliberately deferred until the facade proves itself |
| Q2 | Publishing cadence | **Private GitHub Packages feed until v1.0** | First publish at M4 (post-promotion) with final IDs; feed-auth documentation including the cloud-agent secret snippet is an M4 deliverable; public NuGet + MCP registry + badges land at v1.0 (M6); Trusted Publishing wired dormant at promotion |
| Q3 | `touki.mcp` at parity | **Retire it; Touki references `TraceQ.Core`** | M6 deletes the project; fold-list/BDN specifics survive as thin config + skill; the `Get-TraceHotspots.ps1` fallback decision is recorded at M6 |

For the record, the road not taken on Q1: extract-now was recommended on identity grounds (a product accrues stars/issues/registry presence from day one). Incubation trades that for the tightest dogfood loop and a deferred commitment — a trade the rehearsal check and the M3½ gate are designed to keep cheap. If incubation drags past M3 with the facade green, that's the signal the deferral has stopped paying.

Recorded in the design document's decision log as **D15–D17**.

### AOT - full Native AOT is a goal (reverses D13; 2026-06-09)

The published heads (the `TraceQ` CLI and the `TraceQ.Mcp` server) target full
Native AOT. This reverses D13's "non-AOT" framing: non-AOT is now the *interim*
state, not the end state. Blockers, in dependency order:

1. **TraceEvent** (`Microsoft.Diagnostics.Tracing.TraceEvent`) - the long pole.
   Reflection, dynamically built event parsers, and ETW native interop; no AOT
   annotations, so neither AOT- nor trim-safe. It is mandatory (every analysis
   path reads a trace through it) and flows transitively to both heads, so the
   whole graph stays non-AOT until it is made AOT-safe or replaced. No
   `IsAotCompatible` / `PublishAot` flag goes on any traceq project until this
   clears - the per-assembly analyzer would pass while a real publish still
   fails.
2. **Reflection-based `System.Text.Json`** in `OutputJson` - **done.** A
   `TraceQJsonContext` source-generation context declares every closed
   `AnalysisResult<T>` the CLI and MCP heads serialize; `OutputJson` seeds its
   options from that context and serializes through the resolved `JsonTypeInfo`,
   so the reflection IL2026/IL3050 are gone (verified by the per-assembly AOT
   analyzer). The custom double-rounding converter and relaxed encoder are layered
   on the runtime options under metadata-mode generation, keeping the golden output
   byte-identical. `TraceInfoView` moved from the MCP head into `TraceQ.Core/Output`
   so one context covers all payloads. A new payload type must be registered in the
   context or `Serialize` throws `NotSupportedException` (pinned by a test).
3. **Reflection-based MCP tool discovery** - `WithToolsFromAssembly()` (warns
   IL2026) becomes the generic `WithTools<T>()`, which requires `TraceTools` to
   stop being a static class.

Item 3 is inside our control and can land incrementally; item 1 gates the actual
AOT publish.

### D-N — Naming (gate: M3½ promotion; availability pass: M0)

The promoted repo and the first private packages need the final name, so the *decision* gates promotion — but the availability pass is an M0 task because it's cheap, and a squatted ID discovered mid-promotion would be annoying. Criteria: ≤ 7 chars for the tool command, unsquatted on NuGet (`<Name>`, `<Name>.Core`, `<Name>.Mcp`) and as a GitHub repo, no collision with existing .NET perf tooling (note: "TraceLens" is taken by an existing tracing product), pronounceable in a sentence ("just traceq it"). Current placeholder `traceq` plausibly survives; alternatives worth the pass: `perfq`, `hotpath`, `stackq`. Bikeshedding is not an M0 task.

---

---

## Addendum A - PerfView capabilities not in TraceEvent's public surface

Where a workflow has no public TraceEvent path, here is what is missing, where it lives, the lift to expose it, and traceq's stance. Sourced from the automation guide's section 10 (\"what needs to be factored out of PerfView\"), ordered by how often it bites a real investigation.

| Capability | Lives in | Why it is stuck in PerfView | Lift | traceq stance |
|---|---|---|---|---|
| **Net Mem / Gen 2 object deaths** (the *net surviving heap* family) | `GCHeapSimulator` (PerfView assembly) | Lives in PerfView, but its dependencies are **TraceEvent-only** - no WPF/GUI; it replays alloc ticks + survival/movement events to model live objects | Medium - move to `TraceEvent/Computers/` as a `GCHeapNetMemComputer` emitting a `MutableTraceEventStackSource`, mirroring `ThreadTimeStackComputer` | **Factor later** (backlog). It is the *only* thing blocking the net-mem family; until then the alloc-rate family answers most \"too much allocation\" questions and `gcstats` covers heap growth. |
| **Heap-snapshot capture** (`.gcdump`) | `GCHeapDumper` (HeapDump assembly) | ClrMD + Windows-only native interop; a separate assembly, not packaged for reuse | Low if shelling out; Medium to repackage | **Shell out** to `dotnet-gcdump` (cross-platform) as the supported capture path. *Reading* a `.gcdump` is already library code (`GCHeapDump` + `MemoryGraph` + `MemoryGraphStackSource`), so the retention family's **analysis ships without this lift**. |
| **Collection triggers** (`/StopOnGCOverMsec`, circular buffer, decay, delay) | PerfView `CommandProcessor` state machine | Orchestrates a live session with counter/GC/event watchers that `TraceEventSession` itself does not model | Large | **Out of scope** until a `collect` verb (backlog). Document shelling out to `PerfView collect` or `dotnet-trace` for trigger-based capture (Addendum B). |
| **Alloc type-name fallback** | `TypeNameSymbolResolver` (internal) | `internal`; only needed when an alloc tick lacks `TypeName` | Small | **Re-derive** the few lines if the alloc family needs the fallback (resolves the type from `TypeID` against the module PDB). |
| **`CPUStacks` / `ThreadTimeStacks` builder helpers** | `internal` in TraceEvent `AutomatedAnalysis` | The TraceEvent copies are `internal`; PerfView re-implements them | Trivial | **Replicate** the ~6 lines (the automation guide prints them); optionally upstream a \"make these `public`\" PR to remove the copy-paste for every consumer. |
| **GCStats / JITStats HTML** | PerfView `GcStats.cs` etc. | Rendering only; the *data* (`TraceGC`, `JITStats`) is already public | n/a for data | **Not needed.** traceq emits structured records + dense text; a markdown/HTML renderer is a thin optional add. |

The pattern is consistent: **PerfView keeps the capture-time and GUI-rendering code; TraceEvent owns the data model and analysis.** Every traceq *analysis* family is therefore reachable today except net-mem (one clean lift) and heap *capture* (shell out). The two lifts worth an eventual upstream PR are `GCHeapSimulator` (unblocks a whole family) and making the `AutomatedAnalysis` stack-builder helpers public (removes boilerplate for every TraceEvent consumer, not just traceq).

---

## Addendum B - Tools and viewers to integrate

traceq's stance is to **own analysis** and **integrate, not reimplement, capture and rich rendering**: it consumes what the capture tools produce and emits what the viewers consume, keeping the core small and cross-platform.

### Capture (upstream of traceq)

| Tool | Captures | Platform | Role in the flow |
|---|---|---|---|
| **`dotnet-trace`** | EventPipe `.nettrace` (CPU, GC, alloc, exceptions, EventSource) | cross-platform | The primary capture path traceq analyzes; the EventPipe analog of `PerfView collect`. |
| **`dotnet-gcdump`** | `.gcdump` heap snapshot | cross-platform | The supported retention-family capture (pairs with analysis traceq already has). |
| **`dotnet-counters`** | live counters (no trace) | cross-platform | Pre-trace triage - \"is it GC? threadpool? alloc?\" - that tells the agent *which family to capture*. Natural upstream step; not analyzed by traceq. |
| **PerfView `collect`** | ETW `.etl(.zip)` | Windows | The richest capture: machine-wide, kernel, thread-time, triggers. traceq analyzes the output; shell out here for trigger-based collection. |
| **`perfcollect`** | `perf_event` + LTTng `.trace.zip` | Linux | Native + kernel CPU on Linux; opens in PerfView. A future ingestion target (backlog). |
| **BenchmarkDotNet `[EventPipeProfiler]` / `[EtwProfiler]`** | `.nettrace` / `.etl` into `BenchmarkDotNet.Artifacts/` | both TFMs | The touki-native capture the whole effort started from; the `--benchmark` resolver targets exactly this shape. |

### Viewers (downstream of traceq `export`)

| Viewer | Fed by | Why |
|---|---|---|
| **[speedscope.app](https://speedscope.app)** | `export --format speedscope` | Web flame graph, zero install; already in the touki toolchain. |
| **[Perfetto UI](https://ui.perfetto.dev) / `chrome://tracing`** | `export --format chromium` | Timeline + flame view; shareable trace link. |
| **PerfView** | `export --format perfview`, or open the raw `.etl` | The full GUI when an investigation outgrows the agent (Net-Mem, heap snapshots, triggers). |
| **WPA (Windows Performance Analyzer)** | the same `.etl` | Kernel/ETW deep dives beyond the managed surface. |
| **`tools/speedscope-to-flamegraph.ps1`** | the speedscope export | Inline SVG flame graph for a PR or doc. |
| **MCP resources / MCP Apps** | (post-1.0) | Inline interactive flame graph in the agent surface itself. |

The columns make the integration boundary explicit: anything in the capture table is a process traceq *invokes or ingests from*; anything in the viewer table is a file traceq *writes*. Neither is reimplemented in the core.

---

*Plan ends. The design document's decision log carries the delivery decisions as D15-D17; this repo copy adds the surface-area section (1A) and Addenda A-B from the 2026-06-06 PerfView pass.*
