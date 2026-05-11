// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki;

namespace System;

public static partial class SpanExtensions
{
    extension(ReadOnlySpan<char> source)
    {
        /// <summary>
        ///  Parses <paramref name="source"/> for the specified <paramref name="separator"/>, populating
        ///  <paramref name="destination"/> with <see cref="Range"/> instances representing the regions
        ///  between the separators.
        /// </summary>
        /// <returns>The number of ranges written to <paramref name="destination"/>.</returns>
        /// <remarks>
        ///  <para>
        ///   If <paramref name="destination"/> is too small to contain all the ranges, the last range
        ///   covers the remainder of the source (matching the behavior of <see cref="string.Split(char[], int, StringSplitOptions)"/>).
        ///  </para>
        /// </remarks>
        public int Split(Span<Range> destination, char separator, StringSplitOptions options = StringSplitOptions.None)
        {
            CheckStringSplitOptions(options);
            return SplitCore(source, destination, separator, default, isAny: false, options);
        }

        /// <inheritdoc cref="Split(ReadOnlySpan{char}, Span{Range}, char, StringSplitOptions)"/>
        public int Split(Span<Range> destination, ReadOnlySpan<char> separator, StringSplitOptions options = StringSplitOptions.None)
        {
            CheckStringSplitOptions(options);

            // Empty separator means: split with no separator. The whole source is a single segment
            // (after trim/remove options).
            if (separator.Length == 0)
            {
                return WriteWholeSource(source, destination, options);
            }

            return SplitCore(source, destination, default, separator, isAny: false, options);
        }

        /// <summary>
        ///  Parses <paramref name="source"/> for any of the specified <paramref name="separators"/>,
        ///  populating <paramref name="destination"/> with <see cref="Range"/> instances representing the
        ///  regions between the separators.
        /// </summary>
        public int SplitAny(Span<Range> destination, ReadOnlySpan<char> separators, StringSplitOptions options = StringSplitOptions.None)
        {
            CheckStringSplitOptions(options);

            // BCL behavior: an empty separators set falls back to whitespace splitting.
            if (separators.Length == 0)
            {
                return SplitOnAnyWhiteSpace(source, destination, options);
            }

            return SplitCore(source, destination, default, separators, isAny: true, options);
        }

        /// <summary>
        ///  Parses <paramref name="source"/> for any of the specified string <paramref name="separators"/>,
        ///  populating <paramref name="destination"/> with <see cref="Range"/> instances.
        /// </summary>
        public int SplitAny(Span<Range> destination, ReadOnlySpan<string> separators, StringSplitOptions options = StringSplitOptions.None)
        {
            CheckStringSplitOptions(options);

            if (destination.IsEmpty)
            {
                return 0;
            }

            // Empty / all-empty separators: BCL returns the whole source as one range.
            bool anyNonEmpty = false;
            for (int i = 0; i < separators.Length; i++)
            {
                if (!string.IsNullOrEmpty(separators[i]))
                {
                    anyNonEmpty = true;
                    break;
                }
            }

            if (!anyNonEmpty)
            {
                return WriteWholeSource(source, destination, options);
            }

            int count = 0;
            int start = 0;
            int pos = 0;
            int sourceLength = source.Length;

            while (pos < sourceLength)
            {
                int matchedLength = 0;
                int matchPos = -1;

                // Search for the earliest position of any separator.
                for (int i = pos; i < sourceLength; i++)
                {
                    for (int s = 0; s < separators.Length; s++)
                    {
                        string? sep = separators[s];
                        if (string.IsNullOrEmpty(sep))
                        {
                            continue;
                        }

                        if (i + sep!.Length <= sourceLength
                            && source.Slice(i, sep.Length).SequenceEqual(sep.AsSpan()))
                        {
                            matchPos = i;
                            matchedLength = sep.Length;
                            break;
                        }
                    }

                    if (matchPos >= 0)
                    {
                        break;
                    }
                }

                if (matchPos < 0)
                {
                    break;
                }

                if (count == destination.Length - 1)
                {
                    // Last slot: stuff the remainder of the source as one range.
                    break;
                }

                if (TryWriteSegment(source, start, matchPos, options, destination, ref count))
                {
                    if (count == destination.Length)
                    {
                        return count;
                    }
                }

                start = matchPos + matchedLength;
                pos = start;
            }

            // Final segment.
            if (count < destination.Length)
            {
                TryWriteSegment(source, start, sourceLength, options, destination, ref count);
            }

            return count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckStringSplitOptions(StringSplitOptions options)
    {
        // System.Memory on net481 may or may not expose StringSplitOptions.TrimEntries (value 2)
        // depending on the package version. Accept it either way; reject any other bits.
        if (((int)options & ~3) != 0)
        {
            throw new ArgumentException("Invalid StringSplitOptions value.", nameof(options));
        }
    }

    private static int SplitCore(
        ReadOnlySpan<char> source,
        Span<Range> destination,
        char singleSeparator,
        ReadOnlySpan<char> separators,
        bool isAny,
        StringSplitOptions options)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        int count = 0;
        int start = 0;
        ReadOnlySpan<char> remaining = source;

        while (true)
        {
            int separatorIndex;
            int separatorLength;

            if (isAny)
            {
                separatorIndex = remaining.IndexOfAny(separators);
                separatorLength = 1;
            }
            else if (!separators.IsEmpty)
            {
                separatorIndex = remaining.IndexOf(separators);
                separatorLength = separators.Length;
            }
            else
            {
                separatorIndex = remaining.IndexOf(singleSeparator);
                separatorLength = 1;
            }

            if (separatorIndex < 0)
            {
                break;
            }

            // If we're about to write the second-to-last range, stuff the rest of the source there.
            if (count == destination.Length - 1)
            {
                break;
            }

            int absStart = start;
            int absEnd = start + separatorIndex;

            if (TryWriteSegment(source, absStart, absEnd, options, destination, ref count))
            {
                if (count == destination.Length)
                {
                    return count;
                }
            }

            start = absEnd + separatorLength;
            remaining = source[start..];
        }

        // Final segment.
        if (count < destination.Length)
        {
            TryWriteSegment(source, start, source.Length, options, destination, ref count);
        }

        return count;
    }

    private static int SplitOnAnyWhiteSpace(ReadOnlySpan<char> source, Span<Range> destination, StringSplitOptions options)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        int count = 0;
        int start = 0;

        for (int i = 0; i < source.Length; i++)
        {
            if (!char.IsWhiteSpace(source[i]))
            {
                continue;
            }

            if (count == destination.Length - 1)
            {
                break;
            }

            if (TryWriteSegment(source, start, i, options, destination, ref count))
            {
                if (count == destination.Length)
                {
                    return count;
                }
            }

            start = i + 1;
        }

        if (count < destination.Length)
        {
            TryWriteSegment(source, start, source.Length, options, destination, ref count);
        }

        return count;
    }

    private static int WriteWholeSource(ReadOnlySpan<char> source, Span<Range> destination, StringSplitOptions options)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        int count = 0;
        TryWriteSegment(source, 0, source.Length, options, destination, ref count);
        return count;
    }

    /// <summary>
    ///  Writes a segment to <paramref name="destination"/>, applying <see cref="StringSplitOptions"/>.
    /// </summary>
    private static bool TryWriteSegment(
        ReadOnlySpan<char> source,
        int startInclusive,
        int endExclusive,
        StringSplitOptions options,
        Span<Range> destination,
        ref int count)
    {
        int trimmedStart = startInclusive;
        int trimmedEnd = endExclusive;

        // System.Memory on net481 may expose StringSplitOptions.TrimEntries (value 2). Honor it via
        // the integer form so we don't depend on the symbol being present at compile time.
        if (((int)options & 2) != 0)
        {
            while (trimmedStart < trimmedEnd && char.IsWhiteSpace(source[trimmedStart]))
            {
                trimmedStart++;
            }

            while (trimmedEnd > trimmedStart && char.IsWhiteSpace(source[trimmedEnd - 1]))
            {
                trimmedEnd--;
            }
        }

        if (options.AreAnyFlagsSet(StringSplitOptions.RemoveEmptyEntries) && trimmedStart == trimmedEnd)
        {
            return false;
        }

        destination[count++] = new Range(trimmedStart, trimmedEnd);
        return true;
    }
}
