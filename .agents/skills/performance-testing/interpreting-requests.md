# From a user's question to a measurement

Detail for the [performance-testing](SKILL.md) skill. The other sub-pages cover
the *mechanics* - authoring a benchmark, running it, reading the columns. This
page covers the step before all of them: turning a vague, outcome-shaped user
question into a concrete scenario, the right measurement, and a useful answer.

Users almost never ask in tooling terms. They ask "how long does this take?",
"how much memory does this use?", "where is the time going?", or "help me make
this faster". They do not know to capture a trace, write a benchmark, pick a
scenario, or drill from method to line - **that translation is your job.** Lead
them through it; do not hand back a tool and a shrug.

In this page, *profile* / *the trace analyzer* refers to whatever profiling tool
the consuming repo wires in its overlay (capture an EventPipe or ETW trace, rank
the hot frames, drill to callers and source lines). The mechanics of that tool
live in the overlay's profiling page; the judgment about *when* to reach for it
lives here.

## Map the question to the workflow

| The user asks | They want | What you do |
|---|---|---|
| "How long does X take?" / "Is X fast enough?" | a latency number for a real scenario | benchmark the scenario, report `Mean` + error on both TFMs ([authoring](authoring.md), [running](running.md)) |
| "How much memory does X use?" / "Does X allocate?" | bytes per operation | `[MemoryDiagnoser]` benchmark, report `Allocated` on both TFMs ([interpreting-results](interpreting-results.md)) |
| "Where is the time spent?" / "Why is X slow?" | the hot method or line | profile a trace (EventPipe on the modern runtime, **ETW on .NET Framework**), rank -> callers -> lines; if a thin driver/wrapper tops the modern ranking, cross-check under ETW - inlining can misattribute self-time to the host |
| "What's allocating?" / "What's the GC doing?" | the hot alloc site / GC pressure | the allocation or GC view of a trace |
| "Make X faster" / "improve this method" | a *verified* improvement | the full loop below: scenario -> baseline -> profile -> change -> re-measure |
| "Did my change help / regress anything?" | a before/after delta | baseline before, re-run after, diff both TFMs ([interpreting-results](interpreting-results.md)) |

## First, nail the scenario - a method has no single "speed"

Cost depends on inputs. Before writing any benchmark, decide *what* to measure.
Read the method and answer as much as you can from the code, then ask the user
only what the code cannot tell you. Offer a default with every question so they
can just say "yes".

Things worth pinning down:

- **Inputs and sizes.** Typical case, and the worst case that matters. For a
  span/string API: empty, small, large, and any pathological shape (all-match,
  no-match, deep nesting). Propose a representative set.
- **Target framework(s).** Default to **both** the modern and the Framework
  target when the code compiles for both - results diverge and that divergence is
  often the point. Only narrow to one if the user says so or the code is
  `#if NET`-only.
- **Latency vs throughput.** A per-call latency number and a bulk-throughput
  number answer different questions; ask which they care about.
- **A baseline to compare against.** "Fast" is meaningless alone. Compare against
  the BCL equivalent, the previous implementation, or a stated target.

**Do not stall on questions you can answer yourself.** If the inputs are obvious
from the signature, state the scenario set you picked ("I'll measure empty, a
32-char, and a 4 KB input on both TFMs") and proceed. Asking the user to spell
out what you could have read is its own failure.

## The "make X faster" journey - lead them through it

This is the request that most needs handholding. Walk these steps out loud, one
at a time, so the user sees the method:

1. **Find the scenario.** Read the method, identify the inputs that drive cost,
   propose a representative set. Confirm or proceed with stated assumptions.
2. **Establish the baseline.** Write a benchmark in the perf project
   ([authoring](authoring.md)), run it on both TFMs, and report the table. This
   *is* the answer to "how slow is it today", and it is the thing every later
   change is measured against. The benchmark stays in the repo as the regression
   guard - do not throw the scenario away.
3. **Find where the cost is.** Profile the baseline: rank -> callers -> lines for
   CPU, or the allocation view if `Allocated` is the problem. Tell the user the
   hot spot in plain language ("62% of the time is in the inner copy loop at
   `Parser.cs:214`; it re-walks the segment each pass").
4. **Form a hypothesis tied to the evidence, then change one thing.** Connect the
   edit to what the profile showed. For *how* to write the hot path, branch to
   the codegen skills: the framework-JIT-optimization skill for specialization /
   unrolling / BCL-delegation, and the scratch-buffer-strategy skill for
   stackalloc vs pool vs a stack-with-pool-fallback buffer.
5. **Verify on both TFMs.** Re-run the baseline benchmark, diff against the saved
   numbers, and confirm the *targeted* frame actually moved and nothing regressed
   - especially `Allocated`, and especially the *other* TFM
   ([interpreting-results](interpreting-results.md)). A faster wall clock with
   the targeted frame unchanged is noise or a different win; say so.
6. **Report and offer the next drill.** Show the before/after for both TFMs and
   the line-level evidence, then suggest the logical follow-up.

## Translate the numbers back into the user's words

Do not paste a raw table and stop. Answer the question they asked:

- *How long?* -> "About 180 ns per call for a typical 32-char input, rising to
  ~12 us for a 4 KB input. That's roughly 1.4x the BCL's `string.Format` on the
  modern runtime, and 3x faster on .NET Framework where the BCL path has no
  vectorization."
- *How much memory?* -> "It's allocation-free on the span path (0 B). The string
  overload allocates one result string of N bytes and nothing else." or "It
  allocates a 1 KB array per call - that's the buffer it never pools."
- *Where?* -> "Two thirds of the time is in `<method>` at `<file:line>`, doing
  `<what>`. The rest is the bounds checks the loop repeats."

Always surface a **both-TFM divergence** when there is one - it is frequently the
most actionable thing you can say.

## Follow up - suggest the next question, don't wait for it

After every result, the user usually has a next question they have not formed
yet. Offer it:

- After a **latency** number -> "Want the allocation profile too, or a comparison
  against the BCL / your previous version?"
- After a **memory** number -> "Want me to find the allocation site, or try a
  pooled / `stackalloc` version and measure it?"
- After a **hot spot** -> "Want me to try `<specific change the evidence
  suggests>` and measure whether it helps?"
- After a **hot spot whose call-tree *shape* is the interesting part** (deep
  recursion, a fan of shallow callers, or you want to show an
  attribution flip visually) -> "Want me to open this as an interactive flame
  graph so you can explore it?" Offer this *after* the line-level answer, not
  instead of it - the direct line/heat drill is the more practical result.
- After a **shallow line/heat map** (too few samples to attribute per line) ->
  "The trace sampled too coarsely to pin the hot lines; want me to re-capture
  under ETW (~1 kHz) for a deeper heat map?"
- After a **modern-runtime method ranking topped by a driver/wrapper frame** (its
  body is mostly a call into a loop) -> "EventPipe may be crediting an inlined
  callee's time to its host; want me to re-capture under ETW - which resolves the
  inlinee - and diff the two?" Lengthening the run will not fix this; only ETW
  reattributes it.
- After an **improvement** -> "Want me to push the scenario harder (larger input,
  worst case), or check whether the win holds on the other TFM?"
- After a **surprising divergence** -> "The .NET Framework path is 3x slower
  here; want me to dig into why (likely the missing vectorized BCL API)?"

## Anti-patterns

- **Handing back tooling instead of an answer.** "You can run a benchmark with
  `dotnet run ...`" is a failure when the user asked "is this fast?". Run it and
  tell them.
- **Measuring without a scenario.** A benchmark over an unrepresentative input
  answers a question nobody asked. Pin the scenario first.
- **One TFM when the code targets both.** The divergence is usually the story -
  and on .NET Framework it is often not just a different number but a different
  *hotspot*: the Framework JIT inlines far less, so the hot frame can move
  entirely. Profile .NET Framework directly under ETW rather than reading its
  hotspot off the modern-runtime EventPipe trace.
- **Trusting the modern-runtime EventPipe *method* ranking under heavy
  inlining.** It credits an inlined callee's self-time to its physical host, so a
  thin driver/wrapper can top the ranking while the real hot loop reads near-zero
  - the same hotspot move, this time between *capture methods* on one TFM.
  Cross-check under ETW when a wrapper tops the ranking; duration will not fix it.
- **A single Mean with no error bars or allocation column.** Sub-microsecond
  deltas are usually noise; trust `Allocated` and deltas outside `Error`/`StdDev`
  ([interpreting-results](interpreting-results.md)).
- **Optimizing before profiling.** "Where is the time" has an evidence-based
  answer; guessing wastes the change.
- **Declaring victory from a wall-clock drop alone.** Confirm the targeted cost
  moved and nothing else regressed.
