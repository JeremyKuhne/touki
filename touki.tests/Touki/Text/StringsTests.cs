// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki.Text;

/// <remarks>
///  Running against both .NET Framework and .NET, even if there is an implemenation on .NET to
///  ensure matching behavior.
/// </remarks>
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
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_NegativeLength_Throws(int length)
    {
        Action action = () => string.Create(length, 0, (span, state) => { });
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_NullAction_Throws()
    {
        Action act = () => string.Create(10, 0, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ZeroLength_ReturnsEmpty()
    {
        string result = string.Create(0, 0, (span, state) => { });
        result.Should().BeEmpty();
        result.Should().BeSameAs(string.Empty);
    }

    [Fact]
    public void Create_WithState_CreatesStringCorrectly()
    {
        string result = string.Create(5, 'A', (span, state) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = state;
            }
        });

        result.Should().Be("AAAAA");
    }

    [Fact]
    public void Create_WithComplexState_CreatesStringCorrectly()
    {
        (int start, int count) state = (65, 5);
        string result = string.Create(state.count, state, (span, s) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = (char)(s.start + i);
            }
        });

        result.Should().Be("ABCDE");
    }

    [Fact]
    public void Create_WithInterpolatedString_FormatsCorrectly()
    {
        int value = 42;
        string name = "test";
        string result = string.Create(CultureInfo.InvariantCulture, $"Value: {value}, Name: {name}");

        result.Should().Be("Value: 42, Name: test");
    }

    [Fact]
    public void Create_WithInterpolatedStringAndNull_FormatsCorrectly()
    {
        string? nullValue = null;
        string result = string.Create(CultureInfo.InvariantCulture, $"Null: {nullValue}");

        result.Should().Be("Null: ");
    }

    [Fact]
    public void Create_WithInterpolatedStringAndFormat_FormatsCorrectly()
    {
        double value = 123.456;
        string result = string.Create(CultureInfo.InvariantCulture, $"Value: {value:F2}");

        result.Should().Be("Value: 123.46");
    }

    [Fact]
    public void Create_WithInterpolatedStringAndCulture_FormatsCorrectly()
    {
        double value = 1234.56;
        CultureInfo culture = new("de-DE");
        string result = string.Create(culture, $"{value:N2}");

        result.Should().Be("1.234,56");
    }

    [Fact]
    public void CopyTo_SufficientDestination_CopiesSuccessfully()
    {
        string source = "Hello";
        Span<char> destination = stackalloc char[10];

        source.CopyTo(destination);

        destination[..5].ToString().Should().Be("Hello");
    }

    [Fact]
    public void CopyTo_ExactDestination_CopiesSuccessfully()
    {
        string source = "Test";
        Span<char> destination = stackalloc char[4];

        source.CopyTo(destination);

        destination.ToString().Should().Be("Test");
    }

    [Fact]
    public void CopyTo_EmptyString_DoesNotThrow()
    {
        string source = "";
        Span<char> destination = stackalloc char[5];

        source.CopyTo(destination);

        destination[0].Should().Be('\0');
    }

    [Fact]
    public void CopyTo_DestinationTooShort_Throws()
    {
        string source = "Hello";
        Span<char> destination = stackalloc char[3];

        try
        {
            source.CopyTo(destination);
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [Fact]
    public void CopyTo_EmptyDestination_Throws()
    {
        string source = "Test";
        Span<char> destination = [];

        try
        {
            source.CopyTo(destination);
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [Fact]
    public void TryCopyTo_SufficientDestination_ReturnsTrue()
    {
        string source = "Hello";
        Span<char> destination = stackalloc char[10];
        source.TryCopyTo(destination).Should().BeTrue();
        destination[..5].ToString().Should().Be("Hello");
    }

    [Fact]
    public void TryCopyTo_ExactDestination_ReturnsTrue()
    {
        string source = "Test";
        Span<char> destination = stackalloc char[4];
        source.TryCopyTo(destination).Should().BeTrue();
        destination.ToString().Should().Be("Test");
    }

    [Fact]
    public void TryCopyTo_DestinationTooShort_ReturnsFalse()
    {
        string source = "Hello";
        Span<char> destination = stackalloc char[3];
        source.TryCopyTo(destination).Should().BeFalse();
    }

    [Fact]
    public void TryCopyTo_EmptyDestination_ReturnsFalse()
    {
        string source = "Test";
        Span<char> destination = [];
        source.TryCopyTo(destination).Should().BeFalse();
    }

    [Fact]
    public void TryCopyTo_EmptyString_ReturnsTrue()
    {
        string source = "";
        Span<char> destination = stackalloc char[5];
        source.TryCopyTo(destination).Should().BeTrue();
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

    [Fact]
    public void Concat_TwoSpans_EmptySpans_ReturnsEmpty()
    {
        ReadOnlySpan<char> str0 = [];
        ReadOnlySpan<char> str1 = [];

        string result = string.Concat(str0, str1);

        result.Should().BeEmpty();
        result.Should().BeSameAs(string.Empty);
    }

    [Fact]
    public void Concat_TwoSpans_FirstEmpty_ReturnsSecond()
    {
        ReadOnlySpan<char> str0 = [];
        ReadOnlySpan<char> str1 = "Hello".AsSpan();

        string result = string.Concat(str0, str1);

        result.Should().Be("Hello");
    }

    [Fact]
    public void Concat_TwoSpans_SecondEmpty_ReturnsFirst()
    {
        ReadOnlySpan<char> str0 = "World".AsSpan();
        ReadOnlySpan<char> str1 = [];

        string result = string.Concat(str0, str1);

        result.Should().Be("World");
    }

    [Fact]
    public void Concat_TwoSpans_BothNonEmpty_ConcatenatesCorrectly()
    {
        ReadOnlySpan<char> str0 = "Hello".AsSpan();
        ReadOnlySpan<char> str1 = "World".AsSpan();

        string result = string.Concat(str0, str1);

        result.Should().Be("HelloWorld");
    }

    [Fact]
    public void Concat_TwoSpans_WithSlicedSpans_ConcatenatesCorrectly()
    {
        string source1 = "abcdefg";
        string source2 = "123456";
        ReadOnlySpan<char> str0 = source1.AsSpan(2, 3);
        ReadOnlySpan<char> str1 = source2.AsSpan(1, 4);

        string result = string.Concat(str0, str1);

        result.Should().Be("cde2345");
    }

    [Fact]
    public void Concat_ThreeSpans_AllEmpty_ReturnsEmpty()
    {
        ReadOnlySpan<char> str0 = [];
        ReadOnlySpan<char> str1 = [];
        ReadOnlySpan<char> str2 = [];

        string result = string.Concat(str0, str1, str2);

        result.Should().BeEmpty();
        result.Should().BeSameAs(string.Empty);
    }

    [Fact]
    public void Concat_ThreeSpans_SomeEmpty_ConcatenatesNonEmpty()
    {
        ReadOnlySpan<char> str0 = "Hello".AsSpan();
        ReadOnlySpan<char> str1 = [];
        ReadOnlySpan<char> str2 = "World".AsSpan();

        string result = string.Concat(str0, str1, str2);

        result.Should().Be("HelloWorld");
    }

    [Fact]
    public void Concat_ThreeSpans_AllNonEmpty_ConcatenatesCorrectly()
    {
        ReadOnlySpan<char> str0 = "Hello".AsSpan();
        ReadOnlySpan<char> str1 = " ".AsSpan();
        ReadOnlySpan<char> str2 = "World".AsSpan();

        string result = string.Concat(str0, str1, str2);

        result.Should().Be("Hello World");
    }

    [Fact]
    public void Concat_ThreeSpans_WithUnicode_ConcatenatesCorrectly()
    {
        ReadOnlySpan<char> str0 = "café".AsSpan();
        ReadOnlySpan<char> str1 = " ♥ ".AsSpan();
        ReadOnlySpan<char> str2 = "文字".AsSpan();

        string result = string.Concat(str0, str1, str2);

        result.Should().Be("café ♥ 文字");
    }

    [Fact]
    public void Concat_FourSpans_AllEmpty_ReturnsEmpty()
    {
        ReadOnlySpan<char> str0 = [];
        ReadOnlySpan<char> str1 = [];
        ReadOnlySpan<char> str2 = [];
        ReadOnlySpan<char> str3 = [];

        string result = string.Concat(str0, str1, str2, str3);

        result.Should().BeEmpty();
        result.Should().BeSameAs(string.Empty);
    }

    [Fact]
    public void Concat_FourSpans_SomeEmpty_ConcatenatesNonEmpty()
    {
        ReadOnlySpan<char> str0 = "The".AsSpan();
        ReadOnlySpan<char> str1 = [];
        ReadOnlySpan<char> str2 = "quick".AsSpan();
        ReadOnlySpan<char> str3 = "fox".AsSpan();

        string result = string.Concat(str0, str1, str2, str3);

        result.Should().Be("Thequickfox");
    }

    [Fact]
    public void Concat_FourSpans_AllNonEmpty_ConcatenatesCorrectly()
    {
        ReadOnlySpan<char> str0 = "The".AsSpan();
        ReadOnlySpan<char> str1 = " quick".AsSpan();
        ReadOnlySpan<char> str2 = " brown".AsSpan();
        ReadOnlySpan<char> str3 = " fox".AsSpan();

        string result = string.Concat(str0, str1, str2, str3);

        result.Should().Be("The quick brown fox");
    }

    [Fact]
    public void Concat_FourSpans_WithSurrogatePairs_ConcatenatesCorrectly()
    {
        ReadOnlySpan<char> str0 = "Test".AsSpan();
        ReadOnlySpan<char> str1 = " \uD83D\uDE00".AsSpan();
        ReadOnlySpan<char> str2 = " emoji".AsSpan();
        ReadOnlySpan<char> str3 = " \uD83D\uDE01".AsSpan();

        string result = string.Concat(str0, str1, str2, str3);

        result.Should().Be("Test \uD83D\uDE00 emoji \uD83D\uDE01");
    }

    [Fact]
    public void Concat_FourSpans_LargeStrings_ConcatenatesCorrectly()
    {
        string large1 = new('a', 1000);
        string large2 = new('b', 1000);
        string large3 = new('c', 1000);
        string large4 = new('d', 1000);

        string result = string.Concat(large1.AsSpan(), large2.AsSpan(), large3.AsSpan(), large4.AsSpan());

        result.Length.Should().Be(4000);
        result[..1000].Should().Be(large1);
        result[1000..2000].Should().Be(large2);
        result[2000..3000].Should().Be(large3);
        result[3000..].Should().Be(large4);
    }

    [Fact]
    public void ReplaceLineEndings_NoNewlines_ReturnsSameString()
    {
        string input = "Hello World";
        string result = input.ReplaceLineEndings();

        result.Should().BeSameAs(input);
    }

    [Fact]
    public void ReplaceLineEndings_WithCRLF_ReplacesWithEnvironmentNewLine()
    {
        string input = "Hello\r\nWorld";
        string result = input.ReplaceLineEndings();

        result.Should().Be("Hello\r\nWorld");
    }

    [Fact]
    public void ReplaceLineEndings_WithLF_ReplacesWithEnvironmentNewLine()
    {
        string input = "Hello\nWorld";
        string result = input.ReplaceLineEndings();

        result.Should().Be("Hello\r\nWorld");
    }

    [Fact]
    public void ReplaceLineEndings_WithCR_ReplacesWithEnvironmentNewLine()
    {
        string input = "Hello\rWorld";
        string result = input.ReplaceLineEndings();

        result.Should().Be("Hello\r\nWorld");
    }

    [Fact]
    public void ReplaceLineEndings_WithFormFeed_ReplacesWithEnvironmentNewLine()
    {
        string input = "Hello\fWorld";
        string result = input.ReplaceLineEndings();

        result.Should().Be("Hello\r\nWorld");
    }

    [Fact]
    public void ReplaceLineEndings_WithNEL_ReplacesWithEnvironmentNewLine()
    {
        string input = "Hello\u0085World";
        string result = input.ReplaceLineEndings();

        result.Should().Be("Hello\r\nWorld");
    }

    [Fact]
    public void ReplaceLineEndings_WithLS_ReplacesWithEnvironmentNewLine()
    {
        string input = "Hello\u2028World";
        string result = input.ReplaceLineEndings();

        result.Should().Be("Hello\r\nWorld");
    }

    [Fact]
    public void ReplaceLineEndings_WithPS_ReplacesWithEnvironmentNewLine()
    {
        string input = "Hello\u2029World";
        string result = input.ReplaceLineEndings();

        result.Should().Be("Hello\r\nWorld");
    }

    [Fact]
    public void ReplaceLineEndings_WithMixedNewlines_ReplacesAll()
    {
        string input = "Line1\r\nLine2\nLine3\rLine4\fLine5";
        string result = input.ReplaceLineEndings();

        result.Should().Be("Line1\r\nLine2\r\nLine3\r\nLine4\r\nLine5");
    }

    [Fact]
    public void ReplaceLineEndings_WithCustomReplacement_ReplacesWithCustom()
    {
        string input = "Hello\r\nWorld\nTest";
        string result = input.ReplaceLineEndings("||");

        result.Should().Be("Hello||World||Test");
    }

    [Fact]
    public void ReplaceLineEndings_WithEmptyReplacement_RemovesNewlines()
    {
        string input = "Hello\r\nWorld\nTest";
        string result = input.ReplaceLineEndings("");

        result.Should().Be("HelloWorldTest");
    }

    [Fact]
    public void ReplaceLineEndings_WithLineFeed_UsesOptimizedPath()
    {
        string input = "Hello\r\nWorld\rTest\fEnd";
        string result = input.ReplaceLineEndings("\n");

        result.Should().Be("Hello\nWorld\nTest\nEnd");
    }

    [Fact]
    public void ReplaceLineEndings_EmptyString_ReturnsEmpty()
    {
        string input = "";
        string result = input.ReplaceLineEndings();

        result.Should().BeSameAs(input);
    }

    [Fact]
    public void ReplaceLineEndings_OnlyNewlines_ReplacesAll()
    {
        string input = "\r\n\n\r\f";
        string result = input.ReplaceLineEndings("X");

        result.Should().Be("XXXX");
    }

    [Fact]
    public void ReplaceLineEndings_ConsecutiveNewlines_ReplacesEach()
    {
        string input = "Hello\r\r\nWorld";
        string result = input.ReplaceLineEndings("|");

        result.Should().Be("Hello||World");
    }

    [Fact]
    public void ReplaceLineEndings_WithNullReplacement_Throws()
    {
        string input = "Hello\nWorld";
        Action action = () => input.ReplaceLineEndings(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReplaceLineEndings_MultipleUnicodeNewlines_ReplacesAll()
    {
        string input = "A\u0085B\u2028C\u2029D";
        string result = input.ReplaceLineEndings("-");

        result.Should().Be("A-B-C-D");
    }

    [Fact]
    public void ReplaceLineEndings_LongStringWithManyNewlines_ReplacesAll()
    {
        string input = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"Line{i}"));
        string result = input.ReplaceLineEndings(" | ");

        result.Should().Contain(" | ");
        result.Should().NotContain("\n");
    }
}
