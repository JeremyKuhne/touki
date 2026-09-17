// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Security.Cryptography;

namespace Touki;

[TestClass]
public class CryptographicOperationsTests
{
    [TestMethod]
    public void FixedTimeEquals_BothEmpty_ReturnsTrue()
    {
        CryptographicOperations.FixedTimeEquals(
            [],
            []).Should().BeTrue();
    }

    [TestMethod]
    public void FixedTimeEquals_EqualContent_ReturnsTrue()
    {
        ReadOnlySpan<byte> a = [1, 2, 3, 4, 5, 6, 7, 8];
        ReadOnlySpan<byte> b = [1, 2, 3, 4, 5, 6, 7, 8];
        CryptographicOperations.FixedTimeEquals(a, b).Should().BeTrue();
    }

    [TestMethod]
    public void FixedTimeEquals_DifferentContent_ReturnsFalse()
    {
        ReadOnlySpan<byte> a = [1, 2, 3, 4, 5];
        ReadOnlySpan<byte> b = [1, 2, 3, 4, 6];
        CryptographicOperations.FixedTimeEquals(a, b).Should().BeFalse();
    }

    [TestMethod]
    public void FixedTimeEquals_FirstByteDiffers_ReturnsFalse()
    {
        ReadOnlySpan<byte> a = [9, 2, 3, 4, 5];
        ReadOnlySpan<byte> b = [1, 2, 3, 4, 5];
        CryptographicOperations.FixedTimeEquals(a, b).Should().BeFalse();
    }

    [TestMethod]
    public void FixedTimeEquals_DifferentLengths_ReturnsFalse()
    {
        ReadOnlySpan<byte> a = [1, 2, 3];
        ReadOnlySpan<byte> b = [1, 2, 3, 4];
        CryptographicOperations.FixedTimeEquals(a, b).Should().BeFalse();
    }

    [TestMethod]
    public void FixedTimeEquals_OneEmpty_ReturnsFalse()
    {
        ReadOnlySpan<byte> a = [];
        ReadOnlySpan<byte> b = [1];
        CryptographicOperations.FixedTimeEquals(a, b).Should().BeFalse();
        CryptographicOperations.FixedTimeEquals(b, a).Should().BeFalse();
    }

    [TestMethod]
    public void FixedTimeEquals_LongEqualSpans_ReturnsTrue()
    {
        byte[] data = new byte[256];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)i;
        }

        byte[] copy = (byte[])data.Clone();
        CryptographicOperations.FixedTimeEquals(data, copy).Should().BeTrue();
    }

    [TestMethod]
    public void FixedTimeEquals_LastByteDifferent_ReturnsFalse()
    {
        byte[] a = new byte[256];
        byte[] b = new byte[256];
        b[255] = 1;
        CryptographicOperations.FixedTimeEquals(a, b).Should().BeFalse();
    }
}
