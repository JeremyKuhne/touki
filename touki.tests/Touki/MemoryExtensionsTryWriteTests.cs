// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki;

public class MemoryExtensionsTryWriteTests
{
    [Fact]
    public void TryWrite_LiteralOnly_Writes()
    {
        Span<char> dest = stackalloc char[16];
        bool ok = dest.TryWrite($"hello", out int written);
        ok.Should().BeTrue();
        written.Should().Be(5);
        dest[..written].ToString().Should().Be("hello");
    }

    [Fact]
    public void TryWrite_LiteralAndInt()
    {
        Span<char> dest = stackalloc char[32];
        bool ok = dest.TryWrite($"answer={42}", out int written);
        ok.Should().BeTrue();
        dest[..written].ToString().Should().Be("answer=42");
    }

    [Fact]
    public void TryWrite_DestinationTooSmall_ReturnsFalse()
    {
        Span<char> dest = stackalloc char[3];
        bool ok = dest.TryWrite($"hello", out int written);
        ok.Should().BeFalse();
        written.Should().Be(0);
    }

    [Fact]
    public void TryWrite_FormatString_Hex()
    {
        Span<char> dest = stackalloc char[16];
        bool ok = dest.TryWrite($"{255:X4}", out int written);
        ok.Should().BeTrue();
        dest[..written].ToString().Should().Be("00FF");
    }

    [Fact]
    public void TryWrite_RightAlignment()
    {
        Span<char> dest = stackalloc char[16];
        bool ok = dest.TryWrite($"|{42,5}|", out int written);
        ok.Should().BeTrue();
        dest[..written].ToString().Should().Be("|   42|");
    }

    [Fact]
    public void TryWrite_LeftAlignment()
    {
        Span<char> dest = stackalloc char[16];
        bool ok = dest.TryWrite($"|{42,-5}|", out int written);
        ok.Should().BeTrue();
        dest[..written].ToString().Should().Be("|42   |");
    }

    [Fact]
    public void TryWrite_AlignmentNoPadding_NoFill()
    {
        Span<char> dest = stackalloc char[16];
        bool ok = dest.TryWrite($"|{12345,3}|", out int written);
        ok.Should().BeTrue();
        dest[..written].ToString().Should().Be("|12345|");
    }

    [Fact]
    public void TryWrite_StringValue()
    {
        Span<char> dest = stackalloc char[16];
        string s = "world";
        bool ok = dest.TryWrite($"hi {s}", out int written);
        ok.Should().BeTrue();
        dest[..written].ToString().Should().Be("hi world");
    }

    [Fact]
    public void TryWrite_NullValue_AppendsEmpty()
    {
        Span<char> dest = stackalloc char[16];
        string? s = null;
        bool ok = dest.TryWrite($"[{s}]", out int written);
        ok.Should().BeTrue();
        dest[..written].ToString().Should().Be("[]");
    }

    [Fact]
    public void TryWrite_SpanValue()
    {
        Span<char> dest = stackalloc char[16];
        ReadOnlySpan<char> s = "abc".AsSpan();
        bool ok = dest.TryWrite($">{s}<", out int written);
        ok.Should().BeTrue();
        dest[..written].ToString().Should().Be(">abc<");
    }

    [Fact]
    public void TryWrite_FormatProvider_AffectsCulture()
    {
        Span<char> dest = stackalloc char[32];
        CultureInfo de = CultureInfo.GetCultureInfo("de-DE");
        bool ok = dest.TryWrite(de, $"{1.5}", out int written);
        ok.Should().BeTrue();
        dest[..written].ToString().Should().Be("1,5");
    }

    [Fact]
    public void TryWrite_Empty_Works()
    {
        Span<char> dest = [];
        bool ok = dest.TryWrite($"", out int written);
        ok.Should().BeTrue();
        written.Should().Be(0);
    }

    [Fact]
    public void TryWrite_ExactFit()
    {
        Span<char> dest = stackalloc char[5];
        bool ok = dest.TryWrite($"hello", out int written);
        ok.Should().BeTrue();
        written.Should().Be(5);
        dest.ToString().Should().Be("hello");
    }

    [Fact]
    public void TryWrite_OverflowMidExpression_ReturnsFalse()
    {
        Span<char> dest = stackalloc char[4];
        bool ok = dest.TryWrite($"abc{42}", out int written);
        ok.Should().BeFalse();
        written.Should().Be(0);
    }
}
