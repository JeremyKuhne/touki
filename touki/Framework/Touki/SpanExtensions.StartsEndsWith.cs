// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public static partial class SpanExtensions
{
    extension<T>(ReadOnlySpan<T> span) where T : IEquatable<T>?
    {
        /// <summary>
        ///  Returns <see langword="true"/> if the span begins with <paramref name="value"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool StartsWith(T value)
        {
            // Fast paths for bit-equatable primitives. Avoids EqualityComparer dispatch on net481's older JIT.
            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte) || typeof(T) == typeof(bool))
            {
                return span.Length != 0
                    && Unsafe.As<T, byte>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                        == Unsafe.As<T, byte>(ref value);
            }

            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                return span.Length != 0
                    && Unsafe.As<T, ushort>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                        == Unsafe.As<T, ushort>(ref value);
            }

            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
            {
                return span.Length != 0
                    && Unsafe.As<T, uint>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                        == Unsafe.As<T, uint>(ref value);
            }

            if (typeof(T) == typeof(long) || typeof(T) == typeof(ulong))
            {
                return span.Length != 0
                    && Unsafe.As<T, ulong>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                        == Unsafe.As<T, ulong>(ref value);
            }

            return span.Length != 0 && EqualityComparer<T>.Default.Equals(span[0], value);
        }

        /// <summary>
        ///  Returns <see langword="true"/> if the span ends with <paramref name="value"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EndsWith(T value)
        {
            int length = span.Length;
            if (length == 0)
            {
                return false;
            }

            if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte) || typeof(T) == typeof(bool))
            {
                return Unsafe.Add(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span))), length - 1)
                    == Unsafe.As<T, byte>(ref value);
            }

            if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
            {
                return Unsafe.Add(ref Unsafe.As<T, ushort>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span))), length - 1)
                    == Unsafe.As<T, ushort>(ref value);
            }

            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
            {
                return Unsafe.Add(ref Unsafe.As<T, uint>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span))), length - 1)
                    == Unsafe.As<T, uint>(ref value);
            }

            if (typeof(T) == typeof(long) || typeof(T) == typeof(ulong))
            {
                return Unsafe.Add(ref Unsafe.As<T, ulong>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span))), length - 1)
                    == Unsafe.As<T, ulong>(ref value);
            }

            return EqualityComparer<T>.Default.Equals(span[length - 1], value);
        }
    }

    extension<T>(Span<T> span) where T : IEquatable<T>?
    {
        /// <inheritdoc cref="StartsWith{T}(ReadOnlySpan{T}, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool StartsWith(T value) => ((ReadOnlySpan<T>)span).StartsWith(value);

        /// <inheritdoc cref="EndsWith{T}(ReadOnlySpan{T}, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EndsWith(T value) => ((ReadOnlySpan<T>)span).EndsWith(value);
    }
}
