---
name: framework-jit-optimization
description: Optimize hot-path code for the `net481` target in `touki/Framework/`. Use when writing or reviewing performance-sensitive loops, deciding whether to specialize a generic method for primitive types, choosing between scalar/unrolled/BCL-delegating implementations, or diagnosing why a net481 micro-benchmark regresses on the older RyuJIT. For BenchmarkDotNet harness mechanics (authoring/running benchmarks, evaluating allocations) see the `performance-testing` skill.
---

# .NET Framework 4.8.1 JIT optimization

The library targets `net481` in addition to modern .NET. Code under `touki/Framework/`
is excluded from the modern build, so it only ever runs on the older RyuJIT. Treat
`net481` as a separate optimization target with its own rules.

This skill captures decisions distilled from real benchmarks under
[touki.perf/](../../../touki.perf/). Always validate with the
[performance-testing](../performance-testing/SKILL.md) skill workflow before
committing a change.

## What is and isn't available on `net481`

- **No auto-vectorization.** The BCL `MemoryExtensions` methods on net481 ship with
  System.Memory. They are hand-tuned scalar / integer-stride implementations &mdash;
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
vectorization &mdash; just careful scalar code &mdash; but it still substantially
beats a naive per-element loop for whole-buffer primitives.

**Practical consequence:** do not assume "the BCL is vectorized so my generic code
is fine." On `net481` a hand-written specialized loop frequently beats the BCL by
2&ndash;3&times; for full-scan workloads.

## Decision flow for a new framework-only fast path

1. Start with the simplest possible scalar loop and measure it as the baseline.
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
   showing they win &mdash; in practice they regress more often than they help.
   See [antipatterns.md](antipatterns.md).
7. Run the same benchmark on `net10.0` to confirm you have not regressed the
   modern path. If a specialization is harmful on net10 (because the BCL is
   actually vectorized there), guard it with `#if NETFRAMEWORK` so only `net481`
   gets the loop.

## Quick reference: ratios from real benchmarks

All numbers are from the smoke benchmarks in `touki.perf/`. Treat as
order-of-magnitude, not exact &mdash; rerun before claiming a specific number in
a PR.

| Decision | Net481 effect (length 4096, full scan) |
| --- | --- |
| `typeof(T)` specialization vs generic `IEquatable` loop | 1.42&times; faster |
| `[AggressiveInlining]` on a tight scalar loop (length 16) | 1.82&times; &rarr; 1.07&times; vs baseline |
| Unroll-4 indexed (`ptr[0..3]` + `ptr += 4`) | 1.5&times; faster than scalar |
| Unroll-8 same form | **1.6&times; slower** than scalar at 256+ |
| Per-iteration `*ptr; ptr++` instead of indexed reads | ~1.4&times; slower than indexed |
| Integer-indexed unroll (`ptr[i+0..3]; i += 4`) | **Worse than the scalar baseline** |
| Branchless `*ptr = v == old ? new : v` (sparse matches) | **1.5&ndash;3&times; slower** than branchful conditional store |
| SWAR haszero for char `Replace` (dense matches) | **3&times; slower** than scalar |
| BCL `IndexOf` for `Replace` (full-scan) | 2.18&ndash;3.08&times; slower than specialized scalar |
| BCL `IndexOf` for `Count` (sparse matches, 1/64 density) | 2&ndash;3&times; **faster** than full-scan specialization |
| Exponential `SequenceEqual` probe for `CommonPrefixLength` (4096, full match) | 3.3&times; faster than per-element scalar |

The two BCL rows look contradictory. They aren't. See
[bcl-tradeoffs.md](bcl-tradeoffs.md).

## Sub-pages

- [specialization.md](specialization.md) &mdash; `typeof(T)` pattern, `Unsafe.As`,
  primitive bit-equality classes, when generic methods get inlined.
- [unrolling.md](unrolling.md) &mdash; the only unroll form that wins on `net481`,
  and three that don't.
- [bcl-tradeoffs.md](bcl-tradeoffs.md) &mdash; when to defer to BCL `IndexOf` /
  `SequenceEqual` on `net481` despite no vectorization.
- [antipatterns.md](antipatterns.md) &mdash; specific tricks that look clever but
  regress on the older JIT.
