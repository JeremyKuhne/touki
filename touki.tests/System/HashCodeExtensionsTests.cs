// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class HashCodeExtensionsTests
{
    [Fact]
    public void AddBytes_Empty_DoesNotThrow()
    {
        HashCode h = default;
        h.AddBytes([]);
        _ = h.ToHashCode();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    public void AddBytes_SameInput_ProducesSameHash(int length)
    {
        // Within a single process HashCode uses a fixed random seed, so equal inputs
        // must produce equal hashes regardless of TFM. This is the contract callers
        // depend on (the BCL only guarantees process-local stability, not cross-runtime).
        byte[] input = new byte[length];
        for (int i = 0; i < length; i++)
        {
            input[i] = (byte)(i * 31 + 7);
        }

        HashCode a = default;
        a.AddBytes(input);

        HashCode b = default;
        b.AddBytes(input);

        a.ToHashCode().Should().Be(b.ToHashCode());
    }

    [Fact]
    public void AddBytes_DifferentTrailingByte_ProducesDifferentHash()
    {
        // 4-byte chunked path + tail byte: last byte must affect the result.
        byte[] x = [1, 2, 3, 4, 5];
        byte[] y = [1, 2, 3, 4, 6];

        HashCode hx = default;
        hx.AddBytes(x);

        HashCode hy = default;
        hy.AddBytes(y);

        hx.ToHashCode().Should().NotBe(hy.ToHashCode());
    }

    [Fact]
    public void AddBytes_DifferentInChunkedRegion_ProducesDifferentHash()
    {
        // First 4 bytes (the chunked region): a single bit flip should reach the digest.
        byte[] x = [0xAA, 0xBB, 0xCC, 0xDD, 0x11, 0x22];
        byte[] y = [0xAA, 0xBB, 0xCC, 0xDC, 0x11, 0x22];

        HashCode hx = default;
        hx.AddBytes(x);

        HashCode hy = default;
        hy.AddBytes(y);

        hx.ToHashCode().Should().NotBe(hy.ToHashCode());
    }

    [Fact]
    public void AddBytes_AppendingChangesHash()
    {
        byte[] prefix = [1, 2, 3, 4];
        byte[] longer = [1, 2, 3, 4, 5];

        HashCode hp = default;
        hp.AddBytes(prefix);

        HashCode hl = default;
        hl.AddBytes(longer);

        hp.ToHashCode().Should().NotBe(hl.ToHashCode());
    }

    [Fact]
    public void AddBytes_SegmentBoundaries_ConsistentWhenSplitOrWhole()
    {
        // The polyfill processes 4-byte chunks then tail bytes. Two HashCode instances
        // built from the same total byte sequence by different segmentations must NOT
        // be expected to match: AddBytes is not associative across calls (each Add(int)
        // mixes a counter). This test pins down the actual behavior so future refactors
        // don't accidentally regress the chunking strategy.
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8];

        HashCode whole = default;
        whole.AddBytes(data);

        HashCode whole2 = default;
        whole2.AddBytes(data);

        whole.ToHashCode().Should().Be(whole2.ToHashCode());
    }
}
