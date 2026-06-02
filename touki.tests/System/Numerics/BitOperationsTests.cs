// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Numerics;

public class BitOperationsTests
{
    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    [Arguments(1, true)]
    [Arguments(2, true)]
    [Arguments(4, true)]
    [Arguments(8, true)]
    [Arguments(16, true)]
    [Arguments(32, true)]
    [Arguments(64, true)]
    [Arguments(128, true)]
    [Arguments(256, true)]
    [Arguments(1073741824, true)] // 2^30
    [Arguments(0, false)]
    [Arguments(-1, false)]
    [Arguments(-2, false)]
    [Arguments(3, false)]
    [Arguments(5, false)]
    [Arguments(6, false)]
    [Arguments(7, false)]
    [Arguments(9, false)]
    [Arguments(15, false)]
    [Arguments(int.MaxValue, false)] // 2^31-1
    [Arguments(int.MinValue, false)] // -2^31
    public void IsPow2_Int32(int value, bool expected)
    {
        BitOperations.IsPow2(value).Should().Be(expected);
    }

    [Test]
    [Arguments(1u, true)]
    [Arguments(2u, true)]
    [Arguments(4u, true)]
    [Arguments(8u, true)]
    [Arguments(16u, true)]
    [Arguments(32u, true)]
    [Arguments(64u, true)]
    [Arguments(128u, true)]
    [Arguments(256u, true)]
    [Arguments(2147483648u, true)] // 2^31
    [Arguments(0u, false)]
    [Arguments(3u, false)]
    [Arguments(5u, false)]
    [Arguments(6u, false)]
    [Arguments(7u, false)]
    [Arguments(9u, false)]
    [Arguments(15u, false)]
    [Arguments(uint.MaxValue, false)] // 2^32-1
    public void IsPow2_UInt32(uint value, bool expected)
    {
        BitOperations.IsPow2(value).Should().Be(expected);
    }

    [Test]
    [Arguments(1L, true)]
    [Arguments(2L, true)]
    [Arguments(4L, true)]
    [Arguments(8L, true)]
    [Arguments(16L, true)]
    [Arguments(32L, true)]
    [Arguments(64L, true)]
    [Arguments(128L, true)]
    [Arguments(256L, true)]
    [Arguments(0L, false)]
    [Arguments(-1L, false)]
    [Arguments(-2L, false)]
    [Arguments(3L, false)]
    [Arguments(5L, false)]
    [Arguments(6L, false)]
    [Arguments(7L, false)]
    [Arguments(9L, false)]
    [Arguments(15L, false)]
    [Arguments(long.MaxValue, false)] // 2^63-1
    [Arguments(long.MinValue, false)] // -2^63
    public void IsPow2_Int64(long value, bool expected)
    {
        BitOperations.IsPow2(value).Should().Be(expected);
    }

    [Test]
    [Arguments(1UL, true)]
    [Arguments(2UL, true)]
    [Arguments(4UL, true)]
    [Arguments(8UL, true)]
    [Arguments(16UL, true)]
    [Arguments(32UL, true)]
    [Arguments(64UL, true)]
    [Arguments(128UL, true)]
    [Arguments(256UL, true)]
    [Arguments(0UL, false)]
    [Arguments(3UL, false)]
    [Arguments(5UL, false)]
    [Arguments(6UL, false)]
    [Arguments(7UL, false)]
    [Arguments(9UL, false)]
    [Arguments(15UL, false)]
    [Arguments(ulong.MaxValue, false)] // 2^64-1
    public void IsPow2_UInt64(ulong value, bool expected)
    {
        BitOperations.IsPow2(value).Should().Be(expected);
    }

    [Test]
    [Arguments(1, true)]
    [Arguments(2, true)]
    [Arguments(4, true)]
    [Arguments(8, true)]
    [Arguments(16, true)]
    [Arguments(32, true)]
    [Arguments(64, true)]
    [Arguments(128, true)]
    [Arguments(256, true)]
    [Arguments(0, false)]
    [Arguments(-1, false)]
    [Arguments(-2, false)]
    [Arguments(3, false)]
    [Arguments(5, false)]
    [Arguments(6, false)]
    [Arguments(7, false)]
    [Arguments(9, false)]
    [Arguments(15, false)]
    public void IsPow2_NInt(int value, bool expected)
    {
        BitOperations.IsPow2((nint)value).Should().Be(expected);
    }

    [Test]
    [Arguments(1u, true)]
    [Arguments(2u, true)]
    [Arguments(4u, true)]
    [Arguments(8u, true)]
    [Arguments(16u, true)]
    [Arguments(32u, true)]
    [Arguments(64u, true)]
    [Arguments(128u, true)]
    [Arguments(256u, true)]
    [Arguments(0u, false)]
    [Arguments(3u, false)]
    [Arguments(5u, false)]
    [Arguments(6u, false)]
    [Arguments(7u, false)]
    [Arguments(9u, false)]
    [Arguments(15u, false)]
    public void IsPow2_NUInt(uint value, bool expected)
    {
        BitOperations.IsPow2((nuint)value).Should().Be(expected);
    }

    [Test]
    [Arguments(1u, 1u)]
    [Arguments(2u, 2u)]
    [Arguments(4u, 4u)]
    [Arguments(8u, 8u)]
    [Arguments(16u, 16u)]
    [Arguments(32u, 32u)]
    [Arguments(64u, 64u)]
    [Arguments(128u, 128u)]
    [Arguments(256u, 256u)]
    [Arguments(0u, 0u)]
    [Arguments(3u, 4u)]
    [Arguments(5u, 8u)]
    [Arguments(6u, 8u)]
    [Arguments(7u, 8u)]
    [Arguments(9u, 16u)]
    [Arguments(15u, 16u)]
    [Arguments(1023u, 1024u)]
    [Arguments(1025u, 2048u)]
    [Arguments(0x80000001u, 0u)] // Overflow case
    [Arguments(uint.MaxValue, 0u)] // Overflow case
    public void RoundUpToPowerOf2_UInt32(uint value, uint expected)
    {
        BitOperations.RoundUpToPowerOf2(value).Should().Be(expected);
    }

    [Test]
    [Arguments(1UL, 1UL)]
    [Arguments(2UL, 2UL)]
    [Arguments(4UL, 4UL)]
    [Arguments(8UL, 8UL)]
    [Arguments(16UL, 16UL)]
    [Arguments(32UL, 32UL)]
    [Arguments(64UL, 64UL)]
    [Arguments(128UL, 128UL)]
    [Arguments(256UL, 256UL)]
    [Arguments(0UL, 0UL)]
    [Arguments(3UL, 4UL)]
    [Arguments(5UL, 8UL)]
    [Arguments(6UL, 8UL)]
    [Arguments(7UL, 8UL)]
    [Arguments(9UL, 16UL)]
    [Arguments(15UL, 16UL)]
    [Arguments(1023UL, 1024UL)]
    [Arguments(1025UL, 2048UL)]
    [Arguments(0x8000000000000001UL, 0UL)] // Overflow case
    [Arguments(ulong.MaxValue, 0UL)] // Overflow case
    public void RoundUpToPowerOf2_UInt64(ulong value, ulong expected)
    {
        BitOperations.RoundUpToPowerOf2(value).Should().Be(expected);
    }

    [Test]
    [Arguments(1u, 1u)]
    [Arguments(2u, 2u)]
    [Arguments(4u, 4u)]
    [Arguments(8u, 8u)]
    [Arguments(16u, 16u)]
    [Arguments(32u, 32u)]
    [Arguments(64u, 64u)]
    [Arguments(128u, 128u)]
    [Arguments(256u, 256u)]
    [Arguments(0u, 0u)]
    [Arguments(3u, 4u)]
    [Arguments(5u, 8u)]
    [Arguments(6u, 8u)]
    [Arguments(7u, 8u)]
    [Arguments(9u, 16u)]
    [Arguments(15u, 16u)]
    [Arguments(1023u, 1024u)]
    [Arguments(1025u, 2048u)]
    public void RoundUpToPowerOf2_NUInt(uint value, uint expected)
    {
        BitOperations.RoundUpToPowerOf2((nuint)value).Should().Be((nuint)expected);
    }

    [Test]
    [Arguments(0u, 32)]
    [Arguments(1u, 31)]
    [Arguments(2u, 30)]
    [Arguments(4u, 29)]
    [Arguments(8u, 28)]
    [Arguments(16u, 27)]
    [Arguments(32u, 26)]
    [Arguments(64u, 25)]
    [Arguments(128u, 24)]
    [Arguments(256u, 23)]
    [Arguments(3u, 30)]
    [Arguments(5u, 29)]
    [Arguments(7u, 29)]
    [Arguments(9u, 28)]
    [Arguments(0x0FFFFFFFu, 4)]
    [Arguments(0xFFFFFFFFu, 0)]
    public void LeadingZeroCount_UInt32(uint value, int expected)
    {
        BitOperations.LeadingZeroCount(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0UL, 64)]
    [Arguments(1UL, 63)]
    [Arguments(2UL, 62)]
    [Arguments(4UL, 61)]
    [Arguments(8UL, 60)]
    [Arguments(16UL, 59)]
    [Arguments(32UL, 58)]
    [Arguments(64UL, 57)]
    [Arguments(128UL, 56)]
    [Arguments(256UL, 55)]
    [Arguments(3UL, 62)]
    [Arguments(5UL, 61)]
    [Arguments(7UL, 61)]
    [Arguments(9UL, 60)]
    [Arguments(0x0FFFFFFFFFFFFFFFUL, 4)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0)]
    public void LeadingZeroCount_UInt64(ulong value, int expected)
    {
        BitOperations.LeadingZeroCount(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0UL, 64)]
    [Arguments(1UL, 63)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 0)]
    public void LeadingZeroCount_NUInt(ulong value, int expected)
    {
        BitOperations.LeadingZeroCount((nuint)value).Should().Be(expected);
    }

    [Test]
    [Arguments(0u, 0)]
    [Arguments(1u, 0)]
    [Arguments(2u, 1)]
    [Arguments(4u, 2)]
    [Arguments(8u, 3)]
    [Arguments(16u, 4)]
    [Arguments(32u, 5)]
    [Arguments(64u, 6)]
    [Arguments(128u, 7)]
    [Arguments(256u, 8)]
    [Arguments(3u, 1)]
    [Arguments(5u, 2)]
    [Arguments(7u, 2)]
    [Arguments(9u, 3)]
    [Arguments(15u, 3)]
    [Arguments(uint.MaxValue, 31)]
    public void Log2_UInt32(uint value, int expected)
    {
        BitOperations.Log2(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0UL, 0)]
    [Arguments(1UL, 0)]
    [Arguments(2UL, 1)]
    [Arguments(4UL, 2)]
    [Arguments(8UL, 3)]
    [Arguments(16UL, 4)]
    [Arguments(32UL, 5)]
    [Arguments(64UL, 6)]
    [Arguments(128UL, 7)]
    [Arguments(256UL, 8)]
    [Arguments(3UL, 1)]
    [Arguments(5UL, 2)]
    [Arguments(7UL, 2)]
    [Arguments(9UL, 3)]
    [Arguments(15UL, 3)]
    [Arguments(ulong.MaxValue, 63)]
    public void Log2_UInt64(ulong value, int expected)
    {
        BitOperations.Log2(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0UL, 0)]
    [Arguments(1UL, 0)]
    [Arguments(2UL, 1)]
    [Arguments(ulong.MaxValue, 63)]
    public void Log2_NUInt(ulong value, int expected)
    {
        BitOperations.Log2((nuint)value).Should().Be(expected);
    }

    [Test]
    [Arguments(0u, 0)]
    [Arguments(1u, 1)]
    [Arguments(2u, 1)]
    [Arguments(4u, 1)]
    [Arguments(8u, 1)]
    [Arguments(16u, 1)]
    [Arguments(32u, 1)]
    [Arguments(64u, 1)]
    [Arguments(128u, 1)]
    [Arguments(256u, 1)]
    [Arguments(3u, 2)]
    [Arguments(5u, 2)]
    [Arguments(7u, 3)]
    [Arguments(9u, 2)]
    [Arguments(15u, 4)]
    [Arguments(0x0FFFFFFFu, 28)]
    [Arguments(0xFFFFFFFFu, 32)]
    public void PopCount_UInt32(uint value, int expected)
    {
        BitOperations.PopCount(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0UL, 0)]
    [Arguments(1UL, 1)]
    [Arguments(2UL, 1)]
    [Arguments(4UL, 1)]
    [Arguments(8UL, 1)]
    [Arguments(16UL, 1)]
    [Arguments(32UL, 1)]
    [Arguments(64UL, 1)]
    [Arguments(128UL, 1)]
    [Arguments(256UL, 1)]
    [Arguments(3UL, 2)]
    [Arguments(5UL, 2)]
    [Arguments(7UL, 3)]
    [Arguments(9UL, 2)]
    [Arguments(15UL, 4)]
    [Arguments(0x0FFFFFFFFFFFFFFFUL, 60)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 64)]
    public void PopCount_UInt64(ulong value, int expected)
    {
        BitOperations.PopCount(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0UL, 0)]
    [Arguments(1UL, 1)]
    [Arguments(0xFFFFFFFFFFFFFFFFUL, 64)]
    public void PopCount_NUInt(ulong value, int expected)
    {
        BitOperations.PopCount((nuint)value).Should().Be(expected);
    }

    [Test]
    [Arguments(0, 32)]
    [Arguments(1, 0)]
    [Arguments(2, 1)]
    [Arguments(4, 2)]
    [Arguments(8, 3)]
    [Arguments(16, 4)]
    [Arguments(32, 5)]
    [Arguments(64, 6)]
    [Arguments(128, 7)]
    [Arguments(256, 8)]
    [Arguments(3, 0)]
    [Arguments(5, 0)]
    [Arguments(6, 1)]
    [Arguments(7, 0)]
    [Arguments(9, 0)]
    [Arguments(10, 1)]
    [Arguments(12, 2)]
    [Arguments(0xF0, 4)]
    public void TrailingZeroCount_Int32(int value, int expected)
    {
        BitOperations.TrailingZeroCount(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0u, 32)]
    [Arguments(1u, 0)]
    [Arguments(2u, 1)]
    [Arguments(4u, 2)]
    [Arguments(8u, 3)]
    [Arguments(16u, 4)]
    [Arguments(32u, 5)]
    [Arguments(64u, 6)]
    [Arguments(128u, 7)]
    [Arguments(256u, 8)]
    [Arguments(3u, 0)]
    [Arguments(5u, 0)]
    [Arguments(6u, 1)]
    [Arguments(7u, 0)]
    [Arguments(9u, 0)]
    [Arguments(10u, 1)]
    [Arguments(12u, 2)]
    [Arguments(0xF0u, 4)]
    public void TrailingZeroCount_UInt32(uint value, int expected)
    {
        BitOperations.TrailingZeroCount(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0L, 64)]
    [Arguments(1L, 0)]
    [Arguments(2L, 1)]
    [Arguments(4L, 2)]
    [Arguments(8L, 3)]
    [Arguments(16L, 4)]
    [Arguments(32L, 5)]
    [Arguments(64L, 6)]
    [Arguments(128L, 7)]
    [Arguments(256L, 8)]
    [Arguments(3L, 0)]
    [Arguments(5L, 0)]
    [Arguments(6L, 1)]
    [Arguments(7L, 0)]
    [Arguments(9L, 0)]
    [Arguments(10L, 1)]
    [Arguments(12L, 2)]
    [Arguments(0xF0L, 4)]
    public void TrailingZeroCount_Int64(long value, int expected)
    {
        BitOperations.TrailingZeroCount(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0UL, 64)]
    [Arguments(1UL, 0)]
    [Arguments(2UL, 1)]
    [Arguments(4UL, 2)]
    [Arguments(8UL, 3)]
    [Arguments(16UL, 4)]
    [Arguments(32UL, 5)]
    [Arguments(64UL, 6)]
    [Arguments(128UL, 7)]
    [Arguments(256UL, 8)]
    [Arguments(3UL, 0)]
    [Arguments(5UL, 0)]
    [Arguments(6UL, 1)]
    [Arguments(7UL, 0)]
    [Arguments(9UL, 0)]
    [Arguments(10UL, 1)]
    [Arguments(12UL, 2)]
    [Arguments(0xF0UL, 4)]
    public void TrailingZeroCount_UInt64(ulong value, int expected)
    {
        BitOperations.TrailingZeroCount(value).Should().Be(expected);
    }

    [Test]
    [Arguments(0L, 64)]
    [Arguments(1L, 0)]
    [Arguments(2L, 1)]
    public void TrailingZeroCount_NInt(long value, int expected)
    {
        BitOperations.TrailingZeroCount((nint)value).Should().Be(expected);
    }

    [Test]
    [Arguments(0UL, 64)]
    [Arguments(1UL, 0)]
    [Arguments(2UL, 1)]
    public void TrailingZeroCount_NUInt(ulong value, int expected)
    {
        BitOperations.TrailingZeroCount((nuint)value).Should().Be(expected);
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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
