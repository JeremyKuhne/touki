// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki;

namespace System;

/// <summary>
///  Span extensions for common operations.
/// </summary>
public static partial class SpanExtensions
{
    /// <summary>
    ///  Slice the given <paramref name="span"/> at null, if present.
    /// </summary>
    public static ReadOnlySpan<char> SliceAtNull(this ReadOnlySpan<char> span)
    {
        int index = span.IndexOf('\0');
        return index == -1 ? span : span[..index];
    }

    /// <summary>
    ///  Slice the given <paramref name="span"/> at null, if present.
    /// </summary>
    public static Span<char> SliceAtNull(this Span<char> span)
    {
        int index = span.IndexOf('\0');
        return index == -1 ? span : span[..index];
    }

    /// <summary>
    ///  Splits into strings on the given <paramref name="delimiter"/>.
    /// </summary>
    public static IEnumerable<string> SplitToEnumerable(this ReadOnlySpan<char> span, char delimiter, bool includeEmptyStrings = false)
    {
        List<string> strings = [];
        SpanReader<char> reader = new(span);
        while (reader.TryReadTo(delimiter, out var next))
        {
            if (includeEmptyStrings || !next.IsEmpty)
            {
                strings.Add(next.ToString());
            }
        }

        return strings;
    }

#if NETFRAMEWORK || NET6_0
    /// <summary>
    ///  Searches for the first index of any value other than the specified <paramref name="value"/>.
    /// </summary>
    /// <typeparam name="T">The type of the span and values.</typeparam>
    /// <param name="span">The span to search.</param>
    /// <param name="value">A value to avoid.</param>
    /// <remarks>
    ///  .NET Framework extension to match .NET functionality.
    /// </remarks>
    /// <returns>
    ///  The index in the span of the first occurrence of any value other than <paramref name="value"/>.
    ///  If all of the values are <paramref name="value"/>, returns -1.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfAnyExcept<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (!span[i].Equals(value))
            {
                return i;
            }
        }

        return -1;
    }
#endif
}
