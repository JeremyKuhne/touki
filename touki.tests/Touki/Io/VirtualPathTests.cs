// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Tests for <see cref="PathSegmentEnumerator"/>.
/// </summary>
public class VirtualPathTests
{
    [Fact]
    public void VirtualPath_SinglePath_ConstructsCorrectly()
    {
        PathSegmentEnumerator path = new(Paths.ChangeAlternateDirectorySeparators("test/path"));

        path.Length.Should().Be(9);
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_TwoPaths_WithoutSeparators_ConstructsCorrectly()
    {
        PathSegmentEnumerator path = new("first", "second");

        path.Length.Should().Be(12); // 5 + 6 + 1 (virtual separator)
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_TwoPaths_FirstEndsWithSeparator_ConstructsCorrectly()
    {
        PathSegmentEnumerator path = new(Paths.ChangeAlternateDirectorySeparators("first/"), "second");

        path.Length.Should().Be(12); // 6 + 6 (no virtual separator needed)
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_TwoPaths_SecondStartsWithSeparator_ConstructsCorrectly()
    {
        PathSegmentEnumerator path = new("first", Paths.ChangeAlternateDirectorySeparators("/second"));

        path.Length.Should().Be(12); // 5 + 7 (no virtual separator needed)
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_TwoPaths_BothHaveSeparators_ConstructsCorrectly()
    {
        PathSegmentEnumerator path = new($"first{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}second");

        path.Length.Should().Be(13); // 6 + 7 (no virtual separator needed)
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_EmptyFirstPath_ConstructsCorrectly()
    {
        PathSegmentEnumerator path = new("", "second");

        path.Length.Should().Be(6);
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_EmptySecondPath_ConstructsCorrectly()
    {
        PathSegmentEnumerator path = new("first", "");

        path.Length.Should().Be(5);
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_BothPathsEmpty_ConstructsCorrectly()
    {
        PathSegmentEnumerator path = new("", "");

        path.Length.Should().Be(0);
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_Indexer_FirstPath_ReturnsCorrectCharacters()
    {
        PathSegmentEnumerator path = new("hello", "world");

        path[0].Should().Be('h');
        path[1].Should().Be('e');
        path[2].Should().Be('l');
        path[3].Should().Be('l');
        path[4].Should().Be('o');
    }

    [Fact]
    public void VirtualPath_Indexer_VirtualSeparator_ReturnsDirectorySeparator()
    {
        PathSegmentEnumerator path = new("hello", "world");

        path[5].Should().Be(Path.DirectorySeparatorChar);
    }

    [Fact]
    public void VirtualPath_Indexer_SecondPath_ReturnsCorrectCharacters()
    {
        PathSegmentEnumerator path = new("hello", "world");

        path[6].Should().Be('w');
        path[7].Should().Be('o');
        path[8].Should().Be('r');
        path[9].Should().Be('l');
        path[10].Should().Be('d');
    }

    [Fact]
    public void VirtualPath_Indexer_WithExistingSeparators_ReturnsCorrectCharacters()
    {
        PathSegmentEnumerator path = new($"hello{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}world");

        path[0].Should().Be('h');
        path[5].Should().Be(Path.DirectorySeparatorChar);
        path[6].Should().Be(Path.DirectorySeparatorChar);
        path[7].Should().Be('w');
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_SingleSegment_ReturnsSegment()
    {
        PathSegmentEnumerator path = new("hello");

        bool result = path.MoveNext();

        result.Should().BeTrue();
        path.Current.ToString().Should().Be("hello");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_NoMoreSegments_ReturnsFalse()
    {
        PathSegmentEnumerator path = new("hello");

        path.MoveNext(); // First call
        bool result = path.MoveNext(); // Second call

        result.Should().BeFalse();
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_MultipleSegments_ReturnsEachSegment()
    {
        PathSegmentEnumerator path = new($"first{Path.DirectorySeparatorChar}second{Path.DirectorySeparatorChar}third");

        bool result1 = path.MoveNext();
        result1.Should().BeTrue();
        path.Current.ToString().Should().Be("first");

        bool result2 = path.MoveNext();
        result2.Should().BeTrue();
        path.Current.ToString().Should().Be("second");

        bool result3 = path.MoveNext();
        result3.Should().BeTrue();
        path.Current.ToString().Should().Be("third");

        bool result4 = path.MoveNext();
        result4.Should().BeFalse();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_TwoPaths_ReturnsAllSegments()
    {
        PathSegmentEnumerator path = new($"first{Path.DirectorySeparatorChar}second", $"third{Path.DirectorySeparatorChar}fourth");

        bool result1 = path.MoveNext();
        result1.Should().BeTrue();
        path.Current.ToString().Should().Be("first");

        bool result2 = path.MoveNext();
        result2.Should().BeTrue();
        path.Current.ToString().Should().Be("second");

        bool result3 = path.MoveNext();
        result3.Should().BeTrue();
        path.Current.ToString().Should().Be("third");

        bool result4 = path.MoveNext();
        result4.Should().BeTrue();
        path.Current.ToString().Should().Be("fourth");

        bool result5 = path.MoveNext();
        result5.Should().BeFalse();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_LeadingSeparators_SkipsSeparators()
    {
        PathSegmentEnumerator path = new($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}first{Path.DirectorySeparatorChar}second");

        bool result1 = path.MoveNext();
        result1.Should().BeTrue();
        path.Current.ToString().Should().Be("first");

        bool result2 = path.MoveNext();
        result2.Should().BeTrue();
        path.Current.ToString().Should().Be("second");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_TrailingSeparators_IgnoresTrailingSeparators()
    {
        PathSegmentEnumerator path = new($"first{Path.DirectorySeparatorChar}second{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}");

        bool result1 = path.MoveNext();
        result1.Should().BeTrue();
        path.Current.ToString().Should().Be("first");

        bool result2 = path.MoveNext();
        result2.Should().BeTrue();
        path.Current.ToString().Should().Be("second");

        bool result3 = path.MoveNext();
        result3.Should().BeFalse();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_ConsecutiveSeparators_SkipsEmptySegments()
    {
        PathSegmentEnumerator path = new($"first{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}second{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}third");

        bool result1 = path.MoveNext();
        result1.Should().BeTrue();
        path.Current.ToString().Should().Be("first");

        bool result2 = path.MoveNext();
        result2.Should().BeTrue();
        path.Current.ToString().Should().Be("second");

        bool result3 = path.MoveNext();
        result3.Should().BeTrue();
        path.Current.ToString().Should().Be("third");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_OnlySeparators_ReturnsFalse()
    {
        PathSegmentEnumerator path = new($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}");

        bool result = path.MoveNext();

        result.Should().BeFalse();
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_EmptyPath_ReturnsFalse()
    {
        PathSegmentEnumerator path = new("");

        bool result = path.MoveNext();

        result.Should().BeFalse();
        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_CrossingBoundary_ReturnsCorrectSegments()
    {
        PathSegmentEnumerator path = new("first", "second");

        bool result1 = path.MoveNext();
        result1.Should().BeTrue();
        path.Current.ToString().Should().Be("first");

        bool result2 = path.MoveNext();
        result2.Should().BeTrue();
        path.Current.ToString().Should().Be("second");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_FirstPathWithTrailingSeparator_HandlesCorrectly()
    {
        PathSegmentEnumerator path = new($"first{Path.DirectorySeparatorChar}", "second");

        bool result1 = path.MoveNext();
        result1.Should().BeTrue();
        path.Current.ToString().Should().Be("first");

        bool result2 = path.MoveNext();
        result2.Should().BeTrue();
        path.Current.ToString().Should().Be("second");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_SecondPathWithLeadingSeparator_HandlesCorrectly()
    {
        PathSegmentEnumerator path = new("first", $"{Path.DirectorySeparatorChar}second");

        bool result1 = path.MoveNext();
        result1.Should().BeTrue();
        path.Current.ToString().Should().Be("first");

        bool result2 = path.MoveNext();
        result2.Should().BeTrue();
        path.Current.ToString().Should().Be("second");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_SegmentSpanningBoundary_HandlesCorrectly()
    {
        // This tests when a segment would span across the virtual boundary
        PathSegmentEnumerator path = new("path", "segment");

        bool result1 = path.MoveNext();
        result1.Should().BeTrue();
        path.Current.ToString().Should().Be("path");

        bool result2 = path.MoveNext();
        result2.Should().BeTrue();
        path.Current.ToString().Should().Be("segment");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_ComplexPath_ReturnsAllSegments()
    {
        PathSegmentEnumerator path = new(
            Paths.ChangeAlternateDirectorySeparators("a/b/c"),
            Paths.ChangeAlternateDirectorySeparators("d/e/f"));

        List<string> segments = [];
        while (path.MoveNext())
        {
            segments.Add(path.Current.ToString());
        }

        segments.Should().BeEquivalentTo(["a", "b", "c", "d", "e", "f"]);
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_OnlyFirstPath_ReturnsFirstPathSegments()
    {
        PathSegmentEnumerator path = new(Paths.ChangeAlternateDirectorySeparators("first/second/third"), "");

        List<string> segments = [];
        while (path.MoveNext())
        {
            segments.Add(path.Current.ToString());
        }

        segments.Should().BeEquivalentTo(["first", "second", "third"]);
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_OnlySecondPath_ReturnsSecondPathSegments()
    {
        PathSegmentEnumerator path = new("", Paths.ChangeAlternateDirectorySeparators("first/second/third"));

        List<string> segments = [];
        while (path.MoveNext())
        {
            segments.Add(path.Current.ToString());
        }

        segments.Should().BeEquivalentTo(["first", "second", "third"]);
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_SingleCharacterSegments_HandlesCorrectly()
    {
        PathSegmentEnumerator path = new(
            Paths.ChangeAlternateDirectorySeparators($"a/b"),
            Paths.ChangeAlternateDirectorySeparators($"c/d"));

        List<string> segments = [];
        while (path.MoveNext())
        {
            segments.Add(path.Current.ToString());
        }

        segments.Should().BeEquivalentTo(["a", "b", "c", "d"]);
    }

    [Theory]
    [InlineData("path", "", "path")]
    [InlineData("", "path", "path")]
    [InlineData("first", "second", "first", "second")]
    public void VirtualPath_MoveNextSegment_VariousInputs_ReturnsExpectedSegments(string firstPath, string secondPath, params string[] expectedSegments)
    {
        PathSegmentEnumerator path = new(firstPath.AsSpan(), secondPath.AsSpan());

        List<string> actualSegments = [];
        while (path.MoveNext())
        {
            actualSegments.Add(path.Current.ToString());
        }

        actualSegments.Should().BeEquivalentTo(expectedSegments);
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_VariousInputsWithSeparators_ReturnsExpectedSegments()
    {
        // Test cases with separators using platform-specific separator
        string sep = Path.DirectorySeparatorChar.ToString();

        // Test "a/b", "c/d" -> ["a", "b", "c", "d"]
        PathSegmentEnumerator path1 = new($"a{sep}b", $"c{sep}d");
        List<string> segments1 = [];
        while (path1.MoveNext())
        {
            segments1.Add(path1.Current.ToString());
        }

        segments1.Should().BeEquivalentTo(["a", "b", "c", "d"]);

        // Test "///a///", "///b///" -> ["a", "b"]
        PathSegmentEnumerator path2 = new($"{sep}{sep}{sep}a{sep}{sep}{sep}", $"{sep}{sep}{sep}b{sep}{sep}{sep}");
        List<string> segments2 = [];
        while (path2.MoveNext())
        {
            segments2.Add(path2.Current.ToString());
        }
        segments2.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void VirtualPath_CurrentSegment_BeforeFirstMove_ReturnsEmpty()
    {
        PathSegmentEnumerator path = new($"test{Path.DirectorySeparatorChar}path");

        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_CurrentSegment_AfterLastMove_ReturnsEmpty()
    {
        PathSegmentEnumerator path = new("test");

        path.MoveNext();
        path.MoveNext(); // This should return false

        path.Current.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_RecursiveCall_HandlesCorrectly()
    {
        // Test the recursive call for separator skipping
        PathSegmentEnumerator path = new($"{Path.DirectorySeparatorChar}test");

        bool result = path.MoveNext();

        result.Should().BeTrue();
        path.Current.ToString().Should().Be("test");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_MultipleRecursiveCalls_HandlesCorrectly()
    {
        // Test multiple recursive calls for consecutive separators
        PathSegmentEnumerator path = new(Paths.ChangeAlternateDirectorySeparators("////test"));

        bool result = path.MoveNext();

        result.Should().BeTrue();
        path.Current.ToString().Should().Be("test");
    }

    [Fact]
    public void VirtualPath_Documentation_Example()
    {
        // Example from documentation: combining two path segments
        PathSegmentEnumerator path = new(
            Paths.ChangeAlternateDirectorySeparators("src/main"),
            Paths.ChangeAlternateDirectorySeparators("components/Button.cs"));

        List<string> segments = [];
        while (path.MoveNext())
        {
            segments.Add(path.Current.ToString());
        }

        segments.Should().BeEquivalentTo(["src", "main", "components", "Button.cs"]);
    }
}
