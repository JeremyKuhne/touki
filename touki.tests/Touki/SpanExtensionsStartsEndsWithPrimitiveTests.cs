// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class SpanExtensionsStartsEndsWithPrimitiveTests
{
    [TestMethod]
    public void StartsWith_Bool_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<bool> span = [true, false];
        span.StartsWith(true).Should().BeTrue();
        span.StartsWith(false).Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_Bool_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<bool> span = [true, false];
        span.EndsWith(false).Should().BeTrue();
        span.EndsWith(true).Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_SByte_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<sbyte> span = [-1, 0, 1];
        span.StartsWith((sbyte)-1).Should().BeTrue();
        span.StartsWith((sbyte)0).Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_SByte_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<sbyte> span = [-1, 0, 1];
        span.EndsWith((sbyte)1).Should().BeTrue();
        span.EndsWith((sbyte)0).Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_Short_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<short> span = [-32768, 0, 32767];
        span.StartsWith((short)-32768).Should().BeTrue();
        span.StartsWith((short)0).Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_Short_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<short> span = [-32768, 0, 32767];
        span.EndsWith((short)32767).Should().BeTrue();
        span.EndsWith((short)0).Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_UShort_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<ushort> span = [1, 2, 65535];
        span.StartsWith((ushort)1).Should().BeTrue();
        span.StartsWith((ushort)2).Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_UShort_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<ushort> span = [1, 2, 65535];
        span.EndsWith((ushort)65535).Should().BeTrue();
        span.EndsWith((ushort)2).Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_UInt_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<uint> span = [1u, 2u, uint.MaxValue];
        span.StartsWith(1u).Should().BeTrue();
        span.StartsWith(2u).Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_UInt_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<uint> span = [1u, 2u, uint.MaxValue];
        span.EndsWith(uint.MaxValue).Should().BeTrue();
        span.EndsWith(2u).Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_Long_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<long> span = [long.MinValue, 0L, long.MaxValue];
        span.StartsWith(long.MinValue).Should().BeTrue();
        span.StartsWith(0L).Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_Long_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<long> span = [long.MinValue, 0L, long.MaxValue];
        span.EndsWith(long.MaxValue).Should().BeTrue();
        span.EndsWith(0L).Should().BeFalse();
    }

    [TestMethod]
    public void StartsWith_ULong_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<ulong> span = [1UL, 2UL, ulong.MaxValue];
        span.StartsWith(1UL).Should().BeTrue();
        span.StartsWith(2UL).Should().BeFalse();
    }

    [TestMethod]
    public void EndsWith_ULong_RespectsPrimitiveFastPath()
    {
        ReadOnlySpan<ulong> span = [1UL, 2UL, ulong.MaxValue];
        span.EndsWith(ulong.MaxValue).Should().BeTrue();
        span.EndsWith(2UL).Should().BeFalse();
    }
}
