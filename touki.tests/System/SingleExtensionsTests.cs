// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class SingleExtensionsTests
{
    private const float MinNormal = 1.17549435E-38f;
    private const float MaxSubnormal = 1.17549421E-38f;
    private const float NegativeZero = -0.0f;
    private const float NegativeMinNormal = -1.17549435E-38f;
    private const float NegativeMaxSubnormal = -1.17549421E-38f;
    private const float NegativeEpsilon = -float.Epsilon;

    // ---- IsFinite ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, false)]
    [DataRow(float.MinValue, true)]
    [DataRow(-1f, true)]
    [DataRow(NegativeMinNormal, true)]
    [DataRow(NegativeMaxSubnormal, true)]
    [DataRow(NegativeEpsilon, true)]
    [DataRow(float.NaN, false)]
    [DataRow(0f, true)]
    [DataRow(float.Epsilon, true)]
    [DataRow(MaxSubnormal, true)]
    [DataRow(MinNormal, true)]
    [DataRow(1f, true)]
    [DataRow(float.MaxValue, true)]
    [DataRow(float.PositiveInfinity, false)]
    public void IsFinite_ReturnsExpected(float value, bool expected)
    {
        float.IsFinite(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsFinite_NegativeZero_ReturnsTrue()
    {
        float.IsFinite(NegativeZero).Should().BeTrue();
    }

    // ---- IsNegative ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, true)]
    [DataRow(float.MinValue, true)]
    [DataRow(-1f, true)]
    [DataRow(NegativeMinNormal, true)]
    [DataRow(NegativeMaxSubnormal, true)]
    [DataRow(NegativeEpsilon, true)]
    [DataRow(float.NaN, true)]
    [DataRow(0f, false)]
    [DataRow(float.Epsilon, false)]
    [DataRow(MaxSubnormal, false)]
    [DataRow(MinNormal, false)]
    [DataRow(1f, false)]
    [DataRow(float.MaxValue, false)]
    [DataRow(float.PositiveInfinity, false)]
    public void IsNegative_ReturnsExpected(float value, bool expected)
    {
        float.IsNegative(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsNegative_NegativeZero_ReturnsTrue()
    {
        float.IsNegative(NegativeZero).Should().BeTrue();
    }

    // ---- IsNormal ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, false)]
    [DataRow(float.MinValue, true)]
    [DataRow(-1f, true)]
    [DataRow(NegativeMinNormal, true)]
    [DataRow(NegativeMaxSubnormal, false)]
    [DataRow(NegativeEpsilon, false)]
    [DataRow(float.NaN, false)]
    [DataRow(0f, false)]
    [DataRow(float.Epsilon, false)]
    [DataRow(MaxSubnormal, false)]
    [DataRow(MinNormal, true)]
    [DataRow(1f, true)]
    [DataRow(float.MaxValue, true)]
    [DataRow(float.PositiveInfinity, false)]
    public void IsNormal_ReturnsExpected(float value, bool expected)
    {
        float.IsNormal(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsNormal_NegativeZero_ReturnsFalse()
    {
        float.IsNormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsSubnormal ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, false)]
    [DataRow(float.MinValue, false)]
    [DataRow(-1f, false)]
    [DataRow(NegativeMinNormal, false)]
    [DataRow(NegativeMaxSubnormal, true)]
    [DataRow(NegativeEpsilon, true)]
    [DataRow(float.NaN, false)]
    [DataRow(0f, false)]
    [DataRow(float.Epsilon, true)]
    [DataRow(MaxSubnormal, true)]
    [DataRow(MinNormal, false)]
    [DataRow(1f, false)]
    [DataRow(float.MaxValue, false)]
    [DataRow(float.PositiveInfinity, false)]
    public void IsSubnormal_ReturnsExpected(float value, bool expected)
    {
        float.IsSubnormal(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsSubnormal_NegativeZero_ReturnsFalse()
    {
        float.IsSubnormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsPositive ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, false)]
    [DataRow(float.MinValue, false)]
    [DataRow(-1f, false)]
    [DataRow(float.NaN, false)]
    [DataRow(0f, true)]
    [DataRow(1f, true)]
    [DataRow(float.MaxValue, true)]
    [DataRow(float.PositiveInfinity, true)]
    public void IsPositive_ReturnsExpected(float value, bool expected)
    {
        float.IsPositive(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsPositive_NegativeZero_ReturnsFalse()
    {
        float.IsPositive(NegativeZero).Should().BeFalse();
    }

    // ---- IsRealNumber ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, true)]
    [DataRow(-1f, true)]
    [DataRow(float.NaN, false)]
    [DataRow(0f, true)]
    [DataRow(1f, true)]
    [DataRow(float.PositiveInfinity, true)]
    public void IsRealNumber_ReturnsExpected(float value, bool expected)
    {
        float.IsRealNumber(value).Should().Be(expected);
    }

    // ---- IsInteger ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, false)]
    [DataRow(float.MinValue, true)]
    [DataRow(-1f, true)]
    [DataRow(NegativeMinNormal, false)]
    [DataRow(NegativeEpsilon, false)]
    [DataRow(float.NaN, false)]
    [DataRow(0f, true)]
    [DataRow(float.Epsilon, false)]
    [DataRow(MinNormal, false)]
    [DataRow(1f, true)]
    [DataRow(1.5f, false)]
    [DataRow(float.MaxValue, true)]
    [DataRow(float.PositiveInfinity, false)]
    public void IsInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsInteger(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsInteger_NegativeZero_ReturnsTrue()
    {
        float.IsInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsEvenInteger ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, false)]
    [DataRow(float.MinValue, true)]
    [DataRow(-1f, false)]
    [DataRow(-2f, true)]
    [DataRow(NegativeEpsilon, false)]
    [DataRow(float.NaN, false)]
    [DataRow(0f, true)]
    [DataRow(float.Epsilon, false)]
    [DataRow(1f, false)]
    [DataRow(2f, true)]
    [DataRow(float.MaxValue, true)]
    [DataRow(float.PositiveInfinity, false)]
    public void IsEvenInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsEvenInteger(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsEvenInteger_NegativeZero_ReturnsTrue()
    {
        float.IsEvenInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsOddInteger ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, false)]
    [DataRow(float.MinValue, false)]
    [DataRow(-1f, true)]
    [DataRow(-2f, false)]
    [DataRow(float.NaN, false)]
    [DataRow(0f, false)]
    [DataRow(1f, true)]
    [DataRow(2f, false)]
    [DataRow(float.MaxValue, false)]
    [DataRow(float.PositiveInfinity, false)]
    public void IsOddInteger_ReturnsExpected(float value, bool expected)
    {
        float.IsOddInteger(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsOddInteger_NegativeZero_ReturnsFalse()
    {
        float.IsOddInteger(NegativeZero).Should().BeFalse();
    }

    // ---- IsPow2 ----

    [TestMethod]
    [DataRow(float.NegativeInfinity, false)]
    [DataRow(-1f, false)]
    [DataRow(-2f, false)]
    [DataRow(float.NaN, false)]
    [DataRow(0f, false)]
    [DataRow(float.Epsilon, true)]   // smallest subnormal is 2^-149
    [DataRow(MinNormal, true)]       // 2^-126
    [DataRow(0.5f, true)]
    [DataRow(1f, true)]
    [DataRow(2f, true)]
    [DataRow(4f, true)]
    [DataRow(3f, false)]
    [DataRow(1.5f, false)]
    [DataRow(float.MaxValue, false)]
    [DataRow(float.PositiveInfinity, false)]
    public void IsPow2_ReturnsExpected(float value, bool expected)
    {
        float.IsPow2(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsPow2_NegativeZero_ReturnsFalse()
    {
        float.IsPow2(NegativeZero).Should().BeFalse();
    }
}
