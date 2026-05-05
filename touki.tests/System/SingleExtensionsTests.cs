// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable xUnit1025 // duplicate InlineData (xUnit doesn't distinguish 0.0 from -0.0)

namespace Touki;

public class SingleExtensionsTests
{
    private const float MinNormal = 1.17549435E-38f;
    private const float MaxSubnormal = 1.17549421E-38f;
    private const float NegativeZero = -0.0f;
    private const float NegativeMinNormal = -1.17549435E-38f;
    private const float NegativeMaxSubnormal = -1.17549421E-38f;
    private const float NegativeEpsilon = -float.Epsilon;

    // ---- IsFinite ----

    [Theory]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.MinValue, true)]
    [InlineData(-1f, true)]
    [InlineData(NegativeMinNormal, true)]
    [InlineData(NegativeMaxSubnormal, true)]
    [InlineData(NegativeEpsilon, true)]
    [InlineData(float.NaN, false)]
    [InlineData(0f, true)]
    [InlineData(float.Epsilon, true)]
    [InlineData(MaxSubnormal, true)]
    [InlineData(MinNormal, true)]
    [InlineData(1f, true)]
    [InlineData(float.MaxValue, true)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsFinite_ReturnsExpected(float value, bool expected)
    {
        float.IsFinite(value).Should().Be(expected);
    }

    [Fact]
    public void IsFinite_NegativeZero_ReturnsTrue()
    {
        float.IsFinite(NegativeZero).Should().BeTrue();
    }

    // ---- IsNegative ----

    [Theory]
    [InlineData(float.NegativeInfinity, true)]
    [InlineData(float.MinValue, true)]
    [InlineData(-1f, true)]
    [InlineData(NegativeMinNormal, true)]
    [InlineData(NegativeMaxSubnormal, true)]
    [InlineData(NegativeEpsilon, true)]
    [InlineData(float.NaN, true)]
    [InlineData(0f, false)]
    [InlineData(float.Epsilon, false)]
    [InlineData(MaxSubnormal, false)]
    [InlineData(MinNormal, false)]
    [InlineData(1f, false)]
    [InlineData(float.MaxValue, false)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsNegative_ReturnsExpected(float value, bool expected)
    {
        float.IsNegative(value).Should().Be(expected);
    }

    [Fact]
    public void IsNegative_NegativeZero_ReturnsTrue()
    {
        float.IsNegative(NegativeZero).Should().BeTrue();
    }

    // ---- IsNormal ----

    [Theory]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.MinValue, true)]
    [InlineData(-1f, true)]
    [InlineData(NegativeMinNormal, true)]
    [InlineData(NegativeMaxSubnormal, false)]
    [InlineData(NegativeEpsilon, false)]
    [InlineData(float.NaN, false)]
    [InlineData(0f, false)]
    [InlineData(float.Epsilon, false)]
    [InlineData(MaxSubnormal, false)]
    [InlineData(MinNormal, true)]
    [InlineData(1f, true)]
    [InlineData(float.MaxValue, true)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsNormal_ReturnsExpected(float value, bool expected)
    {
        float.IsNormal(value).Should().Be(expected);
    }

    [Fact]
    public void IsNormal_NegativeZero_ReturnsFalse()
    {
        float.IsNormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsSubnormal ----

    [Theory]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.MinValue, false)]
    [InlineData(-1f, false)]
    [InlineData(NegativeMinNormal, false)]
    [InlineData(NegativeMaxSubnormal, true)]
    [InlineData(NegativeEpsilon, true)]
    [InlineData(float.NaN, false)]
    [InlineData(0f, false)]
    [InlineData(float.Epsilon, true)]
    [InlineData(MaxSubnormal, true)]
    [InlineData(MinNormal, false)]
    [InlineData(1f, false)]
    [InlineData(float.MaxValue, false)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsSubnormal_ReturnsExpected(float value, bool expected)
    {
        float.IsSubnormal(value).Should().Be(expected);
    }

    [Fact]
    public void IsSubnormal_NegativeZero_ReturnsFalse()
    {
        float.IsSubnormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsPositive ----

    [Theory]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.MinValue, false)]
    [InlineData(-1f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(0f, true)]
    [InlineData(1f, true)]
    [InlineData(float.MaxValue, true)]
    [InlineData(float.PositiveInfinity, true)]
    public void IsPositive_ReturnsExpected(float value, bool expected)
    {
        float.IsPositive(value).Should().Be(expected);
    }

    [Fact]
    public void IsPositive_NegativeZero_ReturnsFalse()
    {
        float.IsPositive(NegativeZero).Should().BeFalse();
    }

    // ---- IsRealNumber ----

    [Theory]
    [InlineData(float.NegativeInfinity, true)]
    [InlineData(-1f, true)]
    [InlineData(float.NaN, false)]
    [InlineData(0f, true)]
    [InlineData(1f, true)]
    [InlineData(float.PositiveInfinity, true)]
    public void IsRealNumber_ReturnsExpected(float value, bool expected)
    {
        float.IsRealNumber(value).Should().Be(expected);
    }

    // ---- IsInteger ----

    [Theory]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.MinValue, true)]
    [InlineData(-1f, true)]
    [InlineData(NegativeMinNormal, false)]
    [InlineData(NegativeEpsilon, false)]
    [InlineData(float.NaN, false)]
    [InlineData(0f, true)]
    [InlineData(float.Epsilon, false)]
    [InlineData(MinNormal, false)]
    [InlineData(1f, true)]
    [InlineData(1.5f, false)]
    [InlineData(float.MaxValue, true)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsInteger(value).Should().Be(expected);
    }

    [Fact]
    public void IsInteger_NegativeZero_ReturnsTrue()
    {
        float.IsInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsEvenInteger ----

    [Theory]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.MinValue, true)]
    [InlineData(-1f, false)]
    [InlineData(-2f, true)]
    [InlineData(NegativeEpsilon, false)]
    [InlineData(float.NaN, false)]
    [InlineData(0f, true)]
    [InlineData(float.Epsilon, false)]
    [InlineData(1f, false)]
    [InlineData(2f, true)]
    [InlineData(float.MaxValue, true)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsEvenInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsEvenInteger(value).Should().Be(expected);
    }

    [Fact]
    public void IsEvenInteger_NegativeZero_ReturnsTrue()
    {
        float.IsEvenInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsOddInteger ----

    [Theory]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.MinValue, false)]
    [InlineData(-1f, true)]
    [InlineData(-2f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(0f, false)]
    [InlineData(1f, true)]
    [InlineData(2f, false)]
    [InlineData(float.MaxValue, false)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsOddInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsOddInteger(value).Should().Be(expected);
    }

    [Fact]
    public void IsOddInteger_NegativeZero_ReturnsFalse()
    {
        float.IsOddInteger(NegativeZero).Should().BeFalse();
    }

    // ---- IsPow2 ----

    [Theory]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(-1f, false)]
    [InlineData(-2f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(0f, false)]
    [InlineData(float.Epsilon, true)]   // smallest subnormal is 2^-149
    [InlineData(MinNormal, true)]       // 2^-126
    [InlineData(0.5f, true)]
    [InlineData(1f, true)]
    [InlineData(2f, true)]
    [InlineData(4f, true)]
    [InlineData(3f, false)]
    [InlineData(1.5f, false)]
    [InlineData(float.MaxValue, false)]
    [InlineData(float.PositiveInfinity, false)]
    public void IsPow2_ReturnsExpected(float value, bool expected)
    {
        float.IsPow2(value).Should().Be(expected);
    }

    [Fact]
    public void IsPow2_NegativeZero_ReturnsFalse()
    {
        float.IsPow2(NegativeZero).Should().BeFalse();
    }
}
