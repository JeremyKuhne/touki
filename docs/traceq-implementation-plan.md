# `traceq` implementation plan

**Status:** Active — Q1–Q3 resolved (§6)
**Date:** 2026-06-04
**Basis:** *Agentic access to TraceEvent — design document* (2026-06-04). Section references (§) and decisions (D#) below point at that document.
**Resolved path:** incubate in Touki under a self-contained `traceq/` subtree and **promote at the M3 gate** · **private GitHub Packages feed until v1.0** (public NuGet + MCP registry at 1.0) · **retire `touki.mcp` at parity**, Touki references `TraceQ.Core`.

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
| **Exceptions** (4) | a throw | count | throw-site stack | `source.Clr.ExceptionStart` over `MutableTraceEventStackSource` | **new** (design lists `Exceptions`) |
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

**2. Physical trim (a derived file) - the `trim` verb.** A machine-wide `.etl` is expensive to re-open and awkward to share. `trim` rewrites it to a smaller scenario trace - one process, an optional time window, only the providers a family needs - written back as `.etl` / `.nettrace` (or straight to `.etlx`). This is the literal \"trim the capture to the target scenario, filter out irrelevant process information\" the user asked for, and it makes every later verb and every hand-off cheaper. It extends the design's existing `convert` / `clean` verbs (M2).

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

The move itself, then the two things touki.mcp doesn't yet have: the contract and breadth.

1. Relocate the §1 assets into `TraceQ.Core` (plain copy, no history). Introduce the **two-layer service architecture** from section 1A: a `StackSourceProvider` abstraction (one per family) feeding a **provider-agnostic engine**, so `Rank`, `Callers`, `Tree`, `HotLines`, `SourceHeatmap`, `Diff`, and `Export` are written *once* and run on any provider; `TraceInfo` fronts the aggregator for the at-a-glance summary. The CPU provider is the existing aggregator; the rest (step 5) are new providers over the same `TraceLog`.
2. **Output contract (§4.4)** as a core type every service returns through: `schemaVersion`, warnings, steering hints, text renderer (dense fixed-width), compact JSON (kill `WriteIndented`), budget + 25k ceiling enforcement, deterministic ordering/rounding. Golden-file tests on both OSes pin determinism.
3. **Symbol gate:** resolution rate already computed in `TraceInfo`; add the < 0.8 warning text with remediation, and `--strict` → exit 3 semantics.
4. **Tier-1 disk ETLX cache** (clean-room reimplementation of the pvanalyze pattern: sidecar `<name>.traceq.etlx`, mtime validation, lock file, atomic publish) and **tier-2 LRU** on the existing store (it is currently an unbounded dictionary).
5. **New providers and engine verbs**, in eval-task order. *Engine* (provider-agnostic): `Diff` (task 4) via `InternStackSource.Diff`; the **filter / scope grammar** (`FilterParams`, defaulting to scenario scope) wired into every verb; `Export` to speedscope / chromium. *Providers*: `ThreadTime` wall-clock / blocked time (`ThreadTimeStackComputer` for ETW, `SampleProfilerThreadTimeComputer` for EventPipe, + `StartStopActivityComputer`) - the highest-value new family; `Alloc` (`GCAllocationTick`); `GcStats` (task 5, `TraceGC`); `EventQuery` with pagination + payload truncation (task 7); then `JitStats`, `Exceptions`, `Datas`. *Retention* (`GCHeapDump` + `MemoryGraphStackSource`) and *net-mem* (`GCHeapSimulator`, gated on the Addendum A factoring) land when an eval task demands them.
6. **The `trim` verb** (section 1A): rewrite a machine-wide capture to a scenario-scoped trace (one process, optional time window, only the needed providers). Its output feeds the same engine, so a parity test asserts `rank` over a trimmed trace matches `rank` over the parent within tolerance.
7. **O1 spike, week one:** Windows CI job converts a fixture `.etl` → `.etlx`; Ubuntu job opens it and asserts managed frame names resolve. This settles whether §7's cross-machine hand-off is advertised or downgraded — cheapest possible de-risking of the design's biggest assumption.

**Exit:** parity harness green (§3); contract golden files green on both OSes; the scenario-scope default verified (a machine-wide fixture ranks to the workload process without `--all-processes`); O1 answered with a yes/no in the design doc.

### M2 — CLI head

Full verb set over the services with the uniform global option grammar (§5.1). The verbs fall into three groups: **engine** (`rank`, `callers`, `tree`, `lines`, `heatmap`, `diff`, `export`), each taking a `--metric`/provider selector and the filter flags (`--process`, `--start`/`--end`, `--group`, `--fold`, `--fold-pct`, `--all-processes`); **family** shortcuts (`cpu`, `threadtime`, `alloc`, `gcstats`, `jitstats`, `exceptions`, `events`, `heap`) that preset the provider; and **file** ops (`convert`, `clean`, and the new **`trim`** that rewrites a machine-wide capture to a scenario-scoped trace - section 1A). The `--benchmark` resolver locates the BDN `Activity Benchmark(...)` wrapper and scopes inside it (D6 - replaces the documented-trap approach, with its own unit tests against a real BDN trace fixture); exit codes per §4.4.6. Scenario scope is the default; `--all-processes` opts out.

Help is treated as a build artifact: examples-first templates, and a CI **help lint** (top-level help lists every verb with a one-liner; per-verb help ≤ 60 lines with ≥ 2 examples; the canonical workflow appears in top-level help). README mirrors. `dotnet pack` produces a locally installable tool.

**Exit:** a human completes eval tasks 1–7 using only the CLI against fixtures; help lint green; local `dotnet tool install` round-trips on both OSes; an `export --format speedscope` opens in speedscope.app; a `trim`med trace's `rank` matches the parent within tolerance.

### M3 — MCP facade

The eight tools (§5.2) re-cut over the same services: port touki.mcp's description text, apply the D5 consolidation (`trace_rank` with `metric`), fold `list_threads` into `trace_info`, add `trace_gc`/`trace_diff`/`trace_query_events`. The broadened surface (section 1A) raises the consolidation stakes, not the tool count: the **engine** tools take a `metric`/provider parameter, so `trace_rank(metric: cpu|threadtime|alloc|…)` is *one* tool spanning every family rather than one tool per family - the token budget below is what forces this. `trace_export` and `trace_trim` join the curated set (they are how an agent hands a human a flame graph or shrinks a trace); the rich family reports (`alloc`/`jit`/`exceptions`/`heap`) stay CLI-first and are promoted to MCP only when an eval task demands it (backlog O4). Annotations (`readOnlyHint` et al.), `outputSchema` + `structuredContent` where the C# SDK supports them, the server `instructions` field carrying the workflow summary, and `traceq mcp` hosting with stderr-only logging.

Two CI checks born here and kept forever: the **stdout-purity test** (run the server under load, including a deliberately chatty logging provider, and assert nothing but JSON-RPC reaches stdout) and the **schema budget check** (serialize the tool list, fail above ~1.5k tokens). MCP Inspector smoke plus one scripted client round-trip test.

**Exit:** eval tasks 1–5 completed through the facade in Claude Code and VS Code agent mode (manual runs; the harness arrives in M5); both CI checks green.

### M3½ — Promotion

With the facade demonstrably standing alone, execute the rehearsed extraction:

1. **Name finalized** (§6, D-N) — the repo needs it now, and the first private packages at M4 need final IDs. The availability pass was an M0 task, so this is a decision, not research.
2. Copy `traceq/`'s files into a fresh `JeremyKuhne/<name>` repo at the root (a plain copy plus an initial commit; history is intentionally not carried - the parity harness, not git lineage, is the correctness guarantee, and the `touki.mcp` history was judged not worth preserving).
3. Stand up the promoted repo: port the subtree CI jobs verbatim; seed AGENTS.md / copilot-instructions trimmed from Touki's (deliberately lighter — this is a tool product, not a public API surface); enable the GitHub Packages NuGet feed; wire Trusted Publishing, dormant until the v1.0 public push.
4. **Delete the subtree from Touki** in a single commit. `touki.mcp` itself remains untouched and working — it stays the daily driver until M6 flips the switch.

**Exit:** promoted repo CI green standalone; the rehearsal check retires (it has done its job); Touki's tree clean of `traceq/`.

### M4 — Knowledge layer and distribution

Write the SKILL.md, trap catalog, and AGENTS.md snippet from `docs/` single-sourcing with the drift check (§6 of the design); generate `server.json`; self-contained non-AOT publish profiles (D13); **first packages (Core, tool, Mcp shim) land on the promoted repo's GitHub Packages feed — private until v1.0.** Feed-auth realities are an M4 deliverable because they bite every consumer: CI uses `GITHUB_TOKEN`; local installs need a PAT-backed `nuget.config` source; the Copilot cloud agent needs the credential supplied through `copilot-setup-steps.yml` env/secrets — ship that snippet alongside the §8.2 one. The NuGet MCP package-type metadata rides along now so nothing changes shape at 1.0; install badges and the MCP registry entry wait for the public publish (M6).

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

The extraction's safety net is a fixed corpus of traces with known-good answers:

- **Corpus:** one small dedicated benchmark (not touki.perf itself — something tiny and stable, e.g. a deliberately hot string loop) captured three ways: net10 `[EventPipeProfiler]` `.nettrace` + `.speedscope.json`, and net481 `[EtwProfiler]` `.etl`, plus the `.etlx` conversion of the latter (which is also the O1 fixture). Each fixture ships with its build output so `--symbols` line-level tests run.
- **Storage:** a tiny smoke `.nettrace` (< ~5 MB) committed in-repo; the full corpus attached to a GitHub release and pulled by tests on demand (avoids LFS, which the Copilot cloud agent needs extra setup for, and keeps clones light). A `make-fixtures` script regenerates the corpus on a Windows machine when TraceEvent or BDN versions move.
- **Parity assertions:** `traceq` self/inclusive/callers rankings vs. `Get-TraceHotspots.ps1` and touki.mcp `analyze` on identical inputs, within a small relative tolerance and identical row ordering for the top N. Once M1 exits green, the legacy oracles are frozen — drift after that is a `traceq` bug by definition.

---

## 4. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| O1 fails (ETLX not cleanly readable off-Windows) | Cross-machine hand-off story collapses to "analyze where collected" | Week-one spike in M1; design §7 already words the fallback |
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
| Q1 | Where does this live? | **Incubate in Touki; promote at the M3 gate** | Self-contained `traceq/` subtree with its own build props (M0); extraction-rehearsal CI check enforcing zero coupling; promotion is an explicit milestone (M3½) with the naming gate attached; discovery/identity deliberately deferred until the facade proves itself |
| Q2 | Publishing cadence | **Private GitHub Packages feed until v1.0** | First publish at M4 (post-promotion) with final IDs; feed-auth documentation including the cloud-agent secret snippet is an M4 deliverable; public NuGet + MCP registry + badges land at v1.0 (M6); Trusted Publishing wired dormant at promotion |
| Q3 | `touki.mcp` at parity | **Retire it; Touki references `TraceQ.Core`** | M6 deletes the project; fold-list/BDN specifics survive as thin config + skill; the `Get-TraceHotspots.ps1` fallback decision is recorded at M6 |

For the record, the road not taken on Q1: extract-now was recommended on identity grounds (a product accrues stars/issues/registry presence from day one). Incubation trades that for the tightest dogfood loop and a deferred commitment — a trade the rehearsal check and the M3½ gate are designed to keep cheap. If incubation drags past M3 with the facade green, that's the signal the deferral has stopped paying.

Recorded in the design document's decision log as **D15–D17**.

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
