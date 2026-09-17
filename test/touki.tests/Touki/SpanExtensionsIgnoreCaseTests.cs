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
[TestClass]
public class SpanExtensionsIgnoreCaseTests
{
    // -- EqualsOrdinalIgnoreCase ------------------------------------------------------

    [TestMethod]
    public void EqualsOrdinalIgnoreCase_BothEmpty_ReturnsTrue() =>
        "".AsSpan().EqualsOrdinalIgnoreCase("".AsSpan()).Should().BeTrue();

    [TestMethod]
    public void EqualsOrdinalIgnoreCase_LengthMismatch_ReturnsFalse()
    {
        "abc".AsSpan().EqualsOrdinalIgnoreCase("ab".AsSpan()).Should().BeFalse();
        "ab".AsSpan().EqualsOrdinalIgnoreCase("abc".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    // Below the BCL crossover (16): scalar fast-path.
    [DataRow("a", "A", true)]
    [DataRow("abc", "ABC", true)]
    [DataRow("Hello!", "hello!", true)]
    [DataRow("abc", "abd", false)]
    // Straddling the BCL crossover (>= 16): BCL dispatch.
    [DataRow("0123456789abcdef", "0123456789ABCDEF", true)]
    [DataRow("0123456789abcdefg", "0123456789ABCDEFG", true)]
    [DataRow("0123456789abcdef", "0123456789ABCDEX", false)]
    [DataRow("the-quick-brown-fox-jumps", "THE-QUICK-BROWN-FOX-JUMPS", true)]
    public void EqualsOrdinalIgnoreCase_AcrossLengths_MatchesString(string left, string right, bool expected) =>
        left.AsSpan().EqualsOrdinalIgnoreCase(right.AsSpan()).Should().Be(expected);

    [TestMethod]
    // Unicode case-fold pairs: must match string.Equals(OrdinalIgnoreCase).
    [DataRow("caf\u00e9", "CAF\u00c9")]          // café / CAFÉ (Latin-1)
    [DataRow("\u00fcber", "\u00dcBER")]          // über / ÜBER
    [DataRow("\u017d\u0061\u0062", "\u017e\u0061\u0042")] // Žab / žaB
    public void EqualsOrdinalIgnoreCase_UnicodeCaseFold_ReturnsTrue(string left, string right)
    {
        left.AsSpan().EqualsOrdinalIgnoreCase(right.AsSpan()).Should().BeTrue();
        // Cross-check against the BCL contract this method is targeting.
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [TestMethod]
    // Non-letter Unicode characters compare strictly ordinal.
    [DataRow("\u4e2d\u6587", "\u4e2d\u6587", true)]   // CJK same
    [DataRow("\u4e2d\u6587", "\u4e2d\u6588", false)]
    public void EqualsOrdinalIgnoreCase_NonLetterUnicode_OrdinalOnly(string left, string right, bool expected) =>
        left.AsSpan().EqualsOrdinalIgnoreCase(right.AsSpan()).Should().Be(expected);

    // -- StartsWithOrdinalIgnoreCase --------------------------------------------------

    [TestMethod]
    [DataRow("", "", true)]
    [DataRow("abc", "", true)]
    [DataRow("", "a", false)]
    [DataRow("abc", "AB", true)]
    [DataRow("ABCdef", "abc", true)]
    [DataRow("ABCdef", "abcd", true)]
    [DataRow("ABCdef", "xyz", false)]
    // Across BCL crossover.
    [DataRow("the-quick-brown-fox-jumps", "THE-QUICK-BROWN-", true)]
    [DataRow("the-quick-brown-fox-jumps", "THE-QUICK-BROWN-FOX-JUMP", true)]
    [DataRow("the-quick-brown-fox-jumps", "the-quick-broxn-", false)]
    public void StartsWithOrdinalIgnoreCase_Cases(string input, string prefix, bool expected) =>
        input.AsSpan().StartsWithOrdinalIgnoreCase(prefix.AsSpan()).Should().Be(expected);

    [TestMethod]
    public void StartsWithOrdinalIgnoreCase_UnicodeFold_ReturnsTrue() =>
        "caf\u00e9-au-lait".AsSpan().StartsWithOrdinalIgnoreCase("CAF\u00c9".AsSpan()).Should().BeTrue();

    // -- EndsWithOrdinalIgnoreCase ----------------------------------------------------

    [TestMethod]
    [DataRow("", "", true)]
    [DataRow("abc", "", true)]
    [DataRow("", "a", false)]
    [DataRow("abc", "BC", true)]
    [DataRow("ABCdef", "DEF", true)]
    [DataRow("ABCdef", "cdef", true)]
    [DataRow("ABCdef", "xyz", false)]
    // Across BCL crossover.
    [DataRow("the-quick-brown-fox-jumps", "BROWN-FOX-JUMPS", true)]
    [DataRow("the-quick-brown-fox-jumps", "QUICK-BROWN-FOX-JUMPS", true)]
    [DataRow("the-quick-brown-fox-jumps", "brown-fox-jumpz", false)]
    public void EndsWithOrdinalIgnoreCase_Cases(string input, string suffix, bool expected) =>
        input.AsSpan().EndsWithOrdinalIgnoreCase(suffix.AsSpan()).Should().Be(expected);

    [TestMethod]
    public void EndsWithOrdinalIgnoreCase_UnicodeFold_ReturnsTrue() =>
        "served-with-caf\u00e9".AsSpan().EndsWithOrdinalIgnoreCase("CAF\u00c9".AsSpan()).Should().BeTrue();

    // -- CompareOrdinalIgnoreCase -----------------------------------------------------

    [TestMethod]
    [DataRow("", "", 0)]
    [DataRow("a", "", 1)]
    [DataRow("", "a", -1)]
    [DataRow("abc", "ABC", 0)]
    [DataRow("ABC", "abd", -1)]
    [DataRow("abd", "ABC", 1)]
    [DataRow("ab", "abc", -1)]
    [DataRow("abc", "ab", 1)]
    public void CompareOrdinalIgnoreCase_AsciiCases(string left, string right, int expectedSign)
    {
        int actual = SpanExtensions.CompareOrdinalIgnoreCase(left.AsSpan(), right.AsSpan());
        Math.Sign(actual).Should().Be(expectedSign);

        // Cross-check the BCL contract.
        Math.Sign(string.Compare(left, right, StringComparison.OrdinalIgnoreCase)).Should().Be(expectedSign);
    }

    [TestMethod]
    // Inputs spanning the BCL crossover threshold on both sides.
    [DataRow("0123456789abcdef", "0123456789ABCDEF", 0)]
    [DataRow("0123456789abcdef", "0123456789ABCDEG", -1)]
    [DataRow("0123456789abcdefg", "0123456789ABCDEF", 1)]
    public void CompareOrdinalIgnoreCase_AcrossCrossover_MatchesString(string left, string right, int expectedSign)
    {
        Math.Sign(SpanExtensions.CompareOrdinalIgnoreCase(left.AsSpan(), right.AsSpan())).Should().Be(expectedSign);
        Math.Sign(string.Compare(left, right, StringComparison.OrdinalIgnoreCase)).Should().Be(expectedSign);
    }

    [TestMethod]
    // Non-ASCII causes the ASCII fast-path to bail; result must still match string.Compare.
    [DataRow("caf\u00e9", "CAF\u00c9")]      // equal under Unicode IC
    [DataRow("caf\u00e9", "CAF\u00ca")]      // é vs Ê - different under Unicode IC too
    [DataRow("abc\u4e2d", "ABC\u4e2d")]      // CJK trailing, ASCII prefix only differs in case
    public void CompareOrdinalIgnoreCase_UnicodeTail_MatchesString(string left, string right)
    {
        int actual = SpanExtensions.CompareOrdinalIgnoreCase(left.AsSpan(), right.AsSpan());
        int expected = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        Math.Sign(actual).Should().Be(Math.Sign(expected));
    }

    [TestMethod]
    public void EqualsOrdinalIgnoreCase_NullVsEmpty_Equal() =>
        // ReadOnlySpan<char>.Empty equals "".AsSpan() under all comparisons.
        "".AsSpan().EqualsOrdinalIgnoreCase(default).Should().BeTrue();

    // -- EqualsAsciiLetterIgnoreCase / StartsWith / EndsWith (POSIX-style fold) --------

    [TestMethod]
    [DataRow("", "", true)]
    [DataRow("abc", "ABC", true)]
    [DataRow("Hello, World!", "HELLO, WORLD!", true)]
    [DataRow("abc", "abd", false)]
    [DataRow("abc", "ab", false)]
    // Non-ASCII compares ordinal: identical sides match, case-folded non-ASCII do NOT match.
    [DataRow("caf\u00e9", "caf\u00e9", true)]    // café == café (ordinal)
    [DataRow("CAF\u00e9", "caf\u00e9", true)]    // ASCII letters fold, é ordinal
    [DataRow("caf\u00e9", "caf\u00c9", false)]   // é vs É: ordinal differ
    [DataRow("caf\u00e9", "CAF\u00c9", false)]   // ASCII folds but é vs É differ
    [DataRow("a\u4e2db", "A\u4e2dB", true)]      // CJK ordinal-equal
    [DataRow("a\u4e2db", "A\u4e2eB", false)]     // CJK differ
    // Across length boundary - the helper has no BCL crossover (POSIX is always scalar).
    [DataRow("the-quick-brown-fox-jumps", "THE-QUICK-BROWN-FOX-JUMPS", true)]
    [DataRow("the-quick-brown-fox-jumps", "the-quick-broxn-fox-jumps", false)]
    public void EqualsAsciiLetterIgnoreCase_Cases(string left, string right, bool expected) =>
        left.AsSpan().EqualsAsciiLetterIgnoreCase(right.AsSpan()).Should().Be(expected);

    [TestMethod]
    public void EqualsAsciiLetterIgnoreCase_LengthMismatch_ReturnsFalse()
    {
        "abc".AsSpan().EqualsAsciiLetterIgnoreCase("ab".AsSpan()).Should().BeFalse();
        "ab".AsSpan().EqualsAsciiLetterIgnoreCase("abc".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("", "", true)]
    [DataRow("ABCdef", "", true)]
    [DataRow("ABCdef", "abc", true)]
    [DataRow("ABCdef", "abd", false)]
    [DataRow("", "a", false)]
    // Non-ASCII in the prefix region must compare ordinal.
    [DataRow("caf\u00e9-au-lait", "CAF\u00e9", true)]      // ASCII case folds, é matches ordinal
    [DataRow("caf\u00e9-au-lait", "CAF\u00c9", false)]     // é vs É - mismatch
    public void StartsWithAsciiLetterIgnoreCase_Cases(string input, string prefix, bool expected) =>
        input.AsSpan().StartsWithAsciiLetterIgnoreCase(prefix.AsSpan()).Should().Be(expected);

    [TestMethod]
    [DataRow("", "", true)]
    [DataRow("ABCdef", "", true)]
    [DataRow("ABCdef", "DEF", true)]
    [DataRow("ABCdef", "DEX", false)]
    [DataRow("", "a", false)]
    [DataRow("served-with-caf\u00e9", "CAF\u00e9", true)]
    [DataRow("served-with-caf\u00e9", "CAF\u00c9", false)]
    public void EndsWithAsciiLetterIgnoreCase_Cases(string input, string suffix, bool expected) =>
        input.AsSpan().EndsWithAsciiLetterIgnoreCase(suffix.AsSpan()).Should().Be(expected);
}
