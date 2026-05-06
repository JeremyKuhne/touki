// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Tests;

public class StringExtensionsPolyfillTests
{
    // ---- Contains(char) / Contains(char, StringComparison) / Contains(string, StringComparison) ----

    [Theory]
    [InlineData("Hello", 'H', true)]
    [InlineData("Hello", 'l', true)]
    [InlineData("Hello", 'z', false)]
    [InlineData("", 'a', false)]
    public void Contains_Char_ReturnsExpected(string source, char value, bool expected)
    {
        source.Contains(value).Should().Be(expected);
    }

    [Fact]
    public void Contains_Char_OrdinalIgnoreCase_AcceptsAnyCase()
    {
        "Hello".Contains('h', StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Fact]
    public void Contains_Char_Ordinal_RejectsDifferentCase()
    {
        "Hello".Contains('h', StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public void Contains_String_OrdinalIgnoreCase_AcceptsAnyCase()
    {
        "Hello World".Contains("HELLO", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Fact]
    public void Contains_String_NullValue_Throws()
    {
        Action action = () => "x".Contains(null!, StringComparison.Ordinal);
        action.Should().Throw<ArgumentNullException>();
    }

    // ---- StartsWith / EndsWith char ----

    [Theory]
    [InlineData("Hello", 'H', true)]
    [InlineData("Hello", 'h', false)]
    [InlineData("Hello", 'e', false)]
    [InlineData("", 'a', false)]
    public void StartsWith_Char_ReturnsExpected(string source, char value, bool expected)
    {
        source.StartsWith(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("Hello", 'o', true)]
    [InlineData("Hello", 'O', false)]
    [InlineData("Hello", 'l', false)]
    [InlineData("", 'a', false)]
    public void EndsWith_Char_ReturnsExpected(string source, char value, bool expected)
    {
        source.EndsWith(value).Should().Be(expected);
    }

    // ---- TryCopyTo ----

    [Fact]
    public void TryCopyTo_DestinationLargeEnough_CopiesAndReturnsTrue()
    {
        Span<char> dest = new char[10];
        bool ok = "abc".TryCopyTo(dest);
        ok.Should().BeTrue();
        dest[0].Should().Be('a');
        dest[1].Should().Be('b');
        dest[2].Should().Be('c');
    }

    [Fact]
    public void TryCopyTo_DestinationTooSmall_ReturnsFalse()
    {
        Span<char> dest = new char[2];
        bool ok = "abc".TryCopyTo(dest);
        ok.Should().BeFalse();
    }

    [Fact]
    public void TryCopyTo_EmptySource_AlwaysSucceeds()
    {
        Span<char> dest = [];
        "".TryCopyTo(dest).Should().BeTrue();
    }

    // ---- GetHashCode(StringComparison) ----

    [Fact]
    public void GetHashCode_StringComparison_OrdinalIgnoreCase_SameForCaseVariants()
    {
        int a = "Hello".GetHashCode(StringComparison.OrdinalIgnoreCase);
        int b = "HELLO".GetHashCode(StringComparison.OrdinalIgnoreCase);
        a.Should().Be(b);
    }

    [Fact]
    public void GetHashCode_StringComparison_Ordinal_DifferentForCaseVariants()
    {
        int a = "Hello".GetHashCode(StringComparison.Ordinal);
        int b = "HELLO".GetHashCode(StringComparison.Ordinal);
        a.Should().NotBe(b);
    }

    // ---- Replace(string, string?, StringComparison) ----

    [Fact]
    public void Replace_StringComparison_Ordinal_ExactMatch()
    {
        "abcabc".Replace("ab", "X", StringComparison.Ordinal).Should().Be("XcXc");
    }

    [Fact]
    public void Replace_StringComparison_OrdinalIgnoreCase_AnyCase()
    {
        "abcAbC".Replace("AB", "X", StringComparison.OrdinalIgnoreCase).Should().Be("XcXC");
    }

    [Fact]
    public void Replace_StringComparison_NullNewValue_TreatedAsEmpty()
    {
        "abcabc".Replace("ab", null, StringComparison.OrdinalIgnoreCase).Should().Be("cc");
    }

    [Fact]
    public void Replace_StringComparison_NoMatch_ReturnsOriginal()
    {
        "abc".Replace("z", "X", StringComparison.Ordinal).Should().Be("abc");
    }

    [Fact]
    public void Replace_StringComparison_NullOldValue_Throws()
    {
        Action action = () => "abc".Replace(null!, "x", StringComparison.Ordinal);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Replace_StringComparison_EmptyOldValue_Throws()
    {
        Action action = () => "abc".Replace("", "x", StringComparison.Ordinal);
        action.Should().Throw<ArgumentException>();
    }

    // ---- ReplaceLineEndings ----

    [Theory]
    [InlineData("a\nb", "a|b")]
    [InlineData("a\rb", "a|b")]
    [InlineData("a\r\nb", "a|b")]
    [InlineData("a\u0085b", "a|b")]
    [InlineData("a\u2028b", "a|b")]
    [InlineData("a\u2029b", "a|b")]
    [InlineData("a\fb", "a|b")]
    [InlineData("no line endings", "no line endings")]
    public void ReplaceLineEndings_Custom_ReplacesAllRecognizedEndings(string input, string expected)
    {
        input.ReplaceLineEndings("|").Should().Be(expected);
    }

    [Fact]
    public void ReplaceLineEndings_CRLF_TreatedAsSingleEnding()
    {
        // \r\n should yield ONE replacement, not two.
        "a\r\nb\r\nc".ReplaceLineEndings("|").Should().Be("a|b|c");
    }

    [Fact]
    public void ReplaceLineEndings_NoArgument_UsesEnvironmentNewLine()
    {
        "a\nb".ReplaceLineEndings().Should().Be("a" + Environment.NewLine + "b");
    }

    [Fact]
    public void ReplaceLineEndings_NullReplacement_Throws()
    {
        Action action = () => "a\nb".ReplaceLineEndings(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    // ---- Split(char, ...) / Split(string, ...) ----

    [Fact]
    public void Split_Char_StringSplitOptions_RemoveEmpty()
    {
        "a,,b".Split(',', StringSplitOptions.RemoveEmptyEntries).Should().Equal("a", "b");
    }

    [Fact]
    public void Split_Char_StringSplitOptions_None_KeepsEmpty()
    {
        "a,,b".Split(',', StringSplitOptions.None).Should().Equal("a", "", "b");
    }

    [Fact]
    public void Split_Char_Count_LimitsSplits()
    {
        "a,b,c,d".Split(',', 2, StringSplitOptions.None).Should().Equal("a", "b,c,d");
    }

    [Fact]
    public void Split_String_StringSplitOptions_RemoveEmpty()
    {
        "a::b".Split("::", StringSplitOptions.RemoveEmptyEntries).Should().Equal("a", "b");
    }

    [Fact]
    public void Split_String_Count_LimitsSplits()
    {
        "a::b::c::d".Split("::", 2, StringSplitOptions.None).Should().Equal("a", "b::c::d");
    }

    [Fact]
    public void Split_String_NullSeparator_ReturnsSingleSourceElement()
    {
        // BCL behavior for Split((string?)null, opts): treats as no separator, returns the source unchanged.
        "a b\tc".Split((string?)null, StringSplitOptions.None).Should().Equal("a b\tc");
    }

    [Fact]
    public void Split_Char_EmptySource_ReturnsSingleEmpty()
    {
        "".Split(',', StringSplitOptions.None).Should().Equal("");
    }

    [Fact]
    public void Split_Char_NoMatch_ReturnsSingle()
    {
        "abc".Split(',', StringSplitOptions.None).Should().Equal("abc");
    }

    [Fact]
    public void Split_Char_CountZero_ReturnsEmpty()
    {
        "a,b,c".Split(',', 0, StringSplitOptions.None).Should().BeEmpty();
    }

    [Fact]
    public void Split_Char_CountOne_ReturnsOriginal()
    {
        "a,b,c".Split(',', 1, StringSplitOptions.None).Should().Equal("a,b,c");
    }

    [Fact]
    public void Split_Char_NegativeCount_Throws()
    {
        Action action = () => "a,b".Split(',', -1, StringSplitOptions.None);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- Contains negative & boundary ----

    [Fact]
    public void Contains_Char_EmptyString_ReturnsFalse()
    {
        "".Contains('a').Should().BeFalse();
    }

    [Fact]
    public void Contains_String_EmptyValue_ReturnsTrue()
    {
        // BCL contract: an empty needle is contained anywhere.
        "Hello".Contains("", StringComparison.Ordinal).Should().BeTrue();
    }

    [Fact]
    public void Contains_String_InvalidComparison_Throws()
    {
        Action action = () => "Hello".Contains("yz", (StringComparison)42);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Contains_Char_InvalidComparison_Throws()
    {
        Action action = () => "x".Contains('y', (StringComparison)42);
        action.Should().Throw<ArgumentException>();
    }

    // ---- GetHashCode negative ----

    [Fact]
    public void GetHashCode_StringComparison_Invalid_Throws()
    {
        Action action = () => "x".GetHashCode((StringComparison)42);
        action.Should().Throw<ArgumentException>();
    }

    // ---- Replace negative & boundary ----

    [Fact]
    public void Replace_StringComparison_InvalidComparison_Throws()
    {
        Action action = () => "abc".Replace("a", "b", (StringComparison)42);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Replace_StringComparison_EmptySource_ReturnsEmpty()
    {
        "".Replace("a", "b", StringComparison.Ordinal).Should().BeEmpty();
    }

    [Fact]
    public void Replace_StringComparison_OldValueLongerThanSource_ReturnsOriginal()
    {
        "ab".Replace("abcdef", "x", StringComparison.Ordinal).Should().Be("ab");
    }

    [Fact]
    public void Replace_StringComparison_AdjacentMatches_AllReplaced()
    {
        "aaaa".Replace("aa", "X", StringComparison.Ordinal).Should().Be("XX");
    }

    [Fact]
    public void Replace_StringComparison_OrdinalNullNewValue_TreatedAsEmpty()
    {
        "abc".Replace("b", null, StringComparison.Ordinal).Should().Be("ac");
    }

    // ---- ReplaceLineEndings negative & boundary ----

    [Fact]
    public void ReplaceLineEndings_EmptySource_ReturnsEmpty()
    {
        "".ReplaceLineEndings("|").Should().BeEmpty();
    }

    [Fact]
    public void ReplaceLineEndings_OnlyLineEnding_ReturnsReplacement()
    {
        "\n".ReplaceLineEndings("|").Should().Be("|");
    }

    [Fact]
    public void ReplaceLineEndings_LeadingAndTrailing_BothReplaced()
    {
        "\nabc\n".ReplaceLineEndings("|").Should().Be("|abc|");
    }

    [Fact]
    public void ReplaceLineEndings_VerticalTab_NotTreatedAsLineEnding()
    {
        // \v (U+000B) is NOT a recognized line ending per the BCL spec.
        "a\vb".ReplaceLineEndings("|").Should().Be("a\vb");
    }

    [Fact]
    public void ReplaceLineEndings_LoneCR_ReplacedAsSingleEnding()
    {
        "a\rb".ReplaceLineEndings("|").Should().Be("a|b");
    }

    [Fact]
    public void ReplaceLineEndings_LoneLF_ReplacedAsSingleEnding()
    {
        "a\nb".ReplaceLineEndings("|").Should().Be("a|b");
    }

    [Fact]
    public void ReplaceLineEndings_CRThenChar_NotMisinterpreted()
    {
        // \r followed by non-\n must not consume the next char.
        "a\rXb".ReplaceLineEndings("|").Should().Be("a|Xb");
    }

    [Fact]
    public void ReplaceLineEndings_LongInput_ExceedsStackBuffer()
    {
        // Force the ValueStringBuilder beyond the 256-char stack buffer.
        string source = new string('a', 1000) + "\n" + new string('b', 1000);
        string result = source.ReplaceLineEndings("|");
        result.Should().Be(new string('a', 1000) + "|" + new string('b', 1000));
    }

    // ---- TryCopyTo edge ----

    [Fact]
    public void TryCopyTo_EmptyDestinationNonEmptySource_ReturnsFalse()
    {
        Span<char> dest = [];
        "abc".TryCopyTo(dest).Should().BeFalse();
    }

    [Fact]
    public void TryCopyTo_ExactFit_CopiesAndReturnsTrue()
    {
        Span<char> dest = new char[3];
        bool ok = "abc".TryCopyTo(dest);
        ok.Should().BeTrue();
        dest.ToString().Should().Be("abc");
    }
}
