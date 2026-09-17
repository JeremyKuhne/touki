// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Runtime.CompilerServices;

[TestClass]
public class UnsafeExtensionsTests
{
    [TestMethod]
    public void BitCast_FloatToUInt32_RoundTrips()
    {
        const float Value = 3.14159f;
        uint bits = Unsafe.BitCast<float, uint>(Value);
        float roundTripped = Unsafe.BitCast<uint, float>(bits);
        roundTripped.Should().Be(Value);
    }

    [TestMethod]
    public void BitCast_DoubleToUInt64_MatchesBitConverter()
    {
        const double Value = -123.456;
        ulong bits = Unsafe.BitCast<double, ulong>(Value);
        bits.Should().Be((ulong)BitConverter.DoubleToInt64Bits(Value));
    }

    [TestMethod]
    public void BitCast_Int32ToFloat_PreservesBitPattern()
    {
        int bits = unchecked((int)0xC0490FDB);
        float result = Unsafe.BitCast<int, float>(bits);
        result.Should().Be(BitConverter.Int32BitsToSingle(bits));
    }

    [TestMethod]
    public void BitCast_DifferentSizes_ThrowsNotSupportedException()
    {
        Action act = () => _ = Unsafe.BitCast<int, long>(1);
        act.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void BitCast_FromShortToUShort_RoundTrips()
    {
        const short Value = -1234;
        ushort bits = Unsafe.BitCast<short, ushort>(Value);
        bits.Should().Be(unchecked((ushort)Value));
        short roundTripped = Unsafe.BitCast<ushort, short>(bits);
        roundTripped.Should().Be(Value);
    }
}
