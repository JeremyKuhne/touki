// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// CS8500 ("takes the address of, gets the size of, or declares a pointer to
// a managed type") fires on every `fixed (T* ...)` and `(<primitive>*)p` cast
// in this file. Each one is statically dominated by an `if (typeof(T) ==
// typeof(<primitive>))` runtime check, which guarantees `T` is an unmanaged
// primitive on every path that reaches the pointer. The compiler cannot see
// through the typeof check, so the warning is structurally unavoidable here
// without rewriting the specialization in a way that defeats its purpose.
#pragma warning disable CS8500

namespace System;

public static partial class SpanExtensions
{
    extension<T>(Span<T> span) where T : IEquatable<T>?
    {
        /// <summary>
        ///  Replaces all occurrences of <paramref name="oldValue"/> with <paramref name="newValue"/>.
        /// </summary>
        /// <param name="oldValue">The value to be replaced with <paramref name="newValue"/>.</param>
        /// <param name="newValue">The value to replace all occurrences of <paramref name="oldValue"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Replace(T oldValue, T newValue)
        {
            if (Equal(oldValue, newValue))
            {
                return;
            }

            // Specialize for 2-byte primitives (char / short / ushort): equality on the raw
            // bit pattern matches IEquatable equality, so we can reuse a single ushort* loop.
            // The JIT elides this branch for any other T.
            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                // The explicit `& 0xFFFF` mask is load-bearing on net481 RyuJIT.
                // Without it, when this method is inlined the JIT propagates the
                // caller's int-promoted argument constant through Unsafe.As and
                // emits a 32-bit-wide comparison immediate (e.g. 0xFFFFFFFF for
                // (short)-1) instead of the requested 16-bit value (0xFFFF), and
                // the loop's `cmp byte ptr [...]` style compare never matches.
                // The mask forces the JIT to fold the high bits to zero.
                ushort oldShort = (ushort)(Unsafe.As<T, ushort>(ref oldValue) & 0xFFFF);
                ushort newShort = (ushort)(Unsafe.As<T, ushort>(ref newValue) & 0xFFFF);
                fixed (T* p = span)
                {
                    ushort* ptr = (ushort*)p;
                    ushort* end = ptr + span.Length;
                    ushort* unrollEnd = ptr + (span.Length & ~3);

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
                        if (*ptr == oldShort)
                        {
                            *ptr = newShort;
                        }

                        ptr++;
                    }
                }

                return;
            }

            // Specialize for 1-byte primitives (byte / sbyte) the same way.
            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
            {
                // See the comment above; the explicit `& 0xFF` mask defeats the
                // net481 RyuJIT constant propagation that would otherwise compare
                // against a 32-bit sign-extended value for (sbyte)-1 inputs.
                byte oldByte = (byte)(Unsafe.As<T, byte>(ref oldValue) & 0xFF);
                byte newByte = (byte)(Unsafe.As<T, byte>(ref newValue) & 0xFF);
                fixed (T* p = span)
                {
                    byte* ptr = (byte*)p;
                    byte* end = ptr + span.Length;
                    byte* unrollEnd = ptr + (span.Length & ~3);

                    while (ptr < unrollEnd)
                    {
                        if (ptr[0] == oldByte) ptr[0] = newByte;
                        if (ptr[1] == oldByte) ptr[1] = newByte;
                        if (ptr[2] == oldByte) ptr[2] = newByte;
                        if (ptr[3] == oldByte) ptr[3] = newByte;
                        ptr += 4;
                    }

                    while (ptr < end)
                    {
                        if (*ptr == oldByte)
                        {
                            *ptr = newByte;
                        }

                        ptr++;
                    }
                }

                return;
            }

            ref T current = ref MemoryMarshal.GetReference(span);
            for (int i = 0; i < span.Length; i++)
            {
                ref T item = ref Unsafe.Add(ref current, i);
                if (Equal(item, oldValue))
                {
                    item = newValue;
                }
            }
        }
    }

    extension<T>(ReadOnlySpan<T> source) where T : IEquatable<T>?
    {
        /// <summary>
        ///  Copies <paramref name="source"/> to <paramref name="destination"/>, replacing all occurrences of
        ///  <paramref name="oldValue"/> with <paramref name="newValue"/>.
        /// </summary>
        /// <exception cref="ArgumentException">
        ///  Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Replace(Span<T> destination, T oldValue, T newValue)
        {
            if (destination.Length < source.Length)
            {
                throw new ArgumentException(
                    "Destination is too short.",
                    nameof(destination));
            }

            int length = source.Length;
            if (Equal(oldValue, newValue) || length == 0)
            {
                source.CopyTo(destination);
                return;
            }

            // Specialize for 2-byte primitives (char / short / ushort): equality on the raw
            // bit pattern matches IEquatable equality. The JIT elides this branch for any other T.
            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                // See the corresponding comment in Replace(T, T) above.
                ushort oldShort = (ushort)(Unsafe.As<T, ushort>(ref oldValue) & 0xFFFF);
                ushort newShort = (ushort)(Unsafe.As<T, ushort>(ref newValue) & 0xFFFF);
                fixed (T* sp = source)
                fixed (T* dp = destination)
                {
                    ushort* src = (ushort*)sp;
                    ushort* dst = (ushort*)dp;
                    ushort* end = src + length;
                    ushort* unrollEnd = src + (length & ~3);

                    while (src < unrollEnd)
                    {
                        ushort item0 = src[0];
                        ushort item1 = src[1];
                        ushort item2 = src[2];
                        ushort item3 = src[3];
                        dst[0] = item0;
                        dst[1] = item1;
                        dst[2] = item2;
                        dst[3] = item3;
                        if (item0 == oldShort) dst[0] = newShort;
                        if (item1 == oldShort) dst[1] = newShort;
                        if (item2 == oldShort) dst[2] = newShort;
                        if (item3 == oldShort) dst[3] = newShort;
                        src += 4;
                        dst += 4;
                    }

                    while (src < end)
                    {
                        ushort item = *src;
                        *dst = item;
                        if (item == oldShort) *dst = newShort;
                        src++;
                        dst++;
                    }
                }

                return;
            }

            // Specialize for 1-byte primitives (byte / sbyte) the same way.
            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
            {
                byte oldByte = (byte)(Unsafe.As<T, byte>(ref oldValue) & 0xFF);
                byte newByte = (byte)(Unsafe.As<T, byte>(ref newValue) & 0xFF);
                fixed (T* sp = source)
                fixed (T* dp = destination)
                {
                    byte* src = (byte*)sp;
                    byte* dst = (byte*)dp;
                    byte* end = src + length;
                    byte* unrollEnd = src + (length & ~3);

                    while (src < unrollEnd)
                    {
                        byte item0 = src[0];
                        byte item1 = src[1];
                        byte item2 = src[2];
                        byte item3 = src[3];
                        dst[0] = item0;
                        dst[1] = item1;
                        dst[2] = item2;
                        dst[3] = item3;
                        if (item0 == oldByte) dst[0] = newByte;
                        if (item1 == oldByte) dst[1] = newByte;
                        if (item2 == oldByte) dst[2] = newByte;
                        if (item3 == oldByte) dst[3] = newByte;
                        src += 4;
                        dst += 4;
                    }

                    while (src < end)
                    {
                        byte item = *src;
                        *dst = item;
                        if (item == oldByte) *dst = newByte;
                        src++;
                        dst++;
                    }
                }

                return;
            }

            ref T sourceRef = ref MemoryMarshal.GetReference(source);
            ref T destRef = ref MemoryMarshal.GetReference(destination);
            for (int i = 0; i < length; i++)
            {
                T item = Unsafe.Add(ref sourceRef, i);
                Unsafe.Add(ref destRef, i) = Equal(item, oldValue) ? newValue : item;
            }
        }

        /// <summary>
        ///  Determines whether two sequences are equal by comparing the elements using the provided
        ///  <paramref name="comparer"/>.
        /// </summary>
        public bool SequenceEqual(ReadOnlySpan<T> other, IEqualityComparer<T>? comparer)
        {
            if (source.Length != other.Length)
            {
                return false;
            }

            // When the comparer is the default, route to the BCL fast path. On .NET Framework this is
            // a hand-tuned per-primitive scalar/ulong-stride compare in System.Memory (no SIMD), but it
            // still beats a per-element loop here because it processes multiple elements per inner step.
            if (comparer is null || comparer == EqualityComparer<T>.Default)
            {
                return source.SequenceEqual(other);
            }

            for (int i = 0; i < source.Length; i++)
            {
                if (!comparer.Equals(source[i], other[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    extension<T>(Span<T> span) where T : IEquatable<T>?
    {
        /// <inheritdoc cref="SequenceEqual{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, IEqualityComparer{T}?)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SequenceEqual(ReadOnlySpan<T> other, IEqualityComparer<T>? comparer) =>
            ((ReadOnlySpan<T>)span).SequenceEqual(other, comparer);
    }
}
