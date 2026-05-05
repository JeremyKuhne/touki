// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class MathExtensionsTests
{
    [Theory]
    [InlineData(5, 0, 10, 5)]
    [InlineData(-5, 0, 10, 0)]
    [InlineData(15, 0, 10, 10)]
    [InlineData(0, 0, 10, 0)]
    [InlineData(10, 0, 10, 10)]
    public void Clamp_Int_ReturnsExpected(int value, int min, int max, int expected)
    {
        Math.Clamp(value, min, max).Should().Be(expected);
    }

    [Fact]
    public void Clamp_MinGreaterThanMax_Throws()
    {
        Action action = () => Math.Clamp(0, 10, 5);
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0.5, 0.0, 1.0, 0.5)]
    [InlineData(-0.5, 0.0, 1.0, 0.0)]
    [InlineData(1.5, 0.0, 1.0, 1.0)]
    public void Clamp_Double_ReturnsExpected(double value, double min, double max, double expected)
    {
        Math.Clamp(value, min, max).Should().Be(expected);
    }

    [Fact]
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

    [Fact]
    public void DivRem_Long_ReturnsTuple()
    {
        (long q, long r) = Math.DivRem(17L, 5L);
        q.Should().Be(3L);
        r.Should().Be(2L);
    }

    [Fact]
    public void DivRem_Byte_ReturnsTuple()
    {
        (byte q, byte r) = Math.DivRem((byte)17, (byte)5);
        q.Should().Be((byte)3);
        r.Should().Be((byte)2);
    }

    [Fact]
    public void DivRem_Sbyte_ReturnsTuple()
    {
        (sbyte q, sbyte r) = Math.DivRem((sbyte)-17, (sbyte)5);
        q.Should().Be((sbyte)-3);
        r.Should().Be((sbyte)-2);
    }

    [Fact]
    public void DivRem_Short_ReturnsTuple()
    {
        (short q, short r) = Math.DivRem((short)17, (short)5);
        q.Should().Be((short)3);
        r.Should().Be((short)2);
    }

    [Fact]
    public void DivRem_Ushort_ReturnsTuple()
    {
        (ushort q, ushort r) = Math.DivRem((ushort)17, (ushort)5);
        q.Should().Be((ushort)3);
        r.Should().Be((ushort)2);
    }

    [Fact]
    public void Acosh_OfOne_IsZero()
    {
        Math.Acosh(1.0).Should().Be(0.0);
    }

    [Fact]
    public void Acosh_LargeValue_DoesNotOverflow()
    {
        // d*d would overflow for d=1e308; result must be finite (~ Log(2*1e308)).
        double result = Math.Acosh(1e308);
        result.Should().BeGreaterThan(0.0);
        double.IsFinite(result).Should().BeTrue();
    }

    [Fact]
    public void Asinh_OfZero_IsZero()
    {
        Math.Asinh(0.0).Should().Be(0.0);
    }

    [Fact]
    public void Asinh_LargeMagnitude_DoesNotOverflow()
    {
        double pos = Math.Asinh(1e308);
        double neg = Math.Asinh(-1e308);
        double.IsFinite(pos).Should().BeTrue();
        double.IsFinite(neg).Should().BeTrue();
        neg.Should().Be(-pos);
    }

    [Fact]
    public void Atanh_OfZero_IsZero()
    {
        Math.Atanh(0.0).Should().Be(0.0);
    }

    [Theory]
    [InlineData(8.0, 2.0)]
    [InlineData(27.0, 3.0)]
    [InlineData(0.0, 0.0)]
    public void Cbrt_Positive_ReturnsExpected(double value, double expected)
    {
        Math.Cbrt(value).Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public void Cbrt_Negative_ReturnsNegative()
    {
        Math.Cbrt(-8.0).Should().BeApproximately(-2.0, 1e-12);
    }

    [Fact]
    public void Cbrt_NegativeZero_PreservesSign()
    {
        double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000_0000_0000_0000UL));
        double result = Math.Cbrt(negativeZero);
        result.Should().Be(0.0);
        double.IsNegative(result).Should().BeTrue();
    }

    [Fact]
    public void BitDecrement_PositiveZero_ReturnsNegativeEpsilon()
    {
        Math.BitDecrement(0.0).Should().Be(-double.Epsilon);
    }

    [Fact]
    public void BitIncrement_NegativeZero_ReturnsEpsilon()
    {
        Math.BitIncrement(-0.0).Should().Be(double.Epsilon);
    }

    [Fact]
    public void BitIncrement_PositiveInfinity_ReturnsPositiveInfinity()
    {
        Math.BitIncrement(double.PositiveInfinity).Should().Be(double.PositiveInfinity);
    }

    [Fact]
    public void CopySign_PositiveAndNegative_TakesSignOfSecond()
    {
        Math.CopySign(5.0, -1.0).Should().Be(-5.0);
        Math.CopySign(-5.0, 1.0).Should().Be(5.0);
    }

    [Fact]
    public void FusedMultiplyAdd_Basic_Computes()
    {
        Math.FusedMultiplyAdd(2.0, 3.0, 4.0).Should().Be(10.0);
    }

    [Fact]
    public void ILogB_PowersOfTwo_ReturnExponent()
    {
        Math.ILogB(1.0).Should().Be(0);
        Math.ILogB(2.0).Should().Be(1);
        Math.ILogB(0.5).Should().Be(-1);
    }

    [Fact]
    public void ILogB_Zero_ReturnsIntMinValue()
    {
        Math.ILogB(0.0).Should().Be(int.MinValue);
    }

    [Theory]
    [InlineData(1.0, 0.0)]
    [InlineData(2.0, 1.0)]
    [InlineData(8.0, 3.0)]
    public void Log2_PowersOfTwo_ReturnExponent(double value, double expected)
    {
        Math.Log2(value).Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public void MaxMagnitude_PicksLargerAbs()
    {
        Math.MaxMagnitude(-5.0, 3.0).Should().Be(-5.0);
        Math.MaxMagnitude(2.0, -7.0).Should().Be(-7.0);
    }

    [Fact]
    public void MinMagnitude_PicksSmallerAbs()
    {
        Math.MinMagnitude(-5.0, 3.0).Should().Be(3.0);
        Math.MinMagnitude(2.0, -7.0).Should().Be(2.0);
    }

    [Theory]
    [InlineData(1.0, 1, 2.0)]
    [InlineData(1.0, 10, 1024.0)]
    [InlineData(1.0, -1, 0.5)]
    [InlineData(3.0, 0, 3.0)]
    public void ScaleB_Basic_Computes(double x, int n, double expected)
    {
        Math.ScaleB(x, n).Should().Be(expected);
    }

    [Fact]
    public void ReciprocalEstimate_OfTwo_IsHalf()
    {
        Math.ReciprocalEstimate(2.0).Should().BeApproximately(0.5, 1e-12);
    }

    [Fact]
    public void ReciprocalSqrtEstimate_OfFour_IsHalf()
    {
        Math.ReciprocalSqrtEstimate(4.0).Should().BeApproximately(0.5, 1e-12);
    }

    [Fact]
    public void SinCos_ReturnsSinAndCos()
    {
        (double sin, double cos) = Math.SinCos(0.0);
        sin.Should().Be(0.0);
        cos.Should().Be(1.0);
    }

    [Fact]
    public void BigMul_Int_ReturnsLong()
    {
        Math.BigMul(int.MaxValue, 2).Should().Be((long)int.MaxValue * 2);
    }

    [Fact]
    public void BigMul_Long_ReturnsHighAndLow()
    {
        long high = Math.BigMul(long.MaxValue, 2L, out long low);
        // long.MaxValue * 2 = 0xFFFFFFFFFFFFFFFE (as 128-bit unsigned), which as 128-bit signed is high=0, low=-2.
        high.Should().Be(0);
        low.Should().Be(-2L);
    }

    [Fact]
    public void BigMul_Long_NegativeOperands_ReturnsHighAndLow()
    {
        // (-1) * (-1) == 1 → high = 0, low = 1
        long high = Math.BigMul(-1L, -1L, out long low);
        high.Should().Be(0);
        low.Should().Be(1L);
    }

    [Fact]
    public void BigMul_Ulong_ReturnsHighAndLow()
    {
        ulong high = Math.BigMul(ulong.MaxValue, 2UL, out ulong low);
        high.Should().Be(1UL);
        low.Should().Be(unchecked((ulong)-2L));
    }
}
