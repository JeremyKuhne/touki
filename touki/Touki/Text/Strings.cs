// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Touki;

/// <summary>
///  Supports string operations and utilities.
/// </summary>
public static partial class Strings
{
    private static Random? s_defaultRandom
#if NET
        = Random.Shared
#endif
        ;

    /// <summary>
    ///  Allocates a string of the specified length filled with null characters.
    /// </summary>
    internal static string FastAllocateString(int length) =>
        // This calls FastAllocateString in the runtime, with extra checks.
        new string('\0', length);

    /// <summary>
    ///  Returns the exact comparison of two spans as if they were strings, including embedded nulls.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Use this method when you want the exact same result you would get from ordinally comparing two strings
    ///   that have the same content.
    ///  </para>
    /// </remarks>
    public static int CompareOrdinalAsString(ReadOnlySpan<char> span1, ReadOnlySpan<char> span2)
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

    /// <summary>
    ///  Generates <paramref name="count"/> random UTF-16 strings.
    /// </summary>
    /// <param name="minLength">
    ///  Minimum length of the generated strings in UTF-16 code units, inclusive.
    /// </param>
    /// <param name="maxLength">
    ///  Maximum length of the generated strings in UTF-16 code units, inclusive.
    /// </param>
    /// <remarks>
    ///  <para>
    ///   Length bounds are inclusive and measure UTF-16 code units (i.e., C# char count).
    ///  </para>
    ///  <para>
    ///   Control characters and U+0000 are excluded. Surrogate pairs are emitted only if enabled.
    ///  </para>
    /// </remarks>
    public static List<string> GenerateRandomStrings(
        int count,
        int minLength = 4,
        int maxLength = 40,
        bool allowSurrogatePairs = false,
        Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0, nameof(count));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minLength, 0, nameof(minLength));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, minLength, nameof(maxLength));

        random ??= s_defaultRandom ??= new Random();
        List<string> result = new(count);
        using BufferScope<char> buffer = new(stackalloc char[128], maxLength);

        for (int i = 0; i < count; i++)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            int length = random.Next(minLength, maxLength + 1);
            int position = 0;

            while (position < length)
            {
                // Optionally emit a surrogate pair (~25% chance) if space allows.
                if (!allowSurrogatePairs || (length - position) < 2 || random.Next(4) != 0)
                {
                    buffer[position++] = Chars.GetRandomSimpleChar(random);
                    continue;
                }

                int codePoint;

                do
                {
                    // 0..0xFFFFF, skip ?FFFE/?FFFF in every plane.
                    codePoint = 0x10000 + random.Next(0x110000 - 0x10000);
                } while ((codePoint & 0xFFFE) == 0xFFFE);

                codePoint -= 0x10000;

                // High surrogate
                buffer[position++] = (char)(0xD800 + (codePoint >> 10));

                // Low surrogate
                buffer[position++] = (char)(0xDC00 + (codePoint & 0x3FF));
            }

            string s = buffer[..length].ToString();
            result.Add(s);
        }

        return result;
#pragma warning restore CA5394 // Do not use insecure randomness
    }
}
