// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class BitConvertersTests
{
    [Test]
    [Arguments(1.0f, 0x3F800000)]
    [Arguments(0.0f, 0x00000000)]
    [Arguments(-1.0f, unchecked((int)0xBF800000))]
    [Arguments(float.NaN, unchecked((int)0xFFC00000))]
    [Arguments(float.PositiveInfinity, 0x7F800000)]
    [Arguments(float.NegativeInfinity, unchecked((int)0xFF800000))]
    [Arguments(float.MaxValue, 0x7F7FFFFF)]
    [Arguments(float.MinValue, unchecked((int)0xFF7FFFFF))]
    [Arguments(float.Epsilon, 0x00000001)]
    public void SingleToInt32Bits(float value, int expected)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        bits.Should().Be(expected);
    }

    [Test]
    [Arguments(1.0f, 0x3F800000u)]
    [Arguments(0.0f, 0x00000000u)]
    [Arguments(-1.0f, 0xBF800000u)]
    [Arguments(float.NaN, 0xFFC00000u)]
    [Arguments(float.PositiveInfinity, 0x7F800000u)]
    [Arguments(float.NegativeInfinity, 0xFF800000u)]
    [Arguments(float.MaxValue, 0x7F7FFFFFu)]
    [Arguments(float.MinValue, 0xFF7FFFFFu)]
    [Arguments(float.Epsilon, 0x00000001u)]
    public void SingleToUInt32Bits(float value, uint expected)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        bits.Should().Be(expected);
    }

    [Test]
    public void DoubleToInt64Bits()
    {
        double value = 1.0;
        long bits = BitConverter.DoubleToInt64Bits(value);
        bits.Should().Be(0x3FF0000000000000); // 1.0 in IEEE 754 format
    }
}
