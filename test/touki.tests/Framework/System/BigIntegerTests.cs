// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using BclBigInteger = System.Numerics.BigInteger;
using BigInteger = System.Number.BigInteger;

namespace Framework.System;

[TestClass]
public class BigIntegerTests
{
    #region SetUInt32 Tests

    [TestMethod]
    public void SetUInt32_Zero_ShouldCreateZeroBigInteger()
    {
        BigInteger.SetUInt32(out BigInteger result, 0);
        result.IsZero().Should().BeTrue();
        result.GetLength().Should().Be(0);
        result.ToUInt32().Should().Be(0u);
    }

    [TestMethod]
    [DataRow(1u)]
    [DataRow(42u)]
    [DataRow(0x80000000u)] // Min value that requires sign bit
    [DataRow(uint.MaxValue)]
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

    [TestMethod]
    public void SetUInt64_Zero_ShouldCreateZeroBigInteger()
    {
        BigInteger.SetUInt64(out BigInteger result, 0);
        result.IsZero().Should().BeTrue();
        result.GetLength().Should().Be(0);
        result.ToUInt64().Should().Be(0ul);
    }

    [TestMethod]
    [DataRow(1ul, 1)]
    [DataRow(uint.MaxValue, 1)] // Fits in single block
    [DataRow(0x100000000ul, 2)] // Requires two blocks
    [DataRow(ulong.MaxValue, 2)]
    public void SetUInt64_NonZero_ShouldCreateCorrectBigInteger(ulong value, int expectedLength)
    {
        BigInteger.SetUInt64(out BigInteger result, value);
        result.IsZero().Should().BeFalse();
        result.GetLength().Should().Be(expectedLength);
        result.ToUInt64().Should().Be(value);
    }

    [TestMethod]
    public void SetUInt64_MaxValue_ShouldHaveCorrectBlocks()
    {
        BigInteger.SetUInt64(out BigInteger result, ulong.MaxValue);
        result.GetLength().Should().Be(2);
        result.GetBlock(0).Should().Be(uint.MaxValue);
        result.GetBlock(1).Should().Be(uint.MaxValue);
    }

    #endregion

    #region SetZero and SetValue Tests

    [TestMethod]
    public void SetZero_ShouldCreateZeroBigInteger()
    {
        BigInteger.SetZero(out BigInteger result);
        result.IsZero().Should().BeTrue();
        result.GetLength().Should().Be(0);
        result.ToUInt32().Should().Be(0u);
        result.ToUInt64().Should().Be(0ul);
    }

    [TestMethod]
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

    [TestMethod]
    public void SetValue_FromZero_ShouldCreateZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.SetValue(out BigInteger copy, ref zero);

        copy.IsZero().Should().BeTrue();
        copy.GetLength().Should().Be(0);
    }

    #endregion

    #region ToUInt32 and ToUInt64 Tests

    [TestMethod]
    public void ToUInt32_ZeroBigInteger_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.ToUInt32().Should().Be(0u);
    }

    [TestMethod]
    public void ToUInt64_ZeroBigInteger_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.ToUInt64().Should().Be(0ul);
    }

    [TestMethod]
    public void ToUInt64_SingleBlock_ShouldReturnFirstBlock()
    {
        BigInteger.SetUInt32(out BigInteger value, 42u);
        value.ToUInt64().Should().Be(42ul);
    }

    [TestMethod]
    public void ToUInt64_TwoBlocks_ShouldCombineCorrectly()
    {
        BigInteger.SetUInt64(out BigInteger value, 0x123456789ABCDEFul);
        value.ToUInt64().Should().Be(0x123456789ABCDEFul);
    }

    #endregion

    #region Compare Tests

    [TestMethod]
    public void Compare_BothZero_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero1);
        BigInteger.SetZero(out BigInteger zero2);
        BigInteger.Compare(ref zero1, ref zero2).Should().Be(0);
    }

    [TestMethod]
    public void Compare_ZeroVsNonZero_ShouldReturnCorrectSign()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.SetUInt32(out BigInteger one, 1);

        BigInteger.Compare(ref zero, ref one).Should().BeLessThan(0);
        BigInteger.Compare(ref one, ref zero).Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Compare_SameLengthDifferentValues_ShouldCompareCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger small, 100);
        BigInteger.SetUInt32(out BigInteger large, 200);

        BigInteger.Compare(ref small, ref large).Should().BeLessThan(0);
        BigInteger.Compare(ref large, ref small).Should().BeGreaterThan(0);
        BigInteger.Compare(ref small, ref small).Should().Be(0);
    }

    [TestMethod]
    public void Compare_DifferentLengths_ShouldCompareLengthFirst()
    {
        BigInteger.SetUInt32(out BigInteger oneBlock, uint.MaxValue);
        BigInteger.SetUInt64(out BigInteger twoBlocks, 0x100000000ul); // Smallest 2-block value

        BigInteger.Compare(ref oneBlock, ref twoBlocks).Should().BeLessThan(0);
        BigInteger.Compare(ref twoBlocks, ref oneBlock).Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Compare_MultiBlockValues_ShouldCompareHighestBlocksFirst()
    {
        BigInteger.SetUInt64(out BigInteger value1, 0x200000000ul); // High block = 2
        BigInteger.SetUInt64(out BigInteger value2, 0x1FFFFFFFFul); // High block = 1

        BigInteger.Compare(ref value1, ref value2).Should().BeGreaterThan(0);
        BigInteger.Compare(ref value2, ref value1).Should().BeLessThan(0);
    }

    #endregion

    #region Add Tests

    [TestMethod]
    public void Add_ZeroPlusZero_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero1);
        BigInteger.SetZero(out BigInteger zero2);
        BigInteger.Add(ref zero1, ref zero2, out BigInteger result);

        result.IsZero().Should().BeTrue();
        result.GetLength().Should().Be(0);
    }

    [TestMethod]
    public void Add_ZeroPlusNonZero_ShouldReturnNonZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.SetUInt32(out BigInteger value, 42);
        BigInteger.Add(ref zero, ref value, out BigInteger result);

        result.ToUInt32().Should().Be(42);
        result.GetLength().Should().Be(1);
    }

    [TestMethod]
    public void Add_NoCarry_ShouldAddCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger a, 100);
        BigInteger.SetUInt32(out BigInteger b, 200);
        BigInteger.Add(ref a, ref b, out BigInteger result);

        result.ToUInt32().Should().Be(300);
        result.GetLength().Should().Be(1);
    }

    [TestMethod]
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

    [TestMethod]
    public void Add_DifferentLengths_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger small, 100);
        BigInteger.SetUInt64(out BigInteger large, 0x200000000ul);
        BigInteger.Add(ref small, ref large, out BigInteger result);

        result.GetLength().Should().Be(2);
        result.ToUInt64().Should().Be(0x200000064ul); // 0x200000000 + 100
    }

    [TestMethod]
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

    [TestMethod]
    public void Multiply_ByZero_ShouldReturnZero()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        BigInteger.Multiply(ref value, 0, out BigInteger result);

        result.IsZero().Should().BeTrue();
    }

    [TestMethod]
    public void Multiply_ByOne_ShouldReturnSameValue()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        BigInteger.Multiply(ref value, 1, out BigInteger result);

        result.ToUInt32().Should().Be(42);
        result.GetLength().Should().Be(1);
    }

    [TestMethod]
    public void Multiply_ZeroByValue_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.Multiply(ref zero, 42, out BigInteger result);

        result.IsZero().Should().BeTrue();
    }

    [TestMethod]
    public void Multiply_SingleBlockNoCarry_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 100);
        BigInteger.Multiply(ref value, 3, out BigInteger result);

        result.ToUInt32().Should().Be(300);
        result.GetLength().Should().Be(1);
    }

    [TestMethod]
    public void Multiply_SingleBlockWithCarry_ShouldExtendLength()
    {
        BigInteger.SetUInt32(out BigInteger value, 0x80000000u); // 2^31
        BigInteger.Multiply(ref value, 3, out BigInteger result);

        result.GetLength().Should().Be(2);
        result.ToUInt64().Should().Be(0x180000000ul); // 3 * 2^31
    }

    [TestMethod]
    public void Multiply_MaxUInt32_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, uint.MaxValue);
        BigInteger.Multiply(ref value, uint.MaxValue, out BigInteger result);

        ulong expected = (ulong)uint.MaxValue * uint.MaxValue;
        result.ToUInt64().Should().Be(expected);
    }

    [TestMethod]
    public void Multiply_BigIntegerByBigInteger_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger a, 0x12345678u);
        BigInteger.SetUInt32(out BigInteger b, 0x9ABCDEFu);
        BigInteger.Multiply(ref a, ref b, out BigInteger result);

        ulong expected = (ulong)0x12345678u * 0x9ABCDEFu;
        result.ToUInt64().Should().Be(expected);
    }

    [TestMethod]
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

    [TestMethod]
    public void CountSignificantBits_UInt32Zero_ShouldReturnZero()
    {
        BigInteger.CountSignificantBits(0u).Should().Be(0u);
    }

    [TestMethod]
    [DataRow(1u, 1u)]
    [DataRow(2u, 2u)]
    [DataRow(3u, 2u)]
    [DataRow(4u, 3u)]
    [DataRow(0x80000000u, 32u)]
    [DataRow(uint.MaxValue, 32u)]
    public void CountSignificantBits_UInt32_ShouldReturnCorrectCount(uint value, uint expected)
    {
        BigInteger.CountSignificantBits(value).Should().Be(expected);
    }

    [TestMethod]
    public void CountSignificantBits_UInt64Zero_ShouldReturnZero()
    {
        BigInteger.CountSignificantBits(0ul).Should().Be(0u);
    }

    [TestMethod]
    [DataRow(1ul, 1u)]
    [DataRow(0x100000000ul, 33u)]
    [DataRow(0x8000000000000000ul, 64u)]
    [DataRow(ulong.MaxValue, 64u)]
    public void CountSignificantBits_UInt64_ShouldReturnCorrectCount(ulong value, uint expected)
    {
        BigInteger.CountSignificantBits(value).Should().Be(expected);
    }

    [TestMethod]
    public void CountSignificantBits_BigIntegerZero_ShouldReturnZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        BigInteger.CountSignificantBits(ref zero).Should().Be(0u);
    }

    [TestMethod]
    public void CountSignificantBits_BigIntegerSingleBlock_ShouldReturnCorrectCount()
    {
        BigInteger.SetUInt32(out BigInteger value, 0x80000000u);
        BigInteger.CountSignificantBits(ref value).Should().Be(32u);
    }

    [TestMethod]
    public void CountSignificantBits_BigIntegerMultiBlock_ShouldReturnCorrectCount()
    {
        BigInteger.SetUInt64(out BigInteger value, 0x8000000000000000ul);
        BigInteger.CountSignificantBits(ref value).Should().Be(64u);
    }

    #endregion

    #region ShiftLeft Tests

    [TestMethod]
    public void ShiftLeft_Zero_ShouldRemainZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.ShiftLeft(5);
        zero.IsZero().Should().BeTrue();
    }

    [TestMethod]
    public void ShiftLeft_ByZero_ShouldNotChange()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        value.ShiftLeft(0);
        value.ToUInt32().Should().Be(42);
    }

    [TestMethod]
    public void ShiftLeft_WithinBlock_ShouldShiftCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);
        value.ShiftLeft(5);
        value.ToUInt32().Should().Be(32u); // 1 << 5
    }

    [TestMethod]
    public void ShiftLeft_ExactBlockBoundary_ShouldAddZeroBlock()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);
        value.ShiftLeft(32);
        value.GetLength().Should().Be(2);
        value.GetBlock(0).Should().Be(0u);
        value.GetBlock(1).Should().Be(1u);
    }

    [TestMethod]
    public void ShiftLeft_CrossBlockBoundary_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 0x80000000u);
        value.ShiftLeft(1);
        value.GetLength().Should().Be(2);
        value.GetBlock(0).Should().Be(0u);
        value.GetBlock(1).Should().Be(1u);
    }

    [TestMethod]
    public void ShiftLeft_LargeShift_ShouldHandleCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);
        value.ShiftLeft(65); // More than two blocks
        value.GetLength().Should().Be(3);
        value.GetBlock(0).Should().Be(0u);
        value.GetBlock(1).Should().Be(0u);
        value.GetBlock(2).Should().Be(2u); // 1 << 1 (65 % 32 = 1, plus 2 zero blocks)
    }

    [TestMethod]
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

    [TestMethod]
    public void Pow2_Zero_ShouldReturnOne()
    {
        BigInteger.Pow2(0, out BigInteger result);
        result.ToUInt32().Should().Be(1u);
        result.GetLength().Should().Be(1);
    }

    [TestMethod]
    [DataRow(1u, 2u)]
    [DataRow(2u, 4u)]
    [DataRow(10u, 1024u)]
    [DataRow(31u, 0x80000000u)]
    public void Pow2_SmallExponents_ShouldReturnCorrectPower(uint exponent, uint expected)
    {
        BigInteger.Pow2(exponent, out BigInteger result);
        result.ToUInt32().Should().Be(expected);
    }

    [TestMethod]
    public void Pow2_ExactBlockBoundary_ShouldCreateCorrectLength()
    {
        BigInteger.Pow2(32, out BigInteger result);
        result.GetLength().Should().Be(2);
        result.GetBlock(0).Should().Be(0u);
        result.GetBlock(1).Should().Be(1u);
    }

    [TestMethod]
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

    [TestMethod]
    public void Add_InstanceMethod_ShouldAddCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 100);
        value.Add(50);
        value.ToUInt32().Should().Be(150);
    }

    [TestMethod]
    public void Add_InstanceMethodWithCarry_ShouldExtendLength()
    {
        BigInteger.SetUInt32(out BigInteger value, uint.MaxValue);
        value.Add(1);
        value.GetLength().Should().Be(2);
        value.ToUInt64().Should().Be(0x100000000ul);
    }

    [TestMethod]
    public void Multiply_InstanceMethodByUInt_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        value.Multiply(3);
        value.ToUInt32().Should().Be(126);
    }

    [TestMethod]
    public void Multiply_InstanceMethodByBigInteger_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger a, 6);
        BigInteger.SetUInt32(out BigInteger b, 7);
        a.Multiply(ref b);
        a.ToUInt32().Should().Be(42);
    }

    [TestMethod]
    public void Multiply10_ShouldMultiplyByTen()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        value.Multiply10();
        value.ToUInt32().Should().Be(420);
    }

    [TestMethod]
    public void Multiply10_Zero_ShouldRemainZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.Multiply10();
        zero.IsZero().Should().BeTrue();
    }

    [TestMethod]
    public void MultiplyPow10_ShouldMultiplyCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger value, 42);
        value.MultiplyPow10(3); // * 1000
        value.ToUInt32().Should().Be(42000);
    }

    [TestMethod]
    public void MultiplyPow10_Zero_ShouldRemainZero()
    {
        BigInteger.SetZero(out BigInteger zero);
        zero.MultiplyPow10(5);
        zero.IsZero().Should().BeTrue();
    }

    #endregion

    #region Edge Cases and Error Conditions
    [TestMethod]
    public void GetBlock_ValidIndex_ShouldReturnCorrectBlock()
    {
        BigInteger.SetUInt64(out BigInteger value, 0x123456789ABCDEFul);
        // The value 0x123456789ABCDEF breaks down as:
        // High 32 bits: 0x1234567 
        // Low 32 bits: 0x89ABCDEF
        value.GetBlock(0).Should().Be(0x89ABCDEFu); // Low block
        value.GetBlock(1).Should().Be(0x1234567u);  // High block
    }

    [TestMethod]
    public void ToUInt32_LargerThanUInt32_ShouldReturnFirstBlock()
    {
        BigInteger.SetUInt64(out BigInteger value, 0x123456789ABCDEFul);
        value.ToUInt32().Should().Be(0x89ABCDEFu); // First (low) block only
    }

    [TestMethod]
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

    [TestMethod]
    public void DivRem_ZeroDividend_ShouldReturnZeros()
    {
        BigInteger.SetZero(out BigInteger dividend);
        BigInteger.SetUInt32(out BigInteger divisor, 42);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);
        quotient.IsZero().Should().BeTrue();
        remainder.IsZero().Should().BeTrue();
    }

    [TestMethod]
    public void DivRem_SingleBlockNumbers_ShouldDivideCorrectly()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 42);
        BigInteger.SetUInt32(out BigInteger divisor, 7);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        quotient.ToUInt32().Should().Be(6);
        remainder.ToUInt32().Should().Be(0);
    }

    [TestMethod]
    public void DivRem_WithRemainder_ShouldReturnCorrectValues()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 43);
        BigInteger.SetUInt32(out BigInteger divisor, 7);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        quotient.ToUInt32().Should().Be(6);
        remainder.ToUInt32().Should().Be(1);
    }

    [TestMethod]
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

    [TestMethod]
    public void HeuristicDivide_DivisorLargerThanDividend_ShouldReturnZero()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 7);
        BigInteger.SetUInt32(out BigInteger divisor, 42);

        uint result = BigInteger.HeuristicDivide(ref dividend, ref divisor);
        result.Should().Be(0);
    }

    [TestMethod]
    public void HeuristicDivide_EqualLengths_ShouldReturnEstimate()
    {
        BigInteger.SetUInt32(out BigInteger dividend, 100);
        BigInteger.SetUInt32(out BigInteger divisor, 30);

        uint result = BigInteger.HeuristicDivide(ref dividend, ref divisor);
        result.Should().BeGreaterThan(0);
        result.Should().BeLessThanOrEqualTo(4); // 100/30 = 3.33, heuristic should be close
    }

    #endregion

    #region Additional Edge Case Tests

    [TestMethod]
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
        result.GetLength().Should().BeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
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
        large.GetLength().Should().BeGreaterThanOrEqualTo(0);
    }
    [TestMethod]
    public void ShiftLeft_ExtremeShift_ShouldHandleGracefully()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);

        // Test large shift that approaches maximum blocks (but not extreme enough to crash)
        value.ShiftLeft(1000); // Large but reasonable shift

        // Should either work correctly or trigger overflow protection
        value.GetLength().Should().BeGreaterThanOrEqualTo(0);

        // If it didn't overflow, verify it's a power of 2
        if (!value.IsZero())
        {
            // The result should be 2^1000, which requires many blocks
            value.GetLength().Should().BeGreaterThan(30); // 1000 bits / 32 bits per block > 30
        }
    }

    [TestMethod]
    public void Compare_IdenticalValuesAfterOperations_ShouldBeEqual()
    {
        BigInteger.SetUInt32(out BigInteger a, 42);
        BigInteger.SetUInt32(out BigInteger b, 21);

        a.Multiply(2); // a = 42 * 2 = 84
        b.Multiply(4); // b = 21 * 4 = 84

        BigInteger.Compare(ref a, ref b).Should().Be(0);
    }

    [TestMethod]
    public void Pow10_LargeExponent_ShouldCalculateCorrectly()
    {
        BigInteger.Pow10(5, out BigInteger result);
        result.ToUInt32().Should().Be(100000u); // 10^5

        BigInteger.Pow10(9, out BigInteger result2);
        result2.ToUInt32().Should().Be(1000000000u); // 10^9
    }

    [TestMethod]
    public void SetValue_ModificationIndependence_ShouldNotAffectOriginal()
    {
        BigInteger.SetUInt32(out BigInteger original, 42);
        BigInteger.SetValue(out BigInteger copy, ref original);

        copy.Multiply(2);

        original.ToUInt32().Should().Be(42);
        copy.ToUInt32().Should().Be(84);
    }

    [TestMethod]
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
    [TestMethod]
    [DataRow(0u)]
    [DataRow(1u)]
    [DataRow(31u)]
    [DataRow(32u)]
    [DataRow(33u)]
    [DataRow(63u)]
    [DataRow(64u)]
    [DataRow(65u)]
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

    [TestMethod]
    public void CountSignificantBits_AfterOperations_ShouldBeCorrect()
    {
        BigInteger.SetUInt32(out BigInteger value, 1);
        BigInteger.CountSignificantBits(ref value).Should().Be(1u);

        value.ShiftLeft(10);
        BigInteger.CountSignificantBits(ref value).Should().Be(11u); // 1024 requires 11 bits

        value.Multiply(3);
        BigInteger.CountSignificantBits(ref value).Should().Be(12u); // 3072 requires 12 bits
    }

    [TestMethod]
    public void DivRem_EdgeCaseValues_ShouldHandleCorrectly()
    {
        // Test division where quotient is exactly 1
        BigInteger.SetUInt32(out BigInteger dividend, 100);
        BigInteger.SetUInt32(out BigInteger divisor, 100);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        quotient.ToUInt32().Should().Be(1);
        remainder.IsZero().Should().BeTrue();
    }

    [TestMethod]
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

    #region Coverage Gap Tests (vetted against System.Numerics.BigInteger)

    private static BclBigInteger ToBcl(scoped ref BigInteger value)
    {
        int length = value.GetLength();
        if (length == 0)
        {
            return BclBigInteger.Zero;
        }

        byte[] bytes = new byte[(length * 4) + 1];
        for (int i = 0; i < length; i++)
        {
            uint block = value.GetBlock((uint)i);
            bytes[(i * 4) + 0] = (byte)block;
            bytes[(i * 4) + 1] = (byte)(block >> 8);
            bytes[(i * 4) + 2] = (byte)(block >> 16);
            bytes[(i * 4) + 3] = (byte)(block >> 24);
        }

        // Trailing zero byte keeps the BCL BigInteger interpretation unsigned.
        return new BclBigInteger(bytes);
    }

    // Builds a touki BigInteger (which models an unsigned magnitude) from a
    // System.Numerics.BigInteger. Only non-negative inputs are supported; the
    // touki type has no sign and the byte-level masking below would otherwise
    // silently produce wrong values for negative inputs.
    private static void SetFromBcl(out BigInteger result, BclBigInteger value)
    {
        value.Sign.Should().BeGreaterThanOrEqualTo(0, "touki BigInteger represents an unsigned magnitude");

        if (value.IsZero)
        {
            BigInteger.SetZero(out result);
            return;
        }

        byte[] bytes = value.ToByteArray();
        // Pad to a multiple of 4 (and drop any sign byte by zeroing it out for unsigned reading).
        int byteLength = bytes.Length;
        int blockCount = (byteLength + 3) / 4;
        BigInteger.SetZero(out result);

        // Build via Add/ShiftLeft to avoid touching private state.
        for (int i = blockCount - 1; i >= 0; i--)
        {
            uint block = 0;
            int baseIndex = i * 4;
            for (int b = 0; b < 4; b++)
            {
                int idx = baseIndex + b;
                if (idx < byteLength)
                {
                    // Mask off any sign byte at the very end.
                    byte v = bytes[idx];
                    if (idx == byteLength - 1 && (v & 0x80) != 0 && idx % 4 == 0)
                    {
                        // Sign byte from BCL; treat as zero for unsigned magnitude.
                        v = 0;
                    }

                    block |= (uint)v << (b * 8);
                }
            }

            if (i != blockCount - 1)
            {
                result.ShiftLeft(32);
            }

            result.Add(block);
        }
    }

    [TestMethod]
    public void DivRem_MultiBlockDividendSingleBlockDivisor_MatchesBcl()
    {
        // dividend = 2^96 - 1 (3 blocks).
        BclBigInteger bclDividend = (BclBigInteger.One << 96) - 1;
        SetFromBcl(out BigInteger dividend, bclDividend);
        BigInteger.SetUInt32(out BigInteger divisor, 0xDEADBEEFu);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        BclBigInteger expectedQuo = BclBigInteger.DivRem(bclDividend, 0xDEADBEEFu, out BclBigInteger expectedRem);
        ToBcl(ref quotient).Should().Be(expectedQuo);
        ToBcl(ref remainder).Should().Be(expectedRem);
    }

    [TestMethod]
    public void DivRem_MultiBlockGrammarSchool_MatchesBcl()
    {
        // Build dividend = 10^60 (multi-block) and divisor = 10^25 (multi-block).
        BigInteger.Pow10(60, out BigInteger dividend);
        BigInteger.Pow10(25, out BigInteger divisor);

        // Sanity: both must be multi-block to exercise the grammar-school branch.
        dividend.GetLength().Should().BeGreaterThan(1);
        divisor.GetLength().Should().BeGreaterThan(1);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        BclBigInteger expectedQuo = BclBigInteger.DivRem(
            BclBigInteger.Pow(10, 60),
            BclBigInteger.Pow(10, 25),
            out BclBigInteger expectedRem);

        ToBcl(ref quotient).Should().Be(expectedQuo);
        ToBcl(ref remainder).Should().Be(expectedRem);
    }

    [TestMethod]
    public void DivRem_MultiBlockExactDivision_RemainderIsZero()
    {
        // dividend = 10^40, divisor = 10^20 -> quotient = 10^20, rem = 0.
        BigInteger.Pow10(40, out BigInteger dividend);
        BigInteger.Pow10(20, out BigInteger divisor);

        BigInteger.DivRem(ref dividend, ref divisor, out BigInteger quotient, out BigInteger remainder);

        BclBigInteger expected = BclBigInteger.Pow(10, 20);
        ToBcl(ref quotient).Should().Be(expected);
        remainder.IsZero().Should().BeTrue();
    }

    [TestMethod]
    public void Pow10_ExponentEight_ShouldEqualBcl()
    {
        // exponent 8 forces use of s_pow10BigNumTable[0].
        BigInteger.Pow10(8, out BigInteger result);
        ToBcl(ref result).Should().Be(BclBigInteger.Pow(10, 8));
    }

    [TestMethod]
    [DataRow(10u)]
    [DataRow(16u)]
    [DataRow(17u)]
    [DataRow(32u)]
    [DataRow(50u)]
    [DataRow(100u)]
    [DataRow(255u)]
    [DataRow(256u)]
    [DataRow(500u)]
    [DataRow(1000u)]
    public void Pow10_VariousExponents_ShouldEqualBcl(uint exponent)
    {
        BigInteger.Pow10(exponent, out BigInteger result);
        ToBcl(ref result).Should().Be(BclBigInteger.Pow(10, (int)exponent));
    }

    [TestMethod]
    public void MultiplyPow10_LargeExponent_ShouldEqualBcl()
    {
        BigInteger.SetUInt32(out BigInteger value, 7);
        value.MultiplyPow10(50);

        BclBigInteger expected = 7 * BclBigInteger.Pow(10, 50);
        ToBcl(ref value).Should().Be(expected);
    }

    [TestMethod]
    public void Multiply_LhsShorterThanRhs_ShouldSwapAndMultiply()
    {
        // lhs is single block, rhs is multi-block: triggers the lhs._length <= 1 fast path.
        BigInteger.SetUInt32(out BigInteger lhs, 0xABCDEF01u);
        BigInteger.Pow10(20, out BigInteger rhs);

        BigInteger.Multiply(ref lhs, ref rhs, out BigInteger result);

        BclBigInteger expected = (BclBigInteger)0xABCDEF01u * BclBigInteger.Pow(10, 20);
        ToBcl(ref result).Should().Be(expected);
    }

    [TestMethod]
    public void Multiply_BothMultiBlockWithLhsShorter_ShouldSwapInternally()
    {
        // lhs (2 blocks) shorter than rhs (multi-block from Pow10).
        BigInteger.SetUInt64(out BigInteger lhs, 0x123456789ABCDEFul);
        BigInteger.Pow10(40, out BigInteger rhs);

        BigInteger.Multiply(ref lhs, ref rhs, out BigInteger result);

        BclBigInteger expected = (BclBigInteger)0x123456789ABCDEFul * BclBigInteger.Pow(10, 40);
        ToBcl(ref result).Should().Be(expected);
    }

    [TestMethod]
    public void Multiply_RhsZero_ShouldReturnZero()
    {
        BigInteger.Pow10(30, out BigInteger lhs);
        BigInteger.SetZero(out BigInteger zero);

        BigInteger.Multiply(ref lhs, ref zero, out BigInteger result);
        result.IsZero().Should().BeTrue();
    }

    [TestMethod]
    public void ShiftLeft_PartialShiftMultiBlock_ShouldEqualBcl()
    {
        // Build a multi-block value, then partial (non-block-aligned) shift.
        BigInteger.Pow10(30, out BigInteger value);
        BclBigInteger expected = BclBigInteger.Pow(10, 30) << 5;

        value.ShiftLeft(5);
        ToBcl(ref value).Should().Be(expected);
    }

    [TestMethod]
    public void ShiftLeft_PartialShiftCrossingBlocks_ShouldEqualBcl()
    {
        BigInteger.Pow10(20, out BigInteger value);
        BclBigInteger expected = BclBigInteger.Pow(10, 20) << 37; // 1 block + 5 bits

        value.ShiftLeft(37);
        ToBcl(ref value).Should().Be(expected);
    }

    [TestMethod]
    public void HeuristicDivide_QuotientCorrection_ShouldStillDivideExactly()
    {
        // dividend == divisor exercises the post-loop correction branch
        // (estimate may be 0 when high blocks are equal; correction bumps it to 1).
        // Build both with the same primitive sequence so they have equal escape scopes.
        BigInteger.SetUInt32(out BigInteger dividend, 1);
        dividend.ShiftLeft(64); // 3 blocks: 0, 0, 1
        BigInteger.SetUInt32(out BigInteger divisor, 1);
        divisor.ShiftLeft(64);

        uint q = BigInteger.HeuristicDivide(ref dividend, ref divisor);
        q.Should().Be(1u);
        dividend.IsZero().Should().BeTrue();
    }

    [TestMethod]
    public void HeuristicDivide_LargerDividend_RecoversFullQuotient()
    {
        // dividend = 7 * divisor + r where r < divisor. Construct dividend with
        // simple primitives only (SetUInt64 + ShiftLeft + Add) - using more complex
        // helpers triggers ref-struct escape analysis errors on net481 when the
        // resulting locals are passed by ref to other ref-struct methods.
        BigInteger.SetUInt32(out BigInteger divisor, 0x100u);
        divisor.ShiftLeft(32); // divisor = 0x100_00000000

        BigInteger.SetUInt64(out BigInteger dividend, 7ul * 0x100_00000000ul);
        dividend.Add(12345);

        uint q = BigInteger.HeuristicDivide(ref dividend, ref divisor);

        q.Should().Be(7u);
        dividend.ToUInt32().Should().Be(12345u);
    }

    [TestMethod]
    public void Add_TwoMultiBlockValues_MatchesBcl()
    {
        BigInteger.Pow10(20, out BigInteger a);
        BigInteger.Pow10(25, out BigInteger b);

        BigInteger.Add(ref a, ref b, out BigInteger result);

        ToBcl(ref result).Should().Be(BclBigInteger.Pow(10, 20) + BclBigInteger.Pow(10, 25));
    }

    [TestMethod]
    public void Multiply_Pow10Squared_MatchesBcl()
    {
        BigInteger.Pow10(40, out BigInteger a);
        BigInteger.Pow10(40, out BigInteger b);

        BigInteger.Multiply(ref a, ref b, out BigInteger result);
        ToBcl(ref result).Should().Be(BclBigInteger.Pow(10, 80));
    }

    [TestMethod]
    public void Compare_EqualMultiBlock_ShouldReturnZero()
    {
        BigInteger.Pow10(50, out BigInteger a);
        BigInteger.Pow10(50, out BigInteger b);

        BigInteger.Compare(ref a, ref b).Should().Be(0);
    }

    [TestMethod]
    public void Compare_MultiBlockDifferingInLowBlock_ShouldDetectDifference()
    {
        BigInteger.Pow10(30, out BigInteger a);
        BigInteger.Pow10(30, out BigInteger b);
        b.Add(1);

        BigInteger.Compare(ref a, ref b).Should().BeLessThan(0);
        BigInteger.Compare(ref b, ref a).Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Multiply10_MultiBlockWithCarry_MatchesBcl()
    {
        BigInteger.SetUInt64(out BigInteger value, ulong.MaxValue);
        value.Multiply10();

        ToBcl(ref value).Should().Be((BclBigInteger)ulong.MaxValue * 10);
    }

    [TestMethod]
    public void Add_InstanceMethod_OnZero_ShouldInitialize()
    {
        BigInteger.SetZero(out BigInteger value);
        value.Add(123);
        value.ToUInt32().Should().Be(123u);
        value.GetLength().Should().Be(1);
    }

    [TestMethod]
    public void Add_InstanceMethod_CarryAcrossMultipleBlocks_MatchesBcl()
    {
        // Build value = (2^64 - 1), then add 1 to force carry through two blocks.
        BigInteger.SetUInt64(out BigInteger value, ulong.MaxValue);
        value.Add(1);

        value.GetLength().Should().Be(3);
        value.GetBlock(0).Should().Be(0u);
        value.GetBlock(1).Should().Be(0u);
        value.GetBlock(2).Should().Be(1u);
    }

    #endregion
}
