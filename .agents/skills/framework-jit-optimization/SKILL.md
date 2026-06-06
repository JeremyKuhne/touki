---
description: Optimize hot-path code for the `net481` (.NET Framework) target in a multi-targeted library's Framework-only sources. Use when writing or reviewing performance-sensitive loops, deciding whether to specialize a generic method for primitive types, choosing between scalar/unrolled/BCL-delegating implementations, or diagnosing why a net481 micro-benchmark regresses on the older RyuJIT. For BenchmarkDotNet harness mechanics (authoring/running benchmarks, evaluating allocations) see the `performance-testing` skill.
license: MIT
metadata:
    github-path: skills/framework-jit-optimization
    github-pinned: v0.3.0
    github-ref: refs/tags/v0.3.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: 428c10165d3481855b1a0477c715f7bace02e89d
    portability: semi-portable
name: framework-jit-optimization
---
# .NET Framework 4.8.1 JIT optimization

A multi-targeted library targets `net481` in addition to modern .NET. Code in the
Framework-only source tree (the `Framework/` subtree by convention, excluded from
the modern build) only ever runs on the older RyuJIT. Treat `net481` as a separate
optimization target with its own rules.

This skill captures decisions distilled from real BenchmarkDotNet experiments in
the repo's perf project (`<root>.perf` by convention). The deep span-walking
field manual is bundled alongside this skill in
[references/framework-span-performance.md](references/framework-span-performance.md).

Always validate with the `performance-testing` skill workflow before committing a
change, and run the `pre-pr-self-review` checklist before opening a PR - in
particular its framework-correctness items (allocation-free over raw speed; perf
claims must name the JIT and be measured) apply directly to changes guided by this
skill.

For the broader "how do I polyfill API X for net472?" question (which packages to
prefer, when to hand-roll), see the `polyfill-dotnet-api` skill. This skill picks
up after that decision is already made and the polyfill lives in the Framework-only
tree.

For choosing how a hot path gets its scratch buffer (zeroed `stackalloc` vs
`[SkipLocalsInit]` vs a stack-with-pool-fallback buffer vs an `ArrayPool` rental,
and the net481/net10 size crossovers), see the `scratch-buffer-strategy` skill.

A consuming repository wires the concrete cross-skill links and source-tree paths
in its overlay.

## What is and isn't available on `net481`

- **No auto-vectorization.** The BCL `MemoryExtensions` methods on net481 ship with
  System.Memory. They are hand-tuned scalar / integer-stride implementations -
  they do **not** use SIMD.
- **No `System.Runtime.Intrinsics`.** `Vector128`/`Vector256`/`Vector512`,
  `Sse2.CompareEqual`, `Avx2.MoveMask` are .NET 5+. Not available here.
- **No tiered JIT or PGO.** What you write is what gets compiled, once.
- **`Vector<T>` from `System.Numerics.Vectors`** technically exists but does not
  auto-vectorize equality-replace loops on the older JIT, and per-load/store
  overhead loses to a plain unrolled scalar loop at typical sizes.
- **No source-level loop alignment** controls.

"Integer-stride" means routines like `IndexOf` and `SequenceEqual` internally
process multiple elements per loop step (e.g. compare a `ulong` chunk that spans
4 chars or 8 bytes) rather than one element at a time. This is **not**
vectorization - just careful scalar code - but it still substantially
beats a naive per-element loop for whole-buffer primitives.

**Practical consequence:** do not assume "the BCL is vectorized so my generic code
is fine." On `net481` a hand-written specialized loop frequently beats the BCL by
2-3&times; for full-scan workloads.

## Decision flow for a new framework-only fast path

1. Start with the simplest possible scalar loop and measure it as the baseline.
   Capture the baseline on **both** `net10.0` and `net481` before editing, and
   keep the full BenchmarkDotNet rows (`Mean`/`Error`/`StdDev`/`Allocated`), not
   a one-line summary - see the before/after discipline in the
   `performance-testing` skill. EventPipe line
   profiling is net10-only, but every change still has to be re-measured on
   net481 overall.
2. Decide whether to specialize. See [specialization.md](specialization.md) for the
   `typeof(T)` pattern and primitive equivalence classes.
3. Decide whether to defer to a BCL primitive. See
   [bcl-tradeoffs.md](bcl-tradeoffs.md) for the visit-most-vs-skip-runs rule.
4. If specializing, add `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on the
   generic entry point. The `net481` JIT's default heuristics are conservative;
   small specialized loops often will not inline without it.
5. If the loop is the hot path, unroll by 4 with indexed reads + bulk pointer
   increment. See [unrolling.md](unrolling.md) for the right form (and the wrong
   ones).
6. **Stop there.** Do not pursue SIMD, SWAR, or branchless tricks without data
   showing they win - in practice they regress more often than they help.
   See [antipatterns.md](antipatterns.md).
7. Run the same benchmark on `net10.0` to confirm you have not regressed the
   modern path. If a specialization is harmful on net10 (because the BCL is
   actually vectorized there), guard it with `#if NETFRAMEWORK` so only `net481`
   gets the loop.
8. Report both TFMs' before/after tables together, and confirm the targeted hot
   line/method from the net10 trace actually shrank (e.g. `System.Array.Copy`
   self-time dropping). A faster `Mean` with the targeted frame unchanged is
   usually noise or an unrelated win.

## Quick reference: ratios from real benchmarks

All numbers are from the smoke benchmarks in the repo's perf project. Treat as
order-of-magnitude, not exact - rerun before claiming a specific number in
a PR.

| Decision | Net481 effect (length 4096, full scan) |
| --- | --- |
| `typeof(T)` specialization vs generic `IEquatable` loop | 1.42&times; faster |
| `[AggressiveInlining]` on a tight scalar loop (length 16) | 1.82&times; &rarr; 1.07&times; vs baseline |
| Unroll-4 indexed (`ptr[0..3]` + `ptr += 4`) | 1.5&times; faster than scalar |
| Unroll-8 same form | **1.6&times; slower** than scalar at 256+ |
| Per-iteration `*ptr; ptr++` instead of indexed reads | ~1.4&times; slower than indexed |
| Integer-indexed unroll (`ptr[i+0..3]; i += 4`) | **Worse than the scalar baseline** |
| Branchless `*ptr = v == old ? new : v` (sparse matches) | **1.5-3&times; slower** than branchful conditional store |
| SWAR haszero for char `Replace` (dense matches) | **3&times; slower** than scalar |
| BCL `IndexOf` for `Replace` (full-scan) | 2.18-3.08&times; slower than specialized scalar |
| BCL `IndexOf` for `Count` (sparse matches, 1/64 density) | 2-3&times; **faster** than full-scan specialization |
| Exponential `SequenceEqual` probe for `CommonPrefixLength` (4096, full match) | 3.3&times; faster than per-element scalar |
| Tuple swap `(a, b) = (b, a)` for plain locals | **~23% slower** than `T t = a; a = b; b = t;` |
| Tuple swap on paired `Span<T>` indexed swap (sort hot path) | **~9% slower** than explicit temps |
| Tuple swap on a single `Span<T>` indexed swap or two `ref` locals | Equivalent (within noise) |

The two BCL rows look contradictory. They aren't. See
[bcl-tradeoffs.md](bcl-tradeoffs.md).

## Sub-pages

- [specialization.md](specialization.md) - `typeof(T)` pattern, `Unsafe.As`,
  primitive bit-equality classes, when generic methods get inlined.
- [unrolling.md](unrolling.md) - the only unroll form that wins on `net481`,
  and three that don't.
- [bcl-tradeoffs.md](bcl-tradeoffs.md) - when to defer to BCL `IndexOf` /
  `SequenceEqual` on `net481` despite no vectorization.
- [antipatterns.md](antipatterns.md) - specific tricks that look clever but
  regress on the older JIT.
