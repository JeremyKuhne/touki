// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable xUnit1025 // duplicate InlineData (xUnit doesn't distinguish 0.0 from -0.0)

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
    // -0.0 cases are handled in dedicated [Fact] tests because xUnit's InlineData
    // can't distinguish 0.0 from -0.0.

    // ---- IsFinite ----

    [Theory]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(double.MinValue, true)]
    [InlineData(-1.0, true)]
    [InlineData(NegativeMinNormal, true)]
    [InlineData(NegativeMaxSubnormal, true)]
    [InlineData(NegativeEpsilon, true)]
    [InlineData(double.NaN, false)]
    [InlineData(0.0, true)]
    [InlineData(double.Epsilon, true)]
    [InlineData(MaxSubnormal, true)]
    [InlineData(MinNormal, true)]
    [InlineData(1.0, true)]
    [InlineData(double.MaxValue, true)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsFinite_ReturnsExpected(double value, bool expected)
    {
        double.IsFinite(value).Should().Be(expected);
    }

    [Fact]
    public void IsFinite_NegativeZero_ReturnsTrue()
    {
        double.IsFinite(NegativeZero).Should().BeTrue();
    }

    // ---- IsNegative ----

    [Theory]
    [InlineData(double.NegativeInfinity, true)]
    [InlineData(double.MinValue, true)]
    [InlineData(-1.0, true)]
    [InlineData(NegativeMinNormal, true)]
    [InlineData(NegativeMaxSubnormal, true)]
    [InlineData(NegativeEpsilon, true)]
    [InlineData(double.NaN, true)]
    [InlineData(0.0, false)]
    [InlineData(double.Epsilon, false)]
    [InlineData(MaxSubnormal, false)]
    [InlineData(MinNormal, false)]
    [InlineData(1.0, false)]
    [InlineData(double.MaxValue, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsNegative_ReturnsExpected(double value, bool expected)
    {
        double.IsNegative(value).Should().Be(expected);
    }

    [Fact]
    public void IsNegative_NegativeZero_ReturnsTrue()
    {
        double.IsNegative(NegativeZero).Should().BeTrue();
    }

    // ---- IsNormal ----

    [Theory]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(double.MinValue, true)]
    [InlineData(-1.0, true)]
    [InlineData(NegativeMinNormal, true)]
    [InlineData(NegativeMaxSubnormal, false)]
    [InlineData(NegativeEpsilon, false)]
    [InlineData(double.NaN, false)]
    [InlineData(0.0, false)]
    [InlineData(double.Epsilon, false)]
    [InlineData(MaxSubnormal, false)]
    [InlineData(MinNormal, true)]
    [InlineData(1.0, true)]
    [InlineData(double.MaxValue, true)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsNormal_ReturnsExpected(double value, bool expected)
    {
        double.IsNormal(value).Should().Be(expected);
    }

    [Fact]
    public void IsNormal_NegativeZero_ReturnsFalse()
    {
        double.IsNormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsSubnormal ----

    [Theory]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(double.MinValue, false)]
    [InlineData(-1.0, false)]
    [InlineData(NegativeMinNormal, false)]
    [InlineData(NegativeMaxSubnormal, true)]
    [InlineData(NegativeEpsilon, true)]
    [InlineData(double.NaN, false)]
    [InlineData(0.0, false)]
    [InlineData(double.Epsilon, true)]
    [InlineData(MaxSubnormal, true)]
    [InlineData(MinNormal, false)]
    [InlineData(1.0, false)]
    [InlineData(double.MaxValue, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsSubnormal_ReturnsExpected(double value, bool expected)
    {
        double.IsSubnormal(value).Should().Be(expected);
    }

    [Fact]
    public void IsSubnormal_NegativeZero_ReturnsFalse()
    {
        double.IsSubnormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsPositive ----

    [Theory]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(double.MinValue, false)]
    [InlineData(-1.0, false)]
    [InlineData(NegativeMinNormal, false)]
    [InlineData(NegativeMaxSubnormal, false)]
    [InlineData(NegativeEpsilon, false)]
    [InlineData(double.NaN, false)]
    [InlineData(0.0, true)]
    [InlineData(double.Epsilon, true)]
    [InlineData(MaxSubnormal, true)]
    [InlineData(MinNormal, true)]
    [InlineData(1.0, true)]
    [InlineData(double.MaxValue, true)]
    [InlineData(double.PositiveInfinity, true)]
    public void IsPositive_ReturnsExpected(double value, bool expected)
    {
        double.IsPositive(value).Should().Be(expected);
    }

    [Fact]
    public void IsPositive_NegativeZero_ReturnsFalse()
    {
        double.IsPositive(NegativeZero).Should().BeFalse();
    }

    [Fact]
    public void IsPositive_NegativeNaN_ReturnsFalse()
    {
        double negNaN = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000000UL));
        double.IsPositive(negNaN).Should().BeFalse();
    }

    // ---- IsRealNumber ----
    // Always true except NaN.

    [Theory]
    [InlineData(double.NegativeInfinity, true)]
    [InlineData(-1.0, true)]
    [InlineData(double.NaN, false)]
    [InlineData(0.0, true)]
    [InlineData(1.0, true)]
    [InlineData(double.PositiveInfinity, true)]
    public void IsRealNumber_ReturnsExpected(double value, bool expected)
    {
        double.IsRealNumber(value).Should().Be(expected);
    }

    // ---- IsInteger ----

    [Theory]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(double.MinValue, true)]
    [InlineData(-1.0, true)]
    [InlineData(NegativeMinNormal, false)]
    [InlineData(NegativeMaxSubnormal, false)]
    [InlineData(NegativeEpsilon, false)]
    [InlineData(double.NaN, false)]
    [InlineData(0.0, true)]
    [InlineData(double.Epsilon, false)]
    [InlineData(MaxSubnormal, false)]
    [InlineData(MinNormal, false)]
    [InlineData(1.0, true)]
    [InlineData(double.MaxValue, true)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsInteger(value).Should().Be(expected);
    }

    [Fact]
    public void IsInteger_NegativeZero_ReturnsTrue()
    {
        double.IsInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsEvenInteger ----
    // Note: BCL says double.MinValue and double.MaxValue are even (they're enormous integers
    // whose lowest bits are zero by the FP representation).

    [Theory]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(double.MinValue, true)]
    [InlineData(-1.0, false)]
    [InlineData(-2.0, true)]
    [InlineData(NegativeMinNormal, false)]
    [InlineData(NegativeMaxSubnormal, false)]
    [InlineData(NegativeEpsilon, false)]
    [InlineData(double.NaN, false)]
    [InlineData(0.0, true)]
    [InlineData(double.Epsilon, false)]
    [InlineData(MaxSubnormal, false)]
    [InlineData(MinNormal, false)]
    [InlineData(1.0, false)]
    [InlineData(2.0, true)]
    [InlineData(double.MaxValue, true)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsEvenInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsEvenInteger(value).Should().Be(expected);
    }

    [Fact]
    public void IsEvenInteger_NegativeZero_ReturnsTrue()
    {
        double.IsEvenInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsOddInteger ----

    [Theory]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(double.MinValue, false)]
    [InlineData(-1.0, true)]
    [InlineData(-2.0, false)]
    [InlineData(NegativeMinNormal, false)]
    [InlineData(NegativeMaxSubnormal, false)]
    [InlineData(NegativeEpsilon, false)]
    [InlineData(double.NaN, false)]
    [InlineData(0.0, false)]
    [InlineData(double.Epsilon, false)]
    [InlineData(MaxSubnormal, false)]
    [InlineData(MinNormal, false)]
    [InlineData(1.0, true)]
    [InlineData(2.0, false)]
    [InlineData(double.MaxValue, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsOddInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsOddInteger(value).Should().Be(expected);
    }

    [Fact]
    public void IsOddInteger_NegativeZero_ReturnsFalse()
    {
        double.IsOddInteger(NegativeZero).Should().BeFalse();
    }

    // ---- IsPow2 ----

    [Theory]
    [InlineData(double.NegativeInfinity, false)]
    [InlineData(double.MinValue, false)]
    [InlineData(-1.0, false)]
    [InlineData(-2.0, false)] // negative 2^1 is NOT a pow2 (sign-bit set).
    [InlineData(NegativeMinNormal, false)]
    [InlineData(NegativeEpsilon, false)]
    [InlineData(double.NaN, false)]
    [InlineData(0.0, false)]
    [InlineData(double.Epsilon, true)]   // 2^-1074
    [InlineData(MinNormal, true)]        // 2^-1022
    [InlineData(0.5, true)]
    [InlineData(1.0, true)]
    [InlineData(2.0, true)]
    [InlineData(4.0, true)]
    [InlineData(3.0, false)]
    [InlineData(1.5, false)]
    [InlineData(double.MaxValue, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsPow2_ReturnsExpected(double value, bool expected)
    {
        double.IsPow2(value).Should().Be(expected);
    }

    [Fact]
    public void IsPow2_NegativeZero_ReturnsFalse()
    {
        double.IsPow2(NegativeZero).Should().BeFalse();
    }
}
