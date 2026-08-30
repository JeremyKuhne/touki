// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// ASCII fast-path body derived from .NET BCL's String.Comparison.cs.
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki;

/// <summary>
///  Shared raw-pointer ASCII fast-path for <c>OrdinalIgnoreCase</c> comparisons. Internal so
///  <see cref="StringSegment"/> can call it directly with the backing
///  <see langword="string"/> already pinned.
/// </summary>
internal static class OrdinalIgnoreCaseHelpers
{
    /// <summary>
    ///  Walks the shared prefix folding ASCII letter pairs (<c>a..z</c>/<c>A..Z</c>) and
    ///  comparing.
    /// </summary>
    /// <param name="a">Pointer to the first character of the left buffer.</param>
    /// <param name="lengthA">Length of the left buffer in characters.</param>
    /// <param name="b">Pointer to the first character of the right buffer.</param>
    /// <param name="lengthB">Length of the right buffer in characters.</param>
    /// <param name="scanned">
    ///  On non-ASCII bail, the count of characters that compared ASCII-equal before the
    ///  non-ASCII character was hit. Undefined when the method returns
    ///  <see langword="true"/>.
    /// </param>
    /// <param name="result">
    ///  When the method returns <see langword="true"/>: the final compare result (zero for
    ///  full ASCII match, otherwise the ASCII-fold ordinal difference). Undefined when the
    ///  method returns <see langword="false"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the comparison completed entirely on the ASCII fast-path
    ///  (full match or ASCII mismatch found); <see langword="false"/> if a non-ASCII
    ///  character was hit and the caller must finish via Unicode fallback.
    /// </returns>
    public static unsafe bool CompareAscii(char* a, int lengthA, char* b, int lengthB, out int scanned, out int result)
    {
        int shared = Math.Min(lengthA, lengthB);

        for (int i = 0; i < shared; i++)
        {
            int charA = a[i];
            int charB = b[i];

            if ((charA | charB) > 0x7F)
            {
                scanned = i;
                result = 0;
                return false;
            }

            if ((uint)(charA - 'a') <= 'z' - 'a')
            {
                charA -= 0x20;
            }

            if ((uint)(charB - 'a') <= 'z' - 'a')
            {
                charB -= 0x20;
            }

            if (charA != charB)
            {
                scanned = i;
                result = charA - charB;
                return true;
            }
        }

        scanned = shared;
        result = lengthA - lengthB;
        return true;
    }

    /// <summary>
    ///  Tests two equal-length spans for "ASCII-letter case fold, ordinal everything else"
    ///  equality. Differs from <see cref="CompareAscii"/> by not bailing on non-ASCII
    ///  characters - they simply compare ordinal. Matches POSIX
    ///  <c>fnmatch(FNM_CASEFOLD)</c> / bash <c>nocaseglob</c> / git <c>core.ignoreCase</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Caller must have verified <c>a.Length == b.Length</c>. The loop uses the
    ///   <c>Strategy B</c> hoisted-<c>ref char</c> pattern (see
    ///   <c>docs/framework-span-performance.md</c>): on net472/net481 each per-character
    ///   load becomes a single indexed <c>movzx</c> instead of the slow-span pointer dance.
    ///  </para>
    /// </remarks>
    /// <param name="a">The first equal-length span to compare.</param>
    /// <param name="b">The second equal-length span to compare.</param>
    /// <returns>
    ///  <see langword="true"/> if the spans are equal under ASCII-letter case folding; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public static bool EqualsAsciiLetterFold(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        Debug.Assert(a.Length == b.Length, "EqualsAsciiLetterFold requires equal-length spans.");

        int length = a.Length;
        ref char pa = ref MemoryMarshal.GetReference(a);
        ref char pb = ref MemoryMarshal.GetReference(b);

        for (int i = 0; i < length; i++)
        {
            int charA = Unsafe.Add(ref pa, i);
            int charB = Unsafe.Add(ref pb, i);

            if (charA == charB)
            {
                continue;
            }

            // ASCII-letter fold: only the 26 letter pairs differ by exactly 0x20. Everything
            // else compares ordinal, so any other inequality is an outright mismatch.
            int foldA = (uint)(charA - 'a') <= 'z' - 'a' ? charA - 0x20 : charA;
            int foldB = (uint)(charB - 'a') <= 'z' - 'a' ? charB - 0x20 : charB;
            if (foldA != foldB)
            {
                return false;
            }
        }

        return true;
    }
}
