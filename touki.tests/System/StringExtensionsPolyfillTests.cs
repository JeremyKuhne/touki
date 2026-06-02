// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

public class StringExtensionsPolyfillTests
{
    // ---- Contains(char) / Contains(char, StringComparison) / Contains(string, StringComparison) ----

    [Test]
    [Arguments("Hello", 'H', true)]
    [Arguments("Hello", 'l', true)]
    [Arguments("Hello", 'z', false)]
    [Arguments("", 'a', false)]
    public void Contains_Char_ReturnsExpected(string source, char value, bool expected)
    {
        source.Contains(value).Should().Be(expected);
    }

    [Test]
    public void Contains_Char_OrdinalIgnoreCase_AcceptsAnyCase()
    {
        "Hello".Contains('h', StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Test]
    public void Contains_Char_Ordinal_RejectsDifferentCase()
    {
        "Hello".Contains('h', StringComparison.Ordinal).Should().BeFalse();
    }

    [Test]
    public void Contains_String_OrdinalIgnoreCase_AcceptsAnyCase()
    {
        "Hello World".Contains("HELLO", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Test]
    public void Contains_String_NullValue_Throws()
    {
        Action action = () => "x".Contains(null!, StringComparison.Ordinal);
        action.Should().Throw<ArgumentNullException>();
    }

    // ---- StartsWith / EndsWith char ----

    [Test]
    [Arguments("Hello", 'H', true)]
    [Arguments("Hello", 'h', false)]
    [Arguments("Hello", 'e', false)]
    [Arguments("", 'a', false)]
    public void StartsWith_Char_ReturnsExpected(string source, char value, bool expected)
    {
        source.StartsWith(value).Should().Be(expected);
    }

    [Test]
    [Arguments("Hello", 'o', true)]
    [Arguments("Hello", 'O', false)]
    [Arguments("Hello", 'l', false)]
    [Arguments("", 'a', false)]
    public void EndsWith_Char_ReturnsExpected(string source, char value, bool expected)
    {
        source.EndsWith(value).Should().Be(expected);
    }

    // ---- TryCopyTo ----

    [Test]
    public void TryCopyTo_DestinationLargeEnough_CopiesAndReturnsTrue()
    {
        Span<char> dest = new char[10];
        bool ok = "abc".TryCopyTo(dest);
        ok.Should().BeTrue();
        dest[0].Should().Be('a');
        dest[1].Should().Be('b');
        dest[2].Should().Be('c');
    }

    [Test]
    public void TryCopyTo_DestinationTooSmall_ReturnsFalse()
    {
        Span<char> dest = new char[2];
        bool ok = "abc".TryCopyTo(dest);
        ok.Should().BeFalse();
    }

    [Test]
    public void TryCopyTo_EmptySource_AlwaysSucceeds()
    {
        Span<char> dest = [];
        "".TryCopyTo(dest).Should().BeTrue();
    }

    // ---- GetHashCode(StringComparison) ----

    [Test]
    public void GetHashCode_StringComparison_OrdinalIgnoreCase_SameForCaseVariants()
    {
        int a = "Hello".GetHashCode(StringComparison.OrdinalIgnoreCase);
        int b = "HELLO".GetHashCode(StringComparison.OrdinalIgnoreCase);
        a.Should().Be(b);
    }

    [Test]
    public void GetHashCode_StringComparison_Ordinal_DifferentForCaseVariants()
    {
        int a = "Hello".GetHashCode(StringComparison.Ordinal);
        int b = "HELLO".GetHashCode(StringComparison.Ordinal);
        a.Should().NotBe(b);
    }

    [Test]
    [Arguments(StringComparison.CurrentCulture)]
    [Arguments(StringComparison.CurrentCultureIgnoreCase)]
    [Arguments(StringComparison.InvariantCulture)]
    [Arguments(StringComparison.InvariantCultureIgnoreCase)]
    [Arguments(StringComparison.Ordinal)]
    [Arguments(StringComparison.OrdinalIgnoreCase)]
    public void GetHashCode_StringComparison_AllSupportedComparisons_Returns(StringComparison comparison)
    {
        // Just exercises every branch of ComparerForComparison.
        "Hello".GetHashCode(comparison).Should().Be("Hello".GetHashCode(comparison));
    }

    [Test]
    [Arguments(StringComparison.CurrentCulture)]
    [Arguments(StringComparison.CurrentCultureIgnoreCase)]
    [Arguments(StringComparison.InvariantCulture)]
    [Arguments(StringComparison.InvariantCultureIgnoreCase)]
    [Arguments(StringComparison.Ordinal)]
    [Arguments(StringComparison.OrdinalIgnoreCase)]
    public void Contains_Char_AllSupportedComparisons_FindsMatch(StringComparison comparison)
    {
        // Exercises every branch of ComparisonToCompareInfo.
        "Hello".Contains('e', comparison).Should().BeTrue();
    }

    // ---- Replace(string, string?, StringComparison) ----

    [Test]
    public void Replace_StringComparison_Ordinal_ExactMatch()
    {
        "abcabc".Replace("ab", "X", StringComparison.Ordinal).Should().Be("XcXc");
    }

    [Test]
    public void Replace_StringComparison_OrdinalIgnoreCase_AnyCase()
    {
        "abcAbC".Replace("AB", "X", StringComparison.OrdinalIgnoreCase).Should().Be("XcXC");
    }

    [Test]
    public void Replace_StringComparison_NullNewValue_TreatedAsEmpty()
    {
        "abcabc".Replace("ab", null, StringComparison.OrdinalIgnoreCase).Should().Be("cc");
    }

    [Test]
    public void Replace_StringComparison_NoMatch_ReturnsOriginal()
    {
        "abc".Replace("z", "X", StringComparison.Ordinal).Should().Be("abc");
    }

    [Test]
    public void Replace_StringComparison_NullOldValue_Throws()
    {
        Action action = () => "abc".Replace(null!, "x", StringComparison.Ordinal);
        action.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Replace_StringComparison_EmptyOldValue_Throws()
    {
        Action action = () => "abc".Replace("", "x", StringComparison.Ordinal);
        action.Should().Throw<ArgumentException>();
    }

    // ---- ReplaceLineEndings ----

    [Test]
    [Arguments("a\nb", "a|b")]
    [Arguments("a\rb", "a|b")]
    [Arguments("a\r\nb", "a|b")]
    [Arguments("a\u0085b", "a|b")]
    [Arguments("a\u2028b", "a|b")]
    [Arguments("a\u2029b", "a|b")]
    [Arguments("a\fb", "a|b")]
    [Arguments("no line endings", "no line endings")]
    public void ReplaceLineEndings_Custom_ReplacesAllRecognizedEndings(string input, string expected)
    {
        input.ReplaceLineEndings("|").Should().Be(expected);
    }

    [Test]
    public void ReplaceLineEndings_CRLF_TreatedAsSingleEnding()
    {
        // \r\n should yield ONE replacement, not two.
        "a\r\nb\r\nc".ReplaceLineEndings("|").Should().Be("a|b|c");
    }

    [Test]
    public void ReplaceLineEndings_NoArgument_UsesEnvironmentNewLine()
    {
        "a\nb".ReplaceLineEndings().Should().Be("a" + Environment.NewLine + "b");
    }

    [Test]
    public void ReplaceLineEndings_NullReplacement_Throws()
    {
        Action action = () => "a\nb".ReplaceLineEndings(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    // ---- Split(char, ...) / Split(string, ...) ----

    [Test]
    public void Split_Char_StringSplitOptions_RemoveEmpty()
    {
        "a,,b".Split(',', StringSplitOptions.RemoveEmptyEntries).Should().Equal("a", "b");
    }

    [Test]
    public void Split_Char_StringSplitOptions_None_KeepsEmpty()
    {
        "a,,b".Split(',', StringSplitOptions.None).Should().Equal("a", "", "b");
    }

    [Test]
    public void Split_Char_Count_LimitsSplits()
    {
        "a,b,c,d".Split(',', 2, StringSplitOptions.None).Should().Equal("a", "b,c,d");
    }

    [Test]
    public void Split_String_StringSplitOptions_RemoveEmpty()
    {
        "a::b".Split("::", StringSplitOptions.RemoveEmptyEntries).Should().Equal("a", "b");
    }

    [Test]
    public void Split_String_Count_LimitsSplits()
    {
        "a::b::c::d".Split("::", 2, StringSplitOptions.None).Should().Equal("a", "b::c::d");
    }

    [Test]
    public void Split_String_NullSeparator_ReturnsSingleSourceElement()
    {
        // BCL behavior for Split((string?)null, opts): treats as no separator, returns the source unchanged.
        "a b\tc".Split((string?)null, StringSplitOptions.None).Should().Equal("a b\tc");
    }

    [Test]
    public void Split_Char_EmptySource_ReturnsSingleEmpty()
    {
        "".Split(',', StringSplitOptions.None).Should().Equal("");
    }

    [Test]
    public void Split_Char_NoMatch_ReturnsSingle()
    {
        "abc".Split(',', StringSplitOptions.None).Should().Equal("abc");
    }

    [Test]
    public void Split_Char_CountZero_ReturnsEmpty()
    {
        "a,b,c".Split(',', 0, StringSplitOptions.None).Should().BeEmpty();
    }

    [Test]
    public void Split_Char_CountOne_ReturnsOriginal()
    {
        "a,b,c".Split(',', 1, StringSplitOptions.None).Should().Equal("a,b,c");
    }

    [Test]
    public void Split_Char_NegativeCount_Throws()
    {
        Action action = () => "a,b".Split(',', -1, StringSplitOptions.None);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- Contains negative & boundary ----

    [Test]
    public void Contains_Char_EmptyString_ReturnsFalse()
    {
        "".Contains('a').Should().BeFalse();
    }

    [Test]
    public void Contains_String_EmptyValue_ReturnsTrue()
    {
        // BCL contract: an empty needle is contained anywhere.
        "Hello".Contains("", StringComparison.Ordinal).Should().BeTrue();
    }

    [Test]
    public void Contains_String_InvalidComparison_Throws()
    {
        Action action = () => "Hello".Contains("yz", (StringComparison)42);
        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Contains_Char_InvalidComparison_Throws()
    {
        Action action = () => "x".Contains('y', (StringComparison)42);
        action.Should().Throw<ArgumentException>();
    }

    // ---- GetHashCode negative ----

    [Test]
    public void GetHashCode_StringComparison_Invalid_Throws()
    {
        Action action = () => "x".GetHashCode((StringComparison)42);
        action.Should().Throw<ArgumentException>();
    }

    // ---- Replace negative & boundary ----

    [Test]
    public void Replace_StringComparison_InvalidComparison_Throws()
    {
        Action action = () => "abc".Replace("a", "b", (StringComparison)42);
        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Replace_StringComparison_EmptySource_ReturnsEmpty()
    {
        "".Replace("a", "b", StringComparison.Ordinal).Should().BeEmpty();
    }

    [Test]
    public void Replace_StringComparison_OldValueLongerThanSource_ReturnsOriginal()
    {
        "ab".Replace("abcdef", "x", StringComparison.Ordinal).Should().Be("ab");
    }

    [Test]
    public void Replace_StringComparison_AdjacentMatches_AllReplaced()
    {
        "aaaa".Replace("aa", "X", StringComparison.Ordinal).Should().Be("XX");
    }

    [Test]
    public void Replace_StringComparison_OrdinalNullNewValue_TreatedAsEmpty()
    {
        "abc".Replace("b", null, StringComparison.Ordinal).Should().Be("ac");
    }

    // ---- ReplaceLineEndings negative & boundary ----

    [Test]
    public void ReplaceLineEndings_EmptySource_ReturnsEmpty()
    {
        "".ReplaceLineEndings("|").Should().BeEmpty();
    }

    [Test]
    public void ReplaceLineEndings_OnlyLineEnding_ReturnsReplacement()
    {
        "\n".ReplaceLineEndings("|").Should().Be("|");
    }

    [Test]
    public void ReplaceLineEndings_LeadingAndTrailing_BothReplaced()
    {
        "\nabc\n".ReplaceLineEndings("|").Should().Be("|abc|");
    }

    [Test]
    public void ReplaceLineEndings_VerticalTab_NotTreatedAsLineEnding()
    {
        // \v (U+000B) is NOT a recognized line ending per the BCL spec.
        "a\vb".ReplaceLineEndings("|").Should().Be("a\vb");
    }

    [Test]
    public void ReplaceLineEndings_LoneCR_ReplacedAsSingleEnding()
    {
        "a\rb".ReplaceLineEndings("|").Should().Be("a|b");
    }

    [Test]
    public void ReplaceLineEndings_LoneLF_ReplacedAsSingleEnding()
    {
        "a\nb".ReplaceLineEndings("|").Should().Be("a|b");
    }

    [Test]
    public void ReplaceLineEndings_CRThenChar_NotMisinterpreted()
    {
        // \r followed by non-\n must not consume the next char.
        "a\rXb".ReplaceLineEndings("|").Should().Be("a|Xb");
    }

    [Test]
    public void ReplaceLineEndings_LongInput_ExceedsStackBuffer()
    {
        // Force the ValueStringBuilder beyond the 256-char stack buffer.
        string source = new string('a', 1000) + "\n" + new string('b', 1000);
        string result = source.ReplaceLineEndings("|");
        result.Should().Be(new string('a', 1000) + "|" + new string('b', 1000));
    }

    // ---- TryCopyTo edge ----

    [Test]
    public void TryCopyTo_EmptyDestinationNonEmptySource_ReturnsFalse()
    {
        Span<char> dest = [];
        "abc".TryCopyTo(dest).Should().BeFalse();
    }

    [Test]
    public void TryCopyTo_ExactFit_CopiesAndReturnsTrue()
    {
        Span<char> dest = new char[3];
        bool ok = "abc".TryCopyTo(dest);
        ok.Should().BeTrue();
        dest.ToString().Should().Be("abc");
    }

    // ---- CopyTo (throws on too-short destination) ----

    [Test]
    public void CopyTo_DestinationLargeEnough_Copies()
    {
        Span<char> dest = new char[5];
        "abc".CopyTo(dest);
        dest[..3].ToString().Should().Be("abc");
    }

    [Test]
    public void CopyTo_ExactFit_Copies()
    {
        Span<char> dest = new char[3];
        "abc".CopyTo(dest);
        dest.ToString().Should().Be("abc");
    }

    [Test]
    public void CopyTo_DestinationTooShort_Throws()
    {
        Action act = static () =>
        {
            Span<char> dest = new char[2];
            "abc".CopyTo(dest);
        };
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void CopyTo_EmptySource_Succeeds()
    {
        Span<char> dest = [];
        "".CopyTo(dest);
    }

    // ---- Split(string, string, int, StringSplitOptions) extra branches ----

    [Test]
    public void Split_String_String_LimitOne_ReturnsWhole()
    {
        string[] parts = "a,b,c,d".Split(",", count: 1, StringSplitOptions.None);
        parts.Should().Equal("a,b,c,d");
    }

    [Test]
    public void Split_String_String_LimitTwo_ReturnsHeadAndTail()
    {
        string[] parts = "a,b,c,d".Split(",", count: 2, StringSplitOptions.None);
        parts.Should().Equal("a", "b,c,d");
    }

    [Test]
    public void Split_String_String_RemoveEmpty_DropsEmptyParts()
    {
        string[] parts = "a,,b,".Split(",", count: int.MaxValue, StringSplitOptions.RemoveEmptyEntries);
        parts.Should().Equal("a", "b");
    }
}
