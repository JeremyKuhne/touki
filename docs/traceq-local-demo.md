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

filtrace lives in its own repo (github.com/JeremyKuhne/filtrace), cloned beside
touki at `../filtrace`. Build the MCP server and register it, then start it from
the Command Palette (*MCP: List Servers* -> `filtrace` -> *Start Server*):

```pwsh
dotnet build ../filtrace/src/Filtrace.Mcp/Filtrace.Mcp.csproj -c Release
```

The server entry lives in [.vscode/mcp.json](../.vscode/mcp.json) (rebuild the
DLL and restart the server after changing filtrace). Confirm the 13 `trace_*`
tools register in the chat tool picker.

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
captures an EventPipe trace of the relevant `touki.perf` benchmark (`-p EP`),
then drives **rank -> callers -> lines** scoped past the BenchmarkDotNet harness
to the real work, and reports the hot method/line in plain language. Follow up:

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

**Good (target, gated on the runtime-symbol plan):** answering this needs native
runtime symbols, which is the next milestone - see
[§7.6 of the implementation plan](traceq-implementation-plan.md#76-whats-next-the-runtime-symbol-plan-managed--unmanaged).
Until it lands, a good answer is honest: managed frames resolve, but the native
leaf (memset / memcpy / GC) currently shows as an unresolved `?` (~10% in the
glob trace), and the plan is `--native-symbols` + a `classify` view that buckets
the work into zeroing / copying / GC / JIT. Watch that the agent says what it
*cannot* yet measure rather than guessing.

### 7. "Help me make it faster" - the full journey

> **Prompt:** Help me improve the performance of the extended-glob matcher's inner matching loop.

**Good:** the agent walks the loop out loud - (1) pin the scenario, (2) baseline
benchmark on both TFMs, (3) profile and drill to the hot line with evidence,
(4) propose a change tied to that evidence (reaching for the
`framework-jit-optimization` / `scratch-buffer-strategy` skills for the *how*),
(5) re-run both TFMs and confirm the targeted line moved and nothing (especially
`Allocated`) regressed, (6) offer the next drill. It should reuse the existing
benchmark/trace rather than starting over.

---

## Warm-up: you already have a trace

If you just want to exercise the analysis tools against a trace that already
exists, point the agent at a committed fixture and ask:

> - **Prompt:** What's in `..\filtrace\tests\Filtrace.Core.Tests\Fixtures\folding.speedscope.json`, and is anything obviously off? (`trace_info` - resolution rate, threads)
> - **Prompt:** Where's the CPU time in that trace? (`trace_rank` cpu, then a steer toward the hottest frame's callers)
> - **Prompt:** What's allocating the most in `..\filtrace\tests\Filtrace.Core.Tests\Fixtures\alloc.nettrace`? (`trace_rank` alloc - answered in **bytes**, not time)

---

## Red flags (a skill or tool gap, worth noting)

- Hands back a `dotnet run ...` command instead of running the measurement.
- Measures without pinning a scenario, or measures only one TFM when the code
  targets both.
- Reads a net481 hotspot off a net10 EventPipe trace (see step 4).
- Pastes a raw table and stops, instead of answering the question and offering
  the next drill.
- On a machine-wide `.etl`, ranks without scoping to a process (see step 5).
- Guesses at native runtime cost it cannot yet resolve (see step 6).

For the CLI equivalents and every verb's options, see the
[filtrace README](https://github.com/JeremyKuhne/filtrace) and
[tools/Capture-EtwTrace.ps1](../tools/Capture-EtwTrace.ps1).
