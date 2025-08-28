// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Span extensions for common operations.
/// </summary>
public static partial class ReadOnlySpanExtensions
{
    /// <summary>
    ///  Counts all occurrences of <paramref name="targetValue"/> in the span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int Count(this ReadOnlySpan<char> span, char targetValue)
    {
        int count = 0;

        fixed (char* p = span)
        {
            char* ptr = p;
            char* end = p + span.Length;

            while (ptr < end)
            {
                if (*ptr == targetValue)
                {
                    count++;
                }

                ptr++;
            }
        }

        return count;
    }
}
