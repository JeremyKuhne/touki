// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Tests for <see cref="SpanReaderExtensions"/>.
/// </summary>
[TestClass]
public class SpanReaderExtensionsTests
{
    [TestMethod]
    public void TryReadPositiveInteger_ValidInteger()
    {
        ReadOnlySpan<char> span = "12345".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(12345u);
        reader.Position.Should().Be(5);
        reader.End.Should().BeTrue();
    }

    [TestMethod]
    public void TryReadPositiveInteger_SingleDigit()
    {
        ReadOnlySpan<char> span = "7".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(7u);
        reader.Position.Should().Be(1);
        reader.End.Should().BeTrue();
    }

    [TestMethod]
    public void TryReadPositiveInteger_Zero()
    {
        ReadOnlySpan<char> span = "0".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(0u);
        reader.Position.Should().Be(1);
        reader.End.Should().BeTrue();
    }

    [TestMethod]
    public void TryReadPositiveInteger_NoDigits()
    {
        ReadOnlySpan<char> span = "abc".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeFalse();
        value.Should().Be(0u);
        reader.Position.Should().Be(0);
    }

    [TestMethod]
    public void TryReadPositiveInteger_EmptySpan()
    {
        ReadOnlySpan<char> span = "".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeFalse();
        value.Should().Be(0u);
        reader.Position.Should().Be(0);
    }

    [TestMethod]
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

    [TestMethod]
    public void TryReadPositiveInteger_LeadingZeros()
    {
        ReadOnlySpan<char> span = "00123".AsSpan();
        SpanReader<char> reader = new(span);

        reader.TryReadPositiveInteger(out uint value).Should().BeTrue();
        value.Should().Be(123u);
        reader.Position.Should().Be(5);
        reader.End.Should().BeTrue();
    }

    [TestMethod]
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

    [TestMethod]
    [DataRow("4294967296")] // uint.MaxValue + 1
    [DataRow("99999999999999999999")] // Very large number
    public void TryReadPositiveInteger_Overflow(string input)
    {
        ReadOnlySpan<char> span = input.AsSpan();
        SpanReader<char> reader = new(span);

        // Should still succeed but overflow
        reader.TryReadPositiveInteger(out _).Should().BeTrue();
        reader.Position.Should().Be(input.Length);
        reader.End.Should().BeTrue();
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void TryReadInt32LittleEndian_ReadsValueAndAdvances()
    {
        byte[] data = [0x78, 0x56, 0x34, 0x12];
        SpanReader<byte> reader = new(data);

        reader.TryReadInt32LittleEndian(out int value).Should().BeTrue();
        value.Should().Be(0x12345678);
        reader.Position.Should().Be(4);
        reader.End.Should().BeTrue();
    }

    [TestMethod]
    public void TryReadInt32LittleEndian_Sequential_ReadsBoth()
    {
        byte[] data = [1, 0, 0, 0, 2, 0, 0, 0];
        SpanReader<byte> reader = new(data);

        reader.TryReadInt32LittleEndian(out int first).Should().BeTrue();
        reader.TryReadInt32LittleEndian(out int second).Should().BeTrue();
        first.Should().Be(1);
        second.Should().Be(2);
        reader.End.Should().BeTrue();
    }

    [TestMethod]
    public void TryReadInt32LittleEndian_Truncated_ReturnsFalse()
    {
        byte[] data = [1, 2, 3];
        SpanReader<byte> reader = new(data);

        reader.TryReadInt32LittleEndian(out int value).Should().BeFalse();
        value.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [TestMethod]
    public void TryRead7BitEncodedInt32_SingleByte_ReadsValue()
    {
        byte[] data = [5];
        SpanReader<byte> reader = new(data);

        reader.TryRead7BitEncodedInt32(out int value).Should().BeTrue();
        value.Should().Be(5);
        reader.Position.Should().Be(1);
    }

    [TestMethod]
    public void TryRead7BitEncodedInt32_MultiByte_ReadsValue()
    {
        byte[] data = [0xAC, 0x02];
        SpanReader<byte> reader = new(data);

        reader.TryRead7BitEncodedInt32(out int value).Should().BeTrue();
        value.Should().Be(300);
        reader.Position.Should().Be(2);
    }

    [TestMethod]
    public void TryRead7BitEncodedInt32_MaxValue_ReadsValue()
    {
        byte[] data = [0xFF, 0xFF, 0xFF, 0xFF, 0x07];
        SpanReader<byte> reader = new(data);

        reader.TryRead7BitEncodedInt32(out int value).Should().BeTrue();
        value.Should().Be(int.MaxValue);
        reader.Position.Should().Be(5);
    }

    [TestMethod]
    public void TryRead7BitEncodedInt32_Truncated_ReturnsFalse()
    {
        byte[] data = [0x80];
        SpanReader<byte> reader = new(data);

        reader.TryRead7BitEncodedInt32(out int value).Should().BeFalse();
        value.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [TestMethod]
    public void TryRead7BitEncodedInt32_Overflow_ReturnsFalse()
    {
        byte[] data = [0x80, 0x80, 0x80, 0x80, 0x10];
        SpanReader<byte> reader = new(data);

        reader.TryRead7BitEncodedInt32(out int value).Should().BeFalse();
        value.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [TestMethod]
    public void TryRead7BitEncodedInt32_TruncatedMidStream_RestoresPosition()
    {
        // Consume one byte, then attempt a truncated 7-bit read; the failed read must leave the reader
        // where it began (position 1), not partially advanced.
        byte[] data = [0x2A, 0x80];
        SpanReader<byte> reader = new(data);
        reader.TryRead(out byte _).Should().BeTrue();

        reader.TryRead7BitEncodedInt32(out int value).Should().BeFalse();
        value.Should().Be(0);
        reader.Position.Should().Be(1);
    }
}
