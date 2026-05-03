# Primitive specialization in generic methods

Use `typeof(T) == typeof(...)` plus `Unsafe.As<T, TPrimitive>(ref value)` to fork
generic code into per-primitive fast paths that the JIT can fully eliminate when
`T` is something else. For value-type `T`, the comparison is a JIT-time constant:
every other branch becomes dead code.

## Pattern

```c#
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public unsafe void Replace(T oldValue, T newValue)
{
    if (Equal(oldValue, newValue))
    {
        return;
    }

    if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
    {
        ushort oldShort = Unsafe.As<T, ushort>(ref oldValue);
        ushort newShort = Unsafe.As<T, ushort>(ref newValue);
        // ... tight ushort* loop ...
        return;
    }

    if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
    {
        byte oldByte = Unsafe.As<T, byte>(ref oldValue);
        byte newByte = Unsafe.As<T, byte>(ref newValue);
        // ... tight byte* loop ...
        return;
    }

    // Fallback for everything else.
    ref T current = ref MemoryMarshal.GetReference(span);
    // ...
}
```

## Why `Unsafe.As<T, TPrimitive>(ref value)`

A regular cast `(ushort)(object)oldValue` boxes. `Unsafe.As<T, ushort>(ref oldValue)`
is a no-op reinterpret &mdash; the JIT sees the constant `typeof(T) == typeof(ushort)`
test before it, knows the cast is valid, and emits a direct load.

## Bit-equality equivalence classes

`IEquatable<T>.Equals` for primitive integer types is just bit equality, so a
single typed pointer loop covers a whole class:

| C# type pair | Underlying width | Use one loop with cast type |
| --- | --- | --- |
| `byte` / `sbyte` | 8 bits | `byte*` |
| `char` / `short` / `ushort` | 16 bits | `ushort*` |
| `int` / `uint` | 32 bits | `uint*` |
| `long` / `ulong` / `IntPtr` (on 64-bit) | 64 bits | `ulong*` |

`float` / `double` / `decimal` are **not** members of these classes &mdash; their
`IEquatable` semantics differ from bit equality (NaN, negative zero,
denormalized flags). Don't fold them in.

For methods constrained to `where T : IComparable<T>` (e.g. `IndexOfAnyInRange`),
keep the primitives as their signed/unsigned variants because comparison
operators differ. See [SpanExtensions.InRange.cs](../../../touki/Framework/Touki/SpanExtensions.InRange.cs)
for the full byte/sbyte/char/short/ushort/int/uint/long/ulong specialization.

## `[MethodImpl(MethodImplOptions.AggressiveInlining)]`

Always put this on the generic entry method when you specialize:

- The `net481` JIT's default heuristics are conservative. Without the attribute,
  the wrapper method itself is a JIT-time call, defeating the whole point.
- Measured impact on a tight scalar `ref T` loop at length 16:
  `1.82&times; &rarr; 1.07&times;` vs the dedicated-overload baseline. The
  attribute is doing real work, not just hinting.

The inlining attribute is also why we don't need separate `extension(Span<char>)`
overloads &mdash; the generic version with the `typeof` ladder, when inlined,
generates code identical to a hand-written `Span<char>` overload.

## Verifying the JIT actually elides the dead branches

For `T = int`, the `typeof(T) == typeof(char)` branch should become unreachable.
You can confirm with a debugger by setting a breakpoint inside it &mdash; on a
Release build with the attribute applied, the JIT will refuse to set the
breakpoint because the code was eliminated.

If the elision is failing (e.g. you forget `[AggressiveInlining]`, or `T` is
itself a generic in some outer context where `typeof(T)` isn't a JIT-time
constant), you'll see all branches in the disassembly &mdash; and your benchmark
ratio will reflect it. Add the attribute, or hoist the call site so `T` is
concrete.

## See also

- [unrolling.md](unrolling.md) &mdash; what to put inside the per-primitive
  block.
- [bcl-tradeoffs.md](bcl-tradeoffs.md) &mdash; whether to specialize at all,
  vs delegate to a BCL primitive.
