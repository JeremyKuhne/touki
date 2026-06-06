# Span performance on .NET Framework (net472+)

How to write span-based helpers that stay fast on `net472` / `net481`
without giving up the simpler, safer shape that lets net10 ride future
runtime improvements. Sourced from the experiments in
`touki.perf/AsciiIgnoreCasePerf.cs`,
`touki.perf/AsciiIgnoreCaseUnsafePerf.cs`, and the disassembly captures
under `BenchmarkDotNet.Artifacts/results/`.

The numbers and disassembly in this document are pulled from a Raptor
Lake i9-14900K running .NET 10.0.7 and .NET Framework 4.8.1. The shapes
generalize to any net472+ target - there is one RyuJIT for desktop
Framework and it has not received vectorization or PGO work in years.

The companion document is
[bcl-ignorecase-valley-rca.md](bcl-ignorecase-valley-rca.md), which
captures one specific case study (`OrdinalIgnoreCase` at length 8-15)
in detail. This document is the general field manual.

---

## 1. Why spans are slow on net472+

`ReadOnlySpan<T>` and `Span<T>` are **not the same type** on .NET
Framework as they are on modern .NET. The layout differs, and the JIT
that compiles span code is RyuJIT 4.8, not RyuJIT 10.

### 1.1 "Slow span" layout

On modern .NET, `Span<T>` is a `ref struct` containing exactly **one
byref** (a managed `ref T`) and a length:

```csharp
// modern .NET (conceptual)
public readonly ref struct Span<T>
{
    private readonly ref T _reference;
    private readonly int _length;
}
```

`_reference` is a true managed pointer the JIT can keep in a register.
Indexing `span[i]` compiles to a single indexed load (`movzx eax, word
ptr [reg + i*2]` for `Span<char>`).

On .NET Framework (and any "slow span" target - `netstandard2.0` via the
`System.Memory` NuGet, `net472`, `net481`) `Span<T>` is approximately:

```csharp
// .NET Framework (System.Memory package, conceptual)
public readonly ref struct Span<T>
{
    private readonly Pinnable<T> _pinnable;   // managed object or null
    private readonly IntPtr      _byteOffset; // offset within _pinnable
    private readonly int         _length;
}
```

A managed `ref T` cannot be stored in a struct field on Framework
because the runtime predates the byref-in-struct feature. The span has
to carry the object reference *and* a byte offset separately.

The cost shows up every time you index the span. Disassembly of
`a[i]` on net481 (from [bcl-ignorecase-valley-rca.md §B.3](bcl-ignorecase-valley-rca.md)):

```asm
M02_L00:
    cmp       qword ptr [rcx], 0   ; Pinnable null?  (raw-pointer path?)
    jne       short M02_L01
    mov       rax, [rcx+8]         ; no -> load byte offset
    movsxd    r10, r8d
    shl       r10, 1
    add       rax, r10             ; raw + i*2
    jmp       short M02_L02
M02_L01:
    mov       r11, [rcx]           ; load Pinnable object pointer
    cmp       [r11], r11d          ; null-check / GC liveness fence
    lea       r10, [r11+8]         ; skip object header
    mov       rax, [rcx+8]         ; byte offset
    add       r10, rax             ; combine
    movsxd    r11, r8d
    mov       rax, r11
    shl       rax, 1
    add       r10, rax             ; index by 2
    xchg      rax, r10
M02_L02:
    movzx     eax, word ptr [rax]  ; finally load a[i]
```

**~8 µops per character on net481 vs 1 µop on net10.** The same dance
repeats for `b[i]`, so a `for (int i = 0; i < n; i++)` loop walking two
spans pays ~16 µops *just to load the operands*, before any compare or
fold work.

### 1.2 No `Vector128`/`Vector256` intrinsics

Framework RyuJIT does not recognize the `System.Runtime.Intrinsics`
APIs. `Vector<T>` (the old "agnostic" SIMD type) still works but is
significantly slower than the modern intrinsics. The BCL itself does
not vectorize most string/span operations when running on Framework -
even calls like `MemoryExtensions.Equals(span, span, OrdinalIgnoreCase)`
fall through to a scalar per-character loop.

This means **delegating to the BCL is not a free vectorization win on
Framework** the way it is on modern .NET. If you're writing a helper
specifically for Framework speed, you usually need to write the loop
yourself.

### 1.3 Prologue `rep stosd` zero-init (the GC-frame carve-out)

Framework RyuJIT **honors `[SkipLocalsInit]`** - but only for the locals
it is allowed to leave dirty. The desktop CLR still force-zeros every
**GC-tracked** frame slot regardless of the `localsinit` flag, so the
garbage collector can safely report it. A managed reference, an
`object`-containing struct, a `Span<T>` / `ReadOnlySpan<T>` (it carries a
managed byref), and a pinned `fixed` pointer's slot are all GC-tracked.

A span-walking helper that takes spans and pins them has a GC-tracked
frame, so it is zeroed in the prologue **even with `[SkipLocalsInit]`
applied** - this is the GC carve-out, not the attribute being ignored:

```asm
; A pinning span helper, net481 RyuJIT, [SkipLocalsInit] applied
    sub       rsp, 58
    ...
    mov       ecx, 0C              ; 12 dwords = 48 bytes
    xor       eax, eax
    rep stosd                      ; still emitted: GC slots, not the flag
```

Proven by A/B disassembly on both TFMs
([`touki.perf/SkipLocalsInitProbePerf.cs`](../touki.perf/SkipLocalsInitProbePerf.cs)):

| 48-byte frame / 4 KB `stackalloc` | net481 default | net481 `[SkipLocalsInit]` |
|---|---:|---:|
| `stackalloc byte[4096]` (no GC refs) | 52.99 ns | **1.78 ns** - loop gone |
| 48-byte non-GC struct | 5.84 ns | **1.28 ns** - `rep stosd` gone |
| 48-byte `object`-containing struct | 8.58 ns | 8.66 ns - `rep stosd` stays |

So on net481 the attribute removes zeroing for non-GC locals and
`stackalloc` (a 4 KB clear drops ~30x) and is a no-op only for GC-tracked
frames. The cost a span helper still pays - an ~88-byte frame with a
48-byte GC-slot zero-init, ~3 ns/call - is real, but it is the GC
carve-out and `[SkipLocalsInit]` cannot remove it. Modern .NET applies
the same carve-out. For the buffer-zeroing decisions that follow from
this, see the [`scratch-buffer-strategy`](../.agents/skills/scratch-buffer-strategy/SKILL.md)
skill and its bundled
[arraypool-performance.md](../.agents/skills/scratch-buffer-strategy/references/arraypool-performance.md).

### 1.4 Conservative inliner, no cross-assembly generic inlining

Framework RyuJIT is significantly more conservative than modern .NET
about:

- Inlining anything with non-trivial structure.
- Inlining generic methods across assemblies.
- Inlining methods called through interfaces (no dynamic devirt).
- Inlining anything containing a `try`/`catch` or `finally`.

The practical consequence: `MemoryMarshal.GetReference<T>(span)` does
not inline on Framework. Every call becomes an actual `call qword ptr
[...]` indirection. Modern .NET inlines it to a single register move.

---

## 2. The strategy hierarchy

Apply in this order. Stop at the first strategy that gets you within
noise of the modern .NET measurement, because each step trades safety
or maintainability for speed.

### 2.1 Strategy A: write it the safe way, measure both TFMs

Always start here. Use `for (int i = 0; i < span.Length; i++) span[i]`,
foreach over `ReadOnlySpan<T>`, or whatever idiomatic shape the
algorithm wants. Benchmark on **both** net10 and net481.

Three outcomes:

1. **Both TFMs within target.** Done - ship it.
2. **net10 fine, net481 slow.** Continue to Strategy B.
3. **Both slow.** Algorithmic issue. The strategies in this document
   only help with constant-factor JIT/runtime overhead.

The reason for starting here is **portability across runtime evolution**.
The simpler the source, the more future RyuJIT improvements (auto-vector,
better inliner, dynamic PGO) the code accrues for free on modern .NET.
Code written for Framework's JIT often *prevents* the modern JIT from
applying its own optimizations.

### 2.2 Strategy B: hoist `ref T` out of the loop with `MemoryMarshal.GetReference` + `Unsafe.Add`

The single biggest win for span-walking loops on Framework. Compute the
slow-span pointer dance **once** outside the loop and walk a real `ref T`
inside it.

```csharp
// Before - pays the slow-span tax per character on net481
for (int i = 0; i < a.Length; i++)
{
    char x = a[i];
    char y = b[i];
    // ...
}

// After - pays it once at method entry
ref char pa = ref MemoryMarshal.GetReference(a);
ref char pb = ref MemoryMarshal.GetReference(b);
int n = a.Length;
for (int i = 0; i < n; i++)
{
    char x = Unsafe.Add(ref pa, i);
    char y = Unsafe.Add(ref pb, i);
    // ...
}
```

Measured wins on net481 (`AsciiIgnoreCaseUnsafePerf`, same-case ASCII
input):

| Length | `span[i]` loop | `ref` + `Unsafe.Add` | Δ |
|---:|---:|---:|---:|
| 5  | 19.92 ns | 20.04 ns | ±0% |
| 10 | 27.20 ns | 21.99 ns | **−19%** |
| 20 | 46.67 ns | 27.04 ns | **−42%** |
| 64 | 105.81 ns| 59.70 ns | **−44%** |

On net10 the same change is a 14-16% win at short lengths and zero at
long lengths - modern RyuJIT was already keeping the operand in a
register, but `Unsafe.Add` saves the bounds check the indexer would
otherwise emit.

**This pattern is safe** in the C# sense - no `unsafe` block, no
pinning, no GC concerns. `Unsafe.Add` on a managed `ref T` is GC-aware:
the runtime tracks the byref the same way it tracks the original span
reference, and a moving GC updates both.

The one cost: `MemoryMarshal.GetReference` does **not** inline on
net481 - it's a non-inlined `call`. That ~2 ns per call (~4 ns total
for two operands) is why the L=5 row above is a wash. Continue to
Strategy C only if you need to recover that.

### 2.3 Strategy C: `fixed` (pinning)

When the call-frame cost of `MemoryMarshal.GetReference` dominates
(very short inputs, very hot path), drop to `fixed` to inline the pin
fixup at the call site.

```csharp
public static unsafe bool Equals(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
{
    if (a.Length != b.Length) return false;
    fixed (char* pa = a)
    fixed (char* pb = b)
    {
        int n = a.Length;
        for (int i = 0; i < n; i++)
        {
            char x = pa[i];
            char y = pb[i];
            // ...
        }
    }
    return true;
}
```

Measured (same dataset, net481):

| Length | `span[i]` | `ref` + `Unsafe.Add` | `fixed` (pinned) | Pinned vs Span |
|---:|---:|---:|---:|---:|
| 5  | 19.92 ns | 20.04 ns | 17.72 ns | **−11%** |
| 10 | 27.20 ns | 21.99 ns | 21.04 ns | **−23%** |
| 20 | 46.67 ns | 27.04 ns | 28.33 ns | −39% |
| 64 | 105.81 ns| 59.70 ns | 65.79 ns | −38% |

Pinning beats `Unsafe.Add` at short lengths because `fixed` generates
the pin fixup inline (no `GetReference` call). It loses to `Unsafe.Add`
at L ≥ 20 because every additional iteration the pinned pointer pays
nothing the `ref T` doesn't, and the `fixed` block has a small
GC-frame-update cost on entry/exit that `Unsafe.Add` avoids.

**Pinning is explicit `unsafe`.** That is a feature, not a drawback -
the keyword forces a reviewer to look at the pointer arithmetic. Treat
`Unsafe.Add` as "safe but easy to misread" and `fixed` as "unsafe and
obviously unsafe". Both ship the same machine code shape; the
difference is what shows up at the call site for a human reviewer.

**Pinning is preferred over `Unsafe.As`/`Unsafe.AsRef`/`Unsafe.AsPointer`
tricks** that try to launder a managed reference into a raw pointer
without `fixed`. Those tricks are equally unsafe in terms of runtime
semantics (you're still working with raw pointers and ignoring the GC),
but they hide the danger behind a `static class Unsafe` import.
`fixed (char* p = span)` says exactly what it does.

### 2.4 Strategy D: TFM-conditional implementations

For helpers that:

1. Live on a hot path used by many call sites
2. Have a simple modern-.NET implementation that's within noise of any
   Framework-tuned version on net10
3. Pay a measurable Framework tax with the simple shape

…provide **two implementations**, one per TFM, with `#if NET` /
`#else`. The principle is:

> The modern .NET implementation should be as simple as the algorithm
> allows. The Framework implementation can use pinning or unsafe code
> to recover the per-character cost. Never let Framework's JIT
> limitations dictate the modern .NET source shape.

Template:

```csharp
public static bool Equals(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
{
    if (a.Length != b.Length) return false;

#if NET
    // Modern .NET: simple, idiomatic. RyuJIT keeps span data in
    // registers; future runtime versions can auto-vectorize this loop.
    for (int i = 0; i < a.Length; i++)
    {
        if (!EqualOneChar(a[i], b[i])) return false;
    }
    return true;
#else
    // .NET Framework: pin and walk raw pointers. Recovers the
    // slow-span tax (12 µops/char -> 1 µop/char).
    return EqualsPinned(a, b);
#endif
}

#if !NET
private static unsafe bool EqualsPinned(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
{
    fixed (char* pa = a)
    fixed (char* pb = b)
    {
        int n = a.Length;
        for (int i = 0; i < n; i++)
        {
            if (!EqualOneChar(pa[i], pb[i])) return false;
        }
        return true;
    }
}
#endif
```

This pattern lets the modern .NET source remain a target for the JIT's
own optimizer - when .NET 12 or 13 adds a new vectorization pass, the
simple loop picks it up automatically. The Framework implementation
stays frozen at "whatever runs well on RyuJIT 4.8", because RyuJIT 4.8
is itself frozen.

The **within-noise** test for whether to split:

> If a unified implementation written with pinning/Unsafe is within
> ~5% of the simple `span[i]` shape on **net10** at the input sizes
> the helper sees in production, prefer to split. The 5% you might
> theoretically lose on net10 is far smaller than the future
> vectorization wins you forfeit by freezing the source against
> Framework's JIT.

### 2.5 Strategy E: kill the call frame (force-inline)

Orthogonal to A-D, but worth listing. Framework's per-call overhead
(~6 ns: 88-byte frame + 48-byte `rep stosd` + epilogue) is irreducible
*within a called function*. The only way to delete it is to not be a
called function.

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool Equals(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
{
    // Tiny dispatcher - inlines into the caller.
    if (a.Length != b.Length) return false;
    if (a.Length < SomeThreshold) return EqualsCore(a, b);
    return BclEquals(a, b);
}
```

The dispatcher inlines and the per-call frame for `Equals` disappears
on Framework. The work-doing method (`EqualsCore`) stays out-of-line
to avoid bloating every call site. On net481 each eliminated call
frame is worth ~5-6 ns, which is significant for short-input hot
paths.

**Caveat**: `AggressiveInlining` is a *hint*. Framework's JIT will
refuse if the method is too large, contains a try/catch/finally, or
touches `MarshalByRefObject`. Keep the dispatcher trivial.

---

## 3. Cost summary

The fixed costs on net481 RyuJIT, gathered in one place so you can
quickly estimate how much a given strategy might save:

| Cost | Approx ns | Notes |
|---|---:|---|
| Function-call frame (88 B stack + 48 B `rep stosd` + epilogue) | ~5-6 | Per call. The 48 B zero is the GC-tracked slots; `[SkipLocalsInit]` can't remove those (see 1.3). |
| Non-inlined `MemoryMarshal.GetReference<T>(span)` | ~2 | Per call. Doubled if you walk two spans. |
| Slow-span indexer `span[i]` | ~1.5 µops × ~2.5 cycles | Per character. Compare to ~0.3 cycles on net10. |
| `vzeroupper` in prologue | ~1 µop | Emitted whenever the JIT sees AVX state needs clearing. |
| 5 callee-saved register pushes (`MemoryExtensions.Equals` shape) | ~2 | Per call into a "heavyweight" BCL helper. |

A "typical" Framework cost for a span-walking helper called once at L=10
is something like: **6 (call frame) + 4 (two GetReference calls if used) + 10
(10 × per-char load via slow span) + 1 (vzeroupper) + 5 (epilogue & misc) =
~26 ns.** Strategy B drops this to **6 + 4 + 3 + 1 + 5 = ~19 ns**.
Strategy C drops it further to **6 + 0 + 3 + 0 + 4 = ~13 ns** (pinning
inlines the pointer setup but adds a small fixed pin-fixup cost).

---

## 4. Decision flowchart

```text
Write the helper the simple safe way (Strategy A).
Benchmark on net10 AND net481.

  net10 OK, net481 OK? ──── ship it.
                  │
                  no
                  ▼
  Hoist `ref T = MemoryMarshal.GetReference(span)` (Strategy B).
  Re-bench.
                  │
                  ▼
  net481 OK now? ─── good. Is the unified Strategy-B code within
                  │   noise of the Strategy-A code on net10?
                  │
                  ├── yes ─── ship unified.
                  │
                  └── no ──── split TFMs (Strategy D):
                              net10  = Strategy A (simple).
                              net481 = Strategy B (ref+Unsafe.Add).
                  │
                  no (net481 still hot)
                  ▼
  Pin with `fixed` (Strategy C). Re-bench.
                  │
                  ▼
  net481 OK now? ─── split TFMs (Strategy D):
                              net10  = Strategy A (simple).
                              net481 = Strategy C (pinned).
                  │
                  no
                  ▼
  Profile for call-frame overhead. Force-inline a dispatcher
  (Strategy E). Re-bench.
                  │
                  ▼
  Still hot? ──── algorithm issue. Strategies A-E exhaust the
                  constant-factor recovery options.
```

---

## 5. Worked example - the `OrdinalIgnoreCase` ASCII fast-path

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

## 6. Anti-patterns to avoid

- **`unsafe` for cleverness, not necessity.** If Strategy A or B
  meets the target, do not introduce `fixed` "for consistency" or
  "because we're already using `Unsafe.Add`". Every `unsafe` block is
  a permanent maintenance cost.
- **Writing Framework-tuned code into the modern .NET path.** If you
  pin / Unsafe-Add on net10 just to share one implementation with
  Framework, you may be preventing future RyuJIT auto-vectorization
  from kicking in. Measure on net10 with a simpler shape; if it's
  competitive, prefer the split.
- **`[SkipLocalsInit]` as a fix for span-helper frames.** It does *not*
  help there: a span / `fixed` helper's frame is GC-tracked, and the GC
  carve-out zeroes those slots regardless of the flag (see 1.3). It
  *does* suppress zeroing for non-GC locals and `stackalloc` (a 4 KB
  clear drops ~30x on net481), so credit it there - just not as a
  mitigation for the span-walking helpers this doc is about.
- **`Unsafe.AsPointer(ref MemoryMarshal.GetReference(span))` instead of
  `fixed`.** Same machine semantics, harder for a reviewer to spot the
  pinning requirement (there isn't one - the pointer is bare, and a
  moving GC will invalidate it). If you need a raw pointer, use
  `fixed`. If you only need a `ref T`, use `MemoryMarshal.GetReference`
  with `Unsafe.Add`.
- **`stackalloc` to avoid spans.** `stackalloc` returns a `Span<T>`
  on modern .NET and an `IntPtr` cast-shimmed thing on Framework via
  polyfill. Allocating on the stack does not help with the slow-span
  indexer tax - it's the *span type* that's slow, not the backing
  storage. (`stackalloc` is still the right answer for buffer
  ownership; just don't expect it to fix span-indexer performance.)

---

## 7. References

- [bcl-ignorecase-valley-rca.md](bcl-ignorecase-valley-rca.md) - the
  worked case study with full disassembly captures for both TFMs.
- `touki.perf/AsciiIgnoreCaseUnsafePerf.cs` - the Span vs Ref vs
  Pinned A/B/C harness this document's measurements come from.
- `touki.perf/StringSegmentIgnoreCasePerf.cs` - before/after baseline
  for the `StringSegment.CompareTo OrdinalIgnoreCase` refactor that
  drove the Strategy-C decision in &sect;5.
- `BenchmarkDotNet.Artifacts/results/touki.perf.AsciiIgnoreCaseUnsafePerf-asm.md`
  - generated disassembly. Regenerate with `[DisassemblyDiagnoser]`
  on the perf class and `dotnet run -c Release --project touki.perf
  -f net481 -- --filter '*AsciiIgnoreCaseUnsafePerf*'`.
- `docs/dotnet-perf-discoveries.md` - short-form running list of perf
  observations across the codebase.
- [touki/Touki/Buffers/SpanExtensions.IgnoreCase.cs](../touki/Touki/Buffers/SpanExtensions.IgnoreCase.cs)
  - the production helpers that resulted from this experiment.
