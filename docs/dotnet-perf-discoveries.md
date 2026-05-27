# .NET BCL performance discoveries

Working notes on .NET BCL API performance characteristics encountered while
optimizing `touki`. Each entry captures a measurement-backed observation, why
it matters, the mitigation we landed on (or considered), and a follow-up that
might still be worth investigating.

These are **not bug reports** — every observation listed is consistent with
documented BCL behavior. They exist here so future optimization work doesn't
re-discover them from scratch.

Hardware reference: Intel Core i9-14900K, Windows 11, BenchmarkDotNet v0.15.8,
`IterationCount=5  LaunchCount=1  WarmupCount=1`, x64.

For the cross-cutting playbook on writing span-walking helpers that
stay fast on `net472`/`net481` (slow-span layout, `MemoryMarshal.GetReference`
+ `Unsafe.Add` pattern, when to pin, when to split TFM implementations),
see [framework-span-performance.md](framework-span-performance.md).

---

## 1. `MemoryExtensions.Equals(span, span, StringComparison.OrdinalIgnoreCase)` has a perf valley at length 6–15

### Observation (both TFMs)

Measuring `_a.AsSpan().Equals(_b.AsSpan(), StringComparison.OrdinalIgnoreCase)`
on equal-length all-letter ASCII spans of mixed case:

| Length | net10.0 RyuJIT | .NET Framework 4.8.1 RyuJIT |
|---:|---:|---:|
| 5 | 2.6 ns | 26.7 ns |
| 10 | 9.4 ns | 28.9 ns |
| 20 | 2.6 ns | 41.5 ns |
| 64 | 6.3 ns | 69.6 ns |

On net10.0 there is a **non-monotonic valley** around length 6–15 where the
BCL path is ~3× slower than at length 5 or 20. The shape is consistent with
the BCL having (a) a length≤5 small-string special case, (b) a length≥16
vectorized path that handles two `Vector128<short>` registers per iteration,
and (c) a scalar fallback in between.

On .NET Framework 4.8.1 the BCL has no vectorized path at all; cost grows
linearly with length and the cost-per-char is ~1 ns even on a high-end CPU.

### Why it matters in `touki`

`LiteralGlobMatcher.IsMatch` for `IgnoreCase=true` used to call
`input.Equals(_literal.AsSpan(), StringComparison.OrdinalIgnoreCase)` directly.
Typical glob literals are 5–15 chars (file names) — squarely inside the
valley. The benchmark `Touki_Literal_Hit IgnoreCase=True` measured 10.04 ns
against the equivalent `RegexGen_Literal_Hit` at 10.4 ns on net10 — far
slower than it should be for what is functionally a 10-char compare.

### Mitigation (applied)

`Touki.SpanExtensions.EqualsOrdinalIgnoreCase`. The helper:

- Length-mismatch returns `false` in one compare.
- Length ≥ 16 (`BclCrossoverLength`) delegates to BCL — the vectorized path
  there is faster than any scalar loop.
- Length < 16 runs an inlined ASCII fold compare with a per-character
  non-ASCII guard. On the first non-ASCII char, hands the tail to BCL with
  `StringComparison.OrdinalIgnoreCase` so invariant-culture semantics are
  preserved for code points above U+007F.
- Outer dispatch is `[MethodImpl(AggressiveInlining)]`; the fold loop lives
  in a separate non-inlined method. On net481 this matters: without the
  split, the length-20 case carried a ~7 ns method-call overhead even when
  delegating to BCL.

### Results

`Touki_Literal_Hit IgnoreCase=True`: **10.04 → 4.79 ns (−52%)** on net10.0.

Direct micro-bench (`AsciiIgnoreCasePerf`) post-mitigation:

| Length | net10 helper | net10 BCL | net481 helper | net481 BCL |
|---:|---:|---:|---:|---:|
| 5 | 3.26 ns | 2.64 ns | 20.2 ns | 26.8 ns |
| 10 | **6.65 ns** | 9.42 ns | **26.7 ns** | 28.8 ns |
| 20 | 2.53 ns | 2.71 ns | 41.5 ns | 41.9 ns |
| 64 | 6.15 ns | 6.33 ns | 72.2 ns | 69.7 ns |

The 0.6 ns regression at length 5 on net10 is the cost of the helper's
length-check + branch versus BCL's small-string special case. Below the
real-application detection floor for typical glob workloads.

### Follow-up

- Watch the .NET 11 / .NET 12 servicing notes for changes to
  `Ordinal.EqualsIgnoreCase` — if the valley closes, this helper becomes
  pure overhead and can be removed.
- `MemoryExtensions.StartsWith(span, span, OrdinalIgnoreCase)` and
  `EndsWith(...)` likely have the same valley. We have not benchmarked them
  directly; `PrefixGlobMatcher`/`SuffixGlobMatcher` route through them.
  Probably worth a follow-up `SpanExtensions.StartsWithOrdinalIgnoreCase` /
  `EndsWithOrdinalIgnoreCase` once we have a representative bench.
- `MemoryExtensions.IndexOf(span, span, OrdinalIgnoreCase)` has its own
  cost. `ContainsGlobMatcher` uses it. Same valley likely applies.

---

## 2. `[SkipLocalsInit]` on a `stackalloc char[256]` site is below the BDN noise floor on modern hardware

### Observation

Adding `[SkipLocalsInit]` to `GlobMatcherFactory.EncodeProgram` and
`UnescapeToString` (both use `ValueStringBuilder sb = new(stackalloc char[256])`)
showed no measurable mean shift in `GlobMatcherCompilePerf` rows on net10.0
(StdDev was larger than the expected delta). On net481 the attribute is
not honored.

### Why this is notable

The conventional advice is to add `[SkipLocalsInit]` whenever a hot method
uses `stackalloc` to skip the implicit zero-init. The cost being below the
noise floor on a 4-GHz+ CPU with vector stores suggests the JIT already
emits the zero-init as a single `rep stosq` or vectorized memset that costs
~5 cycles for a 256-char buffer. Worth applying for hygiene; do not expect
to measure the gain.

### Mitigation

`[SkipLocalsInit]` was applied to the two factory methods. Kept for hygiene
even though the measurement was a wash.

### Follow-up

If the buffer grows past ~1 KB (`stackalloc char[512+]`) the cost may
re-appear. Re-measure if/when we increase the seed size.

---

## 3. `Vector128/256` setup cost dominates for short inputs

### Observation

Two separate experiments hit the same wall:

1. **`SpanReader<char>` in `GlobMatcherFactory.EncodeProgram`**: replacing a
   scalar `pattern[i++]` index loop with `SpanReader.TryRead`/`TryPeek`
   plus `TryReadToAny` (which uses `IndexOfAny`) regressed *every* compile
   row by 60–150%, including `Compile_Any` which doesn't even reach
   `EncodeProgram`. The wrapper method-call overhead plus vector setup cost
   dominates for 5–10-char pattern reads.
2. **`IndexOf(']') + Append(span)` in `EmitClass`**: replacing a
   `for (i; ; i++) sb.Append(c)` loop over a class body with one
   `pattern.IndexOf(']')` + one `sb.Append(span)` regressed
   `Compile_ManyClasses` by +11%. Real class bodies are 3–7 chars (`[abc]`,
   `[0-9]`); below the `IndexOf` vector-setup amortization point.

### Why this matters

Section 6 of the reference document on character parsing says vectorization
becomes a win past ~16 chars of input. The actual breakeven on this
hardware appears to be ~8–12 chars for the BCL primitives we tested.
Glob patterns and their class bodies, literal runs, and segment lengths
typically sit **below** that threshold.

### Rule of thumb (pinned)

> When choosing between an inlined scalar loop and a vectorized BCL primitive
> (`IndexOf`, `IndexOfAny`, `MemoryExtensions.Equals(StringComparison)`,
> `SequenceEqual` for char), measure at the realistic input length. The
> primitive only wins past the vector setup amortization threshold, which
> on i9-class hardware sits around **8–12 chars for `char`-typed spans**.
> Below that, the inlined loop with `ref char` indexing and the
> `pattern[i++]` form is faster than any vectorized call.

### Follow-up

- A `SearchValues<char>`-based class-membership matcher for
  `CompiledGlobMatcher` was deferred because typical glob classes are
  2–7 chars (`[de]`, `[0-9]`, `[A-Z]`). Re-evaluate once we have a
  benchmark with realistic long classes like `[A-Za-z0-9_]`.

---

## 4. ASCII fast-path only wins when replacing framework dispatch, not against an existing tight loop

### Observation

Slice 1 (this document, item 1) applied an inlined ASCII fold compare to
`LiteralGlobMatcher.IgnoreCase` — replacing
`MemoryExtensions.Equals(span, span, OrdinalIgnoreCase)`. Won by 5+ ns at
typical glob lengths.

The identical code structure applied to `CompiledGlobMatcher.EqualsIgnoreCase`
— replacing a tight static `for (i) AsciiFold(a[i]) != AsciiFold(b[i])`
loop — **regressed** `Touki_Compiled_Hit IgnoreCase` by +16% (15.32 →
17.76 ns). The pre-existing tight loop already had no framework dispatch
to amortize against; the added branches in the "fast path" cost more than
they save.

### Rule of thumb (pinned)

> The ASCII inline fast-path pattern is a win **iff** it replaces framework
> dispatch overhead (e.g.
> `MemoryExtensions.Equals(span, span, StringComparison.OrdinalIgnoreCase)`).
> When the existing code is already a private static tight loop with no
> dispatch, the same pattern's added branches make it slower. Verify the
> dispatch target before applying.

### Follow-up

None. The negative result is now documented; do not retry without new
evidence (e.g., a longer typical literal run inside the bytecode interpreter
that would shift the breakeven).

---

## 5. `[MethodImpl(AggressiveInlining)]` on a small dispatch wrapper matters on net481, not on net10

### Observation

`SpanExtensions.EqualsOrdinalIgnoreCase` was first written as a single
method with the
length-check, the BCL-delegation path, and the ASCII fold loop all inline.
On net481 the length-20 case carried a ~7 ns overhead vs calling the BCL
directly even though that's exactly what the helper does at length 20.

Splitting into:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public bool EqualsOrdinalIgnoreCase(ReadOnlySpan<char> span2)
{
    if (span1.Length != span2.Length) return false;
    if (span1.Length >= BclCrossoverLength)
        return span1.Equals(span2, StringComparison.OrdinalIgnoreCase);
    return span1.Length == 0
        || CompareOrdinalIgnoreCaseAsciiFold(span1, span2) == 0;
}

private static unsafe int CompareOrdinalIgnoreCaseAsciiFold(
    ReadOnlySpan<char> a, ReadOnlySpan<char> b) { /* loop */ }
```

dropped the net481 overhead from 7 ns to ~0 ns. On net10 the change had
no measurable effect.

### Why

.NET Framework 4.8.1 RyuJIT's inliner is less aggressive than modern .NET
RyuJIT's. When a small helper contains a moderately sized fold loop, the
net481 inliner declines to inline at call sites — so even the
delegating-to-BCL path eats a real call. Modern RyuJIT inlines the same
method body without the hint.

### Rule of thumb (pinned)

> For small dispatch wrappers that delegate to BCL APIs for the common case
> and fall back to a hand-rolled loop for an uncommon case, mark the wrapper
> `[MethodImpl(AggressiveInlining)]` and keep the loop in a separate
> non-inlined method. The hint is a no-op on net10 but eliminates a real
> call cost on net481.

### Follow-up

Apply the same split-and-inline pattern preemptively to any future
`SpanExtensions` ignore-case helpers (e.g. an `IndexOfOrdinalIgnoreCase`).

---

## 6. `ValueStringBuilder` getters and indexer are already inlined by both JITs

### Observation

Adding `[MethodImpl(AggressiveInlining)]` to `ValueStringBuilder.Length`,
`Capacity`, and `this[int]` had no measurable effect on `Compile_*`
benchmarks on either net10 or net481. Both getters are trivial bodies that
the JIT auto-inlines.

### Follow-up

None. Confirmed dead end — do not re-apply without new evidence that a
specific call site is failing to inline.

---

## 7. `Vector128<short>.Count = 8` controls the BCL vectorization threshold

### Observation

The crossover point for `MemoryExtensions.Equals(OrdinalIgnoreCase)`
between scalar and vectorized paths is at exactly length **16** on net10.0
(see item 1). This matches `Vector128<short>.Count × 2 = 16` (two SIMD
registers worth of chars).

### Implication

When choosing the threshold for an ASCII fast-path dispatch, **16 is the
natural value on `char`-typed inputs**. Anything between 8 (one Vector128)
and 16 (two Vector128s) is in the "BCL has not yet vectorized" zone where
the inline loop wins. Past 16, the BCL has at least two SIMD loads to
amortize over and wins decisively.

On future hardware where the BCL prefers `Vector256<short>` (16 chars per
load), the threshold may shift to 32. Re-measure when the runtime adds
AVX-512-typed `OrdinalIgnoreCase` paths.

### Follow-up

If we ever add similar fast-paths for `byte`-typed spans (e.g. UTF-8 ASCII),
the threshold should be 32 (`Vector128<byte>.Count × 2`) by the same
analysis.

---

## Index of pinned rules

1. **§3**: Vectorized BCL primitives only beat inlined scalar loops past
   8–12 chars on i9-class hardware for `char`-typed spans.
2. **§4**: ASCII inline fast-path is a win iff replacing framework dispatch,
   not iff replacing an existing tight loop.
3. **§5**: For small dispatch wrappers delegating to BCL, mark the wrapper
   `AggressiveInlining` and keep the loop in a separate method — fixes a
   real net481 inlining cost.
4. **§7**: ASCII fast-path threshold on `char` spans is 16, controlled by
   `Vector128<short>.Count × 2`.
