// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Tests for <see cref="SpanReaderExtensions"/>.
/// </summary>
public class SpanReaderExtensionsTests
{
    [Fact]
    public void TryReadPositiveInteger_ValidInteger()
    {
        ReadOnlySpan<char> span = "12345".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(12345u);
        reader.Position.Should().Be(5);
        reader.End.Should().BeTrue();
    }

    [Fact]
    public void TryReadPositiveInteger_SingleDigit()
    {
        ReadOnlySpan<char> span = "7".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(7u);
        reader.Position.Should().Be(1);
        reader.End.Should().BeTrue();
    }

    [Fact]
    public void TryReadPositiveInteger_Zero()
    {
        ReadOnlySpan<char> span = "0".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(0u);
        reader.Position.Should().Be(1);
        reader.End.Should().BeTrue();
    }

    [Fact]
    public void TryReadPositiveInteger_NoDigits()
    {
        ReadOnlySpan<char> span = "abc".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeFalse();
        value.Should().Be(0u);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void TryReadPositiveInteger_EmptySpan()
    {
        ReadOnlySpan<char> span = "".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeFalse();
        value.Should().Be(0u);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void TryReadPositiveInteger_PartialDigits()
    {
        ReadOnlySpan<char> span = "123abc456".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(123u);
        reader.Position.Should().Be(3);

        // Should be positioned at 'a'
        reader.TryPeek(out char next).Should().BeTrue();
        next.Should().Be('a');
    }

    [Fact]
    public void TryReadPositiveInteger_LeadingZeros()
    {
        ReadOnlySpan<char> span = "00123".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(123u);
        reader.Position.Should().Be(5);
        reader.End.Should().BeTrue();
    }

    [Fact]
    public void TryReadPositiveInteger_MaxValue()
    {
        string maxValueStr = uint.MaxValue.ToString();
        ReadOnlySpan<char> span = maxValueStr.AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(uint.MaxValue);
        reader.Position.Should().Be(maxValueStr.Length);
        reader.End.Should().BeTrue();
    }

    [Theory]
    [InlineData("4294967296")] // uint.MaxValue + 1
    [InlineData("99999999999999999999")] // Very large number
    public void TryReadPositiveInteger_Overflow(string input)
    {
        ReadOnlySpan<char> span = input.AsSpan();
        SpanReader<char> reader = new(span);

        // Should still succeed but overflow
        reader.TryReadPositiveInteger(out _).Should().BeTrue();
        reader.Position.Should().Be(input.Length);
        reader.End.Should().BeTrue();
    }

    [Fact]
    public void TryReadPositiveInteger_AfterOtherOperations()
    {
        ReadOnlySpan<char> span = "abc123def".AsSpan();
        SpanReader<char> reader = new(span);

        // Skip the 'abc' part
        reader.Advance(3);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(123u);
        reader.Position.Should().Be(6);

        // Should be positioned at 'd'
        reader.TryPeek(out char next).Should().BeTrue();
        next.Should().Be('d');
    }

    [Fact]
    public void TryReadPositiveInteger_MultipleIntegers()
    {
        ReadOnlySpan<char> span = "12,34,56".AsSpan();
        SpanReader<char> reader = new(span);

        // Read first integer
        reader.TryReadPositiveInteger(out uint value1).Should().BeTrue();
        value1.Should().Be(12u);
        reader.Position.Should().Be(2);

        // Skip comma
        reader.Advance(1);

        // Read second integer
        reader.TryReadPositiveInteger(out uint value2).Should().BeTrue();
        value2.Should().Be(34u);
        reader.Position.Should().Be(5);

        // Skip comma
        reader.Advance(1);

        // Read third integer
        reader.TryReadPositiveInteger(out uint value3).Should().BeTrue();
        value3.Should().Be(56u);
        reader.Position.Should().Be(8);
        reader.End.Should().BeTrue();
    }

    [Fact]
    public void TryReadPositiveInteger_WithSpaces()
    {
        ReadOnlySpan<char> span = " 123 ".AsSpan();
        SpanReader<char> reader = new(span);

        // Skip leading space
        reader.Advance(1);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(123u);
        reader.Position.Should().Be(4);

        // Should be positioned at trailing space
        reader.TryPeek(out char next).Should().BeTrue();
        next.Should().Be(' ');
    }
}
