// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class SpanExtensionsReplaceTests
{
    // -----------------------------------------------------------------------
    //  Replace<T>(Span<T>, T, T)
    // -----------------------------------------------------------------------

    [Test]
    public void Replace_Generic_NoMatches_LeavesUnchanged()
    {
        Span<int> span = [1, 2, 3];
        span.Replace(99, 0);
        span.ToArray().Should().Equal(1, 2, 3);
    }

    [Test]
    public void Replace_Generic_AllMatches_ReplacesAll()
    {
        Span<int> span = [7, 7, 7];
        span.Replace(7, 1);
        span.ToArray().Should().Equal(1, 1, 1);
    }

    [Test]
    public void Replace_Generic_Mixed_ReplacesMatches()
    {
        Span<int> span = [1, 7, 2, 7, 3];
        span.Replace(7, 0);
        span.ToArray().Should().Equal(1, 0, 2, 0, 3);
    }

    [Test]
    public void Replace_Generic_OldEqualsNew_NoOp()
    {
        Span<int> span = [1, 2, 3];
        span.Replace(2, 2);
        span.ToArray().Should().Equal(1, 2, 3);
    }

    [Test]
    public void Replace_Generic_Empty_NoOp()
    {
        Span<int> span = [];
        span.Replace(1, 2);
        span.Length.Should().Be(0);
    }

    [Test]
    public void Replace_Generic_NullableReference_HandlesNull()
    {
        Span<string?> span = ["a", null, "b", null];
        span.Replace(null, "x");
        span.ToArray().Should().Equal("a", "x", "b", "x");
    }

    [Test]
    public void Replace_Char_StillSpecialized()
    {
        // Existing char-specialized overload should still work via the same call site.
        Span<char> span = "hello".ToCharArray();
        span.Replace('l', 'L');
        span.ToArray().Should().Equal('h', 'e', 'L', 'L', 'o');
    }

    // -----------------------------------------------------------------------
    //  Replace<T>(ReadOnlySpan<T>, Span<T>, T, T)
    // -----------------------------------------------------------------------

    [Test]
    public void Replace_SourceDest_NoMatches_CopiesAsIs()
    {
        ReadOnlySpan<int> source = [1, 2, 3];
        Span<int> dest = new int[3];
        source.Replace(dest, 99, 0);
        dest.ToArray().Should().Equal(1, 2, 3);
    }

    [Test]
    public void Replace_SourceDest_ReplacesMatches()
    {
        ReadOnlySpan<int> source = [1, 7, 2, 7, 3];
        Span<int> dest = new int[5];
        source.Replace(dest, 7, 0);
        dest.ToArray().Should().Equal(1, 0, 2, 0, 3);
    }

    [Test]
    public void Replace_SourceDest_OldEqualsNew_CopiesSource()
    {
        ReadOnlySpan<int> source = [1, 2, 3];
        Span<int> dest = new int[3];
        source.Replace(dest, 2, 2);
        dest.ToArray().Should().Equal(1, 2, 3);
    }

    [Test]
    public void Replace_SourceDest_DestinationLargerThanSource_OnlyWritesSourceLength()
    {
        ReadOnlySpan<int> source = [1, 2, 3];
        Span<int> dest = [9, 9, 9, 9, 9];
        source.Replace(dest, 2, 0);
        dest[..3].ToArray().Should().Equal(1, 0, 3);
        dest[3..].ToArray().Should().Equal(9, 9);
    }

    [Test]
    public void Replace_SourceDest_DestinationTooShort_Throws()
    {
        ArgumentException? caught = null;
        try
        {
            ReadOnlySpan<int> source = [1, 2, 3];
            Span<int> dest = new int[2];
            source.Replace(dest, 2, 0);
        }
        catch (ArgumentException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
    }

    [Test]
    public void Replace_SourceDest_Empty_NoOp()
    {
        ReadOnlySpan<int> source = [];
        Span<int> dest = [];
        source.Replace(dest, 1, 2);
        dest.Length.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    //  SequenceEqual w/ comparer
    // -----------------------------------------------------------------------

    [Test]
    public void SequenceEqual_WithComparer_DifferentLength_ReturnsFalse()
    {
        ReadOnlySpan<int> a = [1, 2];
        ReadOnlySpan<int> b = [1, 2, 3];
        a.SequenceEqual(b, EqualityComparer<int>.Default).Should().BeFalse();
    }

    [Test]
    public void SequenceEqual_WithComparer_NullComparer_UsesDefault()
    {
        ReadOnlySpan<int> a = [1, 2, 3];
        ReadOnlySpan<int> b = [1, 2, 3];
        a.SequenceEqual(b, comparer: null).Should().BeTrue();
    }

    [Test]
    public void SequenceEqual_WithComparer_OrdinalIgnoreCase_True()
    {
        ReadOnlySpan<string> a = ["abc", "DEF"];
        ReadOnlySpan<string> b = ["ABC", "def"];
        a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Test]
    public void SequenceEqual_WithComparer_OrdinalIgnoreCase_FalseWhenDifferent()
    {
        ReadOnlySpan<string> a = ["abc", "DEF"];
        ReadOnlySpan<string> b = ["ABC", "xyz"];
        a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Test]
    public void SequenceEqual_WithComparer_BothEmpty_ReturnsTrue()
    {
        ReadOnlySpan<int>.Empty.SequenceEqual([], EqualityComparer<int>.Default)
            .Should().BeTrue();
    }

    [Test]
    public void SequenceEqual_WithComparer_SpanOverload_Works()
    {
        Span<int> a = [1, 2, 3];
        ReadOnlySpan<int> b = [1, 2, 3];
        a.SequenceEqual(b, EqualityComparer<int>.Default).Should().BeTrue();
    }
}
