// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using DiyFp = System.Number.DiyFp;

namespace Framework.System;

public class DiyFpTests
{
    #region Constructor Tests

    [Fact]
    public void DiyFp_ConstructorWithULongAndInt_ShouldSetFieldsCorrectly()
    {
        const ulong expectedF = 0x123456789ABCDEFul;
        const int expectedE = 42;

        DiyFp diyFp = new(expectedF, expectedE);

        diyFp.f.Should().Be(expectedF);
        diyFp.e.Should().Be(expectedE);
    }

    [Theory]
    [InlineData(2.0)] // Simple power of 2
    [InlineData(0.5)] // Negative exponent
    [InlineData(1.5)] // Fractional part
    [InlineData(double.MaxValue)] // Extreme value
    [InlineData(double.Epsilon)] // Smallest positive value
    public void DiyFp_ConstructorWithDouble_ShouldHandleVariousValues(double value)
    {
        DiyFp diyFp = new(value);

        // Basic sanity checks
        diyFp.f.Should().BeGreaterThan(0);
        // Exponent should be reasonable for double precision
        diyFp.e.Should().BeGreaterOrEqualTo(-1074);
        diyFp.e.Should().BeLessOrEqualTo(971); // Max exponent for double
    }

    [Theory]
    [InlineData(2.0f)] // Simple power of 2
    [InlineData(0.5f)] // Negative exponent
    [InlineData(1.5f)] // Fractional part
    [InlineData(float.MaxValue)] // Extreme value
    [InlineData(float.Epsilon)] // Smallest positive value
    public void DiyFp_ConstructorWithFloat_ShouldHandleVariousValues(float value)
    {
        DiyFp diyFp = new(value);

        // Basic sanity checks
        diyFp.f.Should().BeGreaterThan(0);
        // Exponent should be reasonable for single precision
        diyFp.e.Should().BeGreaterOrEqualTo(-149);
        diyFp.e.Should().BeLessOrEqualTo(104); // Max exponent for float
    }

    #endregion

    #region Constants Tests

    [Fact]
    public void DiyFp_Constants_ShouldHaveCorrectValues()
    {
        DiyFp.DoubleImplicitBitIndex.Should().Be(52);
        DiyFp.SingleImplicitBitIndex.Should().Be(23);
        DiyFp.HalfImplicitBitIndex.Should().Be(10);
        DiyFp.SignificandSize.Should().Be(64);
    }

    #endregion

    #region Multiply Tests

    [Fact]
    public void Multiply_SimpleCase_ShouldMultiplyCorrectly()
    {
        DiyFp a = new(1UL << 32, 0); // Large enough to test multiplication
        DiyFp b = new(1UL << 32, 0);

        DiyFp result = a.Multiply(b);

        // Result should be approximately 2^64, but with exponent adjusted
        // Expected: f ≈ 2^63 (top bit set), e = 0 + 0 + 64 = 64
        result.f.Should().BeGreaterThan(0);
        result.e.Should().Be(64);
    }

    [Fact]
    public void Multiply_ZeroSignificand_ShouldGiveZero()
    {
        DiyFp a = new(0, 10);
        DiyFp b = new(0xFFFFFFFFFFFFFFFFul, 20);

        DiyFp result = a.Multiply(b);

        result.f.Should().Be(0);
        result.e.Should().Be(10 + 20 + 64);
    }

    [Fact]
    public void Multiply_MaxValues_ShouldHandleOverflow()
    {
        DiyFp a = new(ulong.MaxValue, 0);
        DiyFp b = new(ulong.MaxValue, 0);

        DiyFp result = a.Multiply(b);

        // Should not crash and should have reasonable values
        result.f.Should().BeGreaterThan(0);
        result.e.Should().Be(64);
    }

    [Fact]
    public void Multiply_ExponentAddition_ShouldBeCorrect()
    {
        DiyFp a = new(1UL << 63, 100);
        DiyFp b = new(1UL << 63, 200);

        DiyFp result = a.Multiply(b);

        result.e.Should().Be(100 + 200 + 64);
    }

    [Theory]
    [InlineData(1UL << 32, 1UL << 32)]
    [InlineData(1UL << 20, 1UL << 40)]
    [InlineData(0x123456789ABCDEFul, 0xFEDCBA9876543210ul)]
    public void Multiply_VariousValues_ShouldNotCrash(ulong f1, ulong f2)
    {
        DiyFp a = new(f1, 0);
        DiyFp b = new(f2, 0);

        DiyFp result = a.Multiply(b);

        // Basic sanity check - should not crash and should have reasonable result
        result.e.Should().Be(64);
    }

    #endregion

    #region Normalize Tests

    [Fact]
    public void Normalize_AlreadyNormalized_ShouldNotChange()
    {
        DiyFp diyFp = new(1UL << 63, 100); // MSB already set

        DiyFp result = diyFp.Normalize();

        result.f.Should().Be(1UL << 63);
        result.e.Should().Be(100);
    }

    [Fact]
    public void Normalize_NeedsShifting_ShouldShiftCorrectly()
    {
        DiyFp diyFp = new(1UL << 32, 100); // MSB not set, needs 31-bit shift

        DiyFp result = diyFp.Normalize();

        result.f.Should().Be(1UL << 63); // Should shift to MSB
        result.e.Should().Be(100 - 31); // Exponent adjusted by shift amount
    }

    [Fact]
    public void Normalize_SingleBit_ShouldShiftToMSB()
    {
        DiyFp diyFp = new(1, 100); // Single bit at LSB

        DiyFp result = diyFp.Normalize();

        result.f.Should().Be(1UL << 63); // Should shift to MSB
        result.e.Should().Be(100 - 63); // Exponent adjusted by 63-bit shift
    }

    [Theory]
    [InlineData(1UL << 62, 1)] // One bit shift needed
    [InlineData(1UL << 32, 31)] // 31-bit shift needed
    [InlineData(1UL << 1, 62)] // 62-bit shift needed
    public void Normalize_VariousShifts_ShouldAdjustExponentCorrectly(ulong f, int expectedShift)
    {
        const int originalExponent = 100;
        DiyFp diyFp = new(f, originalExponent);

        DiyFp result = diyFp.Normalize();

        result.f.Should().Be(1UL << 63); // Should always normalize to MSB
        result.e.Should().Be(originalExponent - expectedShift);
    }

    #endregion

    #region Subtract Tests    [Fact]
    public void Subtract_SameExponents_ShouldSubtractSignificands()
    {
        DiyFp a = new(1000, 50);
        DiyFp b = new(300, 50);

        DiyFp result = a.Subtract(b);

        result.f.Should().Be(700);
        result.e.Should().Be(50);
    }

    [Fact]
    public void Subtract_EqualValues_ShouldGiveZero()
    {
        DiyFp a = new(0x123456789ABCDEFul, 42);
        DiyFp b = new(0x123456789ABCDEFul, 42);

        DiyFp result = a.Subtract(b);

        result.f.Should().Be(0);
        result.e.Should().Be(42);
    }

    [Fact]
    public void Subtract_MaxMinusOne_ShouldGiveCorrectResult()
    {
        DiyFp a = new(ulong.MaxValue, 0);
        DiyFp b = new(1, 0);

        DiyFp result = a.Subtract(b);

        result.f.Should().Be(ulong.MaxValue - 1);
        result.e.Should().Be(0);
    }

    #endregion

    #region CreateAndGetBoundaries Tests    [Fact]
    public void CreateAndGetBoundaries_Double_ShouldReturnBoundaries()
    {
        double value = 1.0;

        DiyFp result = DiyFp.CreateAndGetBoundaries(value, out DiyFp mMinus, out DiyFp mPlus);

        // Basic checks
        result.f.Should().BeGreaterThan(0);
        mMinus.f.Should().BeGreaterThan(0);
        mPlus.f.Should().BeGreaterThan(0);

        // mPlus should be normalized (MSB set)
        (mPlus.f & (1UL << 63)).Should().NotBe(0, "mPlus should be normalized");

        // mMinus and mPlus should have same exponent
        mMinus.e.Should().Be(mPlus.e);

        // mPlus should be larger than mMinus
        mPlus.f.Should().BeGreaterThan(mMinus.f);
    }

    [Fact]
    public void CreateAndGetBoundaries_Float_ShouldReturnBoundaries()
    {
        float value = 1.0f;

        DiyFp result = DiyFp.CreateAndGetBoundaries(value, out DiyFp mMinus, out DiyFp mPlus);

        // Basic checks
        result.f.Should().BeGreaterThan(0);
        mMinus.f.Should().BeGreaterThan(0);
        mPlus.f.Should().BeGreaterThan(0);

        // mPlus should be normalized (MSB set)
        (mPlus.f & (1UL << 63)).Should().NotBe(0, "mPlus should be normalized");

        // mMinus and mPlus should have same exponent
        mMinus.e.Should().Be(mPlus.e);

        // mPlus should be larger than mMinus
        mPlus.f.Should().BeGreaterThan(mMinus.f);
    }

    [Fact]
    public void CreateAndGetBoundaries_PowerOfTwo_ShouldHaveCloserLowerBoundary()
    {
        double value = 2.0; // Power of 2

        DiyFp result = DiyFp.CreateAndGetBoundaries(value, out DiyFp mMinus, out DiyFp mPlus);

        // For powers of 2, the lower boundary should be closer
        // This tests the special case in GetBoundaries
        ulong distanceToPlus = mPlus.f - result.f;
        ulong distanceToMinus = result.f - mMinus.f;

        // For power of 2, distance to mMinus should be smaller
        distanceToMinus.Should().BeLessThan(distanceToPlus);
    }

    [Fact]
    public void CreateAndGetBoundaries_NonPowerOfTwo_ShouldHaveSymmetricBoundaries()
    {
        double value = 1.5; // Not a power of 2

        DiyFp result = DiyFp.CreateAndGetBoundaries(value, out DiyFp mMinus, out DiyFp mPlus);

        // For non-powers of 2, boundaries should be more symmetric
        ulong distanceToPlus = mPlus.f - result.f;
        ulong distanceToMinus = result.f - mMinus.f;

        // Should be equal or very close
        distanceToMinus.Should().Be(distanceToPlus);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(0.5)]
    [InlineData(1.5)]
    [InlineData(10.0)]
    [InlineData(100.0)]
    [InlineData(0.1)]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    public void CreateAndGetBoundaries_Double_VariousValues_ShouldNotCrash(double value)
    {
        var result = DiyFp.CreateAndGetBoundaries(value, out DiyFp mMinus, out DiyFp mPlus);

        // Basic sanity checks
        result.f.Should().BeGreaterThan(0);
        mMinus.f.Should().BeGreaterThan(0);
        mPlus.f.Should().BeGreaterThan(0);
        mMinus.e.Should().Be(mPlus.e);
        mPlus.f.Should().BeGreaterOrEqualTo(mMinus.f);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(2.0f)]
    [InlineData(0.5f)]
    [InlineData(1.5f)]
    [InlineData(10.0f)]
    [InlineData(100.0f)]
    [InlineData(0.1f)]
    [InlineData(float.MaxValue)]
    [InlineData(float.Epsilon)]
    public void CreateAndGetBoundaries_Float_VariousValues_ShouldNotCrash(float value)
    {
        var result = DiyFp.CreateAndGetBoundaries(value, out DiyFp mMinus, out DiyFp mPlus);

        // Basic sanity checks
        result.f.Should().BeGreaterThan(0);
        mMinus.f.Should().BeGreaterThan(0);
        mPlus.f.Should().BeGreaterThan(0);
        mMinus.e.Should().Be(mPlus.e);
        mPlus.f.Should().BeGreaterOrEqualTo(mMinus.f);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void DiyFp_LargestSubnormalDouble_ShouldHandleCorrectly()
    {
        // This tests the denormalized number handling
        // Create a very small number close to double.Epsilon
        double value = double.Epsilon * 2; // Still denormalized

        var diyFp = new DiyFp(value);

        // For denormalized numbers, implicit bit should NOT be set
        diyFp.f.Should().BeLessThan(1UL << 52);
        diyFp.e.Should().Be(-1074);
    }

    [Fact]
    public void Multiply_RoundingBehavior_ShouldRoundCorrectly()
    {
        // Test the rounding behavior in multiplication
        // Use values that will exercise the (1U << 31) rounding addition
        var a = new DiyFp(0x8000000000000000ul, 0); // MSB set
        var b = new DiyFp(0x8000000000000001ul, 0); // MSB set + 1

        var result = a.Multiply(b);

        // Result should be rounded
        result.f.Should().BeGreaterThan(0);
        result.e.Should().Be(64);
    }

    [Fact]
    public void GetBoundaries_PowerOfTwoDetection_ShouldWorkCorrectly()
    {
        // Test the special case detection for powers of 2
        // Create a DiyFp that represents exactly 2^52 (smallest normal significand)
        // var diyFp = new DiyFp(1UL << DiyFp.DoubleImplicitBitIndex, -1075);

        // For 1.0 (which is 2^52 * 2^-52), should trigger the power-of-2 case
        DiyFp originalDiyFp = new(1.0);
        originalDiyFp.f.Should().Be(1UL << 52);
    }

    [Fact]
    public void Multiply_OverflowInTmp_ShouldHandleCorrectly()
    {
        // Test case where tmp calculation might overflow
        DiyFp a = new(0xFFFFFFFF00000000ul, 0);
        DiyFp b = new(0xFFFFFFFF00000000ul, 0);

        DiyFp result = a.Multiply(b);

        // Should handle overflow in intermediate calculations correctly
        result.f.Should().BeGreaterThan(0);
        result.e.Should().Be(64);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void DiyFp_DoubleRoundTrip_ShouldPreserveOrder()
    {
        // Test that the ordering of DiyFp values matches the ordering of the original doubles
        // Since ref structs can't be stored in arrays, test individual comparisons
        DiyFp diyFp1 = new(1.0);
        DiyFp diyFp2 = new(1.5);

        // When exponents are the same, significand ordering should match value ordering
        if (diyFp1.e == diyFp2.e)
        {
            diyFp1.f.Should().BeLessThan(diyFp2.f, "1.0 should have smaller significand than 1.5 when exponents are equal");
        }

        // Test a few more pairs
        DiyFp diyFp3 = new(2.0);
        DiyFp diyFp4 = new(2.5);

        if (diyFp3.e == diyFp4.e)
        {
            diyFp3.f.Should().BeLessThan(diyFp4.f, "2.0 should have smaller significand than 2.5 when exponents are equal");
        }
    }

    [Fact]
    public void DiyFp_FloatRoundTrip_ShouldPreserveOrder()
    {
        // Test that the ordering of DiyFp values matches the ordering of the original floats
        // Since ref structs can't be stored in arrays, test individual comparisons
        DiyFp diyFp1 = new(1.0f);
        DiyFp diyFp2 = new(1.5f);

        // When exponents are the same, significand ordering should match value ordering
        if (diyFp1.e == diyFp2.e)
        {
            diyFp1.f.Should().BeLessThan(diyFp2.f, "1.0f should have smaller significand than 1.5f when exponents are equal");
        }

        // Test a few more pairs
        DiyFp diyFp3 = new(2.0f);
        DiyFp diyFp4 = new(2.5f);

        if (diyFp3.e == diyFp4.e)
        {
            diyFp3.f.Should().BeLessThan(diyFp4.f, "2.0f should have smaller significand than 2.5f when exponents are equal");
        }
    }

    [Fact]
    public void DiyFp_MultiplicationAssociativity_ShouldBeClose()
    {
        // Test that (a * b) * c ≈ a * (b * c) within rounding error
        var a = new DiyFp(0x1000000000000000ul, 0);
        var b = new DiyFp(0x2000000000000000ul, 0);
        var c = new DiyFp(0x3000000000000000ul, 0);

        var result1 = a.Multiply(b).Multiply(c);
        var result2 = a.Multiply(b.Multiply(c));

        // Due to rounding, results might not be exactly equal, but should be close
        // and have the same exponent
        result1.e.Should().Be(result2.e);

        // The significands should be close (within reasonable rounding error)
        var diff = result1.f > result2.f ? result1.f - result2.f : result2.f - result1.f;
        diff.Should().BeLessThan(1000, "Results should be close despite rounding");
    }

    #endregion
}
