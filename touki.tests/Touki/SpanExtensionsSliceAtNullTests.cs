// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class SpanExtensionsSliceAtNullTests
{
    [Fact]
    public void ReadOnlySpan_NoNull_ReturnsFullSpan()
    {
        ReadOnlySpan<char> span = "hello".AsSpan();
        ReadOnlySpan<char> result = span.SliceAtNull();
        result.SequenceEqual(span).Should().BeTrue();
    }

    [Fact]
    public void ReadOnlySpan_NullAtEnd_ReturnsContentBeforeNull()
    {
        ReadOnlySpan<char> span = "hello\0".AsSpan();
        ReadOnlySpan<char> result = span.SliceAtNull();
        result.SequenceEqual("hello".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ReadOnlySpan_NullInMiddle_ReturnsContentBeforeNull()
    {
        ReadOnlySpan<char> span = "ab\0cd".AsSpan();
        ReadOnlySpan<char> result = span.SliceAtNull();
        result.SequenceEqual("ab".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ReadOnlySpan_NullAtStart_ReturnsEmpty()
    {
        ReadOnlySpan<char> span = "\0abc".AsSpan();
        ReadOnlySpan<char> result = span.SliceAtNull();
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ReadOnlySpan_Empty_ReturnsEmpty()
    {
        ReadOnlySpan<char> span = [];
        ReadOnlySpan<char> result = span.SliceAtNull();
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Span_NoNull_ReturnsFullSpan()
    {
        Span<char> buffer = ['h', 'e', 'l', 'l', 'o'];
        Span<char> result = buffer.SliceAtNull();
        result.Length.Should().Be(5);
        result.SequenceEqual("hello".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void Span_NullInMiddle_ReturnsContentBeforeNull()
    {
        Span<char> buffer = ['a', 'b', '\0', 'c', 'd'];
        Span<char> result = buffer.SliceAtNull();
        result.Length.Should().Be(2);
        result.SequenceEqual("ab".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void Span_NullAtStart_ReturnsEmpty()
    {
        Span<char> buffer = ['\0', 'x', 'y'];
        Span<char> result = buffer.SliceAtNull();
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Span_Empty_ReturnsEmpty()
    {
        Span<char> buffer = [];
        Span<char> result = buffer.SliceAtNull();
        result.IsEmpty.Should().BeTrue();
    }
}
