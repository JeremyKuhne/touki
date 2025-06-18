// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using BitConverters = System.BitConverter;
#endif

namespace Touki;

public class BitConvertersTests
{
    [Theory]
    [InlineData(1.0f, 0x3F800000)]
    [InlineData(0.0f, 0x00000000)]
    [InlineData(-1.0f, unchecked((int)0xBF800000))]
    [InlineData(float.NaN, unchecked((int)0xFFC00000))]
    [InlineData(float.PositiveInfinity, 0x7F800000)]
    [InlineData(float.NegativeInfinity, unchecked((int)0xFF800000))]
    [InlineData(float.MaxValue, 0x7F7FFFFF)]
    [InlineData(float.MinValue, unchecked((int)0xFF7FFFFF))]
    [InlineData(float.Epsilon, 0x00000001)]
    public void SingleToInt32Bits(float value, int expected)
    {
        int bits = BitConverters.SingleToInt32Bits(value);
        bits.Should().Be(expected);
    }

    [Theory]
    [InlineData(1.0f, 0x3F800000u)]
    [InlineData(0.0f, 0x00000000u)]
    [InlineData(-1.0f, 0xBF800000u)]
    [InlineData(float.NaN, 0xFFC00000u)]
    [InlineData(float.PositiveInfinity, 0x7F800000u)]
    [InlineData(float.NegativeInfinity, 0xFF800000u)]
    [InlineData(float.MaxValue, 0x7F7FFFFFu)]
    [InlineData(float.MinValue, 0xFF7FFFFFu)]
    [InlineData(float.Epsilon, 0x00000001u)]
    public void SingleToUInt32Bits(float value, uint expected)
    {
        uint bits = BitConverters.SingleToUInt32Bits(value);
        bits.Should().Be(expected);
    }

    [Fact]
    public void DoubleToInt64Bits()
    {
        double value = 1.0;
        long bits = BitConverters.DoubleToInt64Bits(value);
        Assert.Equal(0x3FF0000000000000, bits); // 1.0 in IEEE 754 format
    }
}
