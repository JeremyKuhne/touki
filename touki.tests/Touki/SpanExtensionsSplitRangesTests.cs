// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class SpanExtensionsSplitRangesTests
{
    private static string[] Materialize(ReadOnlySpan<char> source, Span<Range> ranges, int count)
    {
        string[] result = new string[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = source[ranges[i]].ToString();
        }
        return result;
    }

    // -----------------------------------------------------------------------
    //  Split(Span<Range>, char, …)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Split_Char_Basic()
    {
        ReadOnlySpan<char> source = "a,b,c";
        Span<Range> ranges = new Range[8];
        int count = source.Split(ranges, ',');
        count.Should().Be(3);
        Materialize(source, ranges, count).Should().Equal(["a", "b", "c"]);
    }

    [TestMethod]
    public void Split_Char_Empty_ReturnsSingleEmptyRange()
    {
        ReadOnlySpan<char> source = "";
        Span<Range> ranges = new Range[4];
        int count = source.Split(ranges, ',');
        count.Should().Be(1);
        source[ranges[0]].Length.Should().Be(0);
    }

    [TestMethod]
    public void Split_Char_NoSeparator_ReturnsWholeSource()
    {
        ReadOnlySpan<char> source = "hello";
        Span<Range> ranges = new Range[4];
        int count = source.Split(ranges, ',');
        count.Should().Be(1);
        source[ranges[0]].ToString().Should().Be("hello");
    }

    [TestMethod]
    public void Split_Char_LeadingSeparator_ProducesEmptyFirst()
    {
        ReadOnlySpan<char> source = ",a,b";
        Span<Range> ranges = new Range[4];
        int count = source.Split(ranges, ',');
        count.Should().Be(3);
        Materialize(source, ranges, count).Should().Equal(["", "a", "b"]);
    }

    [TestMethod]
    public void Split_Char_TrailingSeparator_ProducesEmptyLast()
    {
        ReadOnlySpan<char> source = "a,b,";
        Span<Range> ranges = new Range[4];
        int count = source.Split(ranges, ',');
        count.Should().Be(3);
        Materialize(source, ranges, count).Should().Equal(["a", "b", ""]);
    }

    [TestMethod]
    public void Split_Char_DestinationFull_LastRangeContainsRest()
    {
        ReadOnlySpan<char> source = "a,b,c,d,e";
        Span<Range> ranges = new Range[3];
        int count = source.Split(ranges, ',');
        count.Should().Be(3);
        Materialize(source, ranges, count).Should().Equal(["a", "b", "c,d,e"]);
    }

    [TestMethod]
    public void Split_Char_RemoveEmptyEntries()
    {
        ReadOnlySpan<char> source = "a,,b,,c";
        Span<Range> ranges = new Range[8];
        int count = source.Split(ranges, ',', StringSplitOptions.RemoveEmptyEntries);
        count.Should().Be(3);
        Materialize(source, ranges, count).Should().Equal(["a", "b", "c"]);
    }

    [TestMethod]
    public void Split_Char_DestinationEmpty_ReturnsZero()
    {
        ReadOnlySpan<char> source = "a,b";
        Span<Range> ranges = [];
        int count = source.Split(ranges, ',');
        count.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    //  Split(Span<Range>, ROS<char>, …)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Split_StringSeparator_Basic()
    {
        ReadOnlySpan<char> source = "a--b--c";
        Span<Range> ranges = new Range[4];
        int count = source.Split(ranges, "--".AsSpan());
        count.Should().Be(3);
        Materialize(source, ranges, count).Should().Equal(["a", "b", "c"]);
    }

    [TestMethod]
    public void Split_StringSeparator_Empty_ReturnsWholeSource()
    {
        ReadOnlySpan<char> source = "abc";
        Span<Range> ranges = new Range[4];
        int count = source.Split(ranges, []);
        count.Should().Be(1);
        source[ranges[0]].ToString().Should().Be("abc");
    }

    // -----------------------------------------------------------------------
    //  SplitAny(Span<Range>, ROS<char>, …)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void SplitAny_Char_Basic()
    {
        ReadOnlySpan<char> source = "a,b;c.d";
        Span<Range> ranges = new Range[8];
        int count = source.SplitAny(ranges, ",;.".AsSpan());
        count.Should().Be(4);
        Materialize(source, ranges, count).Should().Equal(["a", "b", "c", "d"]);
    }

    [TestMethod]
    public void SplitAny_EmptySeparators_FallsBackToWhitespace()
    {
        ReadOnlySpan<char> source = "a b\tc\nd";
        Span<Range> ranges = new Range[8];
        int count = source.SplitAny(ranges, ReadOnlySpan<char>.Empty);
        count.Should().Be(4);
        Materialize(source, ranges, count).Should().Equal(["a", "b", "c", "d"]);
    }

    [TestMethod]
    public void SplitAny_DestinationFull_LastRangeContainsRest()
    {
        ReadOnlySpan<char> source = "a,b;c.d";
        Span<Range> ranges = new Range[2];
        int count = source.SplitAny(ranges, ",;.".AsSpan());
        count.Should().Be(2);
        Materialize(source, ranges, count).Should().Equal(["a", "b;c.d"]);
    }

    [TestMethod]
    public void SplitAny_RemoveEmptyEntries()
    {
        ReadOnlySpan<char> source = "a,;b,,c";
        Span<Range> ranges = new Range[8];
        int count = source.SplitAny(ranges, ",;".AsSpan(), StringSplitOptions.RemoveEmptyEntries);
        count.Should().Be(3);
        Materialize(source, ranges, count).Should().Equal(["a", "b", "c"]);
    }

    // -----------------------------------------------------------------------
    //  SplitAny(Span<Range>, ROS<string>, …)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void SplitAny_StringSeparators_Basic()
    {
        ReadOnlySpan<char> source = "a--b//c--d";
        Span<Range> ranges = new Range[8];
        int count = source.SplitAny(ranges, new[] { "--", "//" }.AsSpan());
        count.Should().Be(4);
        Materialize(source, ranges, count).Should().Equal(["a", "b", "c", "d"]);
    }

    [TestMethod]
    public void SplitAny_StringSeparators_AllNullOrEmpty_ReturnsWholeSource()
    {
        ReadOnlySpan<char> source = "a b c";
        Span<Range> ranges = new Range[8];
        int count = source.SplitAny(ranges, new string?[] { null, "" }!.AsSpan()!);
        count.Should().Be(1);
        source[ranges[0]].ToString().Should().Be("a b c");
    }

    [TestMethod]
    public void SplitAny_StringSeparators_RemoveEmptyEntries()
    {
        ReadOnlySpan<char> source = "a--b----c";
        Span<Range> ranges = new Range[8];
        int count = source.SplitAny(ranges, new[] { "--" }.AsSpan(), StringSplitOptions.RemoveEmptyEntries);
        count.Should().Be(3);
        Materialize(source, ranges, count).Should().Equal(["a", "b", "c"]);
    }

    // -----------------------------------------------------------------------
    //  Validation
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Split_InvalidOptions_Throws()
    {
        Action act = static () =>
        {
            ReadOnlySpan<char> source = "a,b";
            Span<Range> ranges = new Range[4];
            source.Split(ranges, ',', (StringSplitOptions)0x100);
        };
        act.Should().Throw<ArgumentException>();
    }

    // ---- Branch-coverage tests targeting TryWriteSegment / WriteWholeSource ----
    // Some test cases use (StringSplitOptions)2 because StringSplitOptions.TrimEntries
    // is .NET 5+ only and not present on net481's System.Memory polyfill. The
    // implementation honors the integer flag at runtime regardless.

    [TestMethod]
    public void Split_Char_TrimEntries_TrimsLeadingAndTrailingWhitespace()
    {
        ReadOnlySpan<char> source = "  apple , banana ,  cherry  ";
        Span<Range> ranges = new Range[8];
        int count = source.Split(ranges, ',', (StringSplitOptions)2);
        count.Should().Be(3);
        source[ranges[0]].ToString().Should().Be("apple");
        source[ranges[1]].ToString().Should().Be("banana");
        source[ranges[2]].ToString().Should().Be("cherry");
    }

    [TestMethod]
    public void Split_Char_TrimEntries_AllWhitespace_DropsWhenRemoveEmpty()
    {
        ReadOnlySpan<char> source = "   ,   ,   ";
        Span<Range> ranges = new Range[8];
        int count = source.Split(ranges, ',', (StringSplitOptions)2 | StringSplitOptions.RemoveEmptyEntries);
        count.Should().Be(0);
    }

    [TestMethod]
    public void Split_Char_TrimEntries_AllWhitespace_KeepsEmptyEntries()
    {
        ReadOnlySpan<char> source = "   ,   ,   ";
        Span<Range> ranges = new Range[8];
        int count = source.Split(ranges, ',', (StringSplitOptions)2);
        count.Should().Be(3);
        source[ranges[0]].Length.Should().Be(0);
        source[ranges[1]].Length.Should().Be(0);
        source[ranges[2]].Length.Should().Be(0);
    }

    [TestMethod]
    public void Split_OnAnyWhiteSpace_RemoveEmpty_DropsEmptySegments()
    {
        ReadOnlySpan<char> source = "  apple   banana   cherry  ";
        Span<Range> ranges = new Range[8];
        int count = source.SplitAny(ranges, ReadOnlySpan<char>.Empty, StringSplitOptions.RemoveEmptyEntries);
        count.Should().Be(3);
    }

    [TestMethod]
    public void Split_OnAnyWhiteSpace_TrimEntries_TrimsAndKeeps()
    {
        ReadOnlySpan<char> source = "  apple   banana   cherry  ";
        Span<Range> ranges = new Range[16];
        int count = source.SplitAny(
            ranges,
            ReadOnlySpan<char>.Empty,
            (StringSplitOptions)2 | StringSplitOptions.RemoveEmptyEntries);
        count.Should().Be(3);
        source[ranges[0]].ToString().Should().Be("apple");
        source[ranges[1]].ToString().Should().Be("banana");
        source[ranges[2]].ToString().Should().Be("cherry");
    }

    [TestMethod]
    public void Split_WholeSource_TrimEntries_Trims()
    {
        ReadOnlySpan<char> source = "  hello  ";
        Span<Range> ranges = new Range[4];
        int count = source.Split(ranges, '!', (StringSplitOptions)2);
        count.Should().Be(1);
        source[ranges[0]].ToString().Should().Be("hello");
    }

    [TestMethod]
    public void Split_WholeSource_TrimEntries_AllWhitespace_RemoveEmpty_Empty()
    {
        ReadOnlySpan<char> source = "    ";
        Span<Range> ranges = new Range[4];
        int count = source.Split(ranges, '!', (StringSplitOptions)2 | StringSplitOptions.RemoveEmptyEntries);
        count.Should().Be(0);
    }
}
