// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class DoubleExtensionsTests
{
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(1.0, true)]
    [InlineData(-1.0, true)]
    [InlineData(double.MaxValue, true)]
    [InlineData(double.Epsilon, true)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(double.NaN, false)]
    public void IsFinite_ReturnsExpected(double value, bool expected)
    {
        double.IsFinite(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(1.0, false)]
    [InlineData(-1.0, true)]
    [InlineData(double.NaN, true)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.NegativeInfinity, true)]
    public void IsNegative_ReturnsExpected(double value, bool expected)
    {
        double.IsNegative(value).Should().Be(expected);
    }

    [Fact]
    public void IsNegative_NegativeZero_ReturnsTrue()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000_0000_0000_0000UL));
        double.IsNegative(negativeZero).Should().BeTrue();
    }

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(-1.0, true)]
    [InlineData(0.0, false)]
    [InlineData(double.Epsilon, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsNormal_ReturnsExpected(double value, bool expected)
    {
        double.IsNormal(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1.0, false)]
    [InlineData(0.0, false)]
    [InlineData(double.Epsilon, true)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsSubnormal_ReturnsExpected(double value, bool expected)
    {
        double.IsSubnormal(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(1.0, true)]
    [InlineData(-1.0, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, true)]
    [InlineData(double.NegativeInfinity, false)]
    public void IsPositive_ReturnsExpected(double value, bool expected)
    {
        double.IsPositive(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(1.5, true)]
    [InlineData(double.PositiveInfinity, true)]
    [InlineData(double.NegativeInfinity, true)]
    [InlineData(double.NaN, false)]
    public void IsRealNumber_ReturnsExpected(double value, bool expected)
    {
        double.IsRealNumber(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(1.0, true)]
    [InlineData(-3.0, true)]
    [InlineData(1.5, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.NaN, false)]
    public void IsInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsInteger(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(2.0, true)]
    [InlineData(-4.0, true)]
    [InlineData(1.0, false)]
    [InlineData(1.5, false)]
    public void IsEvenInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsEvenInteger(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(-3.0, true)]
    [InlineData(0.0, false)]
    [InlineData(2.0, false)]
    public void IsOddInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsOddInteger(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(2.0, true)]
    [InlineData(4.0, true)]
    [InlineData(0.5, true)]
    [InlineData(3.0, false)]
    [InlineData(0.0, false)]
    [InlineData(-2.0, false)]
    [InlineData(double.NaN, false)]
    public void IsPow2_ReturnsExpected(double value, bool expected)
    {
        double.IsPow2(value).Should().Be(expected);
    }
}
