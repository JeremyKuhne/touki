# Reading the generated code

Detail for the [performance-testing](SKILL.md) skill. A benchmark says *how
fast*; this page says *why* - it shows what the C# compiler lowered to, what IL
was emitted, and what machine code the JIT produced. Reach for it when a
benchmark result is surprising, when you need to confirm an optimization actually
happened (dead-branch elision, no defensive copy, a bounds check removed), or
when you are comparing two shapes that should differ only in codegen.

Measurement and codegen inspection answer different questions and you usually
need both: the trace says *which* code is hot, the benchmark
([interpreting-results.md](interpreting-results.md)) says *whether* a change
helped, and this page says *what the change did to the emitted code*. For reading
IL specifically to find struct copies and boxing, an IL-copy-inspection skill (if
the repo vendors one) is the deeper tool; this page is the lighter "see all three
layers" overview.

## Three layers, cheapest first

1. **C# lowering** - what `foreach`, `await`, pattern matches, interpolated
   strings, `using`, iterators, and collection expressions desugar into before
   any IL. Answers "what does this syntax actually compile to." Many "mystery
   allocations" (a captured closure, a hoisted display class, an iterator state
   machine) are visible here.
2. **IL** - the emitted bytecode. Answers "did the compiler insert a copy / box /
   call I did not write."
3. **JIT asm** - the machine code RyuJIT produced. Answers "did the bounds check
   get removed, did the `typeof(T)` branch fold away, is this a `cmov` or a
   branch." This is the layer most micro-optimizations actually target.

## sharplab.io - the fastest look

[sharplab.io](https://sharplab.io) compiles a snippet and shows IL, JIT asm, or
the C# **lowering** (the "Results" dropdown -> "C#"). First stop for "what does
this compile to." Caveats:

- It runs a **current modern .NET** JIT (x64). It is representative of the modern
  target, **not** .NET Framework - the slow-span layout, the conservative
  inliner, and the missing vectorization on Framework RyuJIT will not show up.
  Never use sharplab to reason about Framework codegen; use BenchmarkDotNet's
  disassembler on a Framework run instead (below).
- The asm view forces optimizations on, so it shows tier-1-style code, which is
  what you want.

## BenchmarkDotNet `[DisassemblyDiagnoser]` - asm next to timings

The in-harness disassembler. It works on **both** TFMs (this is the only reliable
way to see .NET Framework asm), attaching to the benchmarked process and
disassembling the methods it measured, so the codegen lines up with the numbers.

```c#
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 2, printSource: true)]
public class MyBenchmark { /* [Benchmark] ... */ }
```

- `maxDepth` follows calls that many levels deep; `printSource: true` interleaves
  the C#. Output lands in `BenchmarkDotNet.Artifacts/results/*-asm.md`.
- Use `--filter` to a single method - the disassembly is verbose.
- Run each TFM separately; compare the two asm dumps to see exactly what
  Framework RyuJIT does differently (no vectorization, the slow-span operand
  dance, the prologue `rep stosd`). This is how the framework-JIT-optimization
  ratios get *explained*, not just measured.

## `[HardwareCounters]` - *why* it is slow

When two implementations have similar instruction counts but different timings,
the cause is usually branch mispredicts or cache misses, not raw work. On Windows
(ETW, run elevated) add the counters:

```c#
[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses)]
```

- A branchless rewrite that does not move `BranchMispredictions` was not worth
  it; one that trades a misprediction stall for a `cmov` shows up here. (Note a
  Framework caveat: the older RyuJIT often does **not** emit `cmov` for
  `ushort`/`byte` stores - confirm with the disassembler before assuming a
  ternary went branchless.)
- Rising `CacheMisses` points at a memory-layout problem (poor locality, a struct
  too large per cache line, false sharing) rather than a compute one.

## JIT environment knobs (.NET / CoreCLR only)

For ad-hoc asm dumps outside a benchmark, set these before launching a
modern-runtime process. They drive the runtime's integrated disassembler and do
**not** apply to the desktop .NET Framework JIT (use `[DisassemblyDiagnoser]`
there).

| Variable | Effect |
| --- | --- |
| `DOTNET_JitDisasm=Type:Method` | Dump asm for matching methods to stdout. |
| `DOTNET_JitDisasmSummary=1` | One line per compiled method - catches unexpected re-JITs. |
| `DOTNET_JitDisasmDiffable=1` | Stable output (no addresses) for diffing across a change. |
| `DOTNET_TieredCompilation=0` | Straight to optimized tier-1 so you inspect real codegen, not the tier-0 stub. |
| `DOTNET_TieredPGO=0` | Deterministic codegen for analysis (re-enable for production reality). |

## Tiering and PGO: the measurement traps

The modern runtime compiles tier-0 (unoptimized) first and re-JITs to tier-1 once
a method is hot; **OSR** re-JITs long-running loops mid-flight; **dynamic PGO**
reshapes tier-1 from observed types. BenchmarkDotNet's warmup handles all of this
for *timings*. But when you inspect codegen:

- A disassembly captured too early can be the tier-0 stub. Force tier-1 with
  `DOTNET_TieredCompilation=0`, or put
  `[MethodImpl(MethodImplOptions.AggressiveOptimization)]` on the method to
  compile straight to tier-1 and get deterministic asm (it also forgoes PGO for
  that method, so use it for inspection, not as a blanket production attribute).
- .NET Framework has **no** tiering or PGO - what you write is compiled once. So
  a Framework disassembly is always "final," which is why a framework-JIT skill
  can reason about it directly.

## Confirming an optimization without a disassembler

For a quick yes/no - "did the JIT eliminate this branch" - you do not always need
asm. Set a debugger breakpoint inside the branch you expect to be dead (e.g. the
`typeof(T) == typeof(char)` arm when `T = int`) on a Release build. If the JIT
elided it, the debugger refuses to bind the breakpoint. Cheaper than reading the
full dump when you only need to confirm dead-code elimination.
