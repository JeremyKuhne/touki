// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class DoubleExtensionsTests
{
    private const double MinNormal = 2.2250738585072014e-308;
    private const double MaxSubnormal = 2.2250738585072009e-308;
    private const double NegativeZero = -0.0;
    private const double NegativeMinNormal = -2.2250738585072014e-308;
    private const double NegativeMaxSubnormal = -2.2250738585072009e-308;
    private const double NegativeEpsilon = -double.Epsilon;

    // The BCL test files exercise this canonical 15-row table for each predicate.
    // -0.0 cases are handled in dedicated [TestMethod] tests because parameterized
    // arguments can't distinguish 0.0 from -0.0.

    // ---- IsFinite ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, false)]
    [DataRow(double.MinValue, true)]
    [DataRow(-1.0, true)]
    [DataRow(NegativeMinNormal, true)]
    [DataRow(NegativeMaxSubnormal, true)]
    [DataRow(NegativeEpsilon, true)]
    [DataRow(double.NaN, false)]
    [DataRow(0.0, true)]
    [DataRow(double.Epsilon, true)]
    [DataRow(MaxSubnormal, true)]
    [DataRow(MinNormal, true)]
    [DataRow(1.0, true)]
    [DataRow(double.MaxValue, true)]
    [DataRow(double.PositiveInfinity, false)]
    public void IsFinite_ReturnsExpected(double value, bool expected)
    {
        double.IsFinite(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsFinite_NegativeZero_ReturnsTrue()
    {
        double.IsFinite(NegativeZero).Should().BeTrue();
    }

    // ---- IsNegative ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, true)]
    [DataRow(double.MinValue, true)]
    [DataRow(-1.0, true)]
    [DataRow(NegativeMinNormal, true)]
    [DataRow(NegativeMaxSubnormal, true)]
    [DataRow(NegativeEpsilon, true)]
    [DataRow(double.NaN, true)]
    [DataRow(0.0, false)]
    [DataRow(double.Epsilon, false)]
    [DataRow(MaxSubnormal, false)]
    [DataRow(MinNormal, false)]
    [DataRow(1.0, false)]
    [DataRow(double.MaxValue, false)]
    [DataRow(double.PositiveInfinity, false)]
    public void IsNegative_ReturnsExpected(double value, bool expected)
    {
        double.IsNegative(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsNegative_NegativeZero_ReturnsTrue()
    {
        double.IsNegative(NegativeZero).Should().BeTrue();
    }

    // ---- IsNormal ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, false)]
    [DataRow(double.MinValue, true)]
    [DataRow(-1.0, true)]
    [DataRow(NegativeMinNormal, true)]
    [DataRow(NegativeMaxSubnormal, false)]
    [DataRow(NegativeEpsilon, false)]
    [DataRow(double.NaN, false)]
    [DataRow(0.0, false)]
    [DataRow(double.Epsilon, false)]
    [DataRow(MaxSubnormal, false)]
    [DataRow(MinNormal, true)]
    [DataRow(1.0, true)]
    [DataRow(double.MaxValue, true)]
    [DataRow(double.PositiveInfinity, false)]
    public void IsNormal_ReturnsExpected(double value, bool expected)
    {
        double.IsNormal(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsNormal_NegativeZero_ReturnsFalse()
    {
        double.IsNormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsSubnormal ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, false)]
    [DataRow(double.MinValue, false)]
    [DataRow(-1.0, false)]
    [DataRow(NegativeMinNormal, false)]
    [DataRow(NegativeMaxSubnormal, true)]
    [DataRow(NegativeEpsilon, true)]
    [DataRow(double.NaN, false)]
    [DataRow(0.0, false)]
    [DataRow(double.Epsilon, true)]
    [DataRow(MaxSubnormal, true)]
    [DataRow(MinNormal, false)]
    [DataRow(1.0, false)]
    [DataRow(double.MaxValue, false)]
    [DataRow(double.PositiveInfinity, false)]
    public void IsSubnormal_ReturnsExpected(double value, bool expected)
    {
        double.IsSubnormal(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsSubnormal_NegativeZero_ReturnsFalse()
    {
        double.IsSubnormal(NegativeZero).Should().BeFalse();
    }

    // ---- IsPositive ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, false)]
    [DataRow(double.MinValue, false)]
    [DataRow(-1.0, false)]
    [DataRow(NegativeMinNormal, false)]
    [DataRow(NegativeMaxSubnormal, false)]
    [DataRow(NegativeEpsilon, false)]
    [DataRow(double.NaN, false)]
    [DataRow(0.0, true)]
    [DataRow(double.Epsilon, true)]
    [DataRow(MaxSubnormal, true)]
    [DataRow(MinNormal, true)]
    [DataRow(1.0, true)]
    [DataRow(double.MaxValue, true)]
    [DataRow(double.PositiveInfinity, true)]
    public void IsPositive_ReturnsExpected(double value, bool expected)
    {
        double.IsPositive(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsPositive_NegativeZero_ReturnsFalse()
    {
        double.IsPositive(NegativeZero).Should().BeFalse();
    }

    [TestMethod]
    public void IsPositive_NegativeNaN_ReturnsFalse()
    {
        double negNaN = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000000UL));
        double.IsPositive(negNaN).Should().BeFalse();
    }

    // ---- IsRealNumber ----
    // Always true except NaN.

    [TestMethod]
    [DataRow(double.NegativeInfinity, true)]
    [DataRow(-1.0, true)]
    [DataRow(double.NaN, false)]
    [DataRow(0.0, true)]
    [DataRow(1.0, true)]
    [DataRow(double.PositiveInfinity, true)]
    public void IsRealNumber_ReturnsExpected(double value, bool expected)
    {
        double.IsRealNumber(value).Should().Be(expected);
    }

    // ---- IsInteger ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, false)]
    [DataRow(double.MinValue, true)]
    [DataRow(-1.0, true)]
    [DataRow(NegativeMinNormal, false)]
    [DataRow(NegativeMaxSubnormal, false)]
    [DataRow(NegativeEpsilon, false)]
    [DataRow(double.NaN, false)]
    [DataRow(0.0, true)]
    [DataRow(double.Epsilon, false)]
    [DataRow(MaxSubnormal, false)]
    [DataRow(MinNormal, false)]
    [DataRow(1.0, true)]
    [DataRow(double.MaxValue, true)]
    [DataRow(double.PositiveInfinity, false)]
    public void IsInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsInteger(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsInteger_NegativeZero_ReturnsTrue()
    {
        double.IsInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsEvenInteger ----
    // Note: BCL says double.MinValue and double.MaxValue are even (they're enormous integers
    // whose lowest bits are zero by the FP representation).

    [TestMethod]
    [DataRow(double.NegativeInfinity, false)]
    [DataRow(double.MinValue, true)]
    [DataRow(-1.0, false)]
    [DataRow(-2.0, true)]
    [DataRow(NegativeMinNormal, false)]
    [DataRow(NegativeMaxSubnormal, false)]
    [DataRow(NegativeEpsilon, false)]
    [DataRow(double.NaN, false)]
    [DataRow(0.0, true)]
    [DataRow(double.Epsilon, false)]
    [DataRow(MaxSubnormal, false)]
    [DataRow(MinNormal, false)]
    [DataRow(1.0, false)]
    [DataRow(2.0, true)]
    [DataRow(double.MaxValue, true)]
    [DataRow(double.PositiveInfinity, false)]
    public void IsEvenInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsEvenInteger(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsEvenInteger_NegativeZero_ReturnsTrue()
    {
        double.IsEvenInteger(NegativeZero).Should().BeTrue();
    }

    // ---- IsOddInteger ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, false)]
    [DataRow(double.MinValue, false)]
    [DataRow(-1.0, true)]
    [DataRow(-2.0, false)]
    [DataRow(NegativeMinNormal, false)]
    [DataRow(NegativeMaxSubnormal, false)]
    [DataRow(NegativeEpsilon, false)]
    [DataRow(double.NaN, false)]
    [DataRow(0.0, false)]
    [DataRow(double.Epsilon, false)]
    [DataRow(MaxSubnormal, false)]
    [DataRow(MinNormal, false)]
    [DataRow(1.0, true)]
    [DataRow(2.0, false)]
    [DataRow(double.MaxValue, false)]
    [DataRow(double.PositiveInfinity, false)]
    public void IsOddInteger_ReturnsExpected(double value, bool expected)
    {
        double.IsOddInteger(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsOddInteger_NegativeZero_ReturnsFalse()
    {
        double.IsOddInteger(NegativeZero).Should().BeFalse();
    }

    // ---- IsPow2 ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, false)]
    [DataRow(double.MinValue, false)]
    [DataRow(-1.0, false)]
    [DataRow(-2.0, false)] // negative 2^1 is NOT a pow2 (sign-bit set).
    [DataRow(NegativeMinNormal, false)]
    [DataRow(NegativeEpsilon, false)]
    [DataRow(double.NaN, false)]
    [DataRow(0.0, false)]
    [DataRow(double.Epsilon, true)]   // 2^-1074
    [DataRow(MinNormal, true)]        // 2^-1022
    [DataRow(0.5, true)]
    [DataRow(1.0, true)]
    [DataRow(2.0, true)]
    [DataRow(4.0, true)]
    [DataRow(3.0, false)]
    [DataRow(1.5, false)]
    [DataRow(double.MaxValue, false)]
    [DataRow(double.PositiveInfinity, false)]
    public void IsPow2_ReturnsExpected(double value, bool expected)
    {
        double.IsPow2(value).Should().Be(expected);
    }

    [TestMethod]
    public void IsPow2_NegativeZero_ReturnsFalse()
    {
        double.IsPow2(NegativeZero).Should().BeFalse();
    }
}
