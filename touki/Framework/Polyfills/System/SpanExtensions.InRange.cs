// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki;

namespace System;

public static partial class SpanExtensions
{
    extension<T>(ReadOnlySpan<T> span) where T : IComparable<T>
    {
        /// <summary>
        ///  Searches for the first index of any value in the range between <paramref name="lowInclusive"/>
        ///  and <paramref name="highInclusive"/>, inclusive.
        /// </summary>
        /// <returns>
        ///  The index of the first value in the range, or -1 if none was found.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfAnyInRange(T lowInclusive, T highInclusive) =>
            IndexOfAnyInRangeCore(span, lowInclusive, highInclusive, except: false);

        /// <summary>
        ///  Searches for the first index of any value outside of the range between <paramref name="lowInclusive"/>
        ///  and <paramref name="highInclusive"/>, inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfAnyExceptInRange(T lowInclusive, T highInclusive) =>
            IndexOfAnyInRangeCore(span, lowInclusive, highInclusive, except: true);

        /// <summary>
        ///  Searches for the last index of any value in the range between <paramref name="lowInclusive"/>
        ///  and <paramref name="highInclusive"/>, inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOfAnyInRange(T lowInclusive, T highInclusive) =>
            LastIndexOfAnyInRangeCore(span, lowInclusive, highInclusive, except: false);

        /// <summary>
        ///  Searches for the last index of any value outside of the range between <paramref name="lowInclusive"/>
        ///  and <paramref name="highInclusive"/>, inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOfAnyExceptInRange(T lowInclusive, T highInclusive) =>
            LastIndexOfAnyInRangeCore(span, lowInclusive, highInclusive, except: true);

        /// <summary>
        ///  Returns <see langword="true"/> if any value in the span is in the range between
        ///  <paramref name="lowInclusive"/> and <paramref name="highInclusive"/>, inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyInRange(T lowInclusive, T highInclusive) =>
            IndexOfAnyInRangeCore(span, lowInclusive, highInclusive, except: false) >= 0;

        /// <summary>
        ///  Returns <see langword="true"/> if any value in the span is outside of the range between
        ///  <paramref name="lowInclusive"/> and <paramref name="highInclusive"/>, inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExceptInRange(T lowInclusive, T highInclusive) =>
            IndexOfAnyInRangeCore(span, lowInclusive, highInclusive, except: true) >= 0;
    }

    extension<T>(Span<T> span) where T : IComparable<T>
    {
        /// <inheritdoc cref="IndexOfAnyInRange{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfAnyInRange(T lowInclusive, T highInclusive) =>
            ((ReadOnlySpan<T>)span).IndexOfAnyInRange(lowInclusive, highInclusive);

        /// <inheritdoc cref="IndexOfAnyExceptInRange{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOfAnyExceptInRange(T lowInclusive, T highInclusive) =>
            ((ReadOnlySpan<T>)span).IndexOfAnyExceptInRange(lowInclusive, highInclusive);

        /// <inheritdoc cref="LastIndexOfAnyInRange{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOfAnyInRange(T lowInclusive, T highInclusive) =>
            ((ReadOnlySpan<T>)span).LastIndexOfAnyInRange(lowInclusive, highInclusive);

        /// <inheritdoc cref="LastIndexOfAnyExceptInRange{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int LastIndexOfAnyExceptInRange(T lowInclusive, T highInclusive) =>
            ((ReadOnlySpan<T>)span).LastIndexOfAnyExceptInRange(lowInclusive, highInclusive);

        /// <inheritdoc cref="ContainsAnyInRange{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyInRange(T lowInclusive, T highInclusive) =>
            ((ReadOnlySpan<T>)span).ContainsAnyInRange(lowInclusive, highInclusive);

        /// <inheritdoc cref="ContainsAnyExceptInRange{T}(ReadOnlySpan{T}, T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAnyExceptInRange(T lowInclusive, T highInclusive) =>
            ((ReadOnlySpan<T>)span).ContainsAnyExceptInRange(lowInclusive, highInclusive);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfAnyInRangeCore<T>(
        ReadOnlySpan<T> span,
        T lowInclusive,
        T highInclusive,
        bool except) where T : IComparable<T>
    {
        // Specialize for primitive numerics. .NET Framework has no SIMD support; the BCL provides only
        // hand-tuned scalar implementations and there is no auto-vectorization on the net472 RyuJIT.
        // We use a tight scalar loop with direct comparison operators which is the best we can portably
        // get without reaching for Sse2/Avx2 intrinsics (unavailable here).
        ref T first = ref MemoryMarshal.GetReference(span);
        int length = span.Length;

        if (typeof(T) == typeof(byte))
        {
            return IndexOfAnyInRangeByte(
                ref Unsafe.As<T, byte>(ref first),
                length,
                Unsafe.As<T, byte>(ref lowInclusive),
                Unsafe.As<T, byte>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(sbyte))
        {
            return IndexOfAnyInRangeSByte(
                ref Unsafe.As<T, sbyte>(ref first),
                length,
                Unsafe.As<T, sbyte>(ref lowInclusive),
                Unsafe.As<T, sbyte>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(char))
        {
            return IndexOfAnyInRangeChar(
                ref Unsafe.As<T, char>(ref first),
                length,
                Unsafe.As<T, char>(ref lowInclusive),
                Unsafe.As<T, char>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(short))
        {
            return IndexOfAnyInRangeInt16(
                ref Unsafe.As<T, short>(ref first),
                length,
                Unsafe.As<T, short>(ref lowInclusive),
                Unsafe.As<T, short>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(ushort))
        {
            return IndexOfAnyInRangeUInt16(
                ref Unsafe.As<T, ushort>(ref first),
                length,
                Unsafe.As<T, ushort>(ref lowInclusive),
                Unsafe.As<T, ushort>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(int))
        {
            return IndexOfAnyInRangeInt32(
                ref Unsafe.As<T, int>(ref first),
                length,
                Unsafe.As<T, int>(ref lowInclusive),
                Unsafe.As<T, int>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(uint))
        {
            return IndexOfAnyInRangeUInt32(
                ref Unsafe.As<T, uint>(ref first),
                length,
                Unsafe.As<T, uint>(ref lowInclusive),
                Unsafe.As<T, uint>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(long))
        {
            return IndexOfAnyInRangeInt64(
                ref Unsafe.As<T, long>(ref first),
                length,
                Unsafe.As<T, long>(ref lowInclusive),
                Unsafe.As<T, long>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(ulong))
        {
            return IndexOfAnyInRangeUInt64(
                ref Unsafe.As<T, ulong>(ref first),
                length,
                Unsafe.As<T, ulong>(ref lowInclusive),
                Unsafe.As<T, ulong>(ref highInclusive),
                except);
        }

        // Generic fallback using IComparable<T>.
        for (int i = 0; i < length; i++)
        {
            T value = Unsafe.Add(ref first, i);
            bool inRange = lowInclusive.CompareTo(value) <= 0 && highInclusive.CompareTo(value) >= 0;
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int LastIndexOfAnyInRangeCore<T>(
        ReadOnlySpan<T> span,
        T lowInclusive,
        T highInclusive,
        bool except) where T : IComparable<T>
    {
        ref T first = ref MemoryMarshal.GetReference(span);
        int length = span.Length;

        if (typeof(T) == typeof(byte))
        {
            return LastIndexOfAnyInRangeByte(
                ref Unsafe.As<T, byte>(ref first),
                length,
                Unsafe.As<T, byte>(ref lowInclusive),
                Unsafe.As<T, byte>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(char))
        {
            return LastIndexOfAnyInRangeChar(
                ref Unsafe.As<T, char>(ref first),
                length,
                Unsafe.As<T, char>(ref lowInclusive),
                Unsafe.As<T, char>(ref highInclusive),
                except);
        }

        if (typeof(T) == typeof(int))
        {
            return LastIndexOfAnyInRangeInt32(
                ref Unsafe.As<T, int>(ref first),
                length,
                Unsafe.As<T, int>(ref lowInclusive),
                Unsafe.As<T, int>(ref highInclusive),
                except);
        }

        for (int i = length - 1; i >= 0; i--)
        {
            T value = Unsafe.Add(ref first, i);
            bool inRange = lowInclusive.CompareTo(value) <= 0 && highInclusive.CompareTo(value) >= 0;
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAnyInRangeByte(ref byte first, int length, byte low, byte high, bool except)
    {
        for (int i = 0; i < length; i++)
        {
            byte v = Unsafe.Add(ref first, i);
            bool inRange = (uint)(v - low) <= (uint)(high - low);
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAnyInRangeSByte(ref sbyte first, int length, sbyte low, sbyte high, bool except)
    {
        for (int i = 0; i < length; i++)
        {
            sbyte v = Unsafe.Add(ref first, i);
            bool inRange = v >= low && v <= high;
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAnyInRangeChar(ref char first, int length, char low, char high, bool except)
    {
        for (int i = 0; i < length; i++)
        {
            char v = Unsafe.Add(ref first, i);
            bool inRange = (uint)(v - low) <= (uint)(high - low);
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAnyInRangeInt16(ref short first, int length, short low, short high, bool except)
    {
        for (int i = 0; i < length; i++)
        {
            short v = Unsafe.Add(ref first, i);
            bool inRange = v >= low && v <= high;
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAnyInRangeUInt16(ref ushort first, int length, ushort low, ushort high, bool except)
    {
        for (int i = 0; i < length; i++)
        {
            ushort v = Unsafe.Add(ref first, i);
            bool inRange = (uint)(v - low) <= (uint)(high - low);
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAnyInRangeInt32(ref int first, int length, int low, int high, bool except)
    {
        for (int i = 0; i < length; i++)
        {
            int v = Unsafe.Add(ref first, i);
            bool inRange = v >= low && v <= high;
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAnyInRangeUInt32(ref uint first, int length, uint low, uint high, bool except)
    {
        for (int i = 0; i < length; i++)
        {
            uint v = Unsafe.Add(ref first, i);
            bool inRange = (v - low) <= (high - low);
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAnyInRangeInt64(ref long first, int length, long low, long high, bool except)
    {
        for (int i = 0; i < length; i++)
        {
            long v = Unsafe.Add(ref first, i);
            bool inRange = v >= low && v <= high;
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAnyInRangeUInt64(ref ulong first, int length, ulong low, ulong high, bool except)
    {
        for (int i = 0; i < length; i++)
        {
            ulong v = Unsafe.Add(ref first, i);
            bool inRange = (v - low) <= (high - low);
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastIndexOfAnyInRangeByte(ref byte first, int length, byte low, byte high, bool except)
    {
        for (int i = length - 1; i >= 0; i--)
        {
            byte v = Unsafe.Add(ref first, i);
            bool inRange = (uint)(v - low) <= (uint)(high - low);
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastIndexOfAnyInRangeChar(ref char first, int length, char low, char high, bool except)
    {
        for (int i = length - 1; i >= 0; i--)
        {
            char v = Unsafe.Add(ref first, i);
            bool inRange = (uint)(v - low) <= (uint)(high - low);
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastIndexOfAnyInRangeInt32(ref int first, int length, int low, int high, bool except)
    {
        for (int i = length - 1; i >= 0; i--)
        {
            int v = Unsafe.Add(ref first, i);
            bool inRange = v >= low && v <= high;
            if (inRange != except)
            {
                return i;
            }
        }

        return -1;
    }
}
