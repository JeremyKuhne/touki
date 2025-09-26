// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

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

        int hash1 = string.GetHashCode(span);
        int hash2 = value.GetHashCode();

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_EqualsString_Sliced()
    {
        string test = "The quick brown fox jumps over the lazy dog.";
        ReadOnlySpan<char> span = test.AsSpan()[11..];

        int hash1 = string.GetHashCode(span);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
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

        int actualResult = span1.CompareOrdinalAsString(span2);
        int expectedResult = string.Compare(str1, str2, StringComparison.Ordinal);

        actualResult.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(0, 4, 8)]
    [InlineData(-1, 4, 8)]
    public void GenerateRandomStrings_CountLessThanOrEqualZero_Throws(int count, int minLength, int maxLength)
    {
        Action action = () => string.GenerateRandomStrings(count, minLength, maxLength, allowSurrogatePairs: false, random: new Random(1));
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1, 0, 8)]
    [InlineData(1, -5, 8)]
    public void GenerateRandomStrings_MinLengthLessThanOrEqualZero_Throws(int count, int minLength, int maxLength)
    {
        Action action = () => string.GenerateRandomStrings(count, minLength, maxLength, allowSurrogatePairs: false, random: new Random(1));
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1, 10, 9)]
    [InlineData(5, 10, 5)]
    public void GenerateRandomStrings_MaxLengthLessThanMinLength_Throws(int count, int minLength, int maxLength)
    {
        Action action = () => string.GenerateRandomStrings(count, minLength, maxLength, allowSurrogatePairs: false, random: new Random(1));
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GenerateRandomStrings_NoSurrogates_ReturnsCountAndLengthsWithinBounds()
    {
        int count = 100;
        int minLength = 4;
        int maxLength = 40;
        Random random = new(12345);

        List<string> result = string.GenerateRandomStrings(count, minLength, maxLength, allowSurrogatePairs: false, random: random);

        result.Should().HaveCount(count);

        foreach (string s in result)
        {
            // Expected: lengths are within [min, max] and no disallowed chars.
            s.Length.Should().BeInRange(minLength, maxLength);

            foreach (char c in s)
            {
                int code = c;

                bool inAsciiPrintable = code is >= 0x0020 and <= 0x007E;
                bool inGapA2 = code is >= 0x00A0 and <= 0xD7FF;
                bool inGapC = code is >= 0xE000 and <= 0xFFFD;
                bool inNonCharacters = code is >= 0xFDD0 and <= 0xFDEF;
                bool isControlOrDel = code is < 0x0020 or 0x007F;
                bool isC1Control = code is >= 0x0080 and <= 0x009F;
                bool isSurrogate = code is >= 0xD800 and <= 0xDFFF;

                bool allowed = (inAsciiPrintable || inGapA2 || inGapC) && !inNonCharacters;

                allowed.Should().BeTrue($"U+{code:X4} should be allowed when surrogate pairs are disabled");
                isControlOrDel.Should().BeFalse("Control characters must be excluded");
                isC1Control.Should().BeFalse("C1 control characters must be excluded");
                isSurrogate.Should().BeFalse("Surrogates must not appear when disabled");
            }
        }
    }

    [Fact]
    public void GenerateRandomStrings_DeterministicWithSameSeed_AndDifferentWithDifferentSeed()
    {
        List<string> a = string.GenerateRandomStrings(10, 4, 10, allowSurrogatePairs: false, random: new Random(1));
        List<string> b = string.GenerateRandomStrings(10, 4, 10, allowSurrogatePairs: false, random: new Random(1));
        List<string> c = string.GenerateRandomStrings(10, 4, 10, allowSurrogatePairs: false, random: new Random(2));

        a.Should().Equal(b);
        a.Should().NotEqual(c);
    }

    [Fact]
    public void GenerateRandomStrings_MinEqualsMax_ReturnsExactLength()
    {
        int exactLength = 8;
        List<string> result = string.GenerateRandomStrings(50, exactLength, exactLength, allowSurrogatePairs: false, random: new Random(123));

        foreach (string s in result)
        {
            s.Length.Should().Be(exactLength);
        }
    }

    [Fact]
    public void GenerateRandomStrings_WithSurrogatePairs_WellFormedAndNoForbiddenScalars()
    {
        int count = 200;
        int minLength = 4;
        int maxLength = 20;
        Random random = new(42);

        List<string> result = string.GenerateRandomStrings(count, minLength, maxLength, allowSurrogatePairs: true, random: random);

        result.Should().HaveCount(count);

        foreach (string s in result)
        {
            s.Length.Should().BeInRange(minLength, maxLength);

            for (int i = 0; i < s.Length; i++)
            {
                int code = s[i];

                // Skip controls and U+0000 entirely.
                (code == 0).Should().BeFalse("U+0000 must be excluded");
                (code < 0x20 || code == 0x7F || (code >= 0x80 && code <= 0x9F)).Should().BeFalse("Control characters must be excluded");

                bool isHigh = code is >= 0xD800 and <= 0xDBFF;
                bool isLow = code is >= 0xDC00 and <= 0xDFFF;

                if (isHigh)
                {
                    (i + 1 < s.Length).Should().BeTrue("High surrogate must be followed by a low surrogate");
                    int low = s[i + 1];
                    (low >= 0xDC00 && low <= 0xDFFF).Should().BeTrue("High surrogate must be followed by a low surrogate");

                    // Reconstruct code point and ensure it is not a ?FFFE/?FFFF noncharacter.
                    int codePoint = 0x10000 + (((code - 0xD800) << 10) | (low - 0xDC00));
                    ((codePoint & 0xFFFE) != 0xFFFE).Should().BeTrue("Generated scalar must not be ?FFFE/?FFFF");

                    i++; // Skip the low surrogate we just validated.
                }
                else
                {
                    // Should not see a standalone low surrogate.
                    isLow.Should().BeFalse("Low surrogate must not be unpaired");
                    // Also ensure BMP noncharacters are excluded.
                    ((code < 0xFDD0 || code > 0xFDEF) && code <= 0xFFFD).Should().BeTrue("BMP noncharacters must be excluded");
                }
            }
        }
    }
}
