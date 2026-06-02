// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class SpanExtensionsSearchTests
{
    // -----------------------------------------------------------------------
    //  IndexOfAnyExcept
    // -----------------------------------------------------------------------

    [Test]
    public void IndexOfAnyExcept_SingleValue_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [7, 7, 7];
        span.IndexOfAnyExcept(7).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyExcept_SingleValue_FirstDifferent_ReturnsZero()
    {
        ReadOnlySpan<int> span = [1, 7, 7];
        span.IndexOfAnyExcept(7).Should().Be(0);
    }

    [Test]
    public void IndexOfAnyExcept_SingleValue_Empty_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [];
        span.IndexOfAnyExcept(7).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyExcept_TwoValues_FindsFirstOther()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 1, 2];
        span.IndexOfAnyExcept(1, 2).Should().Be(2);
    }

    [Test]
    public void IndexOfAnyExcept_TwoValues_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [1, 2, 1, 2];
        span.IndexOfAnyExcept(1, 2).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyExcept_ThreeValues_FindsFirstOther()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4, 5];
        span.IndexOfAnyExcept(1, 2, 3).Should().Be(3);
    }

    [Test]
    public void IndexOfAnyExcept_ThreeValues_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 1, 2, 3];
        span.IndexOfAnyExcept(1, 2, 3).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyExcept_ValuesSpan_Empty_ReturnsZeroIfNonEmptySource()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        span.IndexOfAnyExcept([]).Should().Be(0);
    }

    [Test]
    public void IndexOfAnyExcept_ValuesSpan_BothEmpty_ReturnsMinusOne()
    {
        ReadOnlySpan<int>.Empty.IndexOfAnyExcept([]).Should().Be(-1);
    }

    [Test]
    public void IndexOfAnyExcept_ValuesSpan_FourValues_FindsFirstOther()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4, 5];
        ReadOnlySpan<int> values = [1, 2, 3, 4];
        span.IndexOfAnyExcept(values).Should().Be(4);
    }

    [Test]
    public void IndexOfAnyExcept_NullableReference_TreatsNullCorrectly()
    {
        ReadOnlySpan<string?> span = ["a", null, "b"];
        span.IndexOfAnyExcept((string?)"a").Should().Be(1);
        span.IndexOfAnyExcept((string?)null).Should().Be(0);
    }

    [Test]
    public void IndexOfAnyExcept_SpanOverload_Works()
    {
        Span<int> span = [7, 7, 1];
        span.IndexOfAnyExcept(7).Should().Be(2);
        span.IndexOfAnyExcept(7, 1).Should().Be(-1);
    }

    // -----------------------------------------------------------------------
    //  LastIndexOfAnyExcept
    // -----------------------------------------------------------------------

    [Test]
    public void LastIndexOfAnyExcept_SingleValue_FindsLast()
    {
        ReadOnlySpan<int> span = [7, 1, 7, 2, 7];
        span.LastIndexOfAnyExcept(7).Should().Be(3);
    }

    [Test]
    public void LastIndexOfAnyExcept_SingleValue_AllMatch_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [7, 7, 7];
        span.LastIndexOfAnyExcept(7).Should().Be(-1);
    }

    [Test]
    public void LastIndexOfAnyExcept_TwoValues_FindsLast()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 1, 2];
        span.LastIndexOfAnyExcept(1, 2).Should().Be(2);
    }

    [Test]
    public void LastIndexOfAnyExcept_ThreeValues_FindsLast()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4, 1, 2, 3];
        span.LastIndexOfAnyExcept(1, 2, 3).Should().Be(3);
    }

    [Test]
    public void LastIndexOfAnyExcept_ValuesSpan_Empty_ReturnsLengthMinusOne()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        span.LastIndexOfAnyExcept([]).Should().Be(2);
    }

    [Test]
    public void LastIndexOfAnyExcept_ValuesSpan_FourValues_FindsLast()
    {
        ReadOnlySpan<int> span = [5, 1, 2, 3, 4];
        ReadOnlySpan<int> values = [1, 2, 3, 4];
        span.LastIndexOfAnyExcept(values).Should().Be(0);
    }

    [Test]
    public void LastIndexOfAnyExcept_SpanOverload_Works()
    {
        Span<int> span = [1, 7, 7, 7];
        span.LastIndexOfAnyExcept(7).Should().Be(0);
    }

    // -----------------------------------------------------------------------
    //  ContainsAny / ContainsAnyExcept
    // -----------------------------------------------------------------------

    [Test]
    public void ContainsAny_TwoValues_Found_ReturnsTrue()
    {
        ReadOnlySpan<int> span = [5, 6, 7];
        span.ContainsAny(7, 9).Should().BeTrue();
    }

    [Test]
    public void ContainsAny_TwoValues_NotFound_ReturnsFalse()
    {
        ReadOnlySpan<int> span = [5, 6, 7];
        span.ContainsAny(8, 9).Should().BeFalse();
    }

    [Test]
    public void ContainsAny_ThreeValues_Found_ReturnsTrue()
    {
        ReadOnlySpan<int> span = [5, 6, 7];
        span.ContainsAny(8, 9, 6).Should().BeTrue();
    }

    [Test]
    public void ContainsAny_ValuesSpan_Found_ReturnsTrue()
    {
        ReadOnlySpan<int> span = [5, 6, 7];
        ReadOnlySpan<int> values = [8, 9, 10, 7];
        span.ContainsAny(values).Should().BeTrue();
    }

    [Test]
    public void ContainsAnyExcept_SingleValue_AllMatch_ReturnsFalse()
    {
        ReadOnlySpan<int> span = [7, 7, 7];
        span.ContainsAnyExcept(7).Should().BeFalse();
    }

    [Test]
    public void ContainsAnyExcept_SingleValue_OneDifferent_ReturnsTrue()
    {
        ReadOnlySpan<int> span = [7, 8, 7];
        span.ContainsAnyExcept(7).Should().BeTrue();
    }

    [Test]
    public void ContainsAnyExcept_ValuesSpan_AllMatch_ReturnsFalse()
    {
        ReadOnlySpan<int> span = [1, 2, 3, 4];
        ReadOnlySpan<int> values = [1, 2, 3, 4];
        span.ContainsAnyExcept(values).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Count
    // -----------------------------------------------------------------------

    [Test]
    public void Count_SingleValue_Empty_ReturnsZero()
    {
        ReadOnlySpan<int> span = [];
        span.Count(1).Should().Be(0);
    }

    [Test]
    public void Count_SingleValue_NoMatches_ReturnsZero()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        span.Count(7).Should().Be(0);
    }

    [Test]
    public void Count_SingleValue_AllMatch_ReturnsLength()
    {
        ReadOnlySpan<int> span = [7, 7, 7];
        span.Count(7).Should().Be(3);
    }

    [Test]
    public void Count_SingleValue_Mixed_ReturnsCount()
    {
        ReadOnlySpan<int> span = [1, 7, 2, 7, 7, 3];
        span.Count(7).Should().Be(3);
    }

    [Test]
    public void Count_Char_LargeSpan_Works()
    {
        ReadOnlySpan<char> span = new string('a', 1000) + "x" + new string('a', 1000);
        span.Count('a').Should().Be(2000);
        span.Count('x').Should().Be(1);
        span.Count('z').Should().Be(0);
    }

    [Test]
    public void Count_Sequence_Empty_ReturnsZero()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        span.Count([]).Should().Be(0);
    }

    [Test]
    public void Count_Sequence_NonOverlapping_ReturnsCount()
    {
        ReadOnlySpan<int> span = [1, 2, 1, 2, 1, 2, 3];
        ReadOnlySpan<int> value = [1, 2];
        span.Count(value).Should().Be(3);
    }

    [Test]
    public void Count_Sequence_OverlappingMatchesNonOverlapping()
    {
        // "aaaa" contains "aa" 3 times overlapping but only 2 non-overlapping.
        ReadOnlySpan<char> span = "aaaa";
        ReadOnlySpan<char> value = "aa";
        span.Count(value).Should().Be(2);
    }

    // -----------------------------------------------------------------------
    //  CommonPrefixLength
    // -----------------------------------------------------------------------

    [Test]
    public void CommonPrefixLength_BothEmpty_ReturnsZero()
    {
        ReadOnlySpan<int>.Empty.CommonPrefixLength([]).Should().Be(0);
    }

    [Test]
    public void CommonPrefixLength_NoMatch_ReturnsZero()
    {
        ReadOnlySpan<int> a = [1, 2, 3];
        ReadOnlySpan<int> b = [4, 5, 6];
        a.CommonPrefixLength(b).Should().Be(0);
    }

    [Test]
    public void CommonPrefixLength_FullMatch_ReturnsShorterLength()
    {
        ReadOnlySpan<int> a = [1, 2, 3, 4];
        ReadOnlySpan<int> b = [1, 2, 3];
        a.CommonPrefixLength(b).Should().Be(3);
    }

    [Test]
    public void CommonPrefixLength_PartialMatch_ReturnsDivergencePoint()
    {
        ReadOnlySpan<int> a = [1, 2, 3, 4];
        ReadOnlySpan<int> b = [1, 2, 9, 4];
        a.CommonPrefixLength(b).Should().Be(2);
    }

    [Test]
    public void CommonPrefixLength_LargeChar_Works()
    {
        // Exercise the exponential probe path.
        string prefix = new string('a', 5000);
        ReadOnlySpan<char> a = (prefix + "X").AsSpan();
        ReadOnlySpan<char> b = (prefix + "Y").AsSpan();
        a.CommonPrefixLength(b).Should().Be(5000);
    }

    [Test]
    public void CommonPrefixLength_WithComparer_UsesComparer()
    {
        ReadOnlySpan<string> a = ["abc", "DEF"];
        ReadOnlySpan<string> b = ["ABC", "def"];
        a.CommonPrefixLength(b, StringComparer.OrdinalIgnoreCase).Should().Be(2);
        a.CommonPrefixLength(b, StringComparer.Ordinal).Should().Be(0);
    }

    [Test]
    public void CommonPrefixLength_NullComparer_UsesDefault()
    {
        ReadOnlySpan<int> a = [1, 2, 3];
        ReadOnlySpan<int> b = [1, 2, 9];
        a.CommonPrefixLength(b, comparer: null).Should().Be(2);
    }
}
