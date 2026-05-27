// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// ASCII fast-path body derived from .NET BCL's String.Comparison.cs.
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki;

/// <summary>
///  Ordinal-ignore-case span extensions matching the full Unicode semantics of
///  <c>string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)</c> and
///  <c>string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase)</c> with an ASCII
///  fast-path for short inputs and a hand-off to the BCL's vectorized
///  <see cref="StringComparison.OrdinalIgnoreCase"/> path for inputs at or above
///  <c>16</c> characters. Also provides ASCII-letter-only case folding for callers
///  emulating POSIX <c>fnmatch(FNM_CASEFOLD)</c>, bash <c>nocaseglob</c>, and
///  git <c>core.ignoreCase</c> behavior.
/// </summary>
/// <remarks>
///  <para>
///   <b>Which ignore-case primitive should I use?</b>
///  </para>
///  <para>
///   <list type="bullet">
///    <item>
///     <description>
///      <see cref="System.Text.Ascii.EqualsIgnoreCase(ReadOnlySpan{char}, ReadOnlySpan{char})"/>
///      &#8211; <b>strict ASCII</b>; non-ASCII characters force a <see langword="false"/>
///      return even if both sides are byte-identical. Use this for ASCII protocols (HTTP
///      headers, MIME, identifiers) where non-ASCII data is itself an error condition.
///     </description>
///    </item>
///    <item>
///     <description>
///      <c>EqualsAsciiLetterIgnoreCase</c> / <c>StartsWithAsciiLetterIgnoreCase</c> /
///      <c>EndsWithAsciiLetterIgnoreCase</c> &#8211; <b>ASCII-letter case fold, ordinal
///      everything else</b>. The 26 ASCII letter pairs (<c>A..Z</c>/<c>a..z</c>) compare
///      equal; non-ASCII characters compare ordinal (so <c>caf\u00e9</c> equals
///      <c>caf\u00e9</c> but not <c>caf\u00c9</c>). This matches the documented behavior
///      of POSIX <c>fnmatch(FNM_CASEFOLD)</c>, bash <c>nocaseglob</c>/<c>nocasematch</c>,
///      and git <c>core.ignoreCase</c>.
///     </description>
///    </item>
///    <item>
///     <description>
///      <c>EqualsOrdinalIgnoreCase</c> / <c>CompareOrdinalIgnoreCase</c> /
///      <c>StartsWithOrdinalIgnoreCase</c> / <c>EndsWithOrdinalIgnoreCase</c> &#8211;
///      <b>full Unicode ordinal ignore-case</b>, matching <c>string</c>'s behavior under
///      <see cref="StringComparison.OrdinalIgnoreCase"/>. Use these for MSBuild-style
///      globs, .NET <c>Microsoft.Extensions.FileSystemGlobbing</c> parity, and any
///      general-purpose drop-in for <see cref="string.Equals(string?, string?, StringComparison)"/>
///      / <see cref="string.Compare(string?, string?, StringComparison)"/>.
///     </description>
///    </item>
///   </list>
///  </para>
///  <para>
///   For Unicode-IC inputs shorter than <c>16</c> characters the implementation walks an
///   inline ASCII fold loop (<see cref="OrdinalIgnoreCaseHelpers.CompareAscii"/>) that
///   sidesteps the <c>Vector128&lt;UInt16&gt;</c> codegen valley in the BCL's
///   <c>OrdinalIgnoreCase</c> path on net10 (see <c>docs/bcl-ignorecase-valley-rca.md</c>).
///   For longer inputs control passes to the BCL's vectorized
///   <c>MemoryExtensions.Equals</c> / <c>StartsWith</c> / <c>EndsWith</c> / <c>CompareTo</c>
///   under <see cref="StringComparison.OrdinalIgnoreCase"/>.
///  </para>
///  <para>
///   Span callers pin via <c>fixed</c> in the entry points below; <see cref="StringSegment"/>
///   pins the backing <see langword="string"/> directly and calls
///   <see cref="OrdinalIgnoreCaseHelpers.CompareAscii"/> with raw pointers. Sharing the
///   raw-pointer core keeps both call sites at a single non-inlined frame on net472/net481.
///   See <c>docs/framework-span-performance.md</c>.
///  </para>
/// </remarks>
public static partial class SpanExtensions
{
    /// <summary>
    ///  Length at which the BCL's vectorized <see cref="StringComparison.OrdinalIgnoreCase"/>
    ///  path overtakes the scalar ASCII fold loop. Matches <c>Vector128&lt;short&gt;.Count * 2</c>,
    ///  the BCL's own dispatch threshold. Empirically validated on i9-14900K with .NET 10
    ///  RyuJIT and .NET Framework 4.8.1 RyuJIT.
    /// </summary>
    internal const int BclCrossoverLength = 16;

    extension(ReadOnlySpan<char> span1)
    {
        /// <summary>
        ///  Tests whether two character spans compare equal under
        ///  <see cref="StringComparison.OrdinalIgnoreCase"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EqualsOrdinalIgnoreCase(ReadOnlySpan<char> span2)
        {
            if (span1.Length != span2.Length)
            {
                return false;
            }

            if (span1.Length >= BclCrossoverLength)
            {
                return span1.Equals(span2, StringComparison.OrdinalIgnoreCase);
            }

            return span1.Length == 0
                || CompareOrdinalIgnoreCaseAsciiFold(span1, span2) == 0;
        }

        /// <summary>
        ///  Tests whether <paramref name="span1"/> begins with <paramref name="prefix"/> under
        ///  <see cref="StringComparison.OrdinalIgnoreCase"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool StartsWithOrdinalIgnoreCase(ReadOnlySpan<char> prefix)
        {
            if (prefix.Length > span1.Length)
            {
                return false;
            }

            if (prefix.Length >= BclCrossoverLength)
            {
                return span1.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return prefix.Length == 0
                || CompareOrdinalIgnoreCaseAsciiFold(span1[..prefix.Length], prefix) == 0;
        }

        /// <summary>
        ///  Tests whether <paramref name="span1"/> ends with <paramref name="suffix"/> under
        ///  <see cref="StringComparison.OrdinalIgnoreCase"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EndsWithOrdinalIgnoreCase(ReadOnlySpan<char> suffix)
        {
            if (suffix.Length > span1.Length)
            {
                return false;
            }

            if (suffix.Length >= BclCrossoverLength)
            {
                return span1.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            }

            return suffix.Length == 0
                || CompareOrdinalIgnoreCaseAsciiFold(
                    span1[(span1.Length - suffix.Length)..],
                    suffix) == 0;
        }

        /// <summary>
        ///  Tests whether two character spans compare equal under <b>ASCII-letter case folding
        ///  with ordinal compare for everything else</b>. The 26 ASCII letter pairs match
        ///  case-insensitively; every other character (ASCII non-letter, non-ASCII Latin-1,
        ///  CJK, emoji, etc.) compares byte-for-byte. Matches the documented behavior of
        ///  POSIX <c>fnmatch(FNM_CASEFOLD)</c>, bash <c>nocaseglob</c>, and
        ///  git <c>core.ignoreCase</c>.
        /// </summary>
        public bool EqualsAsciiLetterIgnoreCase(ReadOnlySpan<char> span2) =>
            span1.Length == span2.Length
                && OrdinalIgnoreCaseHelpers.EqualsAsciiLetterFold(span1, span2);

        /// <summary>
        ///  Tests whether <paramref name="span1"/> begins with <paramref name="prefix"/> under
        ///  ASCII-letter case folding (see <see cref="EqualsAsciiLetterIgnoreCase"/>).
        /// </summary>
        public bool StartsWithAsciiLetterIgnoreCase(ReadOnlySpan<char> prefix) =>
            prefix.Length <= span1.Length
                && OrdinalIgnoreCaseHelpers.EqualsAsciiLetterFold(span1[..prefix.Length], prefix);

        /// <summary>
        ///  Tests whether <paramref name="span1"/> ends with <paramref name="suffix"/> under
        ///  ASCII-letter case folding (see <see cref="EqualsAsciiLetterIgnoreCase"/>).
        /// </summary>
        public bool EndsWithAsciiLetterIgnoreCase(ReadOnlySpan<char> suffix) =>
            suffix.Length <= span1.Length
                && OrdinalIgnoreCaseHelpers.EqualsAsciiLetterFold(
                    span1[(span1.Length - suffix.Length)..],
                    suffix);
    }

    /// <summary>
    ///  Compares two character spans under <see cref="StringComparison.OrdinalIgnoreCase"/>
    ///  returning a negative number, zero, or a positive number reflecting the relative order.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Result semantics match <see cref="string.Compare(string?, string?, StringComparison)"/>
    ///   with <see cref="StringComparison.OrdinalIgnoreCase"/>. For short inputs the ASCII
    ///   fast-path uses uppercase ordinal compare; any non-ASCII tail and inputs at or above
    ///   <c>16</c> characters are delegated to
    ///   <c>MemoryExtensions.CompareTo(span, span, StringComparison.OrdinalIgnoreCase)</c>.
    ///  </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CompareOrdinalIgnoreCase(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        // For inputs that are at least the BCL crossover on both sides, the vectorized path
        // wins. Otherwise the scalar ASCII fold below sidesteps the Vector128 valley.
        if (a.Length >= BclCrossoverLength && b.Length >= BclCrossoverLength)
        {
            return a.CompareTo(b, StringComparison.OrdinalIgnoreCase);
        }

        return CompareOrdinalIgnoreCaseAsciiFold(a, b);
    }

    /// <summary>
    ///  Scalar ASCII fast-path with a BCL fallback for the non-ASCII tail. Used by both the
    ///  bool extension entry points and <see cref="CompareOrdinalIgnoreCase"/> for inputs
    ///  shorter than <see cref="BclCrossoverLength"/>.
    /// </summary>
    private static unsafe int CompareOrdinalIgnoreCaseAsciiFold(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        int scanned;

        fixed (char* pa = a)
        fixed (char* pb = b)
        {
            if (OrdinalIgnoreCaseHelpers.CompareAscii(pa, a.Length, pb, b.Length, out scanned, out int result))
            {
                return result;
            }
        }

        // ASCII fast-path stopped at the first non-ASCII character. Delegate the remainder
        // to the BCL so we match the full Unicode ordinal semantics of string.Compare.
        return a[scanned..].CompareTo(b[scanned..], StringComparison.OrdinalIgnoreCase);
    }
}

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
    ///  characters &#8211; they simply compare ordinal. Matches POSIX
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
