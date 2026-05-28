# Anti-patterns on `net481`

Tricks that look clever, are documented elsewhere as wins, and **regress** on
the older RyuJIT. Don't try them without benchmark data showing they win for
your specific call pattern.

## Branchless conditional store

```c#
// BAD on net481 for sparse matches.
*ptr = *ptr == oldShort ? newShort : *ptr;
```

The `net481` JIT does not lower the ternary into a `cmov` for `ushort` / `byte`
stores. You get a guaranteed store every iteration regardless of whether the
value changed. For a sparse-match `Replace` workload (the realistic case), the
extra writes cost 1.5-3&times; vs the branchful form below.

```c#
// GOOD on net481.
ushort v = *ptr;
if (v == oldShort) *ptr = newShort;
ptr++;
```

The branch predictor handles "almost never matches" extremely well. Real-world
`Replace` calls are usually sparse, so the branchful form wins.

**On `net10`** the branchless form is roughly tied or slightly better. If you
want a single shape, prefer branchful: it's the safer choice for the older JIT
and not measurably worse on the newer one.

## Copy-replace exception

For methods that **always store to a separate destination buffer** (e.g.
`ReadOnlySpan<T>.Replace(Span<T> destination, T, T)`), every destination slot
must be written either way. The right form there is "always copy, then
conditionally overwrite":

```c#
ushort i0 = src[0]; ushort i1 = src[1]; ushort i2 = src[2]; ushort i3 = src[3];
dst[0] = i0; dst[1] = i1; dst[2] = i2; dst[3] = i3;
if (i0 == oldShort) dst[0] = newShort;
if (i1 == oldShort) dst[1] = newShort;
if (i2 == oldShort) dst[2] = newShort;
if (i3 == oldShort) dst[3] = newShort;
```

Same conclusion (avoid the JIT's poorly-lowered ternary over `ushort` / `byte`),
different surface form because the copy can't be avoided.

## SWAR haszero scan over a `ulong` window

```c#
// BAD: regresses by up to 3x on dense matches.
const ulong Lo16 = 0x0001_0001_0001_0001UL;
const ulong Hi16 = 0x8000_8000_8000_8000UL;
ulong oldBcast = (ulong)oldShort * Lo16;

while (ptr < unrollEnd)
{
    ulong word = *(ulong*)ptr;
    ulong x = word ^ oldBcast;
    ulong has = (x - Lo16) & ~x & Hi16;
    if (has != 0)
    {
        if (ptr[0] == oldShort) ptr[0] = newShort;
        if (ptr[1] == oldShort) ptr[1] = newShort;
        if (ptr[2] == oldShort) ptr[2] = newShort;
        if (ptr[3] == oldShort) ptr[3] = newShort;
    }
    ptr += 4;
}
```

The bit-trick (`(x - Lo16) & ~x & Hi16`) adds a dependent-chain of 3 arithmetic
ops per chunk, plus a load and an XOR, plus the broadcast computation. **Whenever
any lane matches, the code falls through to the four scalar checks** - you
pay the SWAR overhead **on top of** the mutation cost. On dense matches this
regresses by 3&times;.

The classic strchr-style SWAR optimization makes sense in C without true SIMD;
on `net481` it does not pay back its overhead even for sparse data, because the
`net481` JIT keeps the bit-trick in a long dependency chain rather than
parallelizing the arithmetic with the scalar reads.

## Replacing BCL `IndexOf` with a scalar specialization for sparse search

See [bcl-tradeoffs.md](bcl-tradeoffs.md). The rule cuts both ways - full
scan favors specialization, sparse search favors the BCL. Don't apply one half
without the other.

## Unroll by 8 (and beyond)

See [unrolling.md](unrolling.md). Unroll-8 wins at length 16 and regresses at
length 256+ on `net481`. Stick to unroll-4 unless you have measurements showing
the larger unroll is worth it for *your specific size distribution*.

## Stripping `[MethodImpl(MethodImplOptions.AggressiveInlining)]` "for code size"

The attribute looks like a non-functional hint, but on `net481` it's load-bearing.
A small specialized loop that doesn't get inlined into its caller becomes a
JIT-time call, defeating the entire `typeof(T)` specialization (because the
caller now invokes a generic stub through a virtual dispatch mechanism the
specialization was meant to elide). Measured: 1.82&times; &rarr; 1.07&times; on
a tight scalar loop at length 16 just from adding the attribute back.

If you really must remove it (e.g. method body becomes too large), measure
before and after. Don't strip it because "the method is short anyway, the JIT
will inline it" - on `net481` the JIT often won't.

## `Vector<T>` from `System.Numerics.Vectors` "for portable SIMD"

It exists on `net481`. It does not auto-vectorize equality-replace loops on
the older JIT, and per-load/store overhead loses to a plain unrolled scalar
loop at typical sizes (16-4096). Don't reach for it unless you've
benchmarked it for your specific shape and seen a win.

True SIMD on `net481` requires `System.Runtime.Intrinsics`, which is .NET 5+
only. Out of scope here.
