// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Tests for <see cref="VirtualPath"/>.
/// </summary>
public class VirtualPathTests
{
    [Fact]
    public void VirtualPath_SinglePath_ConstructsCorrectly()
    {
        VirtualPath path = new($"test{Path.DirectorySeparatorChar}path".AsSpan());

        path.Length.Should().Be(9);
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_TwoPaths_WithoutSeparators_ConstructsCorrectly()
    {
        VirtualPath path = new("first".AsSpan(), "second".AsSpan());

        path.Length.Should().Be(12); // 5 + 6 + 1 (virtual separator)
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_TwoPaths_FirstEndsWithSeparator_ConstructsCorrectly()
    {
        VirtualPath path = new($"first{Path.DirectorySeparatorChar}".AsSpan(), "second".AsSpan());

        path.Length.Should().Be(12); // 6 + 6 (no virtual separator needed)
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_TwoPaths_SecondStartsWithSeparator_ConstructsCorrectly()
    {
        VirtualPath path = new("first".AsSpan(), $"{Path.DirectorySeparatorChar}second".AsSpan());

        path.Length.Should().Be(12); // 5 + 7 (no virtual separator needed)
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_TwoPaths_BothHaveSeparators_ConstructsCorrectly()
    {
        VirtualPath path = new($"first{Path.DirectorySeparatorChar}".AsSpan(), $"{Path.DirectorySeparatorChar}second".AsSpan());

        path.Length.Should().Be(13); // 6 + 7 (no virtual separator needed)
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_EmptyFirstPath_ConstructsCorrectly()
    {
        VirtualPath path = new("".AsSpan(), "second".AsSpan());

        path.Length.Should().Be(6);
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_EmptySecondPath_ConstructsCorrectly()
    {
        VirtualPath path = new("first".AsSpan(), "".AsSpan());

        path.Length.Should().Be(5);
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_BothPathsEmpty_ConstructsCorrectly()
    {
        VirtualPath path = new("".AsSpan(), "".AsSpan());

        path.Length.Should().Be(0);
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_Indexer_FirstPath_ReturnsCorrectCharacters()
    {
        VirtualPath path = new("hello".AsSpan(), "world".AsSpan());

        path[0].Should().Be('h');
        path[1].Should().Be('e');
        path[2].Should().Be('l');
        path[3].Should().Be('l');
        path[4].Should().Be('o');
    }

    [Fact]
    public void VirtualPath_Indexer_VirtualSeparator_ReturnsDirectorySeparator()
    {
        VirtualPath path = new("hello".AsSpan(), "world".AsSpan());

        path[5].Should().Be(Path.DirectorySeparatorChar);
    }

    [Fact]
    public void VirtualPath_Indexer_SecondPath_ReturnsCorrectCharacters()
    {
        VirtualPath path = new("hello".AsSpan(), "world".AsSpan());

        path[6].Should().Be('w');
        path[7].Should().Be('o');
        path[8].Should().Be('r');
        path[9].Should().Be('l');
        path[10].Should().Be('d');
    }

    [Fact]
    public void VirtualPath_Indexer_WithExistingSeparators_ReturnsCorrectCharacters()
    {
        VirtualPath path = new($"hello{Path.DirectorySeparatorChar}".AsSpan(), $"{Path.DirectorySeparatorChar}world".AsSpan());

        path[0].Should().Be('h');
        path[5].Should().Be(Path.DirectorySeparatorChar);
        path[6].Should().Be(Path.DirectorySeparatorChar);
        path[7].Should().Be('w');
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_SingleSegment_ReturnsSegment()
    {
        VirtualPath path = new("hello".AsSpan());

        bool result = path.MoveNextSegment();

        result.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("hello");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_NoMoreSegments_ReturnsFalse()
    {
        VirtualPath path = new("hello".AsSpan());

        path.MoveNextSegment(); // First call
        bool result = path.MoveNextSegment(); // Second call

        result.Should().BeFalse();
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_MultipleSegments_ReturnsEachSegment()
    {
        VirtualPath path = new($"first{Path.DirectorySeparatorChar}second{Path.DirectorySeparatorChar}third".AsSpan());

        bool result1 = path.MoveNextSegment();
        result1.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("first");

        bool result2 = path.MoveNextSegment();
        result2.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("second");

        bool result3 = path.MoveNextSegment();
        result3.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("third");

        bool result4 = path.MoveNextSegment();
        result4.Should().BeFalse();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_TwoPaths_ReturnsAllSegments()
    {
        VirtualPath path = new($"first{Path.DirectorySeparatorChar}second".AsSpan(), $"third{Path.DirectorySeparatorChar}fourth".AsSpan());

        bool result1 = path.MoveNextSegment();
        result1.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("first");

        bool result2 = path.MoveNextSegment();
        result2.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("second");

        bool result3 = path.MoveNextSegment();
        result3.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("third");

        bool result4 = path.MoveNextSegment();
        result4.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("fourth");

        bool result5 = path.MoveNextSegment();
        result5.Should().BeFalse();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_LeadingSeparators_SkipsSeparators()
    {
        VirtualPath path = new($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}first{Path.DirectorySeparatorChar}second".AsSpan());

        bool result1 = path.MoveNextSegment();
        result1.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("first");

        bool result2 = path.MoveNextSegment();
        result2.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("second");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_TrailingSeparators_IgnoresTrailingSeparators()
    {
        VirtualPath path = new($"first{Path.DirectorySeparatorChar}second{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}".AsSpan());

        bool result1 = path.MoveNextSegment();
        result1.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("first");

        bool result2 = path.MoveNextSegment();
        result2.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("second");

        bool result3 = path.MoveNextSegment();
        result3.Should().BeFalse();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_ConsecutiveSeparators_SkipsEmptySegments()
    {
        VirtualPath path = new($"first{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}second{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}third".AsSpan());

        bool result1 = path.MoveNextSegment();
        result1.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("first");

        bool result2 = path.MoveNextSegment();
        result2.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("second");

        bool result3 = path.MoveNextSegment();
        result3.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("third");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_OnlySeparators_ReturnsFalse()
    {
        VirtualPath path = new($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}".AsSpan());

        bool result = path.MoveNextSegment();

        result.Should().BeFalse();
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_EmptyPath_ReturnsFalse()
    {
        VirtualPath path = new("".AsSpan());

        bool result = path.MoveNextSegment();

        result.Should().BeFalse();
        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_CrossingBoundary_ReturnsCorrectSegments()
    {
        VirtualPath path = new("first".AsSpan(), "second".AsSpan());

        bool result1 = path.MoveNextSegment();
        result1.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("first");

        bool result2 = path.MoveNextSegment();
        result2.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("second");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_FirstPathWithTrailingSeparator_HandlesCorrectly()
    {
        VirtualPath path = new($"first{Path.DirectorySeparatorChar}".AsSpan(), "second".AsSpan());

        bool result1 = path.MoveNextSegment();
        result1.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("first");

        bool result2 = path.MoveNextSegment();
        result2.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("second");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_SecondPathWithLeadingSeparator_HandlesCorrectly()
    {
        VirtualPath path = new("first".AsSpan(), $"{Path.DirectorySeparatorChar}second".AsSpan());

        bool result1 = path.MoveNextSegment();
        result1.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("first");

        bool result2 = path.MoveNextSegment();
        result2.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("second");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_SegmentSpanningBoundary_HandlesCorrectly()
    {
        // This tests when a segment would span across the virtual boundary
        VirtualPath path = new("path".AsSpan(), "segment".AsSpan());

        bool result1 = path.MoveNextSegment();
        result1.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("path");

        bool result2 = path.MoveNextSegment();
        result2.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("segment");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_ComplexPath_ReturnsAllSegments()
    {
        VirtualPath path = new($"a{Path.DirectorySeparatorChar}b{Path.DirectorySeparatorChar}c".AsSpan(), $"d{Path.DirectorySeparatorChar}e{Path.DirectorySeparatorChar}f".AsSpan());

        List<string> segments = [];
        while (path.MoveNextSegment())
        {
            segments.Add(path.CurrentSegment.ToString());
        }

        segments.Should().BeEquivalentTo(["a", "b", "c", "d", "e", "f"]);
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_OnlyFirstPath_ReturnsFirstPathSegments()
    {
        VirtualPath path = new($"first{Path.DirectorySeparatorChar}second{Path.DirectorySeparatorChar}third".AsSpan(), "".AsSpan());

        List<string> segments = [];
        while (path.MoveNextSegment())
        {
            segments.Add(path.CurrentSegment.ToString());
        }

        segments.Should().BeEquivalentTo(["first", "second", "third"]);
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_OnlySecondPath_ReturnsSecondPathSegments()
    {
        VirtualPath path = new("".AsSpan(), $"first{Path.DirectorySeparatorChar}second{Path.DirectorySeparatorChar}third".AsSpan());

        List<string> segments = [];
        while (path.MoveNextSegment())
        {
            segments.Add(path.CurrentSegment.ToString());
        }

        segments.Should().BeEquivalentTo(["first", "second", "third"]);
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_SingleCharacterSegments_HandlesCorrectly()
    {
        VirtualPath path = new($"a{Path.DirectorySeparatorChar}b".AsSpan(), $"c{Path.DirectorySeparatorChar}d".AsSpan());

        List<string> segments = [];
        while (path.MoveNextSegment())
        {
            segments.Add(path.CurrentSegment.ToString());
        }

        segments.Should().BeEquivalentTo(["a", "b", "c", "d"]);
    }

    [Theory]
    [InlineData("path", "", "path")]
    [InlineData("", "path", "path")]
    [InlineData("first", "second", "first", "second")]
    public void VirtualPath_MoveNextSegment_VariousInputs_ReturnsExpectedSegments(string firstPath, string secondPath, params string[] expectedSegments)
    {
        VirtualPath path = new(firstPath.AsSpan(), secondPath.AsSpan());

        List<string> actualSegments = [];
        while (path.MoveNextSegment())
        {
            actualSegments.Add(path.CurrentSegment.ToString());
        }

        actualSegments.Should().BeEquivalentTo(expectedSegments);
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_VariousInputsWithSeparators_ReturnsExpectedSegments()
    {
        // Test cases with separators using platform-specific separator
        string sep = Path.DirectorySeparatorChar.ToString();

        // Test "a/b", "c/d" -> ["a", "b", "c", "d"]
        VirtualPath path1 = new($"a{sep}b".AsSpan(), $"c{sep}d".AsSpan());
        List<string> segments1 = [];
        while (path1.MoveNextSegment())
        {
            segments1.Add(path1.CurrentSegment.ToString());
        }

        segments1.Should().BeEquivalentTo(["a", "b", "c", "d"]);

        // Test "///a///", "///b///" -> ["a", "b"]
        VirtualPath path2 = new($"{sep}{sep}{sep}a{sep}{sep}{sep}".AsSpan(), $"{sep}{sep}{sep}b{sep}{sep}{sep}".AsSpan());
        List<string> segments2 = [];
        while (path2.MoveNextSegment())
        {
            segments2.Add(path2.CurrentSegment.ToString());
        }
        segments2.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void VirtualPath_CurrentSegment_BeforeFirstMove_ReturnsEmpty()
    {
        VirtualPath path = new($"test{Path.DirectorySeparatorChar}path".AsSpan());

        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_CurrentSegment_AfterLastMove_ReturnsEmpty()
    {
        VirtualPath path = new("test".AsSpan());

        path.MoveNextSegment();
        path.MoveNextSegment(); // This should return false

        path.CurrentSegment.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_RecursiveCall_HandlesCorrectly()
    {
        // Test the recursive call for separator skipping
        VirtualPath path = new($"{Path.DirectorySeparatorChar}test".AsSpan());

        bool result = path.MoveNextSegment();

        result.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("test");
    }

    [Fact]
    public void VirtualPath_MoveNextSegment_MultipleRecursiveCalls_HandlesCorrectly()
    {
        // Test multiple recursive calls for consecutive separators
        VirtualPath path = new($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}test".AsSpan());

        bool result = path.MoveNextSegment();

        result.Should().BeTrue();
        path.CurrentSegment.ToString().Should().Be("test");
    }

    [Fact]
    public void VirtualPath_Documentation_Example()
    {
        // Example from documentation: combining two path segments
        VirtualPath path = new($"src{Path.DirectorySeparatorChar}main".AsSpan(), $"components{Path.DirectorySeparatorChar}Button.cs".AsSpan());

        List<string> segments = [];
        while (path.MoveNextSegment())
        {
            segments.Add(path.CurrentSegment.ToString());
        }

        segments.Should().BeEquivalentTo(["src", "main", "components", "Button.cs"]);
    }
}
