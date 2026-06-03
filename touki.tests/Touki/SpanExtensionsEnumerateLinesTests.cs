// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class SpanExtensionsEnumerateLinesTests
{
    private static List<string> CollectLines(ReadOnlySpan<char> source)
    {
        List<string> lines = [];
        foreach (ReadOnlySpan<char> line in source.EnumerateLines())
        {
            lines.Add(line.ToString());
        }

        return lines;
    }

    [TestMethod]
    public void EnumerateLines_Empty_ReturnsSingleEmptyLine()
    {
        CollectLines(default).Should().Equal([""]);
    }

    [TestMethod]
    public void EnumerateLines_NoTerminator_ReturnsWholeSpan()
    {
        CollectLines("hello world".AsSpan()).Should().Equal(["hello world"]);
    }

    [TestMethod]
    public void EnumerateLines_LF_SplitsLines()
    {
        CollectLines("a\nb\nc".AsSpan()).Should().Equal(["a", "b", "c"]);
    }

    [TestMethod]
    public void EnumerateLines_CRLF_SplitsLines()
    {
        CollectLines("a\r\nb\r\nc".AsSpan()).Should().Equal(["a", "b", "c"]);
    }

    [TestMethod]
    public void EnumerateLines_CR_SplitsLines()
    {
        CollectLines("a\rb\rc".AsSpan()).Should().Equal(["a", "b", "c"]);
    }

    [TestMethod]
    public void EnumerateLines_TrailingTerminator_AddsEmptyLine()
    {
        CollectLines("a\n".AsSpan()).Should().Equal(["a", ""]);
    }

    [TestMethod]
    public void EnumerateLines_LeadingTerminator_StartsWithEmpty()
    {
        CollectLines("\nb".AsSpan()).Should().Equal(["", "b"]);
    }

    [TestMethod]
    public void EnumerateLines_DoubleTerminator_HasEmptyLineBetween()
    {
        CollectLines("a\n\nb".AsSpan()).Should().Equal(["a", "", "b"]);
    }

    [TestMethod]
    public void EnumerateLines_AllUnicodeTerminators()
    {
        // Matches BCL MemoryExtensions.EnumerateLines: \v is NOT a line terminator (only \f is).
        CollectLines("a\fb\u0085c\u2028d\u2029e".AsSpan())
            .Should().Equal(["a", "b", "c", "d", "e"]);
    }

    [TestMethod]
    public void EnumerateLines_VerticalTab_NotATerminator()
    {
        CollectLines("a\vb".AsSpan()).Should().Equal(["a\vb"]);
    }

    [TestMethod]
    public void EnumerateLines_MixedCRLFAndLF_HandlesBoth()
    {
        CollectLines("a\r\nb\nc\r\n".AsSpan()).Should().Equal(["a", "b", "c", ""]);
    }

    [TestMethod]
    public void EnumerateLines_CRThenNonLF_OnlyConsumesCR()
    {
        CollectLines("a\rb".AsSpan()).Should().Equal(["a", "b"]);
    }

    [TestMethod]
    public void EnumerateLines_SpanOverload_Works()
    {
        Span<char> span = "a\nb".ToCharArray();
        List<string> lines = [];
        foreach (ReadOnlySpan<char> line in span.EnumerateLines())
        {
            lines.Add(line.ToString());
        }

        lines.Should().Equal(["a", "b"]);
    }

    [TestMethod]
    public void IsWhiteSpace_Empty_ReturnsTrue()
    {
        ReadOnlySpan<char>.Empty.IsWhiteSpace().Should().BeTrue();
    }

    [TestMethod]
    public void IsWhiteSpace_AllSpaces_ReturnsTrue()
    {
        "   \t\r\n ".AsSpan().IsWhiteSpace().Should().BeTrue();
    }

    [TestMethod]
    public void IsWhiteSpace_ContainsNonWhitespace_ReturnsFalse()
    {
        " a ".AsSpan().IsWhiteSpace().Should().BeFalse();
    }

    [TestMethod]
    public void IsWhiteSpace_UnicodeWhitespace_ReturnsTrue()
    {
        "\u00A0\u2003\u3000".AsSpan().IsWhiteSpace().Should().BeTrue();
    }
}
