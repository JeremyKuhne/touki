# Span performance on .NET Framework (net472+)

> **The portable field manual moved.** The general guidance - slow-span
> layout, the no-intrinsics / conservative-inliner constraints, the
> `[SkipLocalsInit]` GC-frame carve-out, the Strategy A-E hierarchy, the
> cost summary, the decision flowchart, and the anti-patterns - now lives
> in the `framework-jit-optimization` skill, bundled as
> [.agents/skills/framework-jit-optimization/references/framework-span-performance.md](../.agents/skills/framework-jit-optimization/references/framework-span-performance.md).
> Read that first.

This file retains the **touki-specific** material that does not travel with
the portable skill: the `OrdinalIgnoreCase` worked example and the touki
reference list. It uses the Strategy A-E vocabulary defined in the field
manual linked above.

---

## Worked example - the `OrdinalIgnoreCase` ASCII fast-path

Sequence of decisions taken for the helper that became
`Touki.SpanExtensions.EqualsOrdinalIgnoreCase` /
`CompareOrdinalIgnoreCase`. Reference trace.

**Start (Strategy A)** - straightforward `for (int i = 0; i < a.Length;
i++)` reading `a[i]` and `b[i]`. Measured at L=10 on net10: 6.6 ns.
Measured at L=10 on net481: 26.7 ns.

**Diagnosis** - net10 fine, net481 hot. The disassembly (§B.3 of the
RCA) shows ~16 µops per loop iteration are slow-span pointer-dance for
the two indexer reads.

**Strategy B applied (experimentally)** - `MemoryMarshal.GetReference`
+ `Unsafe.Add`. Net481 L=10 drops to ~22 ns. Net10 L=10 drops to 5.6
ns. Both improved; net10 win is small.

**Within-noise test** - At L=10 on net10, Strategy B is 5.62 ns vs
Strategy A's 6.55 ns. Within 14% - *not* within the 5% noise band, but
both are tiny absolute values. At L=64 they tie (42.7 vs 43.4 ns). The
unified Strategy B code is *not* losing anything visible on net10, so
ship it unified.

**Alternative**: had Strategy B regressed net10 by >5% (e.g., because
the modern JIT couldn't auto-vectorize the `Unsafe.Add` form but could
auto-vectorize the indexer form), the decision would have been to
split: net10 keeps the simple indexer; net481 gets the Strategy-B
rewrite under `#if !NET`.

**Strategy C applied (after the helper was hoisted into a public
extension method)** - when the work moved into `SpanExtensions` as a
public API, the extra call-frame overhead on net481 surfaced: routing
the `StringSegment.CompareTo` path through `MemoryMarshal.GetReference`
(non-inlined on net481) regressed the L=5 path from ~10 ns to ~19 ns.
Switched the shared core to take raw `char*` and pin via `fixed` at the
call sites (`SpanExtensions.CompareOrdinalIgnoreCaseAsciiFold` and
`StringSegment.CompareToOrdinalIgnoreCase`). `fixed` inlines the pin
fixup so the call chain collapses to a single frame on net481; perf
returned to baseline. See
[touki/Touki/Buffers/SpanExtensions.IgnoreCase.cs](../touki/Touki/Buffers/SpanExtensions.IgnoreCase.cs)
for the resulting layout.

**Strategy E (force-inline)** - applied. The extension entry points
(`Equals*` / `StartsWith*` / `EndsWith*`) carry
`[MethodImpl(AggressiveInlining)]` so their length-dispatch check
(`< 16` &rarr; scalar; `>= 16` &rarr; BCL vectorized) folds into the
call site on net481. Combined with the shared raw-pointer core this
keeps the per-call overhead bounded.

---

## References

- [bcl-ignorecase-valley-rca.md](bcl-ignorecase-valley-rca.md) - the
  worked case study with full disassembly captures for both TFMs.
- `touki.perf/AsciiIgnoreCaseUnsafePerf.cs` - the Span vs Ref vs
  Pinned A/B/C harness this document's measurements come from.
- `touki.perf/StringSegmentIgnoreCasePerf.cs` - before/after baseline
  for the `StringSegment.CompareTo OrdinalIgnoreCase` refactor that
  drove the Strategy-C decision in the worked example above.
- `BenchmarkDotNet.Artifacts/results/touki.perf.AsciiIgnoreCaseUnsafePerf-asm.md`
  - generated disassembly. Regenerate with `[DisassemblyDiagnoser]`
  on the perf class and `dotnet run -c Release --project touki.perf
  -f net481 -- --filter '*AsciiIgnoreCaseUnsafePerf*'`.
- `docs/dotnet-perf-discoveries.md` - short-form running list of perf
  observations across the codebase.
- [touki/Touki/Buffers/SpanExtensions.IgnoreCase.cs](../touki/Touki/Buffers/SpanExtensions.IgnoreCase.cs)
  - the production helpers that resulted from this experiment.
