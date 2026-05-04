// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class ConvertExtensionsTests
{
    [Fact]
    public void ToHexString_Empty_ReturnsEmpty()
    {
        Convert.ToHexString(ReadOnlySpan<byte>.Empty).Should().BeEmpty();
    }

    [Fact]
    public void ToHexString_Bytes_Uppercase()
    {
        ReadOnlySpan<byte> bytes = [0x00, 0x0F, 0x10, 0xFF, 0xAB, 0xCD];
        Convert.ToHexString(bytes).Should().Be("000F10FFABCD");
    }

    [Fact]
    public void ToHexString_AllByteValues_RoundTrip()
    {
        byte[] all = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            all[i] = (byte)i;
        }

        string hex = Convert.ToHexString(all);
        hex.Length.Should().Be(512);
        byte[] back = Convert.FromHexString(hex);
        back.Should().Equal(all);
    }

    [Fact]
    public void ToHexString_Array_OffsetLength()
    {
        byte[] bytes = [0xAA, 0x01, 0x02, 0xBB];
        Convert.ToHexString(bytes, 1, 2).Should().Be("0102");
    }

    [Fact]
    public void FromHexString_Empty_ReturnsEmptyArray()
    {
        Convert.FromHexString(ReadOnlySpan<char>.Empty).Should().BeEmpty();
    }

    [Fact]
    public void FromHexString_MixedCase_Decodes()
    {
        Convert.FromHexString("aBcDeF01".AsSpan()).Should().Equal([0xAB, 0xCD, 0xEF, 0x01]);
    }

    [Fact]
    public void FromHexString_OddLength_Throws()
    {
        Action a = () => Convert.FromHexString("abc".AsSpan());
        a.Should().Throw<FormatException>();
    }

    [Fact]
    public void FromHexString_InvalidChar_Throws()
    {
        Action a = () => Convert.FromHexString("zz".AsSpan());
        a.Should().Throw<FormatException>();
    }

    [Fact]
    public void FromHexString_String_Decodes()
    {
        Convert.FromHexString("DEADBEEF").Should().Equal([0xDE, 0xAD, 0xBE, 0xEF]);
    }
}
