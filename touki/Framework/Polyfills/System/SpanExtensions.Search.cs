// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Algorithms in this file are adapted from dotnet/runtime (MIT licensed).
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/SpanHelpers.T.cs

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
    extension<T>(ReadOnlySpan<T> span) where T : IEquatable<T>?
    {
        /// <summary>
        ///  Searches for the first index of any value other than the specified <paramref name="value"/>.
        /// </summary>
        /// <param name="value">A value to avoid.</param>
        /// <returns>
        ///  The index in the span of the first occurrence of any value other than <paramref name="value"/>.
        ///  If all of the values are <paramref name="value"/>, returns -1.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int IndexOfAnyExcept(T value)
        {
            // Specialize for 2-byte primitives (char / short / ushort): IEquatable equality is bit equality.
            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                // The explicit `& 0xFFFF` mask is load-bearing on net481 RyuJIT.
                // Inside an [AggressiveInlining] method that takes a generic `T`,
                // `Unsafe.As<T, ushort>(ref param)` on a literal signed-primitive
                // caller (e.g. `(short)-1`) propagates the int-promoted constant
                // 0xFFFFFFFF through the constant tracker, producing a 32-bit
                // compare against a `movzx`-loaded ushort that is always false.
                // Masking in the int domain forces a `conv.u2` and yields correct
                // codegen on both net481 and modern .NET RyuJIT. See
                // touki.tests/Framework/Regressions/UnsafeAsAggressiveInliningRegressionTests.cs
                // and touki.perf/ReplaceUnsafeAsPerf.cs for the captured
                // disassembly evidence.
                ushort target = (ushort)(Unsafe.As<T, ushort>(ref value) & 0xFFFF);
                fixed (T* p = span)
                {
                    ushort* ptr = (ushort*)p;
                    int length = span.Length;
                    ushort* end = ptr + length;
                    ushort* unrollEnd = ptr + (length & ~3);
                    ushort* start = ptr;

                    while (ptr < unrollEnd)
                    {
                        if (ptr[0] != target) return (int)(ptr - start) + 0;
                        if (ptr[1] != target) return (int)(ptr - start) + 1;
                        if (ptr[2] != target) return (int)(ptr - start) + 2;
                        if (ptr[3] != target) return (int)(ptr - start) + 3;
                        ptr += 4;
                    }

                    while (ptr < end)
                    {
                        if (*ptr != target) return (int)(ptr - start);
                        ptr++;
                    }
                }

                return -1;
            }

            // Specialize for 1-byte primitives (byte / sbyte).
            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
            {
                // See the ushort branch above; the explicit `& 0xFF` mask defeats
                // the same net481 RyuJIT constant-propagation bug for sbyte inputs.
                byte target = (byte)(Unsafe.As<T, byte>(ref value) & 0xFF);
                fixed (T* p = span)
                {
                    byte* ptr = (byte*)p;
                    int length = span.Length;
                    byte* end = ptr + length;
                    byte* unrollEnd = ptr + (length & ~3);
                    byte* start = ptr;

                    while (ptr < unrollEnd)
                    {
                        if (ptr[0] != target) return (int)(ptr - start) + 0;
                        if (ptr[1] != target) return (int)(ptr - start) + 1;
                        if (ptr[2] != target) return (int)(ptr - start) + 2;
                        if (ptr[3] != target) return (int)(ptr - start) + 3;
                        ptr += 4;
                    }

                    while (ptr < end)
                    {
                        if (*ptr != target) return (int)(ptr - start);
                        ptr++;
                    }
                }

                return -1;
            }

            ref T current = ref MemoryMarshal.GetReference(span);
            for (int i = 0; i < span.Length; i++)
            {
                ref T item = ref Unsafe.Add(ref current, i);
                if (value is null ? item is not null : !value.Equals(item))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///  Searches for the first index of any value other than <paramref name="value0"/> or <paramref name="value1"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int IndexOfAnyExcept(T value0, T value1)
        {
            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                ushort v0 = (ushort)(Unsafe.As<T, ushort>(ref value0) & 0xFFFF);
                ushort v1 = (ushort)(Unsafe.As<T, ushort>(ref value1) & 0xFFFF);
                fixed (T* p = span)
                {
                    ushort* ptr = (ushort*)p;
                    int length = span.Length;
                    for (int i = 0; i < length; i++)
                    {
                        ushort item = ptr[i];
                        if (item != v0 && item != v1)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
            {
                byte v0 = (byte)(Unsafe.As<T, byte>(ref value0) & 0xFF);
                byte v1 = (byte)(Unsafe.As<T, byte>(ref value1) & 0xFF);
                fixed (T* p = span)
                {
                    byte* ptr = (byte*)p;
                    int length = span.Length;
                    for (int i = 0; i < length; i++)
                    {
                        byte item = ptr[i];
                        if (item != v0 && item != v1)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            ref T current = ref MemoryMarshal.GetReference(span);
            for (int i = 0; i < span.Length; i++)
            {
                ref T item = ref Unsafe.Add(ref current, i);
                if (!Equal(item, value0) && !Equal(item, value1))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///  Searches for the first index of any value other than <paramref name="value0"/>, <paramref name="value1"/>,
        ///  or <paramref name="value2"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int IndexOfAnyExcept(T value0, T value1, T value2)
        {
            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                ushort v0 = (ushort)(Unsafe.As<T, ushort>(ref value0) & 0xFFFF);
                ushort v1 = (ushort)(Unsafe.As<T, ushort>(ref value1) & 0xFFFF);
                ushort v2 = (ushort)(Unsafe.As<T, ushort>(ref value2) & 0xFFFF);
                fixed (T* p = span)
                {
                    ushort* ptr = (ushort*)p;
                    int length = span.Length;
                    for (int i = 0; i < length; i++)
                    {
                        ushort item = ptr[i];
                        if (item != v0 && item != v1 && item != v2)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
            {
                byte v0 = (byte)(Unsafe.As<T, byte>(ref value0) & 0xFF);
                byte v1 = (byte)(Unsafe.As<T, byte>(ref value1) & 0xFF);
                byte v2 = (byte)(Unsafe.As<T, byte>(ref value2) & 0xFF);
                fixed (T* p = span)
                {
                    byte* ptr = (byte*)p;
                    int length = span.Length;
                    for (int i = 0; i < length; i++)
                    {
                        byte item = ptr[i];
                        if (item != v0 && item != v1 && item != v2)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            ref T current = ref MemoryMarshal.GetReference(span);
            for (int i = 0; i < span.Length; i++)
            {
                ref T item = ref Unsafe.Add(ref current, i);
                if (!Equal(item, value0) && !Equal(item, value1) && !Equal(item, value2))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///  Searches for the first index of any value other than the specified <paramref name="values"/>.
        /// </summary>
        public int IndexOfAnyExcept(ReadOnlySpan<T> values)
        {
            switch (values.Length)
            {
                case 0:
                    return span.IsEmpty ? -1 : 0;

                case 1:
                    return span.IndexOfAnyExcept(values[0]);

                case 2:
                    return span.IndexOfAnyExcept(values[0], values[1]);

                case 3:
                    return span.IndexOfAnyExcept(values[0], values[1], values[2]);

                default:
                    ref T current = ref MemoryMarshal.GetReference(span);
                    for (int i = 0; i < span.Length; i++)
                    {
                        ref T item = ref Unsafe.Add(ref current, i);
                        if (values.IndexOf(item) < 0)
                        {
                            return i;
                        }
                    }

                    return -1;
            }
        }

        /// <summary>
        ///  Searches for the last index of any value other than the specified <paramref name="value"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int LastIndexOfAnyExcept(T value)
        {
            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                ushort target = (ushort)(Unsafe.As<T, ushort>(ref value) & 0xFFFF);
                fixed (T* p = span)
                {
                    ushort* start = (ushort*)p;
                    int length = span.Length;
                    // Walk from the tail. unrollStart points just past the unrolled tail region.
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

                return -1;
            }

            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
            {
                byte target = (byte)(Unsafe.As<T, byte>(ref value) & 0xFF);
                fixed (T* p = span)
                {
                    byte* start = (byte*)p;
                    int length = span.Length;
                    byte* ptr = start + length;
                    byte* unrollStart = start + (length & 3);

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

                return -1;
            }

            ref T current = ref MemoryMarshal.GetReference(span);
            for (int i = span.Length - 1; i >= 0; i--)
            {
                if (!Equal(Unsafe.Add(ref current, i), value))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///  Searches for the last index of any value other than <paramref name="value0"/> or <paramref name="value1"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int LastIndexOfAnyExcept(T value0, T value1)
        {
            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                ushort v0 = (ushort)(Unsafe.As<T, ushort>(ref value0) & 0xFFFF);
                ushort v1 = (ushort)(Unsafe.As<T, ushort>(ref value1) & 0xFFFF);
                fixed (T* p = span)
                {
                    ushort* ptr = (ushort*)p;
                    for (int i = span.Length - 1; i >= 0; i--)
                    {
                        ushort item = ptr[i];
                        if (item != v0 && item != v1)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
            {
                byte v0 = (byte)(Unsafe.As<T, byte>(ref value0) & 0xFF);
                byte v1 = (byte)(Unsafe.As<T, byte>(ref value1) & 0xFF);
                fixed (T* p = span)
                {
                    byte* ptr = (byte*)p;
                    for (int i = span.Length - 1; i >= 0; i--)
                    {
                        byte item = ptr[i];
                        if (item != v0 && item != v1)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            ref T current = ref MemoryMarshal.GetReference(span);
            for (int i = span.Length - 1; i >= 0; i--)
            {
                ref T item = ref Unsafe.Add(ref current, i);
                if (!Equal(item, value0) && !Equal(item, value1))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///  Searches for the last index of any value other than <paramref name="value0"/>, <paramref name="value1"/>,
        ///  or <paramref name="value2"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int LastIndexOfAnyExcept(T value0, T value1, T value2)
        {
            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                ushort v0 = (ushort)(Unsafe.As<T, ushort>(ref value0) & 0xFFFF);
                ushort v1 = (ushort)(Unsafe.As<T, ushort>(ref value1) & 0xFFFF);
                ushort v2 = (ushort)(Unsafe.As<T, ushort>(ref value2) & 0xFFFF);
                fixed (T* p = span)
                {
                    ushort* ptr = (ushort*)p;
                    for (int i = span.Length - 1; i >= 0; i--)
                    {
                        ushort item = ptr[i];
                        if (item != v0 && item != v1 && item != v2)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
            {
                byte v0 = (byte)(Unsafe.As<T, byte>(ref value0) & 0xFF);
                byte v1 = (byte)(Unsafe.As<T, byte>(ref value1) & 0xFF);
                byte v2 = (byte)(Unsafe.As<T, byte>(ref value2) & 0xFF);
                fixed (T* p = span)
                {
                    byte* ptr = (byte*)p;
                    for (int i = span.Length - 1; i >= 0; i--)
                    {
                        byte item = ptr[i];
                        if (item != v0 && item != v1 && item != v2)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            ref T current = ref MemoryMarshal.GetReference(span);
            for (int i = span.Length - 1; i >= 0; i--)
            {
                ref T item = ref Unsafe.Add(ref current, i);
                if (!Equal(item, value0) && !Equal(item, value1) && !Equal(item, value2))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///  Searches for the last index of any value other than the specified <paramref name="values"/>.
        /// </summary>
        public int LastIndexOfAnyExcept(ReadOnlySpan<T> values)
        {
            switch (values.Length)
            {
                case 0:
                    return span.IsEmpty ? -1 : span.Length - 1;

                case 1:
                    return span.LastIndexOfAnyExcept(values[0]);

                case 2:
                    return span.LastIndexOfAnyExcept(values[0], values[1]);

                case 3:
                    return span.LastIndexOfAnyExcept(values[0], values[1], values[2]);

                default:
                    ref T current = ref MemoryMarshal.GetReference(span);
                    for (int i = span.Length - 1; i >= 0; i--)
                    {
                        ref T item = ref Unsafe.Add(ref current, i);
                        if (values.IndexOf(item) < 0)
                        {
                            return i;
                        }
                    }

                    return -1;
            }
        }

        /// <summary>
        ///  Searches for an occurrence of <paramref name="value0"/> or <paramref name="value1"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAny(T value0, T value1) => span.IndexOfAny(value0, value1) >= 0;

        /// <summary>
        ///  Searches for an occurrence of <paramref name="value0"/>, <paramref name="value1"/>, or <paramref name="value2"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAny(T value0, T value1, T value2) => span.IndexOfAny(value0, value1, value2) >= 0;

        /// <summary>
        ///  Searches for an occurrence of any of the specified <paramref name="values"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAny(ReadOnlySpan<T> values) => span.IndexOfAny(values) >= 0;

        /// <summary>
        ///  Searches for any value other than the specified <paramref name="value"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExcept(T value) => span.IndexOfAnyExcept(value) >= 0;

        /// <summary>
        ///  Searches for any value other than <paramref name="value0"/> or <paramref name="value1"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExcept(T value0, T value1) => span.IndexOfAnyExcept(value0, value1) >= 0;

        /// <summary>
        ///  Searches for any value other than <paramref name="value0"/>, <paramref name="value1"/>, or <paramref name="value2"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExcept(T value0, T value1, T value2) => span.IndexOfAnyExcept(value0, value1, value2) >= 0;

        /// <summary>
        ///  Searches for any value other than the specified <paramref name="values"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExcept(ReadOnlySpan<T> values) => span.IndexOfAnyExcept(values) >= 0;

        /// <summary>
        ///  Counts the number of times the specified <paramref name="value"/> occurs in the span.
        /// </summary>
        public int Count(T value)
        {
            // Defer to the BCL's IndexOf in System.Memory by walking matches. On .NET Framework this is
            // not vectorized, but its per-primitive ulong-stride scan still skips non-match runs faster
            // than a per-element specialization for sparse inputs (the realistic Count workload).
            ReadOnlySpan<T> remaining = span;
            int count = 0;
            int index;
            while ((index = remaining.IndexOf(value)) >= 0)
            {
                count++;
                remaining = remaining[(index + 1)..];
            }

            return count;
        }

        /// <summary>
        ///  Counts the number of non-overlapping occurrences of <paramref name="value"/> in the span.
        /// </summary>
        public int Count(ReadOnlySpan<T> value)
        {
            switch (value.Length)
            {
                case 0:
                    return 0;

                case 1:
                    return span.Count(value[0]);

                default:
                    ReadOnlySpan<T> remaining = span;
                    int count = 0;
                    int index;
                    while ((index = remaining.IndexOf(value)) >= 0)
                    {
                        count++;
                        remaining = remaining[(index + value.Length)..];
                    }

                    return count;
            }
        }

        /// <summary>
        ///  Finds the length of any common prefix shared between this span and <paramref name="other"/>.
        /// </summary>
        public int CommonPrefixLength(ReadOnlySpan<T> other)
        {
            int length = Math.Min(span.Length, other.Length);

            // For bitwise-equatable primitives, defer to BCL SequenceEqual on a power-of-two prefix
            // to find the divergence quickly. We use an exponential search so cost is O(log n) BCL calls.
            // On .NET Framework SequenceEqual is not vectorized but is hand-tuned per primitive, which is
            // still substantially faster than a scalar per-element loop on long shared prefixes.
            if (typeof(T) == typeof(byte)
                || typeof(T) == typeof(sbyte)
                || typeof(T) == typeof(char)
                || typeof(T) == typeof(short)
                || typeof(T) == typeof(ushort)
                || typeof(T) == typeof(int)
                || typeof(T) == typeof(uint)
                || typeof(T) == typeof(long)
                || typeof(T) == typeof(ulong))
            {
                int matched = 0;
                int probe = 16;
                while (matched + probe <= length
                    && span.Slice(matched, probe).SequenceEqual(other.Slice(matched, probe)))
                {
                    matched += probe;
                    if (probe < 1024)
                    {
                        probe *= 2;
                    }
                }

                ref T spanRef = ref MemoryMarshal.GetReference(span);
                ref T otherRef = ref MemoryMarshal.GetReference(other);
                for (int i = matched; i < length; i++)
                {
                    if (!Equal(Unsafe.Add(ref spanRef, i), Unsafe.Add(ref otherRef, i)))
                    {
                        return i;
                    }
                }

                return length;
            }
            else
            {
                ref T spanRef = ref MemoryMarshal.GetReference(span);
                ref T otherRef = ref MemoryMarshal.GetReference(other);
                for (int i = 0; i < length; i++)
                {
                    if (!Equal(Unsafe.Add(ref spanRef, i), Unsafe.Add(ref otherRef, i)))
                    {
                        return i;
                    }
                }

                return length;
            }
        }
    }

    extension<T>(ReadOnlySpan<T> span)
    {
        /// <summary>
        ///  Determines the length of any common prefix shared between this span and <paramref name="other"/>
        ///  using the specified <paramref name="comparer"/>.
        /// </summary>
        public int CommonPrefixLength(ReadOnlySpan<T> other, IEqualityComparer<T>? comparer)
        {
            IEqualityComparer<T> effective = comparer ?? EqualityComparer<T>.Default;
            int min = Math.Min(span.Length, other.Length);
            for (int i = 0; i < min; i++)
            {
                if (!effective.Equals(span[i], other[i]))
                {
                    return i;
                }
            }

            return min;
        }
    }

    extension<T>(Span<T> span) where T : IEquatable<T>?
    {
        /// <inheritdoc cref="IndexOfAnyExcept{T}(ReadOnlySpan{T}, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfAnyExcept(T value) => ((ReadOnlySpan<T>)span).IndexOfAnyExcept(value);

        /// <inheritdoc cref="IndexOfAnyExcept{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfAnyExcept(T value0, T value1) => ((ReadOnlySpan<T>)span).IndexOfAnyExcept(value0, value1);

        /// <inheritdoc cref="IndexOfAnyExcept{T}(ReadOnlySpan{T}, T, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfAnyExcept(T value0, T value1, T value2) =>
            ((ReadOnlySpan<T>)span).IndexOfAnyExcept(value0, value1, value2);

        /// <inheritdoc cref="IndexOfAnyExcept{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfAnyExcept(ReadOnlySpan<T> values) => ((ReadOnlySpan<T>)span).IndexOfAnyExcept(values);

        /// <inheritdoc cref="LastIndexOfAnyExcept{T}(ReadOnlySpan{T}, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOfAnyExcept(T value) => ((ReadOnlySpan<T>)span).LastIndexOfAnyExcept(value);

        /// <inheritdoc cref="LastIndexOfAnyExcept{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOfAnyExcept(T value0, T value1) =>
            ((ReadOnlySpan<T>)span).LastIndexOfAnyExcept(value0, value1);

        /// <inheritdoc cref="LastIndexOfAnyExcept{T}(ReadOnlySpan{T}, T, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOfAnyExcept(T value0, T value1, T value2) =>
            ((ReadOnlySpan<T>)span).LastIndexOfAnyExcept(value0, value1, value2);

        /// <inheritdoc cref="LastIndexOfAnyExcept{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOfAnyExcept(ReadOnlySpan<T> values) => ((ReadOnlySpan<T>)span).LastIndexOfAnyExcept(values);

        /// <inheritdoc cref="ContainsAny{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAny(T value0, T value1) => ((ReadOnlySpan<T>)span).ContainsAny(value0, value1);

        /// <inheritdoc cref="ContainsAny{T}(ReadOnlySpan{T}, T, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAny(T value0, T value1, T value2) =>
            ((ReadOnlySpan<T>)span).ContainsAny(value0, value1, value2);

        /// <inheritdoc cref="ContainsAny{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAny(ReadOnlySpan<T> values) => ((ReadOnlySpan<T>)span).ContainsAny(values);

        /// <inheritdoc cref="ContainsAnyExcept{T}(ReadOnlySpan{T}, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExcept(T value) => ((ReadOnlySpan<T>)span).ContainsAnyExcept(value);

        /// <inheritdoc cref="ContainsAnyExcept{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExcept(T value0, T value1) => ((ReadOnlySpan<T>)span).ContainsAnyExcept(value0, value1);

        /// <inheritdoc cref="ContainsAnyExcept{T}(ReadOnlySpan{T}, T, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExcept(T value0, T value1, T value2) =>
            ((ReadOnlySpan<T>)span).ContainsAnyExcept(value0, value1, value2);

        /// <inheritdoc cref="ContainsAnyExcept{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExcept(ReadOnlySpan<T> values) => ((ReadOnlySpan<T>)span).ContainsAnyExcept(values);

        /// <inheritdoc cref="Count{T}(ReadOnlySpan{T}, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Count(T value) => ((ReadOnlySpan<T>)span).Count(value);

        /// <inheritdoc cref="Count{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Count(ReadOnlySpan<T> value) => ((ReadOnlySpan<T>)span).Count(value);

        /// <inheritdoc cref="CommonPrefixLength{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CommonPrefixLength(ReadOnlySpan<T> other) => ((ReadOnlySpan<T>)span).CommonPrefixLength(other);
    }

    extension<T>(Span<T> span)
    {
        /// <inheritdoc cref="CommonPrefixLength{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, IEqualityComparer{T}?)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CommonPrefixLength(ReadOnlySpan<T> other, IEqualityComparer<T>? comparer) =>
            ((ReadOnlySpan<T>)span).CommonPrefixLength(other, comparer);
    }

    /// <summary>
    ///  Equality matching <see cref="System.MemoryExtensions"/>'s contract:
    ///  <c>value?.Equals(item) ?? item is null</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Equal<T>(T item, T value) where T : IEquatable<T>? =>
        value is null ? item is null : value.Equals(item);
}
