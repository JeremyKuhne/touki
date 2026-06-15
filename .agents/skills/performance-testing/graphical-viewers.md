# Handing off an interactive flame graph: speedscope and Perfetto

Detail for the [performance-testing](SKILL.md) skill. The rest of
[profiling.md](profiling.md) is about the *direct* drill - `trace_rank`,
`trace_lines`, `trace_heatmap` - which is almost always the right answer. This
page is about the *optional* last resort: opening the trace in a rich interactive
flame-graph viewer for a human.

## Reach for the direct drill first

The filtrace tools answer "where is the time" at line precision, in text, with no
viewer literacy required - so they are the practical default, and the thing to
offer before a graphical viewer:

- **"Which method dominates?"** -> `trace_rank` (method self-time ranking).
- **"Which line is hot?"** -> `trace_lines` (per-`file:line` self-time, scoped to
  a method).
- **"Show the hot lines on the source."** -> `trace_heatmap` (per-line heat
  overlaid on one source file).
- **"What calls this frame?"** -> `trace_callers`.

These are faster to run, quote exact numbers, fold the JIT-helper artifacts, and
need no explanation. **Recommend them before suggesting a rich viewer.** A user
who asked "why is this slow" wants the hot line, not a tour of a flame-graph UI.

## When a graphical view is actually worth it

Offer one only when the *shape* of the data - not a single number - is the
insight, or the user wants to explore interactively. Good moments:

- **The call-tree shape is the story.** Wide-vs-deep, unexpected recursion, a fan
  of many shallow callers - a flame graph shows this at a glance where a ranked
  table flattens it.
- **Making a divergence legible to a human.** The EventPipe-vs-ETW attribution
  flip (profiling.md, reason 4) is far more convincing shown as two flame graphs
  side by side than as two tables: `ProduceAlternative` is a sliver under
  `RunEngine` in the EventPipe profile and a wide frame in the ETW one.
- **Exploring an unfamiliar method** whose call structure you do not yet know -
  clicking down the hot path interactively beats guessing `--method` filters.
- **Handing a visual to someone else** (a PR reviewer, an issue) who will not run
  filtrace.

If none of those apply, stay in the direct drill.

## Pick the viewer by export format

filtrace's `export` writes either format; the viewer follows from it. **Scope a
machine-wide `.etl` when you export** - pass `--process <name>` (CLI) or the
`process` argument (the `trace_export` MCP tool), exactly as the ranking verbs
do, or the export captures whichever process was busiest (a background app, not
the benchmark).

| | speedscope | Perfetto |
|---|---|---|
| Export format | `speedscope` (the default) | `chromium` (`--format chromium`) |
| Weight | light; 1 file, fast | heavy; 10-45x larger JSON |
| Best view | **Left Heavy** (merged hotspots) + Sandwich (caller/callee) | timeline, multi-track, SQL queries |
| Deep-link control | view only (no frame scope) | rich `startupCommands` (pin/expand/query) |
| Right when | quick hotspot shape, the EventPipe/ETW flip | wall-clock/timeline questions, SQL drilldowns |

For a CPU hotspot question - which is the usual case here - **speedscope is the
better fit**: lighter, and its Left Heavy view is exactly the merged-hotspot
picture. Perfetto earns its weight only when the question turns to wall-clock
timeline, concurrency, or SQL over the trace.

## Launch hands-free, with the right view already active

Both openers serve the file from a one-shot loopback HTTP listener with the CORS
header the web app requires, open the deep link in the default browser, serve the
file once, then exit. Nothing is uploaded; the trace stays on `127.0.0.1`. They
use different ports (speedscope 9002, Perfetto 9001), so both can be open at once
for a side-by-side.

```powershell
# speedscope, opening straight into Left Heavy (the hotspot view).
./tools/Open-SpeedscopeTrace.ps1 BenchmarkDotNet.Artifacts/flamegraphs/<name>.speedscope.json
# -View sandwich | time-ordered to open in a different mode.

# Perfetto, expanding + pinning the aggregate track so you land on the flame.
./tools/Open-PerfettoTrace.ps1 BenchmarkDotNet.Artifacts/flamegraphs/<name>.perfetto.json
```

The fully offline speedscope alternative is `npx -y speedscope <file>`: it embeds
the profile in a self-contained temp HTML and opens it, but **cannot set the
initial view** (it always opens in Time Order). Use the script when you want Left
Heavy on load; use `npx` when you want a single self-contained file with no local
server.

Why a server at all: neither web app can `fetch` a `file://` path, so a
self-contained `file://` HTML does not work for loading-by-URL. The scripts
reproduce Perfetto's own `open_trace_in_ui` approach - a loopback server plus a
deep link - which a browser allows because `127.0.0.1` is a trustworthy origin.

## Guide the user once it is open - these UIs assume knowledge

**speedscope** opens in **Left Heavy** (the opener sets `view=left-heavy`): stacks
are merged and sorted by weight, so the widest top frame is the hotspot. Tell the
user to:

- click a frame to zoom into its subtree (Escape zooms back out);
- switch to **Sandwich** (top-left mode toggle) and click a function to see its
  callers above and callees below - the visual form of `trace_callers`;
- use the search box (the magnifier) to filter frames by name - this is a UI
  control, not a URL parameter, so it cannot be preset.

**Perfetto** shows the flame graph under the pinned `filtrace` track (the opener
expands and pins it). Tell the user to:

- drag-select a time region, then read the per-frame breakdown in the bottom
  tab;
- use the **Query (SQL)** tab for ad-hoc rankings - but note the caveat below.

## Caveats that will mislead if unsaid

- **Perfetto slice durations are inclusive, not self-time.** filtrace's chromium
  export reconstructs begin/end slices from the samples, so a frame's `dur` (and
  any `SUM(dur)` SQL) is inclusive of its callees. For self-time, trust
  `trace_rank` / `trace_lines` - do not read a Perfetto SQL sum as self-time.
- **filtrace exports a single aggregate track.** The engine's rankings aggregate
  across threads, so the flame graph is one synthetic thread named by the
  export's `--name` (default `filtrace`), not the real per-thread timeline. It is a
  flame graph, not a scheduling view.
- **speedscope cannot deep-link a scope** - only the view. To focus a frame the
  user clicks it; there is no preset frame filter.
- **A flame graph is not the source of truth for a number.** It is for *shape*
  and *exploration*; quote the hot line/percentage from the filtrace drill, which
  folds the sampling artifacts the raw flame graph still shows.
