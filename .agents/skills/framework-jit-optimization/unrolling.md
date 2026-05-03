# Loop unrolling on `net481`

The only unroll form that wins consistently on `net481` (and is harmless on
`net10`) is **unroll-by-4 with constant-offset indexed reads + a single pointer
increment per iteration**. Three close variants all regress.

## Use this form

```c#
fixed (T* p = span)
{
    ushort* ptr = (ushort*)p;
    int length = span.Length;
    ushort* end = ptr + length;
    ushort* unrollEnd = ptr + (length & ~3);

    while (ptr < unrollEnd)
    {
        if (ptr[0] == oldShort) ptr[0] = newShort;
        if (ptr[1] == oldShort) ptr[1] = newShort;
        if (ptr[2] == oldShort) ptr[2] = newShort;
        if (ptr[3] == oldShort) ptr[3] = newShort;
        ptr += 4;
    }

    while (ptr < end)
    {
        if (*ptr == oldShort) *ptr = newShort;
        ptr++;
    }
}
```

Two key choices:

1. **Indexed reads from a fixed base.** `ptr[0]`, `ptr[1]`, `ptr[2]`, `ptr[3]`
   compile to four loads with constant offsets from one base register. The
   four loads have no dependency on each other, so the CPU can issue them in
   parallel.
2. **Single `ptr += 4` per iteration.** One arithmetic op at the end of the
   chunk instead of one per element.

Measured: 14&ndash;36% faster than the scalar baseline at every size on both
runtimes.

## Don't use these

### `*ptr; ptr++` four times in a row

```c#
// BAD: serializes the loads through the increment chain.
if (*ptr == oldShort) *ptr = newShort; ptr++;
if (*ptr == oldShort) *ptr = newShort; ptr++;
if (*ptr == oldShort) *ptr = newShort; ptr++;
if (*ptr == oldShort) *ptr = newShort; ptr++;
```

Every load now depends on the previous `ptr++`. The JIT does not recover the
parallelism by recognizing the pattern. Measured: 0.84&times; vs 1.12&times;
ratio at length 256 on `net481` &mdash; the `ptr++` form is actually slower
than the un-unrolled scalar baseline.

### Integer-indexed body

```c#
// BAD: forces the JIT to recompute base + i*sizeof + offset*sizeof per load.
for (int i = 0; i < unrollEnd; i += 4)
{
    if (ptr[i + 0] == oldShort) ptr[i + 0] = newShort;
    if (ptr[i + 1] == oldShort) ptr[i + 1] = newShort;
    if (ptr[i + 2] == oldShort) ptr[i + 2] = newShort;
    if (ptr[i + 3] == oldShort) ptr[i + 3] = newShort;
}
```

The `net481` JIT does not hoist the `ptr + i` calculation. Each `ptr[i + k]`
becomes `lea` + `mov`. **Worse than the scalar baseline at length 4096
(1.22&times;).** Always advance the pointer instead.

### Unroll-by-8

```c#
// BAD on net481, fine on net10.
while (ptr < unrollEnd)
{
    if (ptr[0] == oldShort) ptr[0] = newShort;
    // ...repeat for 1..7...
    ptr += 8;
}
```

Wins at length 16 (small loop, fewer iterations). **Regresses 1.3&ndash;1.6&times;
at length 256+** on `net481`. Most likely the larger method body trips a
register-allocation or loop-alignment threshold. The .NET 10 JIT recovers but
`net481` does not.

## Backward unroll for `LastIndexOf`-style methods

```c#
fixed (T* p = span)
{
    ushort* start = (ushort*)p;
    int length = span.Length;
    ushort* ptr = start + length;
    ushort* unrollStart = start + (length & 3);

    while (ptr > unrollStart)
    {
        ptr -= 4;
        if (ptr[3] != target) return (int)(ptr - start) + 3;
        if (ptr[2] != target) return (int)(ptr - start) + 2;
        if (ptr[1] != target) return (int)(ptr - start) + 1;
        if (ptr[0] != target) return (int)(ptr - start) + 0;
    }

    while (ptr > start)
    {
        ptr--;
        if (*ptr != target) return (int)(ptr - start);
    }
}
```

Note: the `ptr -= 4` happens **before** the four reads (so the indices walk
3, 2, 1, 0 within the chunk, returning the highest matching index first to
preserve the "last occurrence" semantics).

## Tail loop

Always pair the unrolled body with a scalar tail that handles 0&ndash;3
elements. `length & ~3` rounds the unroll boundary down to a multiple of 4;
the remainder is `length & 3` elements walked one at a time.
