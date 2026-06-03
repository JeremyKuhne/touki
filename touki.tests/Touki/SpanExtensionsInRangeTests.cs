// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class SpanExtensionsInRangeTests
{
    // -----------------------------------------------------------------------
    //  IndexOfAnyInRange
    // -----------------------------------------------------------------------

    [TestMethod]
    public void IndexOfAnyInRange_Empty_ReturnsMinusOne()
    {
        ReadOnlySpan<int>.Empty.IndexOfAnyInRange(0, 10).Should().Be(-1);
    }

    [TestMethod]
    public void IndexOfAnyInRange_AllOutside_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [0, 11, 100, -5];
        span.IndexOfAnyInRange(1, 10).Should().Be(-1);
    }

    [TestMethod]
    public void IndexOfAnyInRange_FirstInside_ReturnsZero()
    {
        ReadOnlySpan<int> span = [5, 0, 11];
        span.IndexOfAnyInRange(1, 10).Should().Be(0);
    }

    [TestMethod]
    public void IndexOfAnyInRange_OnlyMiddleInside_ReturnsMiddle()
    {
        ReadOnlySpan<int> span = [0, 5, 100];
        span.IndexOfAnyInRange(1, 10).Should().Be(1);
    }

    [TestMethod]
    public void IndexOfAnyInRange_BoundariesInclusive()
    {
        ReadOnlySpan<int> span = [1, 10];
        span.IndexOfAnyInRange(1, 10).Should().Be(0);
        span.IndexOfAnyInRange(2, 9).Should().Be(-1);
    }

    [TestMethod]
    public void IndexOfAnyInRange_Byte_Specialization()
    {
        ReadOnlySpan<byte> span = [0, 50, 100, 200];
        span.IndexOfAnyInRange((byte)40, (byte)60).Should().Be(1);
        span.IndexOfAnyInRange((byte)201, (byte)255).Should().Be(-1);
    }

    [TestMethod]
    public void IndexOfAnyInRange_Char_Specialization()
    {
        ReadOnlySpan<char> span = "hello123".AsSpan();
        span.IndexOfAnyInRange('0', '9').Should().Be(5);
        span.IndexOfAnyInRange('A', 'Z').Should().Be(-1);
    }

    [TestMethod]
    public void IndexOfAnyInRange_Long_Specialization()
    {
        ReadOnlySpan<long> span = [-10L, 0L, long.MaxValue];
        span.IndexOfAnyInRange(0L, 100L).Should().Be(1);
    }

    [TestMethod]
    public void IndexOfAnyInRange_StringFallback_UsesIComparable()
    {
        ReadOnlySpan<string> span = ["apple", "banana", "cherry"];
        span.IndexOfAnyInRange("b", "d").Should().Be(1);
    }

    // -----------------------------------------------------------------------
    //  IndexOfAnyExceptInRange
    // -----------------------------------------------------------------------

    [TestMethod]
    public void IndexOfAnyExceptInRange_AllInside_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [1, 5, 10];
        span.IndexOfAnyExceptInRange(1, 10).Should().Be(-1);
    }

    [TestMethod]
    public void IndexOfAnyExceptInRange_FirstOutside_ReturnsZero()
    {
        ReadOnlySpan<int> span = [0, 5, 10];
        span.IndexOfAnyExceptInRange(1, 10).Should().Be(0);
    }

    [TestMethod]
    public void IndexOfAnyExceptInRange_OnlyLastOutside_ReturnsLast()
    {
        ReadOnlySpan<int> span = [1, 5, 11];
        span.IndexOfAnyExceptInRange(1, 10).Should().Be(2);
    }

    // -----------------------------------------------------------------------
    //  LastIndexOfAnyInRange / LastIndexOfAnyExceptInRange
    // -----------------------------------------------------------------------

    [TestMethod]
    public void LastIndexOfAnyInRange_FindsLast()
    {
        ReadOnlySpan<int> span = [5, 0, 7, 100];
        span.LastIndexOfAnyInRange(1, 10).Should().Be(2);
    }

    [TestMethod]
    public void LastIndexOfAnyInRange_AllOutside_ReturnsMinusOne()
    {
        ReadOnlySpan<int> span = [0, 100, 200];
        span.LastIndexOfAnyInRange(1, 10).Should().Be(-1);
    }

    [TestMethod]
    public void LastIndexOfAnyInRange_Char_Specialization()
    {
        ReadOnlySpan<char> span = "abc123def456".AsSpan();
        span.LastIndexOfAnyInRange('0', '9').Should().Be(11);
    }

    [TestMethod]
    public void LastIndexOfAnyInRange_Byte_Specialization()
    {
        ReadOnlySpan<byte> span = [5, 200, 7, 100];
        span.LastIndexOfAnyInRange((byte)1, (byte)10).Should().Be(2);
    }

    [TestMethod]
    public void LastIndexOfAnyExceptInRange_FindsLastOutside()
    {
        ReadOnlySpan<int> span = [100, 5, 200, 7];
        span.LastIndexOfAnyExceptInRange(1, 10).Should().Be(2);
    }

    // -----------------------------------------------------------------------
    //  ContainsAnyInRange / ContainsAnyExceptInRange
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ContainsAnyInRange_Found_ReturnsTrue()
    {
        ReadOnlySpan<int> span = [0, 5, 100];
        span.ContainsAnyInRange(1, 10).Should().BeTrue();
    }

    [TestMethod]
    public void ContainsAnyInRange_NotFound_ReturnsFalse()
    {
        ReadOnlySpan<int> span = [0, 100];
        span.ContainsAnyInRange(1, 10).Should().BeFalse();
    }

    [TestMethod]
    public void ContainsAnyExceptInRange_AllInside_ReturnsFalse()
    {
        ReadOnlySpan<int> span = [1, 5, 10];
        span.ContainsAnyExceptInRange(1, 10).Should().BeFalse();
    }

    [TestMethod]
    public void ContainsAnyExceptInRange_OneOutside_ReturnsTrue()
    {
        ReadOnlySpan<int> span = [1, 5, 11];
        span.ContainsAnyExceptInRange(1, 10).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Span<T> mirror
    // -----------------------------------------------------------------------

    [TestMethod]
    public void IndexOfAnyInRange_SpanOverload_Works()
    {
        Span<int> span = [0, 5, 100];
        span.IndexOfAnyInRange(1, 10).Should().Be(1);
        span.ContainsAnyInRange(1, 10).Should().BeTrue();
        span.LastIndexOfAnyExceptInRange(1, 10).Should().Be(2);
    }
}
