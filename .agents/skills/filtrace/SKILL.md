---
name: filtrace
description: Analyze .NET CPU, allocation, exception, GC, JIT, and wall-clock (thread-time) traces - .nettrace, .etl, and speedscope captures - with the filtrace CLI or MCP server. Use when a user asks where time or memory goes in a trace or benchmark, which method or source line is hot, why a run regressed against a baseline, what a captured .nettrace / .etl contains, or to rank / drill / diff / export a profile - including profiling .NET Framework (net481) via ETW, where an EventPipe ranking would mislead.
compatibility: Pairs with the filtrace MCP server (the KlutzyNinja.Filtrace.Mcp package, run via `dnx`) for in-agent tool calls; otherwise shells out to the filtrace CLI (the KlutzyNinja.Filtrace global tool). Either head provides the same analysis, so the skill degrades gracefully to whichever is installed.
license: MIT
metadata:
  github-path: .agents/skills/filtrace
  github-pinned: 04fd8573ef801b98aeb77442642aca9180f0a49e
  github-ref: refs/heads/main
  github-repo: https://github.com/JeremyKuhne/filtrace
  github-tree-sha: f75c6b3147d52a5782e715c2b9578e439cbd9455
  portability: repo-specific
---

# Analyzing .NET traces with filtrace

filtrace ranks, drills into, diffs, and exports CPU / allocation / exception /
GC / JIT / thread-time profiles from `.nettrace`, `.etl`, and speedscope
captures, from both modern .NET and .NET Framework. It is a command-line tool and
an MCP server - there is no GUI. Output is dense text by default, or compact JSON
(`--format json`). It runs on .NET 10 but reads traces from any runtime.

This skill is the *how*; the full reference is single-sourced in
[docs/workflow.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/workflow.md)
and [docs/traps.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/traps.md).

## The workflow: orient -> rank -> drill -> compare

Almost every investigation is the same four moves:

1. **Orient.** Read the trace's format, sample count, and symbol-resolution rate
   first - `filtrace processes <trace>` or the `trace_info` tool. A
   symbol-resolution rate **below 0.8** means managed frames are missing and the
   rankings cannot be trusted; pass `--symbols <build-output-dir>` (the directory
   holding your portable PDBs) before reading further.
2. **Rank.** Find the hottest frames by the metric that matches the question -
   `cpu`, `alloc`, `exceptions`, or `threadtime` (or `rank --metric <m>`).
   Self-time finds the leaf that burns the resource; inclusive-time finds the
   subtree that drives it.
3. **Drill.** Follow the hot frame into detail: `callers <frame>` (who calls it),
   `lines` / `heatmap <file>` (which source lines), or `tree` (what it calls).
4. **Compare.** `diff <before> <after>` to see what regressed or improved, or
   `export --format speedscope` to hand a human a flame graph.

```pwsh
filtrace cpu app.nettrace                    # 1-2. orient + rank self-time
filtrace callers app.nettrace MyApp.Parse    # 3. who drives the hot frame
filtrace lines app.nettrace --symbols bin/Release/net10.0   # 3. hot source lines
filtrace diff before.nettrace after.nettrace # 4. what changed
```

<!-- filtrace:begin verbs -->
### CLI verbs

**Rank** - find the hottest frames by a metric:

| Verb | Ranks | Reads |
|---|---|---|
| `rank --metric <m>` | any metric (`cpu`, `alloc`, `exceptions`, `threadtime`) | per metric |
| `cpu` | CPU self/inclusive time | `.nettrace`, `.etl`, `.speedscope.json` |
| `alloc` | bytes allocated, by site | `.nettrace` |
| `exceptions` | throw sites, by count | `.nettrace` |
| `threadtime` | wall-clock (running + blocked) | `.etl` (Windows) |

**Drill** - follow a ranking into detail:

| Verb | Shows |
|---|---|
| `callers <frame>` | immediate callers of a frame |
| `lines` | hottest source lines of the scoped methods |
| `heatmap <file>` | per-line heat for one source file |
| `tree` | top-down call tree from the root |

**Inventory** - see what a (possibly machine-wide) capture holds:

| Verb | Shows |
|---|---|
| `processes` | processes by CPU-sample weight, to pick a `--process` target |
| `classify` | CPU time by runtime work category (zeroing / copying / GC / JIT) |

**Compare and export:**

| Verb | Does |
|---|---|
| `diff <before> <after>` | what got slower/faster between two traces |
| `export --format <fmt>` | write a flame graph for a viewer - `speedscope` or `chromium` |

**Structured reports** (EventPipe `.nettrace`):

| Verb | Reports |
|---|---|
| `gcstats` | GC counts, pauses, heap summary |
| `jitstats` | JIT method count, compile time, sizes |
| `events --name <n>` | raw events by name, paged |

**File ops** - manage the ETLX conversion cache TraceEvent keeps beside a trace:

| Verb | Does |
|---|---|
| `convert` | build the ETLX cache up front |
| `clean` | remove the ETLX cache to force a rebuild |
<!-- filtrace:end verbs -->

Run `filtrace <verb> --help` for the full option set of any verb.

## Scope and symbols

- **Scope to one process.** The stack verbs that read a multi-process `.etl`
  (`cpu`, `threadtime`, `rank`, `callers`, `lines`, `heatmap`, `tree`, `classify`)
  auto-scope to the busiest process tree. Run `processes` first to see the
  capture, then `--process <name>` to override, or `--all-processes` to widen.
  `alloc` / `exceptions` read a single-process `.nettrace` and have no process
  options.
- **Scope to the benchmark.** For a BenchmarkDotNet capture, `--benchmark` presets
  the root to the measured-workload wrapper so the harness and warmup do not
  dominate the ranking.
- **Symbols.** Managed frames (including NGEN and ReadyToRun framework methods)
  resolve for free from the trace's CLR rundown. `--symbols <dir>` resolves your
  own managed frames and source lines; `--native-symbols` (CPU `.etl` only,
  opt-in, network) names the unmanaged GC / JIT / `memcpy` frames that otherwise
  show as a `?` leaf.

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

2. **Trust the symbol-resolution rate before the rankings.** A rate below **0.8**
   (surfaced by `trace_info` / the load warning) means managed frames are missing
   and the names are unreliable - pass `--symbols <build-output-dir>`. Caveat: the
   aggregate rate conflates managed and native frames, so a net481 ETW capture can
   read low while every *managed* leaf resolves correctly; the warning hedges this
   ("managed-method rankings remain usable").

3. **On a machine-wide `.etl`, confirm the process before scoping.** filtrace
   auto-scopes to the busiest process tree ranked by **CPU-sample count** (a
   long-lived background service wins a wall-clock race but owns few samples), and
   that default is usually right - but run `processes` first to see what is in the
   capture, then pass `--process <name>` if the auto-pick is wrong.

4. **BenchmarkDotNet captures include the harness.** A raw ranking of a BDN trace
   is dominated by the orchestrator and warmup iterations, not your `[Benchmark]`.
   Pass `--benchmark` to preset the root to the measured-workload wrapper so only
   the measured code is ranked.

5. **Native runtime frames need `--native-symbols`.** Without it, the unmanaged
   ~10% of a trace - GC, JIT, `memset` / `memcpy`, write barriers - shows as an
   unresolved `?` leaf. Opt in (CPU `.etl` only; fetches PDBs from the Microsoft
   public symbol server, cached locally) to name it, then `classify` to get the
   zeroing-vs-copying-vs-GC-vs-JIT split. It is off by default so analysis stays
   offline and deterministic.

6. **Self-time and inclusive-time answer different questions.** Self-time finds
   the leaf that burns the resource; inclusive-time finds the subtree that drives
   it. Ranking by the wrong measure hides the frame you want - start with self for
   "what is hot", switch to inclusive for "what is responsible".

7. **Reading an `.etl` is Windows-only.** The ETW -> ETLX conversion needs
   Windows; once converted, the `.etlx` resolves managed frames and analyzes
   identically on any OS ("convert on Windows, analyze anywhere"). The CLI/MCP
   `.etl` paths are guarded and report a clean error off Windows.

8. **The default fold list hides runtime leaves on purpose.** It folds
   `memmove`, write-barriers, and GC-poll helpers into their managed caller -
   right for "which method is hot", wrong for "what kind of work dominates". Use
   `--no-fold` (or `classify`) to let the native leaves rank on their own.
<!-- filtrace:end traps -->

## CLI or MCP

The two heads expose the same analysis:

- **CLI** - `dotnet tool install -g KlutzyNinja.Filtrace`, then `filtrace <verb>`.
- **MCP server** - `dnx KlutzyNinja.Filtrace.Mcp` over stdio, exposing thirteen
  `trace_*` tools (`trace_info`, `trace_rank`, `trace_callers`, `trace_lines`,
  `trace_heatmap`, `trace_tree`, `trace_processes`, `trace_classify`,
  `trace_diff`, `trace_export`, `trace_gc`, `trace_jit`, `trace_query_events`).
  Each returns one envelope: a `schemaVersion`, a `warnings` list, next-step
  `hints`, and the typed result.

See [docs/workflow.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/workflow.md)
for the full verb/tool reference and the MCP config snippet, and
[docs/traps.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/traps.md) for
the trap catalog.
