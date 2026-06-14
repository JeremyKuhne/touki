# What the modern target enables (and `net481` does not)

The other pages in this skill are about recovering performance on `net481`'s frozen
RyuJIT. This page is the counterpart: the optimizations the **modern .NET target**
(`net10`) has that Framework does not, how to use them, and - crucially - why you
should let the modern source stay simple so the JIT applies them for you. It is the
basis for the "split the TFMs" decision in
[references/framework-span-performance.md](references/framework-span-performance.md)
(Strategy D): `net10` gets the shape below, `net481` gets the tuned scalar loop.

The headline rule cuts the opposite way from the rest of this skill:

> On `net481` you hand-tune because the JIT will not. On `net10` you keep the
> source simple because the JIT will - and a Framework-tuned shape often *blocks*
> the modern JIT's own vectorizer, devirtualizer, and escape analysis.

## Reach for the BCL first - it is vectorized on `net10`

Unlike Framework (where delegating to the BCL is not a free vectorization win - see
[bcl-tradeoffs.md](bcl-tradeoffs.md)), on `net10` the BCL span primitives are
SIMD-accelerated and tuned better than almost anything you will hand-roll. Before
writing a loop, check whether one of these already does it:

- **`MemoryExtensions`** - `IndexOf`, `Contains`, `SequenceEqual`,
  `IndexOfAny(Except)`, `CommonPrefixLength` are already vectorized. Do not
  reimplement them.
- **`SearchValues<T>`** - build once into a `static readonly` field, then
  `span.IndexOfAny(values)`. It picks the optimal bitmap/range/`PSHUFB` strategy
  for the set. `SearchValues<string>` (multi-substring) exists too.
- **`System.Numerics.Tensors.TensorPrimitives`** - `Sum`, `Dot`, `Add`,
  `CosineSimilarity`, and many more element-wise/reduction ops over
  `ReadOnlySpan<T>`, FMA-aware and generic-math-based.

These have **no equivalent on `net481`**, so a helper that wants speed on both
targets often splits: BCL primitive under `#if NET`, hand-tuned scalar under
`#else`.

## The canonical hand-vectorized loop (only when the BCL has no fit)

When you do need a custom kernel on `net10`, every one has the same skeleton: gate
on hardware + length, run a `Vector256` main loop over a hoisted `ref T`, reduce
once, then a scalar tail.

```c#
static int Sum(ReadOnlySpan<int> source)
{
    ref int first = ref MemoryMarshal.GetReference(source);
    int length = source.Length;
    int i = 0, sum = 0;

    if (Vector256.IsHardwareAccelerated && length >= Vector256<int>.Count)
    {
        Vector256<int> acc = Vector256<int>.Zero;
        int lastBlock = length - Vector256<int>.Count;
        for (; i <= lastBlock; i += Vector256<int>.Count)
        {
            acc += Vector256.LoadUnsafe(ref first, (nuint)i);
        }

        sum += Vector256.Sum(acc);
    }

    for (; i < length; i++)
    {
        sum += Unsafe.Add(ref first, i);
    }

    return sum;
}
```

- `Vector256.IsHardwareAccelerated` is a **JIT-time constant** - on hardware
  without AVX (or, critically, when this same source compiles for `net481` where
  the type is absent) the whole `if` block must be `#if NET`-guarded; the types live
  in `System.Runtime.Intrinsics`, which is .NET 5+ only.
- The find-first-match variant replaces the reduction with
  `Vector256.Equals(block, needle).ExtractMostSignificantBits()` and, when the mask
  is non-zero, `BitOperations.TrailingZeroCount(mask)` for the position. This is the
  portable `PMOVMSKB` idiom behind `Span.IndexOf`.
- The `ref T` hoist + `Unsafe.Add` tail is the same pattern this skill's
  [references/framework-span-performance.md](references/framework-span-performance.md)
  prescribes for Framework - it is the one shape that is good on both.

**Drop to `System.Runtime.Intrinsics.X86` / `.Arm` only** for an instruction the
portable `Vector128/256/512` layer cannot express (`PSHUFB`/`Vector128.Shuffle` for
in-register table lookup, `PEXT`/`PDEP`, `PCLMULQDQ`, `Crc32`). Every intrinsic
class exposes `IsSupported`, which the JIT folds away, so a widest-path-first ladder
(`Avx512F` -> `Avx2` -> `AdvSimd` -> scalar) has no runtime dispatch cost - but each
path multiplies test surface, so keep a scalar fallback and differential-test it
against the vector path on awkward lengths (0, 1, width-1, width, width+1).

## The struct-generic zero-cost abstraction

The single most powerful `net10` pattern, and the one the BCL leans on
(`TensorPrimitives`, `Span.Sort`). The runtime **monomorphizes value-type
generics**: a generic method over a `struct` that implements an interface gets the
interface calls devirtualized and inlined per instantiation, collapsing the
abstraction to nothing.

```c#
interface IOp { int Apply(int x); }
readonly struct AddOne : IOp { public int Apply(int x) => x + 1; }

static void Map<TOp>(Span<int> data, TOp op) where TOp : struct, IOp
{
    for (int i = 0; i < data.Length; i++)
    {
        data[i] = op.Apply(data[i]);   // devirtualized + inlined to `+ 1`
    }
}
```

This is how you write "one kernel, many operations" without virtual-call tax. It
works on `net481` too (the runtime has always monomorphized value-type generics),
but the weaker Framework inliner is less reliable about inlining the devirtualized
call - measure both.

## Let the JIT do the rest - shapes that light up on `net10`

These cost nothing to adopt and the modern JIT rewards them; `net481` benefits from
some but not all.

- **`sealed`** classes and overrides devirtualize unconditionally. Keep hot types
  `sealed` (a common repo rule for non-derived internal/private types). Dynamic PGO
  additionally does *guarded* devirtualization on monomorphic call sites - so keep a
  hot call site type-stable. `net481` has no PGO.
- **Escape analysis -> stack allocation (.NET 10).** A small `new T[n]` that
  provably does not escape the method is stack-allocated (and may be scalar-replaced
  into registers), with no GC tracking. You get this for free by keeping allocations
  local - do not pool what the JIT will stack-allocate; check codegen first (the
  performance-testing skill's codegen-reading page). `net481` does not do this.
- **Canonical loop shapes keep bounds-check elimination.** `for (int i = 0; i <
  span.Length; i++)` lets the JIT prove `span[i]` in range. Caching `.Length` into a
  local can *defeat* BCE on `net10` - the opposite of the (also unnecessary) habit.
  Slice both operands to a common length so each access is provably safe.
- **Tiering / OSR / PGO** mean first calls run unoptimized and long loops re-JIT
  mid-flight; this is a *measurement* concern, not a coding one - see the traps in
  the performance-testing skill's codegen-reading page.

## Do not freeze the modern source against Framework's JIT

The anti-pattern that this page exists to prevent: pinning, `Unsafe`-walking, or
manually unrolling the **`net10`** path just to share one implementation with
`net481`. Every such shape is something the modern JIT could have vectorized or
stack-allocated and now cannot. The
[references/framework-span-performance.md](references/framework-span-performance.md)
within-noise test is the rule: if the simple `net10` shape is within ~5% of the
Framework-tuned shape on `net10`, split the TFMs and keep `net10` simple. The 5% you
might lose today is far smaller than the future vectorization wins you forfeit by
freezing the source.

## See also

- [references/framework-span-performance.md](references/framework-span-performance.md)
  - the Strategy A-E hierarchy and the TFM-split decision this page feeds.
- [cross-tfm-codegen.md](cross-tfm-codegen.md) - arithmetic, branchless, memory
  layout, and allocation anti-patterns that apply to *both* targets.
- The performance-testing skill's `reading-codegen.md` - confirm the JIT actually
  vectorized / devirtualized / stack-allocated what you expected.
