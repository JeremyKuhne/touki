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
        /// <remarks>
        ///  <para>
        ///   Uses <see cref="EqualityComparer{T}.Default"/> which RyuJIT devirtualizes
        ///   for sealed value types, so this is allocation-free and matches the BCL's
        ///   primitive throughput without resorting to <c>Unsafe</c> reinterpretation
        ///   (which has been observed to mis-read the parameter byte under aggressive
        ///   Release-mode inlining for negative-valued signed primitives).
        ///  </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool StartsWith(T value) =>
            span.Length != 0 && EqualityComparer<T>.Default.Equals(span[0], value);

        /// <summary>
        ///  Returns <see langword="true"/> if the span ends with <paramref name="value"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EndsWith(T value)
        {
            int length = span.Length;
            return length != 0 && EqualityComparer<T>.Default.Equals(span[length - 1], value);
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
