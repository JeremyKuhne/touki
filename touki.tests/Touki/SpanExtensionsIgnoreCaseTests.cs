// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Tests for <see cref="SpanExtensions"/>'s <c>OrdinalIgnoreCase</c> entry points
///  (<c>EqualsOrdinalIgnoreCase</c>, <c>StartsWithOrdinalIgnoreCase</c>,
///  <c>EndsWithOrdinalIgnoreCase</c>, <c>CompareOrdinalIgnoreCase</c>). Verifies that the
///  semantics match <c>string.Equals</c>/<c>string.Compare</c> with
///  <see cref="StringComparison.OrdinalIgnoreCase"/> across the BCL crossover threshold
///  and on non-ASCII inputs.
/// </summary>
public class SpanExtensionsIgnoreCaseTests
{
    // -- EqualsOrdinalIgnoreCase ------------------------------------------------------

    [Fact]
    public void EqualsOrdinalIgnoreCase_BothEmpty_ReturnsTrue() =>
        "".AsSpan().EqualsOrdinalIgnoreCase("".AsSpan()).Should().BeTrue();

    [Fact]
    public void EqualsOrdinalIgnoreCase_LengthMismatch_ReturnsFalse()
    {
        "abc".AsSpan().EqualsOrdinalIgnoreCase("ab".AsSpan()).Should().BeFalse();
        "ab".AsSpan().EqualsOrdinalIgnoreCase("abc".AsSpan()).Should().BeFalse();
    }

    [Theory]
    // Below the BCL crossover (16): scalar fast-path.
    [InlineData("a", "A", true)]
    [InlineData("abc", "ABC", true)]
    [InlineData("Hello!", "hello!", true)]
    [InlineData("abc", "abd", false)]
    // Straddling the BCL crossover (>= 16): BCL dispatch.
    [InlineData("0123456789abcdef", "0123456789ABCDEF", true)]
    [InlineData("0123456789abcdefg", "0123456789ABCDEFG", true)]
    [InlineData("0123456789abcdef", "0123456789ABCDEX", false)]
    [InlineData("the-quick-brown-fox-jumps", "THE-QUICK-BROWN-FOX-JUMPS", true)]
    public void EqualsOrdinalIgnoreCase_AcrossLengths_MatchesString(string left, string right, bool expected) =>
        left.AsSpan().EqualsOrdinalIgnoreCase(right.AsSpan()).Should().Be(expected);

    [Theory]
    // Unicode case-fold pairs: must match string.Equals(OrdinalIgnoreCase).
    [InlineData("caf\u00e9", "CAF\u00c9")]          // café / CAFÉ (Latin-1)
    [InlineData("\u00fcber", "\u00dcBER")]          // über / ÜBER
    [InlineData("\u017d\u0061\u0062", "\u017e\u0061\u0042")] // Žab / žaB
    public void EqualsOrdinalIgnoreCase_UnicodeCaseFold_ReturnsTrue(string left, string right)
    {
        left.AsSpan().EqualsOrdinalIgnoreCase(right.AsSpan()).Should().BeTrue();
        // Cross-check against the BCL contract this method is targeting.
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Theory]
    // Non-letter Unicode characters compare strictly ordinal.
    [InlineData("\u4e2d\u6587", "\u4e2d\u6587", true)]   // CJK same
    [InlineData("\u4e2d\u6587", "\u4e2d\u6588", false)]
    public void EqualsOrdinalIgnoreCase_NonLetterUnicode_OrdinalOnly(string left, string right, bool expected) =>
        left.AsSpan().EqualsOrdinalIgnoreCase(right.AsSpan()).Should().Be(expected);

    // -- StartsWithOrdinalIgnoreCase --------------------------------------------------

    [Theory]
    [InlineData("", "", true)]
    [InlineData("abc", "", true)]
    [InlineData("", "a", false)]
    [InlineData("abc", "AB", true)]
    [InlineData("ABCdef", "abc", true)]
    [InlineData("ABCdef", "abcd", true)]
    [InlineData("ABCdef", "xyz", false)]
    // Across BCL crossover.
    [InlineData("the-quick-brown-fox-jumps", "THE-QUICK-BROWN-", true)]
    [InlineData("the-quick-brown-fox-jumps", "THE-QUICK-BROWN-FOX-JUMP", true)]
    [InlineData("the-quick-brown-fox-jumps", "the-quick-broxn-", false)]
    public void StartsWithOrdinalIgnoreCase_Cases(string input, string prefix, bool expected) =>
        input.AsSpan().StartsWithOrdinalIgnoreCase(prefix.AsSpan()).Should().Be(expected);

    [Fact]
    public void StartsWithOrdinalIgnoreCase_UnicodeFold_ReturnsTrue() =>
        "caf\u00e9-au-lait".AsSpan().StartsWithOrdinalIgnoreCase("CAF\u00c9".AsSpan()).Should().BeTrue();

    // -- EndsWithOrdinalIgnoreCase ----------------------------------------------------

    [Theory]
    [InlineData("", "", true)]
    [InlineData("abc", "", true)]
    [InlineData("", "a", false)]
    [InlineData("abc", "BC", true)]
    [InlineData("ABCdef", "DEF", true)]
    [InlineData("ABCdef", "cdef", true)]
    [InlineData("ABCdef", "xyz", false)]
    // Across BCL crossover.
    [InlineData("the-quick-brown-fox-jumps", "BROWN-FOX-JUMPS", true)]
    [InlineData("the-quick-brown-fox-jumps", "QUICK-BROWN-FOX-JUMPS", true)]
    [InlineData("the-quick-brown-fox-jumps", "brown-fox-jumpz", false)]
    public void EndsWithOrdinalIgnoreCase_Cases(string input, string suffix, bool expected) =>
        input.AsSpan().EndsWithOrdinalIgnoreCase(suffix.AsSpan()).Should().Be(expected);

    [Fact]
    public void EndsWithOrdinalIgnoreCase_UnicodeFold_ReturnsTrue() =>
        "served-with-caf\u00e9".AsSpan().EndsWithOrdinalIgnoreCase("CAF\u00c9".AsSpan()).Should().BeTrue();

    // -- CompareOrdinalIgnoreCase -----------------------------------------------------

    [Theory]
    [InlineData("", "", 0)]
    [InlineData("a", "", 1)]
    [InlineData("", "a", -1)]
    [InlineData("abc", "ABC", 0)]
    [InlineData("ABC", "abd", -1)]
    [InlineData("abd", "ABC", 1)]
    [InlineData("ab", "abc", -1)]
    [InlineData("abc", "ab", 1)]
    public void CompareOrdinalIgnoreCase_AsciiCases(string left, string right, int expectedSign)
    {
        int actual = SpanExtensions.CompareOrdinalIgnoreCase(left.AsSpan(), right.AsSpan());
        Math.Sign(actual).Should().Be(expectedSign);

        // Cross-check the BCL contract.
        Math.Sign(string.Compare(left, right, StringComparison.OrdinalIgnoreCase)).Should().Be(expectedSign);
    }

    [Theory]
    // Inputs spanning the BCL crossover threshold on both sides.
    [InlineData("0123456789abcdef", "0123456789ABCDEF", 0)]
    [InlineData("0123456789abcdef", "0123456789ABCDEG", -1)]
    [InlineData("0123456789abcdefg", "0123456789ABCDEF", 1)]
    public void CompareOrdinalIgnoreCase_AcrossCrossover_MatchesString(string left, string right, int expectedSign)
    {
        Math.Sign(SpanExtensions.CompareOrdinalIgnoreCase(left.AsSpan(), right.AsSpan())).Should().Be(expectedSign);
        Math.Sign(string.Compare(left, right, StringComparison.OrdinalIgnoreCase)).Should().Be(expectedSign);
    }

    [Theory]
    // Non-ASCII causes the ASCII fast-path to bail; result must still match string.Compare.
    [InlineData("caf\u00e9", "CAF\u00c9")]      // equal under Unicode IC
    [InlineData("caf\u00e9", "CAF\u00ca")]      // é vs Ê - different under Unicode IC too
    [InlineData("abc\u4e2d", "ABC\u4e2d")]      // CJK trailing, ASCII prefix only differs in case
    public void CompareOrdinalIgnoreCase_UnicodeTail_MatchesString(string left, string right)
    {
        int actual = SpanExtensions.CompareOrdinalIgnoreCase(left.AsSpan(), right.AsSpan());
        int expected = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        Math.Sign(actual).Should().Be(Math.Sign(expected));
    }

    [Fact]
    public void EqualsOrdinalIgnoreCase_NullVsEmpty_Equal() =>
        // ReadOnlySpan<char>.Empty equals "".AsSpan() under all comparisons.
        "".AsSpan().EqualsOrdinalIgnoreCase(default).Should().BeTrue();

    // -- EqualsAsciiLetterIgnoreCase / StartsWith / EndsWith (POSIX-style fold) --------

    [Theory]
    [InlineData("", "", true)]
    [InlineData("abc", "ABC", true)]
    [InlineData("Hello, World!", "HELLO, WORLD!", true)]
    [InlineData("abc", "abd", false)]
    [InlineData("abc", "ab", false)]
    // Non-ASCII compares ordinal: identical sides match, case-folded non-ASCII do NOT match.
    [InlineData("caf\u00e9", "caf\u00e9", true)]    // café == café (ordinal)
    [InlineData("CAF\u00e9", "caf\u00e9", true)]    // ASCII letters fold, é ordinal
    [InlineData("caf\u00e9", "caf\u00c9", false)]   // é vs É: ordinal differ
    [InlineData("caf\u00e9", "CAF\u00c9", false)]   // ASCII folds but é vs É differ
    [InlineData("a\u4e2db", "A\u4e2dB", true)]      // CJK ordinal-equal
    [InlineData("a\u4e2db", "A\u4e2eB", false)]     // CJK differ
    // Across length boundary - the helper has no BCL crossover (POSIX is always scalar).
    [InlineData("the-quick-brown-fox-jumps", "THE-QUICK-BROWN-FOX-JUMPS", true)]
    [InlineData("the-quick-brown-fox-jumps", "the-quick-broxn-fox-jumps", false)]
    public void EqualsAsciiLetterIgnoreCase_Cases(string left, string right, bool expected) =>
        left.AsSpan().EqualsAsciiLetterIgnoreCase(right.AsSpan()).Should().Be(expected);

    [Fact]
    public void EqualsAsciiLetterIgnoreCase_LengthMismatch_ReturnsFalse()
    {
        "abc".AsSpan().EqualsAsciiLetterIgnoreCase("ab".AsSpan()).Should().BeFalse();
        "ab".AsSpan().EqualsAsciiLetterIgnoreCase("abc".AsSpan()).Should().BeFalse();
    }

    [Theory]
    [InlineData("", "", true)]
    [InlineData("ABCdef", "", true)]
    [InlineData("ABCdef", "abc", true)]
    [InlineData("ABCdef", "abd", false)]
    [InlineData("", "a", false)]
    // Non-ASCII in the prefix region must compare ordinal.
    [InlineData("caf\u00e9-au-lait", "CAF\u00e9", true)]      // ASCII case folds, é matches ordinal
    [InlineData("caf\u00e9-au-lait", "CAF\u00c9", false)]     // é vs É - mismatch
    public void StartsWithAsciiLetterIgnoreCase_Cases(string input, string prefix, bool expected) =>
        input.AsSpan().StartsWithAsciiLetterIgnoreCase(prefix.AsSpan()).Should().Be(expected);

    [Theory]
    [InlineData("", "", true)]
    [InlineData("ABCdef", "", true)]
    [InlineData("ABCdef", "DEF", true)]
    [InlineData("ABCdef", "DEX", false)]
    [InlineData("", "a", false)]
    [InlineData("served-with-caf\u00e9", "CAF\u00e9", true)]
    [InlineData("served-with-caf\u00e9", "CAF\u00c9", false)]
    public void EndsWithAsciiLetterIgnoreCase_Cases(string input, string suffix, bool expected) =>
        input.AsSpan().EndsWithAsciiLetterIgnoreCase(suffix.AsSpan()).Should().Be(expected);
}
