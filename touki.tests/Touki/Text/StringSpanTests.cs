// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public class StringSpanTests
{
    [Fact]
    public void Default_IsEmpty_True()
    {
        StringSpan span = default;
        span.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NullString_IsEmpty()
    {
        StringSpan span = new((string?)null);
        span.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_EmptyString_IsEmpty()
    {
        StringSpan span = new(string.Empty);
        span.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NonEmptyString_IsNotEmpty()
    {
        StringSpan span = new("hello");
        span.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Constructor_EmptySpan_IsEmpty()
    {
        StringSpan span = new([]);
        span.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NonEmptySpan_IsNotEmpty()
    {
        StringSpan span = new("abc".AsSpan());
        span.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ImplicitConversion_FromString_RoundTripsContent()
    {
        StringSpan span = "hello";
        ReadOnlySpan<char> result = span;
        result.SequenceEqual("hello".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromNullString_IsEmpty()
    {
        StringSpan span = (string?)null;
        span.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromReadOnlySpan_RoundTripsContent()
    {
        ReadOnlySpan<char> source = "world".AsSpan();
        StringSpan span = source;
        ReadOnlySpan<char> result = span;
        result.SequenceEqual(source).Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromMutableSpan_RoundTripsContent()
    {
        Span<char> buffer = stackalloc char[5];
        "world".AsSpan().CopyTo(buffer);
        StringSpan span = buffer;
        ReadOnlySpan<char> result = span;
        result.SequenceEqual(buffer).Should().BeTrue();
    }

    [Fact]
    public void ToString_FromString_ReturnsSameInstance()
    {
        string original = "shared";
        StringSpan span = new(original);
        span.ToString().Should().BeSameAs(original);
    }

    [Fact]
    public void ToString_FromEmptySpan_ReturnsEmptyString()
    {
        StringSpan span = new([]);
        span.ToString().Should().BeSameAs(string.Empty);
    }

    [Fact]
    public void ToString_FromSpan_ReturnsContent()
    {
        StringSpan span = new("hi".AsSpan());
        span.ToString().Should().Be("hi");
    }

    [Fact]
    public void ToString_FromNullString_ReturnsEmptyString()
    {
        StringSpan span = new((string?)null);
        span.ToString().Should().BeSameAs(string.Empty);
    }

    [Fact]
    public void ToStringOrNull_FromString_ReturnsSameInstance()
    {
        string original = "shared";
        StringSpan span = new(original);
        span.ToStringOrNull().Should().BeSameAs(original);
    }

    [Fact]
    public void ToStringOrNull_FromEmptyString_ReturnsEmptyInstance()
    {
        StringSpan span = new(string.Empty);
        span.ToStringOrNull().Should().BeSameAs(string.Empty);
    }

    [Fact]
    public void ToStringOrNull_FromNullString_ReturnsNull()
    {
        StringSpan span = new((string?)null);
        span.ToStringOrNull().Should().BeNull();
    }

    [Fact]
    public void ToStringOrNull_FromEmptySpan_ReturnsNull()
    {
        StringSpan span = new([]);
        span.ToStringOrNull().Should().BeNull();
    }

    [Fact]
    public void ToStringOrNull_FromNonEmptySpan_ReturnsContent()
    {
        StringSpan span = new("abc".AsSpan());
        span.ToStringOrNull().Should().Be("abc");
    }
}
