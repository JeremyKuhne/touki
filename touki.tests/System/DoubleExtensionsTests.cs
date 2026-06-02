// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class DoubleExtensionsTests
{
    private const double MinNormal = 2.2250738585072014e-308;
    private const double MaxSubnormal = 2.2250738585072009e-308;
    private const double NegativeZero = -0.0;
    private const double NegativeMinNormal = -2.2250738585072014e-308;
    private const double NegativeMaxSubnormal = -2.2250738585072009e-308;
    private const double NegativeEpsilon = -double.Epsilon;

    // The BCL test files exercise this canonical 15-row table for each predicate.
    // -0.0 cases are handled in dedicated [Test] tests because parameterized
    // arguments can't distinguish 0.0 from -0.0.

    // ---- IsFinite ----

    [Test]
    [Arguments(double.NegativeInfinity, false)]
    [Arguments(double.MinValue, true)]
    [Arguments(-1.0, true)]
    [Arguments(NegativeMinNormal, true)]
    [Arguments(NegativeMaxSubnormal, true)]
    [Arguments(NegativeEpsilon, true)]
    [Arguments(double.NaN, false)]
    [Arguments(0.0, true)]
    [Arguments(double.Epsilon, true)]
    [Arguments(MaxSubnormal, true)]
    [Arguments(MinNormal, true)]
    [Arguments(1.0, true)]
    [Arguments(double.MaxValue, true)]
    [Arguments(double.PositiveInfinity, false)]
    public void IsFinite_ReturnsExpected(double value, bool expected)
    {
        double.IsFinite(value).Should().Be(expected);
    }

    [Test]
    public void IsFinite_NegativeZero_ReturnsTrue()
    {
        double.IsFinite(NegativeZero).Should().BeTrue();
    }

    // ---- IsNegative ----

    [Test]
    [Arguments(double.NegativeInfinity, true)]
    [Arguments(double.MinValue, true)]
    [Arguments(-1.0, true)]
    [Arguments(NegativeMinNormal, true)]
    [Arguments(NegativeMaxSubnormal, true)]
    [Arguments(NegativeEpsilon, true)]
    [Arguments(double.NaN, true)]
    [Arguments(0.0, false)]
    [Arguments(double.Epsilon, false)]
    [Arguments(MaxSubnormal, false)]
    [Arguments(MinNormal, false)]
    [Arguments(1.0, false)]
    [Arguments(double.MaxValue, false)]
    [Arguments(double.PositiveInfinity, false)]
    public void IsNegative_ReturnsExpected(double value, bool expected)
    {
        double.IsNegative(value).Should().Be(expected);
    }

    [Test]
    public void IsNegative_NegativeZero_ReturnsTrue()
    {
        double.IsNegative(NegativeZero).Should().BeTrue();
    }

    // ---- IsNormal ----

    [Test]
    [Arguments(double.NegativeInfinity, false)]
    [Arguments(double.MinValue, true)]
    [Arguments(-1.0, true)]
    [Arguments(NegativeMinNormal, true)]
    [Arguments(NegativeMaxSubnormal, false)]
    [Arguments(NegativeEpsilon, false)]
    [Arguments(double.NaN, false)]
    [Arguments(0.0, false)]
    [Arguments(double.Epsilon, false)]
    [Arguments(MaxSubnormal, false)]
    [Arguments(MinNormal, true)]
    [Arguments(1.0, true)]
    [Arguments(double.MaxValue, true)]
    [Arguments(double.PositiveInfinity, false)]
    public void IsNormal_ReturnsExpected(double value, bool expected)
    {
        double.IsNormal(value).Should().Be(expected);
    }

    [Test]
    public void IsNormal_NegativeZero_ReturnsFalse()
    {
        double.IsNormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsSubnormal ----

    [Test]
    [Arguments(double.NegativeInfinity, false)]
    [Arguments(double.MinValue, false)]
    [Arguments(-1.0, false)]
    [Arguments(NegativeMinNormal, false)]
    [Arguments(NegativeMaxSubnormal, true)]
    [Arguments(NegativeEpsilon, true)]
    [Arguments(double.NaN, false)]
    [Arguments(0.0, false)]
    [Arguments(double.Epsilon, true)]
    [Arguments(MaxSubnormal, true)]
    [Arguments(MinNormal, false)]
    [Arguments(1.0, false)]
    [Arguments(double.MaxValue, false)]
    [Arguments(double.PositiveInfinity, false)]
    public void IsSubnormal_ReturnsExpected(double value, bool expected)
    {
        double.IsSubnormal(value).Should().Be(expected);
    }

    [Test]
    public void IsSubnormal_NegativeZero_ReturnsFalse()
    {
        double.IsSubnormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsPositive ----

    [Test]
    [Arguments(double.NegativeInfinity, false)]
    [Arguments(double.MinValue, false)]
    [Arguments(-1.0, false)]
    [Arguments(NegativeMinNormal, false)]
    [Arguments(NegativeMaxSubnormal, false)]
    [Arguments(NegativeEpsilon, false)]
    [Arguments(double.NaN, false)]
    [Arguments(0.0, true)]
    [Arguments(double.Epsilon, true)]
    [Arguments(MaxSubnormal, true)]
    [Arguments(MinNormal, true)]
    [Arguments(1.0, true)]
    [Arguments(double.MaxValue, true)]
    [Arguments(double.PositiveInfinity, true)]
    public void IsPositive_ReturnsExpected(double value, bool expected)
    {
        double.IsPositive(value).Should().Be(expected);
    }

    [Test]
    public void IsPositive_NegativeZero_ReturnsFalse()
    {
        double.IsPositive(NegativeZero).Should().BeFalse();
    }

    [Test]
    public void IsPositive_NegativeNaN_ReturnsFalse()
    {
        double negNaN = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000000UL));
        double.IsPositive(negNaN).Should().BeFalse();
    }

    // ---- IsRealNumber ----
    // Always true except NaN.

    [Test]
    [Arguments(double.NegativeInfinity, true)]
    [Arguments(-1.0, true)]
    [Arguments(double.NaN, false)]
    [Arguments(0.0, true)]
    [Arguments(1.0, true)]
    [Arguments(double.PositiveInfinity, true)]
    public void IsRealNumber_ReturnsExpected(double value, bool expected)
    {
        double.IsRealNumber(value).Should().Be(expected);
    }

    // ---- IsInteger ----

    [Test]
    [Arguments(double.NegativeInfinity, false)]
    [Arguments(double.MinValue, true)]
    [Arguments(-1.0, true)]
    [Arguments(NegativeMinNormal, false)]
    [Arguments(NegativeMaxSubnormal, false)]
    [Arguments(NegativeEpsilon, false)]
    [Arguments(double.NaN, false)]
    [Arguments(0.0, true)]
    [Arguments(double.Epsilon, false)]
    [Arguments(MaxSubnormal, false)]
    [Arguments(MinNormal, false)]
    [Arguments(1.0, true)]
    [Arguments(double.MaxValue, true)]
    [Arguments(double.PositiveInfinity, false)]
    public void IsInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsInteger(value).Should().Be(expected);
    }

    [Test]
    public void IsInteger_NegativeZero_ReturnsTrue()
    {
        double.IsInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsEvenInteger ----
    // Note: BCL says double.MinValue and double.MaxValue are even (they're enormous integers
    // whose lowest bits are zero by the FP representation).

    [Test]
    [Arguments(double.NegativeInfinity, false)]
    [Arguments(double.MinValue, true)]
    [Arguments(-1.0, false)]
    [Arguments(-2.0, true)]
    [Arguments(NegativeMinNormal, false)]
    [Arguments(NegativeMaxSubnormal, false)]
    [Arguments(NegativeEpsilon, false)]
    [Arguments(double.NaN, false)]
    [Arguments(0.0, true)]
    [Arguments(double.Epsilon, false)]
    [Arguments(MaxSubnormal, false)]
    [Arguments(MinNormal, false)]
    [Arguments(1.0, false)]
    [Arguments(2.0, true)]
    [Arguments(double.MaxValue, true)]
    [Arguments(double.PositiveInfinity, false)]
    public void IsEvenInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsEvenInteger(value).Should().Be(expected);
    }

    [Test]
    public void IsEvenInteger_NegativeZero_ReturnsTrue()
    {
        double.IsEvenInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsOddInteger ----

    [Test]
    [Arguments(double.NegativeInfinity, false)]
    [Arguments(double.MinValue, false)]
    [Arguments(-1.0, true)]
    [Arguments(-2.0, false)]
    [Arguments(NegativeMinNormal, false)]
    [Arguments(NegativeMaxSubnormal, false)]
    [Arguments(NegativeEpsilon, false)]
    [Arguments(double.NaN, false)]
    [Arguments(0.0, false)]
    [Arguments(double.Epsilon, false)]
    [Arguments(MaxSubnormal, false)]
    [Arguments(MinNormal, false)]
    [Arguments(1.0, true)]
    [Arguments(2.0, false)]
    [Arguments(double.MaxValue, false)]
    [Arguments(double.PositiveInfinity, false)]
    public void IsOddInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsOddInteger(value).Should().Be(expected);
    }

    [Test]
    public void IsOddInteger_NegativeZero_ReturnsFalse()
    {
        double.IsOddInteger(NegativeZero).Should().BeFalse();
    }

    // ---- IsPow2 ----

    [Test]
    [Arguments(double.NegativeInfinity, false)]
    [Arguments(double.MinValue, false)]
    [Arguments(-1.0, false)]
    [Arguments(-2.0, false)] // negative 2^1 is NOT a pow2 (sign-bit set).
    [Arguments(NegativeMinNormal, false)]
    [Arguments(NegativeEpsilon, false)]
    [Arguments(double.NaN, false)]
    [Arguments(0.0, false)]
    [Arguments(double.Epsilon, true)]   // 2^-1074
    [Arguments(MinNormal, true)]        // 2^-1022
    [Arguments(0.5, true)]
    [Arguments(1.0, true)]
    [Arguments(2.0, true)]
    [Arguments(4.0, true)]
    [Arguments(3.0, false)]
    [Arguments(1.5, false)]
    [Arguments(double.MaxValue, false)]
    [Arguments(double.PositiveInfinity, false)]
    public void IsPow2_ReturnsExpected(double value, bool expected)
    {
        double.IsPow2(value).Should().Be(expected);
    }

    [Test]
    public void IsPow2_NegativeZero_ReturnsFalse()
    {
        double.IsPow2(NegativeZero).Should().BeFalse();
    }
}
