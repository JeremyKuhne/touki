# filtrace manual demo - user-oriented prompts

A short, manual follow-along for exercising **filtrace** from Copilot Chat the way
a real touki developer actually talks to it: outcome questions - "how long does
this take?", "how much does it allocate?", "where's the time going?", "make this
faster?" - not tool-by-tool instructions.

> Drive this from **Copilot Chat in Agent mode**. Paste each prompt as-is and
> watch the agent *lead*: translate the question into a measurement, write or run
> a benchmark, capture a trace when it needs one, drill in, and answer **in your
> words**. If it hands you a command to run, or makes you pick a tool, that is a
> skill gap worth noting - leading is the whole point. The behavior is shaped by
> the [performance-testing skill](../.agents/skills/performance-testing/SKILL.md)
> and its [interpreting-requests](../.agents/skills/performance-testing/interpreting-requests.md)
> page.

## Setup (one time)

filtrace is published to NuGet (github.com/JeremyKuhne/filtrace). The MCP server
entry in [.vscode/mcp.json](../.vscode/mcp.json) runs version 0.6.0 on demand via
`dnx` (the .NET 10 SDK tool runner) - no clone or build required. Start it from
the Command Palette (*MCP: List Servers* -> `filtrace` -> *Start Server*) and
confirm the 17 `trace_*` tools register in the chat tool picker.

---

## The prompts

Each step is the prompt to paste plus what a good answer looks like. They build
on each other - later steps reuse the scenario and trace the earlier ones set up.

### 1. "How long does this take?"

> **Prompt:** How long does it take Touki to format a string with `ValueStringBuilder` compared to a plain `StringBuilder`?

**Good:** the agent pins a scenario (what's formatted, how big - by asking or by
stating a representative one), finds or adds a `touki.perf` benchmark, runs it on
**both** net10 and net481, and answers in nanoseconds with the comparison and any
TFM divergence. Not a "here's a command to run."

### 2. "How much memory does it use?"

> **Prompt:** Does Touki's glob matching allocate when it checks a path against a pattern? How much?

**Good:** a `[MemoryDiagnoser]` benchmark over a representative pattern/path,
reported as **bytes per operation** on both TFMs ("allocation-free (0 B)" or
"N bytes - one array of ..."), with an offer to find the allocation site if there
is one.

### 3. "Where's the time going?" (net10)

> **Prompt:** Enumerating files with Touki's extended-glob matcher feels slow. Where's the time actually going?

**Good:** the agent recognizes this needs a *profile*, not just a benchmark. It
uses the bundled capture helper to capture the relevant `touki.perf` benchmark,
retains its run manifest and exact generated-child symbols, then drives
**rank -> callers -> lines** with `benchmark: true` on root-aware MCP tools and
reports the hot method/line in plain language. It reads ambiguity, thin-scope,
provider-state, and source-resolution warnings before interpreting percentages.
Follow up:

> **Prompt:** Show me that hot source file with the time marked on each line.

(should produce a per-line heat map ordered to overlay on the source.)

### 4. "...and on .NET Framework?" (the net481 / ETW lesson)

> **Prompt:** Is the hot spot the same on .NET Framework 4.8.1, or different?

**Good:** the agent does **not** reuse the net10 ranking - it knows EventPipe is
net10-only and that net481's weaker inlining can move the hot frame *entirely*.
It captures an **ETW** trace (via [tools/Capture-EtwTrace.ps1](../tools/Capture-EtwTrace.ps1),
which self-elevates with visible progress), scopes it with `--process`, and
reports the net481 hotspot - which may be a different method than net10. In the
glob case it was: a method that was ~1.5% on net10 was ~56% on net481. A bare
"same as net10" without an ETW capture is the failure to watch for.

### 5. "Which process is this even measuring?" (multi-process scope)

> **Prompt:** That ETW capture is machine-wide. How do I make sure we're looking at the benchmark and not something else running on my box?

**Good:** the agent runs `filtrace processes <etl>` to list the processes by weight,
then scopes every later query with `--process touki.perf` (or the right name).
It should explain that an `.etl` is machine-wide, that the auto-scope picks the
most-sampled process, and that a noisy background app can otherwise steal the
ranking.

### 6. "Where does the runtime spend the time - zeroing or copying?"

> **Prompt:** Of that net481 time, how much is the runtime zeroing memory versus copying strings versus GC?

**Good:** answering this needs native runtime symbols, which filtrace resolves on
demand. The agent opts in with `--native-symbols` (frames pulled from the
Microsoft public symbol server and cached locally), then runs `classify` (the
`trace_classify` tool) to bucket the CPU time into zeroing / copying / GC / JIT /
other - the direct answer to "zeroing or copying?". Without `--native-symbols`
the native leaf (memset / memcpy / GC) shows as an unresolved `?` (~10% in the
glob trace), so the failure to watch for is the agent guessing at the native cost
instead of opting in.

### 7. "Help me make it faster" - the full journey

> **Prompt:** Help me improve the performance of the extended-glob matcher's inner matching loop.

**Good:** the agent walks the loop out loud - (1) pin the scenario, (2) baseline
benchmark on both TFMs, (3) profile and drill to the hot line with evidence,
(4) propose a change tied to that evidence (reaching for the
`framework-jit-optimization` / `scratch-buffer-strategy` skills for the *how*),
(5) re-run both TFMs and confirm the targeted line moved and nothing (especially
`Allocated`) regressed, (6) offer the next drill. It should reuse the existing
benchmark/trace rather than starting over.

### 8. "Compare every scenario before and after" (manifest diff/batch)

> **Prompt:** Compare the before and after traces for every parameterized benchmark case. Which targeted frame moved, and did any case regress?

**Good:** the agent uses `trace_batch` for a compact ranking across each capture
manifest and `trace_diff` to pair cases by exact benchmark + parameters. It keeps
root/process/BenchmarkDotNet scope consistent, reports scope-share and normalized
changes, and emits per-operation values only when both manifests have matching
operation count and unit metadata. It drills individual cases only after the batch
view identifies them.

---

## Warm-up: you already have a trace

If you just want to exercise the analysis tools against a trace that already
exists, point the agent at any trace you have and ask. The examples below use
filtrace's own committed test fixtures, so they assume the filtrace repo is
checked out at `../filtrace`; substitute any `.nettrace` / `.speedscope.json`:

> - **Prompt:** What's in `../filtrace/tests/Filtrace.Core.Tests/Fixtures/folding.speedscope.json`, and is anything obviously off? (`trace_info` - resolution rate, threads)
> - **Prompt:** Where's the CPU time in that trace? (`trace_rank` cpu, then a steer toward the hottest frame's callers)
> - **Prompt:** What's allocating the most in `../filtrace/tests/Filtrace.Core.Tests/Fixtures/alloc.nettrace`? (`trace_rank` alloc - answered in **bytes**, not time)

---

## Red flags (a skill or tool gap, worth noting)

- Hands back a `dotnet run ...` command instead of running the measurement.
- Measures without pinning a scenario, or measures only one TFM when the code
  targets both.
- Reads a net481 hotspot off a net10 EventPipe trace (see step 4).
- Pastes a raw table and stops, instead of answering the question and offering
  the next drill.
- Uses a supported file format as proof that a provider was enabled instead of
  checking `analyses.<name>.captureStatus` and `eventCount`.
- Trusts a 1.0 frame-name resolution rate for source lines without checking
  `sourceResolution` and exact PDB identity.
- Ignores `contributingRecordCount`/line attribution warnings for a thin periodic
  CPU scope, or applies periodic-sample thresholds to evented speedscope records.
- On a machine-wide `.etl`, ranks without scoping to a process (see step 5).
- Guesses at native runtime cost instead of opting into `--native-symbols` to
  resolve it (see step 6).

For the CLI equivalents and every verb's options, see the
[filtrace README](https://github.com/JeremyKuhne/filtrace) and
[tools/Capture-EtwTrace.ps1](../tools/Capture-EtwTrace.ps1).
