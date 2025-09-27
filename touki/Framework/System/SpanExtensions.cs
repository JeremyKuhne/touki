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
    ///  Replaces all occurrences of <paramref name="oldValue"/> with <paramref name="newValue"/>.
    /// </summary>
    /// <param name="span">The span in which the elements should be replaced.</param>
    /// <param name="oldValue">The value to be replaced with <paramref name="newValue"/>.</param>
    /// <param name="newValue">The value to replace all occurrences of <paramref name="oldValue"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Replace(this Span<char> span, char oldValue, char newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        fixed (char* p = span)
        {
            char* ptr = p;
            char* end = p + span.Length;

            while (ptr < end)
            {
                if (*ptr == oldValue)
                {
                    *ptr = newValue;
                }

                ptr++;
            }
        }
    }

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
}
