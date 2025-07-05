// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

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
}
