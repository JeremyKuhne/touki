// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Span extensions for common operations.
/// </summary>
public static partial class SpanExtensions
{
    extension(ReadOnlySpan<char> span)
    {
        /// <summary>
        ///  Slice the given span at null, if present.
        /// </summary>
        public ReadOnlySpan<char> SliceAtNull()
        {
            int index = span.IndexOf('\0');
            return index == -1 ? span : span[..index];
        }
    }

    extension(Span<char> span)
    {
        /// <summary>
        ///  Slice the given span at null, if present.
        /// </summary>
        public Span<char> SliceAtNull()
        {
            int index = span.IndexOf((char)0);
            return index == -1 ? span : span[..index];
        }
    }

    extension(ReadOnlySpan<char> span1)
    {
        /// <summary>
        ///  Returns the exact comparison of two spans as if they were strings, including embedded nulls.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Use this method when you want the exact same result you would get from ordinally comparing two strings
        ///   that have the same content.
        ///  </para>
        /// </remarks>
        public int CompareOrdinalAsString(ReadOnlySpan<char> span2)
        {
            int sharedLength = Math.Min(span1.Length, span2.Length);

            int result = span1[..sharedLength].SequenceCompareTo(span2[..sharedLength]);

            if (result != 0 || span1.Length == span2.Length)
            {
                // If the spans are equal and the same length, or we found a mismatch, return the result.
                return result;
            }

            // The shared prefix matched but the lengths differ. Ordinal string comparison reports one of two
            // magnitudes for this case, and the boundary changed in .NET 11:
            //
            //  - .NET Framework and .NET 10 (and earlier) switch on the parity of the shared length: an even,
            //    non-zero shared length yields the length difference, otherwise the next character wins.
            //  - .NET 11 reports the length difference once at least one 32-bit chunk (two chars) of the shared
            //    prefix has been compared; for a shorter shared prefix (zero or one char) the next character wins.
            //
            // Only the sign is contractually meaningful, but callers pin the exact value against string.Compare,
            // so mirror the behavior of the running runtime.
#if NET11_0_OR_GREATER
            bool returnLengthDifference = sharedLength >= 2;
#else
            bool returnLengthDifference = sharedLength != 0 && sharedLength % 2 == 0;
#endif

            // When returning the next character, invert it if it comes from the second span (effectively
            // comparing the shorter span to a trailing "null").
            return returnLengthDifference
                ? span1.Length - span2.Length
                : span1.Length > span2.Length ? span1[sharedLength] : -span2[sharedLength];
        }
    }
}
