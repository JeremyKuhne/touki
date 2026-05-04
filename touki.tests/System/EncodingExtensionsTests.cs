// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Touki;

public class EncodingExtensionsTests
{
    [Fact]
    public void GetByteCount_Span_MatchesString()
    {
        string s = "héllo \u4e2d\u6587";
        Encoding.UTF8.GetByteCount(s.AsSpan()).Should().Be(Encoding.UTF8.GetByteCount(s));
    }

    [Fact]
    public void GetByteCount_EmptySpan_ReturnsZero()
    {
        Encoding.UTF8.GetByteCount([]).Should().Be(0);
    }

    [Fact]
    public void GetBytes_Span_WritesCorrect()
    {
        string s = "abc\u4e2d";
        byte[] expected = Encoding.UTF8.GetBytes(s);
        Span<byte> dst = stackalloc byte[expected.Length];
        int written = Encoding.UTF8.GetBytes(s.AsSpan(), dst);
        written.Should().Be(expected.Length);
        dst.SequenceEqual(expected).Should().BeTrue();
    }

    [Fact]
    public void GetBytes_EmptySource_ReturnsZero()
    {
        Span<byte> dst = stackalloc byte[4];
        Encoding.UTF8.GetBytes([], dst).Should().Be(0);
    }

    [Fact]
    public void GetCharCount_Span_MatchesArray()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("hello \u4e2d");
        Encoding.UTF8.GetCharCount(bytes.AsSpan()).Should().Be(Encoding.UTF8.GetCharCount(bytes));
    }

    [Fact]
    public void GetChars_Span_WritesCorrect()
    {
        string original = "abc\u4e2d";
        byte[] bytes = Encoding.UTF8.GetBytes(original);
        Span<char> dst = stackalloc char[original.Length];
        int written = Encoding.UTF8.GetChars(bytes.AsSpan(), dst);
        written.Should().Be(original.Length);
        dst.ToString().Should().Be(original);
    }

    [Fact]
    public void GetString_Span_RoundTrips()
    {
        string original = "héllo \u4e2d\u6587";
        byte[] bytes = Encoding.UTF8.GetBytes(original);
        Encoding.UTF8.GetString(bytes.AsSpan()).Should().Be(original);
    }

    [Fact]
    public void GetString_EmptySpan_ReturnsEmpty()
    {
        Encoding.UTF8.GetString([]).Should().BeEmpty();
    }
}
