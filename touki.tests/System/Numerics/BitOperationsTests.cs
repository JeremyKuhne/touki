// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Numerics;

public class BitOperationsTests
{
    [Fact]
    public void RotateLeft_UInt32_Basic()
    {
        uint value = 0x12345678;

        // Rotate by 0 (no change)
        uint result = BitOperations.RotateLeft(value, 0);
        result.Should().Be(0x12345678);

        // Rotate by 4 bits
        result = BitOperations.RotateLeft(value, 4);
        result.Should().Be(0x23456781);

        // Rotate by 8 bits
        result = BitOperations.RotateLeft(value, 8);
        result.Should().Be(0x34567812);

        // Rotate by 16 bits (half the bits)
        result = BitOperations.RotateLeft(value, 16);
        result.Should().Be(0x56781234);

        // Rotate by 32 bits (full circle, no change)
        result = BitOperations.RotateLeft(value, 32);
        result.Should().Be(0x12345678);

        // Rotate by more than 32 bits (should be treated as modulo 32)
        result = BitOperations.RotateLeft(value, 36);
        result.Should().Be(0x23456781); // Same as rotating by 4
    }

    [Fact]
    public void RotateLeft_UInt64_Basic()
    {
        ulong value = 0x1234567890ABCDEF;

        // Rotate by 0 (no change)
        ulong result = BitOperations.RotateLeft(value, 0);
        result.Should().Be(0x1234567890ABCDEF);

        // Rotate by 4 bits
        result = BitOperations.RotateLeft(value, 4);
        result.Should().Be(0x234567890ABCDEF1);

        // Rotate by 8 bits
        result = BitOperations.RotateLeft(value, 8);
        result.Should().Be(0x34567890ABCDEF12);

        // Rotate by 32 bits (half the bits)
        result = BitOperations.RotateLeft(value, 32);
        result.Should().Be(0x90ABCDEF12345678);

        // Rotate by 64 bits (full circle, no change)
        result = BitOperations.RotateLeft(value, 64);
        result.Should().Be(0x1234567890ABCDEF);

        // Rotate by more than 64 bits (should be treated as modulo 64)
        result = BitOperations.RotateLeft(value, 68);
        result.Should().Be(0x234567890ABCDEF1); // Same as rotating by 4
    }

    [Fact]
    public void RotateLeft_UIntPtr_Basic()
    {
        nuint value = unchecked((nuint)0x1234567890ABCDEF);

        // Rotate by 4 bits
        nuint result = BitOperations.RotateLeft(value, 4);
        result.Should().Be(unchecked((nuint)0x234567890ABCDEF1));

        // Rotate by 64 bits (full circle, no change)
        result = BitOperations.RotateLeft(value, 64);
        result.Should().Be(unchecked((nuint)0x1234567890ABCDEF));
    }

    [Fact]
    public void RotateRight_UInt32_Basic()
    {
        uint value = 0x12345678;

        // Rotate by 0 (no change)
        uint result = BitOperations.RotateRight(value, 0);
        result.Should().Be(0x12345678);

        // Rotate by 4 bits
        result = BitOperations.RotateRight(value, 4);
        result.Should().Be(0x81234567);

        // Rotate by 8 bits
        result = BitOperations.RotateRight(value, 8);
        result.Should().Be(0x78123456);

        // Rotate by 16 bits (half the bits)
        result = BitOperations.RotateRight(value, 16);
        result.Should().Be(0x56781234);

        // Rotate by 32 bits (full circle, no change)
        result = BitOperations.RotateRight(value, 32);
        result.Should().Be(0x12345678);

        // Rotate by more than 32 bits (should be treated as modulo 32)
        result = BitOperations.RotateRight(value, 36);
        result.Should().Be(0x81234567); // Same as rotating by 4
    }

    [Fact]
    public void RotateRight_UInt64_Basic()
    {
        ulong value = 0x1234567890ABCDEF;

        // Rotate by 0 (no change)
        ulong result = BitOperations.RotateRight(value, 0);
        result.Should().Be(0x1234567890ABCDEF);

        // Rotate by 4 bits
        result = BitOperations.RotateRight(value, 4);
        result.Should().Be(0xF1234567890ABCDE);

        // Rotate by 8 bits
        result = BitOperations.RotateRight(value, 8);
        result.Should().Be(0xEF1234567890ABCD);

        // Rotate by 32 bits (half the bits)
        result = BitOperations.RotateRight(value, 32);
        result.Should().Be(0x90ABCDEF12345678);

        // Rotate by 64 bits (full circle, no change)
        result = BitOperations.RotateRight(value, 64);
        result.Should().Be(0x1234567890ABCDEF);

        // Rotate by more than 64 bits (should be treated as modulo 64)
        result = BitOperations.RotateRight(value, 68);
        result.Should().Be(0xF1234567890ABCDE); // Same as rotating by 4
    }

    [Fact]
    public void RotateRight_UIntPtr_Basic()
    {
        nuint value = unchecked((nuint)0x1234567890ABCDEF);

        // Rotate by 4 bits
        nuint result = BitOperations.RotateRight(value, 4);
        result.Should().Be(unchecked((nuint)0xF1234567890ABCDE));

        // Rotate by 64 bits (full circle, no change)
        result = BitOperations.RotateRight(value, 64);
        result.Should().Be(unchecked((nuint)0x1234567890ABCDEF));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(8, true)]
    [InlineData(16, true)]
    [InlineData(32, true)]
    [InlineData(64, true)]
    [InlineData(128, true)]
    [InlineData(256, true)]
    [InlineData(1073741824, true)] // 2^30
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(-2, false)]
    [InlineData(3, false)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    [InlineData(9, false)]
    [InlineData(15, false)]
    [InlineData(int.MaxValue, false)] // 2^31-1
    [InlineData(int.MinValue, false)] // -2^31
    public void IsPow2_Int32(int value, bool expected)
    {
        BitOperations.IsPow2(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1u, true)]
    [InlineData(2u, true)]
    [InlineData(4u, true)]
    [InlineData(8u, true)]
    [InlineData(16u, true)]
    [InlineData(32u, true)]
    [InlineData(64u, true)]
    [InlineData(128u, true)]
    [InlineData(256u, true)]
    [InlineData(2147483648u, true)] // 2^31
    [InlineData(0u, false)]
    [InlineData(3u, false)]
    [InlineData(5u, false)]
    [InlineData(6u, false)]
    [InlineData(7u, false)]
    [InlineData(9u, false)]
    [InlineData(15u, false)]
    [InlineData(uint.MaxValue, false)] // 2^32-1
    public void IsPow2_UInt32(uint value, bool expected)
    {
        BitOperations.IsPow2(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1L, true)]
    [InlineData(2L, true)]
    [InlineData(4L, true)]
    [InlineData(8L, true)]
    [InlineData(16L, true)]
    [InlineData(32L, true)]
    [InlineData(64L, true)]
    [InlineData(128L, true)]
    [InlineData(256L, true)]
    [InlineData(0L, false)]
    [InlineData(-1L, false)]
    [InlineData(-2L, false)]
    [InlineData(3L, false)]
    [InlineData(5L, false)]
    [InlineData(6L, false)]
    [InlineData(7L, false)]
    [InlineData(9L, false)]
    [InlineData(15L, false)]
    [InlineData(long.MaxValue, false)] // 2^63-1
    [InlineData(long.MinValue, false)] // -2^63
    public void IsPow2_Int64(long value, bool expected)
    {
        BitOperations.IsPow2(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1UL, true)]
    [InlineData(2UL, true)]
    [InlineData(4UL, true)]
    [InlineData(8UL, true)]
    [InlineData(16UL, true)]
    [InlineData(32UL, true)]
    [InlineData(64UL, true)]
    [InlineData(128UL, true)]
    [InlineData(256UL, true)]
    [InlineData(0UL, false)]
    [InlineData(3UL, false)]
    [InlineData(5UL, false)]
    [InlineData(6UL, false)]
    [InlineData(7UL, false)]
    [InlineData(9UL, false)]
    [InlineData(15UL, false)]
    [InlineData(ulong.MaxValue, false)] // 2^64-1
    public void IsPow2_UInt64(ulong value, bool expected)
    {
        BitOperations.IsPow2(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(8, true)]
    [InlineData(16, true)]
    [InlineData(32, true)]
    [InlineData(64, true)]
    [InlineData(128, true)]
    [InlineData(256, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(-2, false)]
    [InlineData(3, false)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    [InlineData(9, false)]
    [InlineData(15, false)]
    public void IsPow2_NInt(int value, bool expected)
    {
        BitOperations.IsPow2((nint)value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1u, true)]
    [InlineData(2u, true)]
    [InlineData(4u, true)]
    [InlineData(8u, true)]
    [InlineData(16u, true)]
    [InlineData(32u, true)]
    [InlineData(64u, true)]
    [InlineData(128u, true)]
    [InlineData(256u, true)]
    [InlineData(0u, false)]
    [InlineData(3u, false)]
    [InlineData(5u, false)]
    [InlineData(6u, false)]
    [InlineData(7u, false)]
    [InlineData(9u, false)]
    [InlineData(15u, false)]
    public void IsPow2_NUInt(uint value, bool expected)
    {
        BitOperations.IsPow2((nuint)value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1u, 1u)]
    [InlineData(2u, 2u)]
    [InlineData(4u, 4u)]
    [InlineData(8u, 8u)]
    [InlineData(16u, 16u)]
    [InlineData(32u, 32u)]
    [InlineData(64u, 64u)]
    [InlineData(128u, 128u)]
    [InlineData(256u, 256u)]
    [InlineData(0u, 0u)]
    [InlineData(3u, 4u)]
    [InlineData(5u, 8u)]
    [InlineData(6u, 8u)]
    [InlineData(7u, 8u)]
    [InlineData(9u, 16u)]
    [InlineData(15u, 16u)]
    [InlineData(1023u, 1024u)]
    [InlineData(1025u, 2048u)]
    [InlineData(0x80000001u, 0u)] // Overflow case
    [InlineData(uint.MaxValue, 0u)] // Overflow case
    public void RoundUpToPowerOf2_UInt32(uint value, uint expected)
    {
        BitOperations.RoundUpToPowerOf2(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1UL, 1UL)]
    [InlineData(2UL, 2UL)]
    [InlineData(4UL, 4UL)]
    [InlineData(8UL, 8UL)]
    [InlineData(16UL, 16UL)]
    [InlineData(32UL, 32UL)]
    [InlineData(64UL, 64UL)]
    [InlineData(128UL, 128UL)]
    [InlineData(256UL, 256UL)]
    [InlineData(0UL, 0UL)]
    [InlineData(3UL, 4UL)]
    [InlineData(5UL, 8UL)]
    [InlineData(6UL, 8UL)]
    [InlineData(7UL, 8UL)]
    [InlineData(9UL, 16UL)]
    [InlineData(15UL, 16UL)]
    [InlineData(1023UL, 1024UL)]
    [InlineData(1025UL, 2048UL)]
    [InlineData(0x8000000000000001UL, 0UL)] // Overflow case
    [InlineData(ulong.MaxValue, 0UL)] // Overflow case
    public void RoundUpToPowerOf2_UInt64(ulong value, ulong expected)
    {
        BitOperations.RoundUpToPowerOf2(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1u, 1u)]
    [InlineData(2u, 2u)]
    [InlineData(4u, 4u)]
    [InlineData(8u, 8u)]
    [InlineData(16u, 16u)]
    [InlineData(32u, 32u)]
    [InlineData(64u, 64u)]
    [InlineData(128u, 128u)]
    [InlineData(256u, 256u)]
    [InlineData(0u, 0u)]
    [InlineData(3u, 4u)]
    [InlineData(5u, 8u)]
    [InlineData(6u, 8u)]
    [InlineData(7u, 8u)]
    [InlineData(9u, 16u)]
    [InlineData(15u, 16u)]
    [InlineData(1023u, 1024u)]
    [InlineData(1025u, 2048u)]
    public void RoundUpToPowerOf2_NUInt(uint value, uint expected)
    {
        BitOperations.RoundUpToPowerOf2((nuint)value).Should().Be((nuint)expected);
    }

    [Theory]
    [InlineData(0u, 32)]
    [InlineData(1u, 31)]
    [InlineData(2u, 30)]
    [InlineData(4u, 29)]
    [InlineData(8u, 28)]
    [InlineData(16u, 27)]
    [InlineData(32u, 26)]
    [InlineData(64u, 25)]
    [InlineData(128u, 24)]
    [InlineData(256u, 23)]
    [InlineData(3u, 30)]
    [InlineData(5u, 29)]
    [InlineData(7u, 29)]
    [InlineData(9u, 28)]
    [InlineData(0x0FFFFFFFu, 4)]
    [InlineData(0xFFFFFFFFu, 0)]
    public void LeadingZeroCount_UInt32(uint value, int expected)
    {
        BitOperations.LeadingZeroCount(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0UL, 64)]
    [InlineData(1UL, 63)]
    [InlineData(2UL, 62)]
    [InlineData(4UL, 61)]
    [InlineData(8UL, 60)]
    [InlineData(16UL, 59)]
    [InlineData(32UL, 58)]
    [InlineData(64UL, 57)]
    [InlineData(128UL, 56)]
    [InlineData(256UL, 55)]
    [InlineData(3UL, 62)]
    [InlineData(5UL, 61)]
    [InlineData(7UL, 61)]
    [InlineData(9UL, 60)]
    [InlineData(0x0FFFFFFFFFFFFFFFUL, 4)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, 0)]
    public void LeadingZeroCount_UInt64(ulong value, int expected)
    {
        BitOperations.LeadingZeroCount(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0UL, 64)]
    [InlineData(1UL, 63)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, 0)]
    public void LeadingZeroCount_NUInt(ulong value, int expected)
    {
        BitOperations.LeadingZeroCount((nuint)value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(1u, 0)]
    [InlineData(2u, 1)]
    [InlineData(4u, 2)]
    [InlineData(8u, 3)]
    [InlineData(16u, 4)]
    [InlineData(32u, 5)]
    [InlineData(64u, 6)]
    [InlineData(128u, 7)]
    [InlineData(256u, 8)]
    [InlineData(3u, 1)]
    [InlineData(5u, 2)]
    [InlineData(7u, 2)]
    [InlineData(9u, 3)]
    [InlineData(15u, 3)]
    [InlineData(uint.MaxValue, 31)]
    public void Log2_UInt32(uint value, int expected)
    {
        BitOperations.Log2(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0UL, 0)]
    [InlineData(1UL, 0)]
    [InlineData(2UL, 1)]
    [InlineData(4UL, 2)]
    [InlineData(8UL, 3)]
    [InlineData(16UL, 4)]
    [InlineData(32UL, 5)]
    [InlineData(64UL, 6)]
    [InlineData(128UL, 7)]
    [InlineData(256UL, 8)]
    [InlineData(3UL, 1)]
    [InlineData(5UL, 2)]
    [InlineData(7UL, 2)]
    [InlineData(9UL, 3)]
    [InlineData(15UL, 3)]
    [InlineData(ulong.MaxValue, 63)]
    public void Log2_UInt64(ulong value, int expected)
    {
        BitOperations.Log2(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0UL, 0)]
    [InlineData(1UL, 0)]
    [InlineData(2UL, 1)]
    [InlineData(ulong.MaxValue, 63)]
    public void Log2_NUInt(ulong value, int expected)
    {
        BitOperations.Log2((nuint)value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(1u, 1)]
    [InlineData(2u, 1)]
    [InlineData(4u, 1)]
    [InlineData(8u, 1)]
    [InlineData(16u, 1)]
    [InlineData(32u, 1)]
    [InlineData(64u, 1)]
    [InlineData(128u, 1)]
    [InlineData(256u, 1)]
    [InlineData(3u, 2)]
    [InlineData(5u, 2)]
    [InlineData(7u, 3)]
    [InlineData(9u, 2)]
    [InlineData(15u, 4)]
    [InlineData(0x0FFFFFFFu, 28)]
    [InlineData(0xFFFFFFFFu, 32)]
    public void PopCount_UInt32(uint value, int expected)
    {
        BitOperations.PopCount(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0UL, 0)]
    [InlineData(1UL, 1)]
    [InlineData(2UL, 1)]
    [InlineData(4UL, 1)]
    [InlineData(8UL, 1)]
    [InlineData(16UL, 1)]
    [InlineData(32UL, 1)]
    [InlineData(64UL, 1)]
    [InlineData(128UL, 1)]
    [InlineData(256UL, 1)]
    [InlineData(3UL, 2)]
    [InlineData(5UL, 2)]
    [InlineData(7UL, 3)]
    [InlineData(9UL, 2)]
    [InlineData(15UL, 4)]
    [InlineData(0x0FFFFFFFFFFFFFFFUL, 60)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, 64)]
    public void PopCount_UInt64(ulong value, int expected)
    {
        BitOperations.PopCount(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0UL, 0)]
    [InlineData(1UL, 1)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, 64)]
    public void PopCount_NUInt(ulong value, int expected)
    {
        BitOperations.PopCount((nuint)value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 3)]
    [InlineData(16, 4)]
    [InlineData(32, 5)]
    [InlineData(64, 6)]
    [InlineData(128, 7)]
    [InlineData(256, 8)]
    [InlineData(3, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 1)]
    [InlineData(7, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(12, 2)]
    [InlineData(0xF0, 4)]
    public void TrailingZeroCount_Int32(int value, int expected)
    {
        BitOperations.TrailingZeroCount(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0u, 32)]
    [InlineData(1u, 0)]
    [InlineData(2u, 1)]
    [InlineData(4u, 2)]
    [InlineData(8u, 3)]
    [InlineData(16u, 4)]
    [InlineData(32u, 5)]
    [InlineData(64u, 6)]
    [InlineData(128u, 7)]
    [InlineData(256u, 8)]
    [InlineData(3u, 0)]
    [InlineData(5u, 0)]
    [InlineData(6u, 1)]
    [InlineData(7u, 0)]
    [InlineData(9u, 0)]
    [InlineData(10u, 1)]
    [InlineData(12u, 2)]
    [InlineData(0xF0u, 4)]
    public void TrailingZeroCount_UInt32(uint value, int expected)
    {
        BitOperations.TrailingZeroCount(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0L, 64)]
    [InlineData(1L, 0)]
    [InlineData(2L, 1)]
    [InlineData(4L, 2)]
    [InlineData(8L, 3)]
    [InlineData(16L, 4)]
    [InlineData(32L, 5)]
    [InlineData(64L, 6)]
    [InlineData(128L, 7)]
    [InlineData(256L, 8)]
    [InlineData(3L, 0)]
    [InlineData(5L, 0)]
    [InlineData(6L, 1)]
    [InlineData(7L, 0)]
    [InlineData(9L, 0)]
    [InlineData(10L, 1)]
    [InlineData(12L, 2)]
    [InlineData(0xF0L, 4)]
    public void TrailingZeroCount_Int64(long value, int expected)
    {
        BitOperations.TrailingZeroCount(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0UL, 64)]
    [InlineData(1UL, 0)]
    [InlineData(2UL, 1)]
    [InlineData(4UL, 2)]
    [InlineData(8UL, 3)]
    [InlineData(16UL, 4)]
    [InlineData(32UL, 5)]
    [InlineData(64UL, 6)]
    [InlineData(128UL, 7)]
    [InlineData(256UL, 8)]
    [InlineData(3UL, 0)]
    [InlineData(5UL, 0)]
    [InlineData(6UL, 1)]
    [InlineData(7UL, 0)]
    [InlineData(9UL, 0)]
    [InlineData(10UL, 1)]
    [InlineData(12UL, 2)]
    [InlineData(0xF0UL, 4)]
    public void TrailingZeroCount_UInt64(ulong value, int expected)
    {
        BitOperations.TrailingZeroCount(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0L, 64)]
    [InlineData(1L, 0)]
    [InlineData(2L, 1)]
    public void TrailingZeroCount_NInt(long value, int expected)
    {
        BitOperations.TrailingZeroCount((nint)value).Should().Be(expected);
    }

    [Theory]
    [InlineData(0UL, 64)]
    [InlineData(1UL, 0)]
    [InlineData(2UL, 1)]
    public void TrailingZeroCount_NUInt(ulong value, int expected)
    {
        BitOperations.TrailingZeroCount((nuint)value).Should().Be(expected);
    }

    [Fact]
    public void Crc32C_Byte_Basic()
    {
        // Initialize with 0
        uint crc = 0;

        // Test with single bytes
        crc = BitOperations.Crc32C(crc, (byte)0);
        crc.Should().Be(0x00000000);

        crc = BitOperations.Crc32C(crc, (byte)1);
        crc.Should().Be(0xF26B8303);

        crc = BitOperations.Crc32C(crc, (byte)0xFF);
        crc.Should().Be(0xBEDFCB26);

        // Test idempotency
        uint crc1 = 0;
        uint crc2 = 0;

        for (byte i = 0; i < 10; i++)
        {
            crc1 = BitOperations.Crc32C(crc1, i);
        }

        for (byte i = 0; i < 10; i++)
        {
            crc2 = BitOperations.Crc32C(crc2, i);
        }

        crc1.Should().Be(crc2);
        crc1.Should().Be(0xE1F1D15A);
    }

    [Fact]
    public void Crc32C_UInt16_Basic()
    {
        // Initialize with 0
        uint crc = 0;

        // Test with various ushort values
        crc = BitOperations.Crc32C(crc, (ushort)0);
        crc.Should().Be(0x00000000);

        crc = BitOperations.Crc32C(crc, (ushort)1);
        crc.Should().Be(0x13A29877);

        crc = BitOperations.Crc32C(crc, (ushort)0xFFFF);
        crc.Should().Be(0xD3DBDD6A);

        // Test that byte-by-byte calculation gives the same result as ushort
        uint crc1 = 0;
        uint crc2 = 0;
        ushort testValue = 0x1234;

        // Calculate using ushort method
        crc1 = BitOperations.Crc32C(crc1, testValue);
        crc1.Should().Be(0xFFA1C4C7);

        // Calculate using byte method (little endian)
        crc2 = BitOperations.Crc32C(crc2, (byte)(testValue & 0xFF));
        crc2 = BitOperations.Crc32C(crc2, (byte)(testValue >> 8));

        crc1.Should().Be(crc2);
    }

    [Fact]
    public void Crc32C_UInt32_Basic()
    {
        // Initialize with 0
        uint crc = 0;

        // Test with various uint values
        crc = BitOperations.Crc32C(crc, 0u);
        crc.Should().Be(0x00000000);

        crc = BitOperations.Crc32C(crc, 1u);
        crc.Should().Be(0xDD45AAB8);

        crc = BitOperations.Crc32C(crc, 0xFFFFFFFFu);
        crc.Should().Be(0xFEA4C91F);

        // Test idempotency
        uint crc1 = 0;
        uint crc2 = 0;

        for (uint i = 0; i < 10; i++)
        {
            crc1 = BitOperations.Crc32C(crc1, i);
        }

        for (uint i = 0; i < 10; i++)
        {
            crc2 = BitOperations.Crc32C(crc2, i);
        }

        crc1.Should().Be(crc2);
        crc1.Should().Be(0x7B671FE6);
    }

    [Fact]
    public void Crc32C_UInt64_Basic()
    {
        // Initialize with 0
        uint crc = 0;

        // Test with various ulong values
        crc = BitOperations.Crc32C(crc, 0UL);
        crc.Should().Be(0x00000000);

        crc = BitOperations.Crc32C(crc, 1UL);
        crc.Should().Be(0x493C7D27);

        crc = BitOperations.Crc32C(crc, 0xFFFFFFFFFFFFFFFFUL);
        crc.Should().Be(0x3643F4B3);

        // Test that it correctly processes as two uint calculations
        uint crc1 = 0;
        uint crc2 = 0;
        ulong testValue = 0x1234567890ABCDEF;

        // Calculate using ulong method
        crc1 = BitOperations.Crc32C(crc1, testValue);
        crc1.Should().Be(0x41C6B4F0);

        // Calculate using two uint method calls
        crc2 = BitOperations.Crc32C(crc2, (uint)testValue);
        crc2 = BitOperations.Crc32C(crc2, (uint)(testValue >> 32));

        // Results should be the same
        crc1.Should().Be(crc2);
    }
}
