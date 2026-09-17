// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class BitConvertersTests
{
    [TestMethod]
    [DataRow(1.0f, 0x3F800000)]
    [DataRow(0.0f, 0x00000000)]
    [DataRow(-1.0f, unchecked((int)0xBF800000))]
    [DataRow(float.NaN, unchecked((int)0xFFC00000))]
    [DataRow(float.PositiveInfinity, 0x7F800000)]
    [DataRow(float.NegativeInfinity, unchecked((int)0xFF800000))]
    [DataRow(float.MaxValue, 0x7F7FFFFF)]
    [DataRow(float.MinValue, unchecked((int)0xFF7FFFFF))]
    [DataRow(float.Epsilon, 0x00000001)]
    public void SingleToInt32Bits(float value, int expected)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        bits.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(1.0f, 0x3F800000u)]
    [DataRow(0.0f, 0x00000000u)]
    [DataRow(-1.0f, 0xBF800000u)]
    [DataRow(float.NaN, 0xFFC00000u)]
    [DataRow(float.PositiveInfinity, 0x7F800000u)]
    [DataRow(float.NegativeInfinity, 0xFF800000u)]
    [DataRow(float.MaxValue, 0x7F7FFFFFu)]
    [DataRow(float.MinValue, 0xFF7FFFFFu)]
    [DataRow(float.Epsilon, 0x00000001u)]
    public void SingleToUInt32Bits(float value, uint expected)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        bits.Should().Be(expected);
    }

    [TestMethod]
    public void DoubleToInt64Bits()
    {
        double value = 1.0;
        long bits = BitConverter.DoubleToInt64Bits(value);
        bits.Should().Be(0x3FF0000000000000); // 1.0 in IEEE 754 format
    }
}
