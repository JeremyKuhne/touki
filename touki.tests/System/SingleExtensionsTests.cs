// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

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

    [Test]
    [Arguments(float.NegativeInfinity, false)]
    [Arguments(float.MinValue, true)]
    [Arguments(-1f, true)]
    [Arguments(NegativeMinNormal, true)]
    [Arguments(NegativeMaxSubnormal, true)]
    [Arguments(NegativeEpsilon, true)]
    [Arguments(float.NaN, false)]
    [Arguments(0f, true)]
    [Arguments(float.Epsilon, true)]
    [Arguments(MaxSubnormal, true)]
    [Arguments(MinNormal, true)]
    [Arguments(1f, true)]
    [Arguments(float.MaxValue, true)]
    [Arguments(float.PositiveInfinity, false)]
    public void IsFinite_ReturnsExpected(float value, bool expected)
    {
        float.IsFinite(value).Should().Be(expected);
    }

    [Test]
    public void IsFinite_NegativeZero_ReturnsTrue()
    {
        float.IsFinite(NegativeZero).Should().BeTrue();
    }

    // ---- IsNegative ----

    [Test]
    [Arguments(float.NegativeInfinity, true)]
    [Arguments(float.MinValue, true)]
    [Arguments(-1f, true)]
    [Arguments(NegativeMinNormal, true)]
    [Arguments(NegativeMaxSubnormal, true)]
    [Arguments(NegativeEpsilon, true)]
    [Arguments(float.NaN, true)]
    [Arguments(0f, false)]
    [Arguments(float.Epsilon, false)]
    [Arguments(MaxSubnormal, false)]
    [Arguments(MinNormal, false)]
    [Arguments(1f, false)]
    [Arguments(float.MaxValue, false)]
    [Arguments(float.PositiveInfinity, false)]
    public void IsNegative_ReturnsExpected(float value, bool expected)
    {
        float.IsNegative(value).Should().Be(expected);
    }

    [Test]
    public void IsNegative_NegativeZero_ReturnsTrue()
    {
        float.IsNegative(NegativeZero).Should().BeTrue();
    }

    // ---- IsNormal ----

    [Test]
    [Arguments(float.NegativeInfinity, false)]
    [Arguments(float.MinValue, true)]
    [Arguments(-1f, true)]
    [Arguments(NegativeMinNormal, true)]
    [Arguments(NegativeMaxSubnormal, false)]
    [Arguments(NegativeEpsilon, false)]
    [Arguments(float.NaN, false)]
    [Arguments(0f, false)]
    [Arguments(float.Epsilon, false)]
    [Arguments(MaxSubnormal, false)]
    [Arguments(MinNormal, true)]
    [Arguments(1f, true)]
    [Arguments(float.MaxValue, true)]
    [Arguments(float.PositiveInfinity, false)]
    public void IsNormal_ReturnsExpected(float value, bool expected)
    {
        float.IsNormal(value).Should().Be(expected);
    }

    [Test]
    public void IsNormal_NegativeZero_ReturnsFalse()
    {
        float.IsNormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsSubnormal ----

    [Test]
    [Arguments(float.NegativeInfinity, false)]
    [Arguments(float.MinValue, false)]
    [Arguments(-1f, false)]
    [Arguments(NegativeMinNormal, false)]
    [Arguments(NegativeMaxSubnormal, true)]
    [Arguments(NegativeEpsilon, true)]
    [Arguments(float.NaN, false)]
    [Arguments(0f, false)]
    [Arguments(float.Epsilon, true)]
    [Arguments(MaxSubnormal, true)]
    [Arguments(MinNormal, false)]
    [Arguments(1f, false)]
    [Arguments(float.MaxValue, false)]
    [Arguments(float.PositiveInfinity, false)]
    public void IsSubnormal_ReturnsExpected(float value, bool expected)
    {
        float.IsSubnormal(value).Should().Be(expected);
    }

    [Test]
    public void IsSubnormal_NegativeZero_ReturnsFalse()
    {
        float.IsSubnormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsPositive ----

    [Test]
    [Arguments(float.NegativeInfinity, false)]
    [Arguments(float.MinValue, false)]
    [Arguments(-1f, false)]
    [Arguments(float.NaN, false)]
    [Arguments(0f, true)]
    [Arguments(1f, true)]
    [Arguments(float.MaxValue, true)]
    [Arguments(float.PositiveInfinity, true)]
    public void IsPositive_ReturnsExpected(float value, bool expected)
    {
        float.IsPositive(value).Should().Be(expected);
    }

    [Test]
    public void IsPositive_NegativeZero_ReturnsFalse()
    {
        float.IsPositive(NegativeZero).Should().BeFalse();
    }

    // ---- IsRealNumber ----

    [Test]
    [Arguments(float.NegativeInfinity, true)]
    [Arguments(-1f, true)]
    [Arguments(float.NaN, false)]
    [Arguments(0f, true)]
    [Arguments(1f, true)]
    [Arguments(float.PositiveInfinity, true)]
    public void IsRealNumber_ReturnsExpected(float value, bool expected)
    {
        float.IsRealNumber(value).Should().Be(expected);
    }

    // ---- IsInteger ----

    [Test]
    [Arguments(float.NegativeInfinity, false)]
    [Arguments(float.MinValue, true)]
    [Arguments(-1f, true)]
    [Arguments(NegativeMinNormal, false)]
    [Arguments(NegativeEpsilon, false)]
    [Arguments(float.NaN, false)]
    [Arguments(0f, true)]
    [Arguments(float.Epsilon, false)]
    [Arguments(MinNormal, false)]
    [Arguments(1f, true)]
    [Arguments(1.5f, false)]
    [Arguments(float.MaxValue, true)]
    [Arguments(float.PositiveInfinity, false)]
    public void IsInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsInteger(value).Should().Be(expected);
    }

    [Test]
    public void IsInteger_NegativeZero_ReturnsTrue()
    {
        float.IsInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsEvenInteger ----

    [Test]
    [Arguments(float.NegativeInfinity, false)]
    [Arguments(float.MinValue, true)]
    [Arguments(-1f, false)]
    [Arguments(-2f, true)]
    [Arguments(NegativeEpsilon, false)]
    [Arguments(float.NaN, false)]
    [Arguments(0f, true)]
    [Arguments(float.Epsilon, false)]
    [Arguments(1f, false)]
    [Arguments(2f, true)]
    [Arguments(float.MaxValue, true)]
    [Arguments(float.PositiveInfinity, false)]
    public void IsEvenInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsEvenInteger(value).Should().Be(expected);
    }

    [Test]
    public void IsEvenInteger_NegativeZero_ReturnsTrue()
    {
        float.IsEvenInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsOddInteger ----

    [Test]
    [Arguments(float.NegativeInfinity, false)]
    [Arguments(float.MinValue, false)]
    [Arguments(-1f, true)]
    [Arguments(-2f, false)]
    [Arguments(float.NaN, false)]
    [Arguments(0f, false)]
    [Arguments(1f, true)]
    [Arguments(2f, false)]
    [Arguments(float.MaxValue, false)]
    [Arguments(float.PositiveInfinity, false)]
    public void IsOddInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsOddInteger(value).Should().Be(expected);
    }

    [Test]
    public void IsOddInteger_NegativeZero_ReturnsFalse()
    {
        float.IsOddInteger(NegativeZero).Should().BeFalse();
    }

    // ---- IsPow2 ----

    [Test]
    [Arguments(float.NegativeInfinity, false)]
    [Arguments(-1f, false)]
    [Arguments(-2f, false)]
    [Arguments(float.NaN, false)]
    [Arguments(0f, false)]
    [Arguments(float.Epsilon, true)]   // smallest subnormal is 2^-149
    [Arguments(MinNormal, true)]       // 2^-126
    [Arguments(0.5f, true)]
    [Arguments(1f, true)]
    [Arguments(2f, true)]
    [Arguments(4f, true)]
    [Arguments(3f, false)]
    [Arguments(1.5f, false)]
    [Arguments(float.MaxValue, false)]
    [Arguments(float.PositiveInfinity, false)]
    public void IsPow2_ReturnsExpected(float value, bool expected)
    {
        float.IsPow2(value).Should().Be(expected);
    }

    [Test]
    public void IsPow2_NegativeZero_ReturnsFalse()
    {
        float.IsPow2(NegativeZero).Should().BeFalse();
    }
}
