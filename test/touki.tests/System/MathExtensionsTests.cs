// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class MathExtensionsTests
{
    // BCL test files use 2^-50 instead of 2^-52 for cross-platform libm tolerance.
    private const double CrossPlatformMachineEpsilon = 8.8817841970012523e-16;

    // ---- Clamp ----

    [TestMethod]
    [DataRow(5, 0, 10, 5)]
    [DataRow(-5, 0, 10, 0)]
    [DataRow(15, 0, 10, 10)]
    [DataRow(0, 0, 10, 0)]
    [DataRow(10, 0, 10, 10)]
    public void Clamp_Int_ReturnsExpected(int value, int min, int max, int expected)
    {
        Math.Clamp(value, min, max).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(0.5, 0.0, 1.0, 0.5)]
    [DataRow(-0.5, 0.0, 1.0, 0.0)]
    [DataRow(1.5, 0.0, 1.0, 1.0)]
    public void Clamp_Double_ReturnsExpected(double value, double min, double max, double expected)
    {
        Math.Clamp(value, min, max).Should().Be(expected);
    }

    [TestMethod]
    public void Clamp_AllSignedAndUnsigned_RoundTrips()
    {
        Math.Clamp((byte)5, (byte)0, (byte)10).Should().Be((byte)5);
        Math.Clamp((sbyte)-5, (sbyte)-10, (sbyte)0).Should().Be((sbyte)-5);
        Math.Clamp((short)5, (short)0, (short)10).Should().Be((short)5);
        Math.Clamp((ushort)5, (ushort)0, (ushort)10).Should().Be((ushort)5);
        Math.Clamp(5u, 0u, 10u).Should().Be(5u);
        Math.Clamp(5L, 0L, 10L).Should().Be(5L);
        Math.Clamp(5UL, 0UL, 10UL).Should().Be(5UL);
        Math.Clamp(0.5f, 0f, 1f).Should().Be(0.5f);
        Math.Clamp(0.5m, 0m, 1m).Should().Be(0.5m);
    }

    [TestMethod]
    public void Clamp_AllTypes_BelowMin_ReturnsMin()
    {
        Math.Clamp((byte)0, (byte)5, (byte)10).Should().Be((byte)5);
        Math.Clamp((sbyte)-20, (sbyte)-10, (sbyte)0).Should().Be((sbyte)-10);
        Math.Clamp((short)-20, (short)0, (short)10).Should().Be((short)0);
        Math.Clamp((ushort)0, (ushort)5, (ushort)10).Should().Be((ushort)5);
        Math.Clamp(0u, 5u, 10u).Should().Be(5u);
        Math.Clamp(-20L, 0L, 10L).Should().Be(0L);
        Math.Clamp(0UL, 5UL, 10UL).Should().Be(5UL);
        Math.Clamp(-1f, 0f, 1f).Should().Be(0f);
        Math.Clamp(-1m, 0m, 1m).Should().Be(0m);
    }

    [TestMethod]
    public void Clamp_AllTypes_AboveMax_ReturnsMax()
    {
        Math.Clamp((byte)20, (byte)0, (byte)10).Should().Be((byte)10);
        Math.Clamp((sbyte)20, (sbyte)-10, (sbyte)0).Should().Be((sbyte)0);
        Math.Clamp((short)20, (short)0, (short)10).Should().Be((short)10);
        Math.Clamp((ushort)20, (ushort)0, (ushort)10).Should().Be((ushort)10);
        Math.Clamp(20u, 0u, 10u).Should().Be(10u);
        Math.Clamp(20L, 0L, 10L).Should().Be(10L);
        Math.Clamp(20UL, 0UL, 10UL).Should().Be(10UL);
        Math.Clamp(2f, 0f, 1f).Should().Be(1f);
        Math.Clamp(2m, 0m, 1m).Should().Be(1m);
    }

    // Mirror of BCL Clamp_MinGreaterThanMax_ThrowsArgumentException across all overloads.
    [TestMethod]
    public void Clamp_MinGreaterThanMax_AllTypes_Throw()
    {
        ((Action)(() => Math.Clamp((sbyte)1, (sbyte)2, (sbyte)1))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp((byte)1, (byte)2, (byte)1))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp((short)1, (short)2, (short)1))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp((ushort)1, (ushort)2, (ushort)1))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp(1, 2, 1))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp(1u, 2u, 1u))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp(1L, 2L, 1L))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp(1UL, 2UL, 1UL))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp(1f, 2f, 1f))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp(1.0, 2.0, 1.0))).Should().Throw<ArgumentException>();
        ((Action)(() => Math.Clamp(1m, 2m, 1m))).Should().Throw<ArgumentException>();
    }

    // ---- DivRem (tuple-returning overloads) ----

    [TestMethod]
    [DataRow((sbyte)sbyte.MaxValue, (sbyte)sbyte.MaxValue, (sbyte)1, (sbyte)0)]
    [DataRow((sbyte)sbyte.MaxValue, (sbyte)2, (sbyte)63, (sbyte)1)]
    [DataRow((sbyte)80, (sbyte)22, (sbyte)3, (sbyte)14)]
    [DataRow((sbyte)80, (sbyte)-22, (sbyte)-3, (sbyte)14)]
    [DataRow((sbyte)-80, (sbyte)22, (sbyte)-3, (sbyte)-14)]
    [DataRow((sbyte)-80, (sbyte)-22, (sbyte)3, (sbyte)-14)]
    [DataRow((sbyte)0, (sbyte)1, (sbyte)0, (sbyte)0)]
    public void DivRem_Sbyte_ReturnsTuple(sbyte dividend, sbyte divisor, sbyte expectedQuotient, sbyte expectedRemainder)
    {
        (sbyte q, sbyte r) = Math.DivRem(dividend, divisor);
        q.Should().Be(expectedQuotient);
        r.Should().Be(expectedRemainder);
    }

    [TestMethod]
    public void DivRem_Sbyte_ZeroDivisor_Throws()
    {
        ((Action)(() => Math.DivRem((sbyte)1, (sbyte)0))).Should().Throw<DivideByZeroException>();
    }

    [TestMethod]
    [DataRow((byte)byte.MaxValue, (byte)byte.MaxValue, (byte)1, (byte)0)]
    [DataRow((byte)byte.MaxValue, (byte)2, (byte)127, (byte)1)]
    [DataRow((byte)52, (byte)5, (byte)10, (byte)2)]
    [DataRow((byte)100, (byte)33, (byte)3, (byte)1)]
    [DataRow((byte)0, (byte)1, (byte)0, (byte)0)]
    public void DivRem_Byte_ReturnsTuple(byte dividend, byte divisor, byte expectedQuotient, byte expectedRemainder)
    {
        (byte q, byte r) = Math.DivRem(dividend, divisor);
        q.Should().Be(expectedQuotient);
        r.Should().Be(expectedRemainder);
    }

    [TestMethod]
    public void DivRem_Byte_ZeroDivisor_Throws()
    {
        ((Action)(() => Math.DivRem((byte)1, (byte)0))).Should().Throw<DivideByZeroException>();
    }

    [TestMethod]
    [DataRow((short)short.MaxValue, (short)2, (short)16383, (short)1)]
    [DataRow((short)12345, (short)22424, (short)0, (short)12345)]
    [DataRow((short)300, (short)22, (short)13, (short)14)]
    [DataRow((short)-300, (short)-22, (short)13, (short)-14)]
    [DataRow((short)13952, (short)2000, (short)6, (short)1952)]
    public void DivRem_Short_ReturnsTuple(short dividend, short divisor, short expectedQuotient, short expectedRemainder)
    {
        (short q, short r) = Math.DivRem(dividend, divisor);
        q.Should().Be(expectedQuotient);
        r.Should().Be(expectedRemainder);
    }

    [TestMethod]
    public void DivRem_Short_ZeroDivisor_Throws()
    {
        ((Action)(() => Math.DivRem((short)1, (short)0))).Should().Throw<DivideByZeroException>();
    }

    [TestMethod]
    [DataRow((ushort)ushort.MaxValue, (ushort)2, (ushort)32767, (ushort)1)]
    [DataRow((ushort)51474, (ushort)31474, (ushort)1, (ushort)20000)]
    [DataRow((ushort)10000, (ushort)333, (ushort)30, (ushort)10)]
    public void DivRem_Ushort_ReturnsTuple(ushort dividend, ushort divisor, ushort expectedQuotient, ushort expectedRemainder)
    {
        (ushort q, ushort r) = Math.DivRem(dividend, divisor);
        q.Should().Be(expectedQuotient);
        r.Should().Be(expectedRemainder);
    }

    [TestMethod]
    public void DivRem_Ushort_ZeroDivisor_Throws()
    {
        ((Action)(() => Math.DivRem((ushort)1, (ushort)0))).Should().Throw<DivideByZeroException>();
    }

    [TestMethod]
    [DataRow(9223372036854775807L, 2000L, 4611686018427387L, 1807L)]
    [DataRow(-9223372036854775808L, -2000L, 4611686018427387L, -1808L)]
    [DataRow(9223372036854775807L, -2000L, -4611686018427387L, 1807L)]
    [DataRow(13952L, 2000L, 6L, 1952L)]
    [DataRow(-14032L, 2000L, -7L, -32L)]
    public void DivRem_Long_ReturnsTuple(long dividend, long divisor, long expectedQuotient, long expectedRemainder)
    {
        (long q, long r) = Math.DivRem(dividend, divisor);
        q.Should().Be(expectedQuotient);
        r.Should().Be(expectedRemainder);
    }

    [TestMethod]
    public void DivRem_Long_ZeroDivisor_Throws()
    {
        ((Action)(() => Math.DivRem(1L, 0L))).Should().Throw<DivideByZeroException>();
    }

    // ---- Acosh ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, double.NaN)]
    [DataRow(-1.0, double.NaN)]
    [DataRow(0.0, double.NaN)]
    [DataRow(0.5, double.NaN)]
    [DataRow(0.99999999, double.NaN)]
    [DataRow(double.NaN, double.NaN)]
    [DataRow(1.0, 0.0)]
    [DataRow(double.PositiveInfinity, double.PositiveInfinity)]
    public void Acosh_EdgeCases_ReturnsExpected(double value, double expected)
    {
        double actual = Math.Acosh(value);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().Be(expected);
        }
    }

    [TestMethod]
    [DataRow(1.5430806348152438, 1.0)]
    [DataRow(2.5091784786580568, 1.5707963267948966)]   // (pi / 2)
    [DataRow(11.591953275521521, 3.1415926535897932)]   // (pi)
    public void Acosh_KnownValues_MatchBcl(double value, double expected)
    {
        Math.Acosh(value).Should().BeApproximately(expected, CrossPlatformMachineEpsilon * 10);
    }

    [TestMethod]
    public void Acosh_LargeValue_DoesNotOverflow()
    {
        double result = Math.Acosh(1e308);
        double.IsFinite(result).Should().BeTrue();
        result.Should().BeGreaterThan(0.0);
    }

    // ---- Asinh ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, double.NegativeInfinity)]
    [DataRow(double.NaN, double.NaN)]
    [DataRow(0.0, 0.0)]
    [DataRow(double.PositiveInfinity, double.PositiveInfinity)]
    public void Asinh_EdgeCases_ReturnsExpected(double value, double expected)
    {
        double actual = Math.Asinh(value);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().Be(expected);
        }
    }

    [TestMethod]
    [DataRow(1.1752011936438015, 1.0)]
    [DataRow(2.3012989023072949, 1.5707963267948966)]   // (pi / 2)
    [DataRow(11.548739357257748, 3.1415926535897932)]   // (pi)
    public void Asinh_KnownValues_MatchBcl(double value, double expected)
    {
        Math.Asinh(value).Should().BeApproximately(expected, CrossPlatformMachineEpsilon * 10);
    }

    [TestMethod]
    [DataRow(1.0)]
    [DataRow(0.5)]
    [DataRow(123.456)]
    [DataRow(1e308)]
    public void Asinh_OddSymmetry_NegEqualsNegated(double value)
    {
        Math.Asinh(-value).Should().Be(-Math.Asinh(value));
    }

    // ---- Atanh ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, double.NaN)]
    [DataRow(-1.5, double.NaN)]
    [DataRow(-1.0, double.NegativeInfinity)]
    [DataRow(0.0, 0.0)]
    [DataRow(1.0, double.PositiveInfinity)]
    [DataRow(1.5, double.NaN)]
    [DataRow(double.PositiveInfinity, double.NaN)]
    [DataRow(double.NaN, double.NaN)]
    public void Atanh_EdgeCases_ReturnsExpected(double value, double expected)
    {
        double actual = Math.Atanh(value);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().Be(expected);
        }
    }

    [TestMethod]
    [DataRow(0.76159415595576489, 1.0)]
    [DataRow(0.91715233566727435, 1.5707963267948966)]   // (pi / 2)
    public void Atanh_KnownValues_MatchBcl(double value, double expected)
    {
        Math.Atanh(value).Should().BeApproximately(expected, CrossPlatformMachineEpsilon * 10);
    }

    // ---- Cbrt ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, double.NegativeInfinity)]
    [DataRow(-8.0, -2.0)]
    [DataRow(-1.0, -1.0)]
    [DataRow(0.0, 0.0)]
    [DataRow(1.0, 1.0)]
    [DataRow(8.0, 2.0)]
    [DataRow(27.0, 3.0)]
    [DataRow(double.PositiveInfinity, double.PositiveInfinity)]
    [DataRow(double.NaN, double.NaN)]
    public void Cbrt_EdgeCases_ReturnsExpected(double value, double expected)
    {
        double actual = Math.Cbrt(value);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().BeApproximately(expected, CrossPlatformMachineEpsilon * 10);
        }
    }

    [TestMethod]
    public void Cbrt_NegativeZero_PreservesSign()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000_0000_0000_0000UL));
        double result = Math.Cbrt(negativeZero);
        result.Should().Be(0.0);
        double.IsNegative(result).Should().BeTrue();
    }

    // ---- BitDecrement / BitIncrement ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, double.NegativeInfinity)]
    [DataRow(double.PositiveInfinity, double.MaxValue)]
    [DataRow(double.NaN, double.NaN)]
    [DataRow(1.0, 0.99999999999999989)]
    [DataRow(0.0, -double.Epsilon)]
    public void BitDecrement_EdgeCases_ReturnsExpected(double value, double expected)
    {
        double actual = Math.BitDecrement(value);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().Be(expected);
        }
    }

    [TestMethod]
    [DataRow(double.NegativeInfinity, double.MinValue)]
    [DataRow(double.PositiveInfinity, double.PositiveInfinity)]
    [DataRow(double.NaN, double.NaN)]
    [DataRow(1.0, 1.0000000000000002)]
    public void BitIncrement_EdgeCases_ReturnsExpected(double value, double expected)
    {
        double actual = Math.BitIncrement(value);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().Be(expected);
        }
    }

    [TestMethod]
    public void BitIncrement_NegativeZero_ReturnsEpsilon()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000_0000_0000_0000UL));
        Math.BitIncrement(negativeZero).Should().Be(double.Epsilon);
    }

    [TestMethod]
    public void BitIncrement_BitDecrement_AreInverse()
    {
        double[] values = [1.0, 2.0, -3.0, 1e-100, 1e100];
        foreach (double v in values)
        {
            Math.BitIncrement(Math.BitDecrement(v)).Should().Be(v);
            Math.BitDecrement(Math.BitIncrement(v)).Should().Be(v);
        }
    }

    // ---- CopySign ----

    [TestMethod]
    [DataRow(5.0, -1.0, -5.0)]
    [DataRow(-5.0, 1.0, 5.0)]
    [DataRow(5.0, double.NegativeInfinity, -5.0)]
    [DataRow(5.0, double.PositiveInfinity, 5.0)]
    [DataRow(double.PositiveInfinity, -1.0, double.NegativeInfinity)]
    [DataRow(double.NegativeInfinity, 1.0, double.PositiveInfinity)]
    public void CopySign_EdgeCases_ReturnsExpected(double x, double y, double expected)
    {
        Math.CopySign(x, y).Should().Be(expected);
    }

    [TestMethod]
    public void CopySign_NegativeNaNSign_PropagatesSignBit()
    {
        double negNaN = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000000UL));
        double result = Math.CopySign(5.0, negNaN);
        double.IsNegative(result).Should().BeTrue();
        result.Should().Be(-5.0);
    }

    // ---- FusedMultiplyAdd ----

    [TestMethod]
    [DataRow(2.0, 3.0, 4.0, 10.0)]
    [DataRow(0.0, double.PositiveInfinity, 1.0, double.NaN)]
    public void FusedMultiplyAdd_EdgeCases_ReturnsExpected(double x, double y, double z, double expected)
    {
        double actual = Math.FusedMultiplyAdd(x, y, z);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().Be(expected);
        }
    }

    [TestMethod]
    public void FusedMultiplyAdd_NaNInput_ReturnsNaN()
    {
        double.IsNaN(Math.FusedMultiplyAdd(double.NaN, 1.0, 1.0)).Should().BeTrue();
        double.IsNaN(Math.FusedMultiplyAdd(1.0, double.NaN, 1.0)).Should().BeTrue();
        double.IsNaN(Math.FusedMultiplyAdd(1.0, 1.0, double.NaN)).Should().BeTrue();
    }

    // ---- ILogB ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, int.MaxValue)]
    [DataRow(double.PositiveInfinity, int.MaxValue)]
    [DataRow(double.NaN, int.MaxValue)]
    [DataRow(0.0, int.MinValue)]
    [DataRow(1.0, 0)]
    [DataRow(2.0, 1)]
    [DataRow(0.5, -1)]
    [DataRow(8.0, 3)]
    [DataRow(0.125, -3)]
    [DataRow(double.Epsilon, -1074)]
    [DataRow(double.MaxValue, 1023)]
    public void ILogB_EdgeCases_ReturnsExpected(double value, int expected)
    {
        Math.ILogB(value).Should().Be(expected);
    }

    [TestMethod]
    public void ILogB_NegativeZero_ReturnsIntMinValue()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000_0000_0000_0000UL));
        Math.ILogB(negativeZero).Should().Be(int.MinValue);
    }

    // ---- Log2 ----

    [TestMethod]
    [DataRow(double.NegativeInfinity, double.NaN)]
    [DataRow(-1.0, double.NaN)]
    [DataRow(0.0, double.NegativeInfinity)]
    [DataRow(1.0, 0.0)]
    [DataRow(2.0, 1.0)]
    [DataRow(8.0, 3.0)]
    [DataRow(0.5, -1.0)]
    [DataRow(double.PositiveInfinity, double.PositiveInfinity)]
    [DataRow(double.NaN, double.NaN)]
    public void Log2_EdgeCases_ReturnsExpected(double value, double expected)
    {
        double actual = Math.Log2(value);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().BeApproximately(expected, CrossPlatformMachineEpsilon * 10);
        }
    }

    // ---- MaxMagnitude / MinMagnitude ----

    [TestMethod]
    [DataRow(-5.0, 3.0, -5.0)]
    [DataRow(2.0, -7.0, -7.0)]
    [DataRow(3.0, 3.0, 3.0)]
    [DataRow(-3.0, 3.0, 3.0)]
    [DataRow(double.PositiveInfinity, 1.0, double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity, 1.0, double.NegativeInfinity)]
    public void MaxMagnitude_EdgeCases_ReturnsExpected(double x, double y, double expected)
    {
        Math.MaxMagnitude(x, y).Should().Be(expected);
    }

    [TestMethod]
    public void MaxMagnitude_NaNInput_ReturnsNaN()
    {
        double.IsNaN(Math.MaxMagnitude(double.NaN, 1.0)).Should().BeTrue();
        double.IsNaN(Math.MaxMagnitude(1.0, double.NaN)).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(-5.0, 3.0, 3.0)]
    [DataRow(2.0, -7.0, 2.0)]
    [DataRow(3.0, 3.0, 3.0)]
    [DataRow(-3.0, 3.0, -3.0)]
    public void MinMagnitude_EdgeCases_ReturnsExpected(double x, double y, double expected)
    {
        Math.MinMagnitude(x, y).Should().Be(expected);
    }

    [TestMethod]
    public void MinMagnitude_NaNInput_ReturnsNaN()
    {
        double.IsNaN(Math.MinMagnitude(double.NaN, 1.0)).Should().BeTrue();
        double.IsNaN(Math.MinMagnitude(1.0, double.NaN)).Should().BeTrue();
    }

    // ---- ScaleB ----

    [TestMethod]
    [DataRow(1.0, 1, 2.0)]
    [DataRow(1.0, 10, 1024.0)]
    [DataRow(1.0, -1, 0.5)]
    [DataRow(3.0, 0, 3.0)]
    [DataRow(0.0, 100, 0.0)]
    [DataRow(double.PositiveInfinity, 0, double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity, 0, double.NegativeInfinity)]
    [DataRow(double.NaN, 0, double.NaN)]
    [DataRow(1.0, int.MaxValue, double.PositiveInfinity)]
    [DataRow(double.MaxValue, 1, double.PositiveInfinity)]
    public void ScaleB_EdgeCases_ReturnsExpected(double x, int n, double expected)
    {
        double actual = Math.ScaleB(x, n);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().Be(expected);
        }
    }

    [TestMethod]
    public void ScaleB_LargeNegativeN_ReachesSubnormal()
    {
        // 1.0 * 2^-1074 == double.Epsilon (smallest subnormal).
        Math.ScaleB(1.0, -1074).Should().Be(double.Epsilon);
    }

    // ---- ReciprocalEstimate / ReciprocalSqrtEstimate / SinCos ----

    // Math.ReciprocalEstimate / ReciprocalSqrtEstimate are documented as
    // platform-dependent approximations. x86 RCPSS / RSQRTSS give ~11 bits of
    // precision (~5e-4); ARMv8 FRECPE / FRSQRTE guarantee accuracy to ~1 part
    // in 256 (~4e-3). The macOS-arm64 runner returns 0.4990234375 (= 511/1024)
    // for ReciprocalEstimate(2.0). 1e-2 covers both architectures with margin.
    private const double ReciprocalEstimateTolerance = 1e-2;

    [TestMethod]
    public void ReciprocalEstimate_OfTwo_IsHalf()
    {
        Math.ReciprocalEstimate(2.0).Should().BeApproximately(0.5, ReciprocalEstimateTolerance);
    }

    [TestMethod]
    public void ReciprocalSqrtEstimate_OfFour_IsHalf()
    {
        Math.ReciprocalSqrtEstimate(4.0).Should().BeApproximately(0.5, ReciprocalEstimateTolerance);
    }

    [TestMethod]
    public void SinCos_ReturnsSinAndCos()
    {
        (double sin, double cos) = Math.SinCos(0.0);
        sin.Should().Be(0.0);
        cos.Should().Be(1.0);
    }

    [TestMethod]
    [DataRow(1.0)]
    [DataRow(1.5707963267948966)] // pi/2
    [DataRow(3.1415926535897932)] // pi
    public void SinCos_MatchesIndividualSinAndCos(double value)
    {
        (double sin, double cos) = Math.SinCos(value);
        sin.Should().BeApproximately(Math.Sin(value), 1e-15);
        cos.Should().BeApproximately(Math.Cos(value), 1e-15);
    }

    // ---- BigMul ----

    [TestMethod]
    [DataRow(0, 0, 0L)]
    [DataRow(2, 3, 6L)]
    [DataRow(3, -2, -6L)]
    [DataRow(-1, -1, 1L)]
    [DataRow(int.MaxValue, 2, (long)int.MaxValue * 2)]
    [DataRow(int.MinValue, 2, (long)int.MinValue * 2)]
    public void BigMul_Int_ReturnsLong(int a, int b, long expected)
    {
        Math.BigMul(a, b).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(0L, 0L, 0L, 0L)]
    [DataRow(2L, 3L, 0L, 6L)]
    [DataRow(3L, -2L, -1L, -6L)]
    [DataRow(-1L, -1L, 0L, 1L)]
    [DataRow(-1L, long.MinValue, 0L, long.MinValue)]
    [DataRow(1L, long.MinValue, -1L, long.MinValue)]
    [DataRow(long.MaxValue, 2L, 0L, -2L)]
    public void BigMul_Long_ReturnsHighAndLow(long a, long b, long expectedHigh, long expectedLow)
    {
        long high = Math.BigMul(a, b, out long low);
        high.Should().Be(expectedHigh);
        low.Should().Be(expectedLow);
    }

    [TestMethod]
    [DataRow(0UL, 0UL, 0UL, 0UL)]
    [DataRow(2UL, 3UL, 0UL, 6UL)]
    [DataRow(ulong.MaxValue, 1UL, 0UL, ulong.MaxValue)]
    public void BigMul_Ulong_ReturnsHighAndLow(ulong a, ulong b, ulong expectedHigh, ulong expectedLow)
    {
        ulong high = Math.BigMul(a, b, out ulong low);
        high.Should().Be(expectedHigh);
        low.Should().Be(expectedLow);
    }

    [TestMethod]
    public void BigMul_Ulong_MaxTimesMax_HighIsMaxMinusOne()
    {
        // ulong.MaxValue * ulong.MaxValue = 0xFFFFFFFFFFFFFFFE_0000000000000001 (128-bit)
        ulong high = Math.BigMul(ulong.MaxValue, ulong.MaxValue, out ulong low);
        high.Should().Be(ulong.MaxValue - 1);
        low.Should().Be(1UL);
    }

    [TestMethod]
    public void BigMul_Ulong_MaxTimesTwo_OverflowsCleanly()
    {
        ulong high = Math.BigMul(ulong.MaxValue, 2UL, out ulong low);
        high.Should().Be(1UL);
        low.Should().Be(unchecked((ulong)-2L));
    }
}
