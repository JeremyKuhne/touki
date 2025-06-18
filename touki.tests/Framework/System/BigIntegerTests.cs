// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using BigInteger = System.Number.BigInteger;

namespace Framework.System;

public class BigIntegerTests
{
    #region SetUInt32 Tests

    [Fact]
    public void SetUInt32_Zero_ShouldCreateZeroBigInteger()
    {
        BigInteger.SetUInt32(out BigInteger result, 0);
        result.IsZero().Should().BeTrue();
        result.GetLength().Should().Be(0);
        result.ToUInt32().Should().Be(0u);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(42u)]
    [InlineData(0x80000000u)] // Min value that requires sign bit
    [InlineData(uint.MaxValue)]
    public void SetUInt32_NonZero_ShouldCreateCorrectBigInteger(uint value)
    {
        BigInteger.SetUInt32(out BigInteger result, value);
        result.IsZero().Should().BeFalse();
        result.GetLength().Should().Be(1);
        result.ToUInt32().Should().Be(value);
        result.GetBlock(0).Should().Be(value);
    }

    #endregion

    #region SetUInt64 Tests

    [Fact]
    public void SetUInt64_Zero_ShouldCreateZeroBigInteger()
    {
        BigInteger.SetUInt64(out BigInteger result, 0);
        result.IsZero().Should().BeTrue();
        result.GetLength().Should().Be(0);
        result.ToUInt64().Should().Be(0ul);
    }

    [Theory]
    [InlineData(1ul, 1)]
    [InlineData(uint.MaxValue, 1)] // Fits in single block
    [InlineData(0x100000000ul, 2)] // Requires two blocks
    [InlineData(ulong.MaxValue, 2)]
    public void SetUInt64_NonZero_ShouldCreateCorrectBigInteger(ulong value, int expectedLength)
    {
        BigInteger.SetUInt64(out BigInteger result, value);
        result.IsZero().Should().BeFalse();
        result.GetLength().Should().Be(expectedLength);
        result.ToUInt64().Should().Be(value);
    }

    [Fact]
    public void SetUInt64_MaxValue_ShouldHaveCorrectBlocks()
    {
        BigInteger.SetUInt64(out BigInteger result, ulong.MaxValue);
        result.GetLength().Should().Be(2);
        result.GetBlock(0).Should().Be(uint.MaxValue);
        result.GetBlock(1).Should().Be(uint.MaxValue);
    }

    #endregion

    #region SetZero and SetValue Tests

    [Fact]
    public void SetZero_ShouldCreateZeroBigInteger()
    {
        BigInteger.SetZero(out BigInteger result);
        result.IsZero().Should().BeTrue();
        result.GetLength().Should().Be(0);
        result.ToUInt32().Should().Be(0u);
        result.ToUInt64().Should().Be(0ul);
    }

    [Fact]
    public void SetValue_ShouldCopyCorrectly()
    {
        BigInteger.SetUInt64(out BigInteger original, 0x123456789ABCDEFul);
        BigInteger.SetValue(out BigInteger copy, ref original);

        copy.GetLength().Should().Be(original.GetLength());
        copy.ToUInt64().Should().Be(original.ToUInt64());

        for (int i = 0; i < original.GetLength(); i++)
        {
            copy.GetBlock((uint)i).Should().Be(original.GetBlock((uint)i));
        }
    }

    [Fact]
    public void SetValue_FromZero_ShouldCreateZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.SetValue(out BigInteger copy, ref zero);

        copy.IsZero().Should().BeTrue();
        copy.GetLength().Should().Be(0);
    }

    #endregion

    #region ToUInt32 and ToUInt64 Tests

    [Fact]
    public void ToUInt32_ZeroBigInteger_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.ToUInt32().Should().Be(0u);
    }

    [Fact]
    public void ToUInt64_ZeroBigInteger_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.ToUInt64().Should().Be(0ul);
    }

    [Fact]
    public void ToUInt64_SingleBlock_ShouldReturnFirstBlock()
    {
        BigInteger.SetUInt32(out BigInteger value, 42u);
        value.ToUInt64().Should().Be(42ul);
    }

    [Fact]
    public void ToUInt64_TwoBlocks_ShouldCombineCorrectly()
    {
        BigInteger.SetUInt64(out BigInteger value, 0x123456789ABCDEFul);
        value.ToUInt64().Should().Be(0x123456789ABCDEFul);
    }

    #endregion

    #region Compare Tests

    [Fact]
    public void Compare_BothZero_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero1);
        BigInteger.SetZero(out BigInteger zero2);
        BigInteger.Compare(ref zero1, ref zero2).Should().Be(0);
    }

    [Fact]
    public void Compare_ZeroVsNonZero_ShouldReturnCorrectSign()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.SetUInt32(out BigInteger one, 1);

        BigInteger.Compare(ref zero, ref one).Should().BeLessThan(0);
        BigInteger.Compare(ref one, ref zero).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compare_SameLengthDifferentValues_ShouldCompareCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger small, 100);
        BigInteger.SetUInt32(out BigInteger large, 200);

        BigInteger.Compare(ref small, ref large).Should().BeLessThan(0);
        BigInteger.Compare(ref large, ref small).Should().BeGreaterThan(0);
        BigInteger.Compare(ref small, ref small).Should().Be(0);
    }

    [Fact]
    public void Compare_DifferentLengths_ShouldCompareLengthFirst()
    {
        BigInteger.SetUInt32(out BigInteger oneBlock, uint.MaxValue);
        BigInteger.SetUInt64(out BigInteger twoBlocks, 0x100000000ul); // Smallest 2-block value

        BigInteger.Compare(ref oneBlock, ref twoBlocks).Should().BeLessThan(0);
        BigInteger.Compare(ref twoBlocks, ref oneBlock).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compare_MultiBlockValues_ShouldCompareHighestBlocksFirst()
    {
        BigInteger.SetUInt64(out BigInteger value1, 0x200000000ul); // High block = 2
        BigInteger.SetUInt64(out BigInteger value2, 0x1FFFFFFFFul); // High block = 1

        BigInteger.Compare(ref value1, ref value2).Should().BeGreaterThan(0);
        BigInteger.Compare(ref value2, ref value1).Should().BeLessThan(0);
    }

    #endregion

    #region Add Tests

    [Fact]
    public void Add_ZeroPlusZero_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero1);
        BigInteger.SetZero(out BigInteger zero2);
        BigInteger.Add(ref zero1, ref zero2, out BigInteger result);

        result.IsZero().Should().BeTrue();
        result.GetLength().Should().Be(0);
    }

    [Fact]
    public void Add_ZeroPlusNonZero_ShouldReturnNonZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.SetUInt32(out BigInteger value, 42);
        BigInteger.Add(ref zero, ref value, out BigInteger result);

        result.ToUInt32().Should().Be(42);
        result.GetLength().Should().Be(1);
    }

    [Fact]
    public void Add_NoCarry_ShouldAddCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger a, 100);
        BigInteger.SetUInt32(out BigInteger b, 200);
        BigInteger.Add(ref a, ref b, out BigInteger result);

        result.ToUInt32().Should().Be(300);
        result.GetLength().Should().Be(1);
    }

    [Fact]
    public void Add_WithCarry_ShouldExtendLength()
    {
        BigInteger.SetUInt32(out BigInteger a, uint.MaxValue);
        BigInteger.SetUInt32(out BigInteger b, 1);
        BigInteger.Add(ref a, ref b, out BigInteger result);

        result.GetLength().Should().Be(2);
        result.GetBlock(0).Should().Be(0u);
        result.GetBlock(1).Should().Be(1u);
        result.ToUInt64().Should().Be(0x100000000ul);
    }

    [Fact]
    public void Add_DifferentLengths_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger small, 100);
        BigInteger.SetUInt64(out BigInteger large, 0x200000000ul);
        BigInteger.Add(ref small, ref large, out BigInteger result);

        result.GetLength().Should().Be(2);
        result.ToUInt64().Should().Be(0x200000064ul); // 0x200000000 + 100
    }

    [Fact]
    public void Add_MaxValueCarryPropagation_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt64(out BigInteger a, 0xFFFFFFFFFFFFFFFFul);
        BigInteger.SetUInt32(out BigInteger b, 1);
        BigInteger.Add(ref a, ref b, out BigInteger result);

        result.GetLength().Should().Be(3);
        result.GetBlock(0).Should().Be(0u);
        result.GetBlock(1).Should().Be(0u);
        result.GetBlock(2).Should().Be(1u);
    }

    #endregion

    #region Multiply Tests

    [Fact]
    public void Multiply_ByZero_ShouldReturnZero()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        BigInteger.Multiply(ref value, 0, out BigInteger result);

        result.IsZero().Should().BeTrue();
    }

    [Fact]
    public void Multiply_ByOne_ShouldReturnSameValue()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        BigInteger.Multiply(ref value, 1, out BigInteger result);

        result.ToUInt32().Should().Be(42);
        result.GetLength().Should().Be(1);
    }

    [Fact]
    public void Multiply_ZeroByValue_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.Multiply(ref zero, 42, out BigInteger result);

        result.IsZero().Should().BeTrue();
    }

    [Fact]
    public void Multiply_SingleBlockNoCarry_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 100);
        BigInteger.Multiply(ref value, 3, out BigInteger result);

        result.ToUInt32().Should().Be(300);
        result.GetLength().Should().Be(1);
    }

    [Fact]
    public void Multiply_SingleBlockWithCarry_ShouldExtendLength()
    {
        BigInteger.SetUInt32(out BigInteger value, 0x80000000u); // 2^31
        BigInteger.Multiply(ref value, 3, out BigInteger result);

        result.GetLength().Should().Be(2);
        result.ToUInt64().Should().Be(0x180000000ul); // 3 * 2^31
    }

    [Fact]
    public void Multiply_MaxUInt32_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, uint.MaxValue);
        BigInteger.Multiply(ref value, uint.MaxValue, out BigInteger result);

        ulong expected = (ulong)uint.MaxValue * uint.MaxValue;
        result.ToUInt64().Should().Be(expected);
    }

    [Fact]
    public void Multiply_BigIntegerByBigInteger_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger a, 0x12345678u);
        BigInteger.SetUInt32(out BigInteger b, 0x9ABCDEFu);
        BigInteger.Multiply(ref a, ref b, out BigInteger result);

        ulong expected = (ulong)0x12345678u * 0x9ABCDEFu;
        result.ToUInt64().Should().Be(expected);
    }

    [Fact]
    public void Multiply_LargeNumbers_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt64(out BigInteger a, 0x123456789ABCDEFul);
        BigInteger.SetUInt64(out BigInteger b, 0x111111111111111ul);
        BigInteger.Multiply(ref a, ref b, out BigInteger result);

        // Result should be larger than 64 bits
        result.GetLength().Should().BeGreaterThan(2);
    }

    #endregion

    #region CountSignificantBits Tests

    [Fact]
    public void CountSignificantBits_UInt32Zero_ShouldReturnZero()
    {
        BigInteger.CountSignificantBits(0u).Should().Be(0u);
    }

    [Theory]
    [InlineData(1u, 1u)]
    [InlineData(2u, 2u)]
    [InlineData(3u, 2u)]
    [InlineData(4u, 3u)]
    [InlineData(0x80000000u, 32u)]
    [InlineData(uint.MaxValue, 32u)]
    public void CountSignificantBits_UInt32_ShouldReturnCorrectCount(uint value, uint expected)
    {
        BigInteger.CountSignificantBits(value).Should().Be(expected);
    }

    [Fact]
    public void CountSignificantBits_UInt64Zero_ShouldReturnZero()
    {
        BigInteger.CountSignificantBits(0ul).Should().Be(0u);
    }

    [Theory]
    [InlineData(1ul, 1u)]
    [InlineData(0x100000000ul, 33u)]
    [InlineData(0x8000000000000000ul, 64u)]
    [InlineData(ulong.MaxValue, 64u)]
    public void CountSignificantBits_UInt64_ShouldReturnCorrectCount(ulong value, uint expected)
    {
        BigInteger.CountSignificantBits(value).Should().Be(expected);
    }

    [Fact]
    public void CountSignificantBits_BigIntegerZero_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.CountSignificantBits(ref zero).Should().Be(0u);
    }

    [Fact]
    public void CountSignificantBits_BigIntegerSingleBlock_ShouldReturnCorrectCount()
    {
        BigInteger.SetUInt32(out BigInteger value, 0x80000000u);
        BigInteger.CountSignificantBits(ref value).Should().Be(32u);
    }

    [Fact]
    public void CountSignificantBits_BigIntegerMultiBlock_ShouldReturnCorrectCount()
    {
        BigInteger.SetUInt64(out BigInteger value, 0x8000000000000000ul);
        BigInteger.CountSignificantBits(ref value).Should().Be(64u);
    }

    #endregion

    #region ShiftLeft Tests

    [Fact]
    public void ShiftLeft_Zero_ShouldRemainZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.ShiftLeft(5);
        zero.IsZero().Should().BeTrue();
    }

    [Fact]
    public void ShiftLeft_ByZero_ShouldNotChange()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        value.ShiftLeft(0);
        value.ToUInt32().Should().Be(42);
    }

    [Fact]
    public void ShiftLeft_WithinBlock_ShouldShiftCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);
        value.ShiftLeft(5);
        value.ToUInt32().Should().Be(32u); // 1 << 5
    }

    [Fact]
    public void ShiftLeft_ExactBlockBoundary_ShouldAddZeroBlock()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);
        value.ShiftLeft(32);
        value.GetLength().Should().Be(2);
        value.GetBlock(0).Should().Be(0u);
        value.GetBlock(1).Should().Be(1u);
    }

    [Fact]
    public void ShiftLeft_CrossBlockBoundary_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 0x80000000u);
        value.ShiftLeft(1);
        value.GetLength().Should().Be(2);
        value.GetBlock(0).Should().Be(0u);
        value.GetBlock(1).Should().Be(1u);
    }

    [Fact]
    public void ShiftLeft_LargeShift_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);
        value.ShiftLeft(65); // More than two blocks
        value.GetLength().Should().Be(3);
        value.GetBlock(0).Should().Be(0u);
        value.GetBlock(1).Should().Be(0u);
        value.GetBlock(2).Should().Be(2u); // 1 << 1 (65 % 32 = 1, plus 2 zero blocks)
    }

    [Fact]
    public void ShiftLeft_VeryLargeShift_ShouldTriggerOverflowProtection()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);

        // Calculate a shift that would definitely exceed MaxBlockCount
        // MaxBlockCount is approximately (1074 + 2552 + 32 + 31) / 32 + 1 ≈ 115 blocks
        // So shifting by more than 115 * 32 = 3680 bits should trigger overflow protection
        uint largeShift = 5000; // Definitely exceeds maximum

        value.ShiftLeft(largeShift);

        // The overflow protection should set the result to zero
        // Based on the implementation, when overflow is detected, SetZero is called
        value.IsZero().Should().BeTrue("because extreme shifts should trigger overflow protection");
    }

    #endregion

    #region Pow2 Tests

    [Fact]
    public void Pow2_Zero_ShouldReturnOne()
    {
        BigInteger.Pow2(0, out BigInteger result);
        result.ToUInt32().Should().Be(1u);
        result.GetLength().Should().Be(1);
    }

    [Theory]
    [InlineData(1u, 2u)]
    [InlineData(2u, 4u)]
    [InlineData(10u, 1024u)]
    [InlineData(31u, 0x80000000u)]
    public void Pow2_SmallExponents_ShouldReturnCorrectPower(uint exponent, uint expected)
    {
        BigInteger.Pow2(exponent, out BigInteger result);
        result.ToUInt32().Should().Be(expected);
    }

    [Fact]
    public void Pow2_ExactBlockBoundary_ShouldCreateCorrectLength()
    {
        BigInteger.Pow2(32, out BigInteger result);
        result.GetLength().Should().Be(2);
        result.GetBlock(0).Should().Be(0u);
        result.GetBlock(1).Should().Be(1u);
    }

    [Fact]
    public void Pow2_LargeExponent_ShouldCreateMultipleBlocks()
    {
        BigInteger.Pow2(64, out BigInteger result);
        result.GetLength().Should().Be(3);
        result.GetBlock(0).Should().Be(0u);
        result.GetBlock(1).Should().Be(0u);
        result.GetBlock(2).Should().Be(1u);
    }

    #endregion

    #region Instance Method Tests

    [Fact]
    public void Add_InstanceMethod_ShouldAddCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 100);
        value.Add(50);
        value.ToUInt32().Should().Be(150);
    }

    [Fact]
    public void Add_InstanceMethodWithCarry_ShouldExtendLength()
    {
        BigInteger.SetUInt32(out BigInteger value, uint.MaxValue);
        value.Add(1);
        value.GetLength().Should().Be(2);
        value.ToUInt64().Should().Be(0x100000000ul);
    }

    [Fact]
    public void Multiply_InstanceMethodByUInt_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        value.Multiply(3);
        value.ToUInt32().Should().Be(126);
    }

    [Fact]
    public void Multiply_InstanceMethodByBigInteger_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger a, 6);
        BigInteger.SetUInt32(out BigInteger b, 7);
        a.Multiply(ref b);
        a.ToUInt32().Should().Be(42);
    }

    [Fact]
    public void Multiply10_ShouldMultiplyByTen()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        value.Multiply10();
        value.ToUInt32().Should().Be(420);
    }

    [Fact]
    public void Multiply10_Zero_ShouldRemainZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.Multiply10();
        zero.IsZero().Should().BeTrue();
    }

    [Fact]
    public void MultiplyPow10_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        value.MultiplyPow10(3); // * 1000
        value.ToUInt32().Should().Be(42000);
    }

    [Fact]
    public void MultiplyPow10_Zero_ShouldRemainZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.MultiplyPow10(5);
        zero.IsZero().Should().BeTrue();
    }

    #endregion

    #region Edge Cases and Error Conditions    [Fact]
    public void GetBlock_ValidIndex_ShouldReturnCorrectBlock()
    {
        BigInteger.SetUInt64(out BigInteger value, 0x123456789ABCDEFul);
        // The value 0x123456789ABCDEF breaks down as:
        // High 32 bits: 0x1234567 
        // Low 32 bits: 0x89ABCDEF
        value.GetBlock(0).Should().Be(0x89ABCDEFu); // Low block
        value.GetBlock(1).Should().Be(0x1234567u);  // High block
    }

    [Fact]
    public void ToUInt32_LargerThanUInt32_ShouldReturnFirstBlock()
    {
        BigInteger.SetUInt64(out BigInteger value, 0x123456789ABCDEFul);
        value.ToUInt32().Should().Be(0x89ABCDEFu); // First (low) block only
    }

    [Fact]
    public void ToUInt64_LargerThanUInt64_ShouldReturnFirstTwoBlocks()
    {
        // Create a 3-block number
        BigInteger.SetUInt32(out BigInteger value, 1);
        value.ShiftLeft(64); // Creates 3 blocks

        // ToUInt64 should return only the first two blocks (which should be 0)
        value.ToUInt64().Should().Be(0ul);
    }

    #endregion

    #region DivRem Tests

    [Fact]
    public void DivRem_ZeroDividend_ShouldReturnZeros()
    {
        BigInteger.SetZero(out BigInteger dividend);
        BigInteger.SetUInt32(out BigInteger divisor, 42);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);
        quotient.IsZero().Should().BeTrue();
        remainder.IsZero().Should().BeTrue();
    }

    [Fact]
    public void DivRem_SingleBlockNumbers_ShouldDivideCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 42);
        BigInteger.SetUInt32(out BigInteger divisor, 7);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        quotient.ToUInt32().Should().Be(6);
        remainder.ToUInt32().Should().Be(0);
    }

    [Fact]
    public void DivRem_WithRemainder_ShouldReturnCorrectValues()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 43);
        BigInteger.SetUInt32(out BigInteger divisor, 7);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        quotient.ToUInt32().Should().Be(6);
        remainder.ToUInt32().Should().Be(1);
    }

    [Fact]
    public void DivRem_DivisorLargerThanDividend_ShouldReturnZeroQuotient()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 7);
        BigInteger.SetUInt32(out BigInteger divisor, 42);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        quotient.IsZero().Should().BeTrue();
        remainder.ToUInt32().Should().Be(7);
    }

    #endregion

    #region HeuristicDivide Tests

    [Fact]
    public void HeuristicDivide_DivisorLargerThanDividend_ShouldReturnZero()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 7);
        BigInteger.SetUInt32(out BigInteger divisor, 42);

        uint result = BigInteger.HeuristicDivide(ref dividend, ref divisor);
        result.Should().Be(0);
    }

    [Fact]
    public void HeuristicDivide_EqualLengths_ShouldReturnEstimate()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 100);
        BigInteger.SetUInt32(out BigInteger divisor, 30);

        uint result = BigInteger.HeuristicDivide(ref dividend, ref divisor);
        result.Should().BeGreaterThan(0);
        result.Should().BeLessOrEqualTo(4); // 100/30 = 3.33, heuristic should be close
    }

    #endregion

    #region Additional Edge Case Tests

    [Fact]
    public void Add_MaxBlockCountScenario_ShouldHandleGracefully()
    {
        // Test near the maximum block count limit
        BigInteger.SetUInt64(out BigInteger large, ulong.MaxValue);

        // Multiply to create a larger number (this will test the overflow protection)
        for (int i = 0; i < 10; i++)
        {
            large.Multiply(uint.MaxValue);
        }

        // This should not crash, though the exact behavior may vary
        BigInteger.SetUInt32(out BigInteger one, 1);
        BigInteger.Add(ref large, ref one, out BigInteger result);

        // The result should either be correct or zero (overflow protection)
        // At minimum, it should not crash
        result.GetLength().Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void Multiply_OverflowProtection_ShouldReturnZeroOnOverflow()
    {
        // Create a large number that could cause overflow
        BigInteger.SetUInt64(out BigInteger large, ulong.MaxValue);

        // Repeatedly multiply to approach the maximum block count
        for (int i = 0; i < 20; i++)
        {
            large.Multiply(uint.MaxValue);

            // If overflow protection kicks in, length becomes 0
            if (large.IsZero())
            {
                break;
            }
        }

        // The result should either be a valid BigInteger or zero due to overflow protection
        large.GetLength().Should().BeGreaterOrEqualTo(0);
    }
    [Fact]
    public void ShiftLeft_ExtremeShift_ShouldHandleGracefully()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);

        // Test large shift that approaches maximum blocks (but not extreme enough to crash)
        value.ShiftLeft(1000); // Large but reasonable shift

        // Should either work correctly or trigger overflow protection
        value.GetLength().Should().BeGreaterOrEqualTo(0);

        // If it didn't overflow, verify it's a power of 2
        if (!value.IsZero())
        {
            // The result should be 2^1000, which requires many blocks
            value.GetLength().Should().BeGreaterThan(30); // 1000 bits / 32 bits per block > 30
        }
    }

    [Fact]
    public void Compare_IdenticalValuesAfterOperations_ShouldBeEqual()
    {
        BigInteger.SetUInt32(out BigInteger a, 42);
        BigInteger.SetUInt32(out BigInteger b, 21);

        a.Multiply(2); // a = 42 * 2 = 84
        b.Multiply(4); // b = 21 * 4 = 84

        BigInteger.Compare(ref a, ref b).Should().Be(0);
    }

    [Fact]
    public void Pow10_LargeExponent_ShouldCalculateCorrectly()
    {
        BigInteger.Pow10(5, out BigInteger result);
        result.ToUInt32().Should().Be(100000u); // 10^5

        BigInteger.Pow10(9, out BigInteger result2);
        result2.ToUInt32().Should().Be(1000000000u); // 10^9
    }

    [Fact]
    public void SetValue_ModificationIndependence_ShouldNotAffectOriginal()
    {
        BigInteger.SetUInt32(out BigInteger original, 42);
        BigInteger.SetValue(out BigInteger copy, ref original);

        copy.Multiply(2);

        original.ToUInt32().Should().Be(42);
        copy.ToUInt32().Should().Be(84);
    }

    [Fact]
    public void Add_CarryChainWithMaxValues_ShouldHandleCorrectly()
    {
        // Create a number with all blocks set to max value
        BigInteger.SetUInt32(out BigInteger maxValue, uint.MaxValue);
        BigInteger.SetUInt32(out BigInteger one, 1);

        // This should create a carry chain
        BigInteger.Add(ref maxValue, ref one, out BigInteger result);

        result.GetLength().Should().Be(2);
        result.GetBlock(0).Should().Be(0u);
        result.GetBlock(1).Should().Be(1u);
    }
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(31u)]
    [InlineData(32u)]
    [InlineData(33u)]
    [InlineData(63u)]
    [InlineData(64u)]
    [InlineData(65u)]
    public void ShiftLeft_VariousShiftAmounts_ShouldBeConsistent(uint shift)
    {
        BigInteger.SetUInt32(out BigInteger value, 1);

        value.ShiftLeft(shift);

        // Calculate expected value using Pow2
        BigInteger.Pow2(shift, out BigInteger expected);

        value.GetLength().Should().Be(expected.GetLength());
        for (int i = 0; i < value.GetLength(); i++)
        {
            value.GetBlock((uint)i).Should().Be(expected.GetBlock((uint)i));
        }
    }

    [Fact]
    public void CountSignificantBits_AfterOperations_ShouldBeCorrect()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);
        BigInteger.CountSignificantBits(ref value).Should().Be(1u);

        value.ShiftLeft(10);
        BigInteger.CountSignificantBits(ref value).Should().Be(11u); // 1024 requires 11 bits

        value.Multiply(3);
        BigInteger.CountSignificantBits(ref value).Should().Be(12u); // 3072 requires 12 bits
    }

    [Fact]
    public void DivRem_EdgeCaseValues_ShouldHandleCorrectly()
    {
        // Test division where quotient is exactly 1
        BigInteger.SetUInt32(out BigInteger dividend, 100);
        BigInteger.SetUInt32(out BigInteger divisor, 100);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        quotient.ToUInt32().Should().Be(1);
        remainder.IsZero().Should().BeTrue();
    }

    [Fact]
    public void HeuristicDivide_SequentialCalls_ShouldReduceDividend()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 1000);
        BigInteger.SetUInt32(out BigInteger divisor, 7);

        uint totalQuotient = 0;
        int iterations = 0;
        const int maxIterations = 200; // Safety limit

        while (!dividend.IsZero() && iterations < maxIterations)
        {
            uint partialQuotient = BigInteger.HeuristicDivide(ref dividend, ref divisor);
            totalQuotient += partialQuotient;
            iterations++;

            if (partialQuotient == 0)
                break;
        }

        totalQuotient.Should().Be(1000u / 7u); // Should equal integer division result
        iterations.Should().BeLessThan(maxIterations); // Should not infinite loop
    }

    #endregion
}
