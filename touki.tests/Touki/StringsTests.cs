// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StringsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Aar")]
    [InlineData("The quick brown fox jumps over the lazy dog.")]
    public void GetHashCode_EqualsString(string? value)
    {
        ReadOnlySpan<char> span = value.AsSpan();
        value ??= "";

        int hash1 = Strings.GetHashCode(span);
        int hash2 = value.GetHashCode();

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_EqualsString_Sliced()
    {
        string test = "The quick brown fox jumps over the lazy dog.";
        ReadOnlySpan<char> span = test.AsSpan()[11..];

        int hash1 = Strings.GetHashCode(span);
        int hash2 = span.ToString().GetHashCode();

        hash1.Should().Be(hash2);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("a", "a")]
    [InlineData("a", "b")]
    [InlineData("b", "a")]
    [InlineData("abc", "abc")]
    [InlineData("abc", "abd")]
    [InlineData("abd", "abc")]
    [InlineData("abc", "abcd")]
    [InlineData("abcd", "abc")]
    [InlineData("The quick brown fox", "The quick brown fox")]
    [InlineData("The quick brown fox", "The quick brown dog")]
    public void CompareOrdinalAsString_MatchesStringCompare(string first, string second)
    {
        ReadOnlySpan<char> span1 = first.AsSpan();
        ReadOnlySpan<char> span2 = second.AsSpan();

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(first, second, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Fact]
    public void CompareOrdinalAsString_WithSlicedSpans_MatchesStringCompare()
    {
        string source1 = "abcdefghijklmnop";
        string source2 = "xyz123abcdefg456";

        ReadOnlySpan<char> span1 = source1.AsSpan(3, 5);
        ReadOnlySpan<char> span2 = source2.AsSpan(6, 5);

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(
            source1.Substring(3, 5),
            source2.Substring(6, 5),
            StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Fact]
    public void CompareOrdinalAsString_WithEmbeddedNulls_MatchesStringCompare()
    {
        // Create strings with embedded null characters
        string str1 = "abc\0def";
        string str2 = "abc\0xyz";

        ReadOnlySpan<char> span1 = str1.AsSpan();
        ReadOnlySpan<char> span2 = str2.AsSpan();

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(str1, str2, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Fact]
    public void CompareOrdinalAsString_WithNullsAtDifferentPositions_MatchesStringCompare()
    {
        string str1 = "ab\0cdef";
        string str2 = "abc\0def";

        ReadOnlySpan<char> span1 = str1.AsSpan();
        ReadOnlySpan<char> span2 = str2.AsSpan();

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(str1, str2, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("", "abc")]
    [InlineData("abc", "")]
    public void CompareOrdinalAsString_OneEmptySpan_MatchesStringCompare(string first, string second)
    {
        ReadOnlySpan<char> span1 = first.AsSpan();
        ReadOnlySpan<char> span2 = second.AsSpan();

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(first, second, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("a", "")]
    [InlineData("", "a")]
    public void CompareOrdinalAsString_SingleCharAndEmpty_MatchesStringCompare(string first, string second)
    {
        ReadOnlySpan<char> span1 = first.AsSpan();
        ReadOnlySpan<char> span2 = second.AsSpan();

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(first, second, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("xyz", "abc")]
    [InlineData("zzz", "aaa")]
    public void CompareOrdinalAsString_CompletelyDifferentStrings_MatchesStringCompare(string first, string second)
    {
        ReadOnlySpan<char> span1 = first.AsSpan();
        ReadOnlySpan<char> span2 = second.AsSpan();

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(first, second, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("a", "ab")]      // Odd shared length (1)
    [InlineData("ab", "a")]      // Odd shared length (1)
    [InlineData("ab", "abc")]    // Even shared length (2)
    [InlineData("abc", "ab")]    // Even shared length (2)
    public void CompareOrdinalAsString_OddVsEvenSharedLength_MatchesStringCompare(string first, string second)
    {
        ReadOnlySpan<char> span1 = first.AsSpan();
        ReadOnlySpan<char> span2 = second.AsSpan();

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(first, second, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Fact]
    public void CompareOrdinalAsString_WithUnicodeCharacters_MatchesStringCompare()
    {
        string str1 = "café \u00A9 文字";
        string str2 = "café \u00A9 测试";

        ReadOnlySpan<char> span1 = str1.AsSpan();
        ReadOnlySpan<char> span2 = str2.AsSpan();

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(str1, str2, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Fact]
    public void CompareOrdinalAsString_WithSurrogatePairs_MatchesStringCompare()
    {
        // Surrogate pairs for emoji characters
        string str1 = "Test \uD83D\uDE00 emoji"; // 😀
        string str2 = "Test \uD83D\uDE01 emoji"; // 😁

        ReadOnlySpan<char> span1 = str1.AsSpan();
        ReadOnlySpan<char> span2 = str2.AsSpan();

        int actualResult = Strings.CompareOrdinalAsString(span1, span2);
        int expectedResult = string.Compare(str1, str2, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }
}
