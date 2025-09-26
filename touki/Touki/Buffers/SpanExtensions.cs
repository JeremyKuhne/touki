// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

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

    /// <summary>
    ///  Returns the exact comparison of two spans as if they were strings, including embedded nulls.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Use this method when you want the exact same result you would get from ordinally comparing two strings
    ///   that have the same content.
    ///  </para>
    /// </remarks>
    public static int CompareOrdinalAsString(this ReadOnlySpan<char> span1, ReadOnlySpan<char> span2)
    {
        int sharedLength = Math.Min(span1.Length, span2.Length);

        int result = span1[..sharedLength].SequenceCompareTo(span2[..sharedLength]);

        if (result != 0 || span1.Length == span2.Length)
        {
            // If the spans are equal and the same length, or we found a mismatch, return the result.
            return result;
        }

        // If we've fully matched the shared length, follow the logic string would do. If there is no shared length
        // or the shared length is odd, we return the next character in the longer span, inverted if it is from
        // the second span (effectively comparing to "null").
        return sharedLength != 0 && sharedLength % 2 == 0
            ? span1.Length - span2.Length
            : span1.Length > span2.Length ? span1[sharedLength] : -span2[sharedLength];
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
