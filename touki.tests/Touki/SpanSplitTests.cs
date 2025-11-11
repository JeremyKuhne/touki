// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class SpanSplitTests
{
    [Fact]
    public void Split_WithSingleCharSeparator_EmptySource_ReturnsEmptySegment()
    {
        ReadOnlySpan<char> source = "";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(0..0);
        source[enumerator.Current].ToString().Should().BeEmpty();
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSingleCharSeparator_NoSeparatorFound_ReturnsWholeSource()
    {
        ReadOnlySpan<char> source = "hello";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(0..5);
        source[enumerator.Current].ToString().Should().Be("hello");
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSingleCharSeparator_OneSeparator_ReturnsTwoSegments()
    {
        ReadOnlySpan<char> source = "hello,world";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("hello");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("world");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSingleCharSeparator_MultipleSeparators_ReturnsMultipleSegments()
    {
        ReadOnlySpan<char> source = "a,b,c,d";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("b");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("c");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("d");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSingleCharSeparator_ConsecutiveSeparators_ReturnsEmptySegments()
    {
        ReadOnlySpan<char> source = "a,,b";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("b");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSingleCharSeparator_LeadingSeparator_ReturnsEmptyFirstSegment()
    {
        ReadOnlySpan<char> source = ",hello";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("hello");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSingleCharSeparator_TrailingSeparator_ReturnsEmptyLastSegment()
    {
        ReadOnlySpan<char> source = "hello,";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("hello");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSingleCharSeparator_OnlySeparator_ReturnsTwoEmptySegments()
    {
        ReadOnlySpan<char> source = ",";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSpanSeparator_EmptySource_ReturnsEmptySegment()
    {
        ReadOnlySpan<char> source = "";
        ReadOnlySpan<char> separator = "::";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(0..0);
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSpanSeparator_NoSeparatorFound_ReturnsWholeSource()
    {
        ReadOnlySpan<char> source = "hello world";
        ReadOnlySpan<char> separator = "::";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("hello world");
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSpanSeparator_OneSeparator_ReturnsTwoSegments()
    {
        ReadOnlySpan<char> source = "hello::world";
        ReadOnlySpan<char> separator = "::";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("hello");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("world");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSpanSeparator_MultipleSeparators_ReturnsMultipleSegments()
    {
        ReadOnlySpan<char> source = "a::b::c";
        ReadOnlySpan<char> separator = "::";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("b");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("c");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithSpanSeparator_ConsecutiveSeparators_ReturnsEmptySegment()
    {
        ReadOnlySpan<char> source = "a::::b";
        ReadOnlySpan<char> separator = "::";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("b");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithEmptySeparator_ReturnsWholeSource()
    {
        ReadOnlySpan<char> source = "hello";
        ReadOnlySpan<char> separator = "";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("hello");
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithEmptySeparator_EmptySource_ReturnsEmptySegment()
    {
        ReadOnlySpan<char> source = "";
        ReadOnlySpan<char> separator = "";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void SplitAny_WithMultipleSeparators_SplitsOnAny()
    {
        ReadOnlySpan<char> source = "a,b;c:d";
        ReadOnlySpan<char> separators = ",;:";
        var enumerator = source.SplitAny(separators);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("b");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("c");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("d");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void SplitAny_WithSingleSeparator_Works()
    {
        ReadOnlySpan<char> source = "a,b,c";
        ReadOnlySpan<char> separators = ",";
        var enumerator = source.SplitAny(separators);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("b");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("c");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void SplitAny_WithNoSeparatorsProvided_UsesWhitespace()
    {
        ReadOnlySpan<char> source = "hello world\ttab\nnewline";
        ReadOnlySpan<char> separators = "";
        var enumerator = source.SplitAny(separators);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("hello");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("world");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("tab");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("newline");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void SplitAny_WithNoSeparatorsProvided_HandlesVariousWhitespace()
    {
        // Test various Unicode whitespace characters
        ReadOnlySpan<char> source = "a\u0020b\u00a0c\u1680d\u2000e";
        ReadOnlySpan<char> separators = "";
        var enumerator = source.SplitAny(separators);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("b");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("c");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("d");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("e");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void SplitAny_WithNoSeparatorsProvided_EmptySource_ReturnsEmptySegment()
    {
        ReadOnlySpan<char> source = "";
        ReadOnlySpan<char> separators = "";
        var enumerator = source.SplitAny(separators);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void SplitAny_WithNoSeparatorsProvided_OnlyWhitespace_ReturnsEmptySegments()
    {
        ReadOnlySpan<char> source = " \t ";
        ReadOnlySpan<char> separators = "";
        var enumerator = source.SplitAny(separators);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void SplitAny_WithNoSeparatorsFound_ReturnsWholeSource()
    {
        ReadOnlySpan<char> source = "hello";
        ReadOnlySpan<char> separators = ",;:";
        var enumerator = source.SplitAny(separators);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("hello");
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithIntSpan_Works()
    {
        ReadOnlySpan<int> source = [1, 2, 0, 3, 4, 0, 5];
        var enumerator = source.Split(0);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([1, 2]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([3, 4]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([5]);

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_GetEnumerator_ReturnsItself()
    {
        ReadOnlySpan<char> source = "a,b";
        var enumerator = source.Split(',');
        var enumerator2 = enumerator.GetEnumerator();

        // Both should iterate independently
        enumerator.MoveNext().Should().BeTrue();
        enumerator2.MoveNext().Should().BeTrue();
    }

    [Fact]
    public void Split_SourceProperty_ReturnsOriginalSource()
    {
        ReadOnlySpan<char> source = "hello,world";
        var enumerator = source.Split(',');

        enumerator.Source.ToString().Should().Be("hello,world");
    }

    [Fact]
    public void Split_CurrentProperty_ReturnsCorrectRange()
    {
        ReadOnlySpan<char> source = "hello,world";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(0..5);
        enumerator.Current.Start.Value.Should().Be(0);
        enumerator.Current.End.Value.Should().Be(5);

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(6..11);
        enumerator.Current.Start.Value.Should().Be(6);
        enumerator.Current.End.Value.Should().Be(11);
    }

    [Fact]
    public void Split_MultipleMoveNextAfterEnd_ReturnsFalse()
    {
        ReadOnlySpan<char> source = "a";
        var enumerator = source.Split(',');

        enumerator.MoveNext().Should().BeTrue();
        enumerator.MoveNext().Should().BeFalse();
        enumerator.MoveNext().Should().BeFalse();
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_ForeachPattern_Works()
    {
        ReadOnlySpan<char> source = "a,b,c";
        List<string> segments = [];

        foreach (Range range in source.Split(','))
        {
            segments.Add(source[range].ToString());
        }

        segments.Should().Equal(["a", "b", "c"]);
    }

    [Fact]
    public void SplitAny_ForeachPattern_WithWhitespace_Works()
    {
        ReadOnlySpan<char> source = "hello world\ttab";
        List<string> segments = [];

        foreach (Range range in source.SplitAny(""))
        {
            segments.Add(source[range].ToString());
        }

        segments.Should().Equal(["hello", "world", "tab"]);
    }

    [Fact]
    public void Split_WithSpanSeparator_LeadingAndTrailing_Works()
    {
        ReadOnlySpan<char> source = "::a::b::";
        ReadOnlySpan<char> separator = "::";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("b");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void SplitAny_ConsecutiveDifferentSeparators_ReturnsEmptySegments()
    {
        ReadOnlySpan<char> source = "a,;b";
        ReadOnlySpan<char> separators = ",;";
        var enumerator = source.SplitAny(separators);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("b");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithLongSpanSeparator_Works()
    {
        ReadOnlySpan<char> source = "start<separator>middle<separator>end";
        ReadOnlySpan<char> separator = "<separator>";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("start");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("middle");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("end");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_PartialMatchNotTreatedAsSeparator_Works()
    {
        ReadOnlySpan<char> source = "a:b::c";
        ReadOnlySpan<char> separator = "::";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("a:b");

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToString().Should().Be("c");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithStringSeparator_NoSeparatorFound_ReturnsWholeSource()
    {
        ReadOnlySpan<string> source = ["alpha", "beta", "gamma"];
        string separator = "|";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal(["alpha", "beta", "gamma"]);
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithStringSeparator_MultipleSeparators_ReturnsSegments()
    {
        ReadOnlySpan<string> source = ["a", "|", "b", "|", "c"];
        string separator = "|";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal(["a"]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal(["b"]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal(["c"]);

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithStringSeparator_LeadingAndTrailingSeparator_ReturnsEmptySegments()
    {
        ReadOnlySpan<string> source = ["|", "a", "|"];
        string separator = "|";
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().BeEmpty();

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal(["a"]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().BeEmpty();

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithStringSpanSeparator_SequenceMatch_SplitsCorrectly()
    {
        ReadOnlySpan<string> source = ["a", "<", "sep", ">", "b", "<", "sep", ">", "c"];
        ReadOnlySpan<string> separator = ["<", "sep", ">"];
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal(["a"]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal(["b"]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal(["c"]);

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithStringSpanSeparator_PartialMatchNotSeparator()
    {
        ReadOnlySpan<string> source = ["a", "<", "sep", "b", ">", "c"];
        ReadOnlySpan<string> separator = ["<", "sep", ">"];
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal(["a", "<", "sep", "b", ">", "c"]);
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithIntSequenceSeparator_SplitsOnSequence()
    {
        ReadOnlySpan<int> source = [1, 0, 0, 2, 3, 0, 0, 4];
        ReadOnlySpan<int> separator = [0, 0];
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([1]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([2, 3]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([4]);

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void SplitAny_WithIntSeparators_SplitsOnEither()
    {
        ReadOnlySpan<int> source = [1, 0, 2, 9, 3];
        ReadOnlySpan<int> separators = [0, 9];
        var enumerator = source.SplitAny(separators);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([1]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([2]);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([3]);

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Split_WithIntEmptySeparator_ReturnsWholeSource()
    {
        ReadOnlySpan<int> source = [1, 2, 3];
        ReadOnlySpan<int> separator = [];
        var enumerator = source.Split(separator);

        enumerator.MoveNext().Should().BeTrue();
        source[enumerator.Current].ToArray().Should().Equal([1, 2, 3]);
        enumerator.MoveNext().Should().BeFalse();
    }
}
