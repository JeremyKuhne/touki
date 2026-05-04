// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki;

namespace System;

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
            if (span.Length == 0)
            {
                return false;
            }

            T first = span[0];
            return value is null ? first is null : value.Equals(first);
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

            T last = span[length - 1];
            return value is null ? last is null : value.Equals(last);
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
