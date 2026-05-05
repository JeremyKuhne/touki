// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class SingleExtensionsTests
{
    [Theory]
    [InlineData(0f, true)]
    [InlineData(1f, true)]
    [InlineData(float.MaxValue, true)]
    [InlineData(float.Epsilon, true)]
    [InlineData(float.PositiveInfinity, false)]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.NaN, false)]
    public void IsFinite_ReturnsExpected(float value, bool expected)
    {
        float.IsFinite(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(-1f, true)]
    [InlineData(float.NaN, true)]
    [InlineData(float.PositiveInfinity, false)]
    [InlineData(float.NegativeInfinity, true)]
    public void IsNegative_ReturnsExpected(float value, bool expected)
    {
        float.IsNegative(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1f, true)]
    [InlineData(0f, false)]
    [InlineData(float.Epsilon, false)]
    [InlineData(float.NaN, false)]
    public void IsNormal_ReturnsExpected(float value, bool expected)
    {
        float.IsNormal(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1f, false)]
    [InlineData(float.Epsilon, true)]
    [InlineData(0f, false)]
    public void IsSubnormal_ReturnsExpected(float value, bool expected)
    {
        float.IsSubnormal(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1f, true)]
    [InlineData(-1f, false)]
    [InlineData(float.NaN, false)]
    public void IsPositive_ReturnsExpected(float value, bool expected)
    {
        float.IsPositive(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1f, true)]
    [InlineData(float.NaN, false)]
    [InlineData(float.PositiveInfinity, true)]
    public void IsRealNumber_ReturnsExpected(float value, bool expected)
    {
        float.IsRealNumber(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1f, true)]
    [InlineData(1.5f, false)]
    [InlineData(float.NaN, false)]
    public void IsInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsInteger(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(2f, true)]
    [InlineData(1f, false)]
    public void IsEvenInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsEvenInteger(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1f, true)]
    [InlineData(2f, false)]
    public void IsOddInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsOddInteger(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1f, true)]
    [InlineData(2f, true)]
    [InlineData(0.5f, true)]
    [InlineData(3f, false)]
    [InlineData(0f, false)]
    [InlineData(-2f, false)]
    public void IsPow2_ReturnsExpected(float value, bool expected)
    {
        float.IsPow2(value).Should().Be(expected);
    }
}
