// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;
using Touki.Text;

namespace Touki.Io;

public class TextWriterExtensionsTests
{
    [Fact]
    public void Write_ReadOnlySpan_AppendsToStringWriter()
    {
        System.IO.StringWriter writer = new();
        ReadOnlySpan<char> span = "Hello".AsSpan();

        writer.Write(span);

        writer.ToString().Should().Be("Hello");
    }

    [Fact]
    public void Write_ReadOnlySpan_Empty_DoesNothing()
    {
        System.IO.StringWriter writer = new();

        writer.Write([]);

        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void WriteLine_ReadOnlySpan_AppendsAndAddsNewLine()
    {
        System.IO.StringWriter writer = new();
        ReadOnlySpan<char> span = "Hello".AsSpan();

        writer.WriteLine(span);

        writer.ToString().Should().Be($"Hello{Environment.NewLine}");
    }

    [Fact]
    public void WriteLine_ReadOnlySpan_Empty_WritesOnlyNewLine()
    {
        System.IO.StringWriter writer = new();

        writer.WriteLine([]);

        writer.ToString().Should().Be(Environment.NewLine);
    }

    [Fact]
    public void Write_StringSegment_WritesSegmentContent()
    {
        System.IO.StringWriter writer = new();
        StringSegment segment = new("Hello World", 6, 5);

        writer.Write(segment.AsSpan());

        writer.ToString().Should().Be("World");
    }

    [Fact]
    public void WriteLine_StringSegment_WritesSegmentContentAndNewLine()
    {
        System.IO.StringWriter writer = new();
        StringSegment segment = new("Hello World", 0, 5);

        writer.WriteLine(segment.AsSpan());

        writer.ToString().Should().Be($"Hello{Environment.NewLine}");
    }

    [Fact]
    public void WriteFormatted_InterpolatedString_AppendsToStreamWriter()
    {
        using MemoryStream stream = new();
        using System.IO.StreamWriter writer = new(stream, Encoding.UTF8, 1024, leaveOpen: true);

        string name = "Touki";
        int version = 42;

        writer.WriteFormatted($"Library: {name}, Version: {version}");
        writer.Flush();

        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8);
        string result = reader.ReadToEnd();

        result.Should().Be("Library: Touki, Version: 42");
    }

    [Fact]
    public void WriteFormatted_EmptyBuilder_WritesNothing()
    {
        using MemoryStream stream = new();
        using System.IO.StreamWriter writer = new(stream, Encoding.UTF8, 1024, leaveOpen: true);
        writer.Flush();
        long length = stream.Length;

        ValueStringBuilder builder = new();
        writer.WriteFormatted(ref builder);
        writer.Flush();

        stream.Length.Should().Be(length);
    }

#if NET
    [Fact]
    public void WriteFormatted_StringOverload_WritesLiteralWithoutBuilder()
    {
        System.IO.StringWriter writer = new();

        writer.WriteFormatted("Hello");

        writer.ToString().Should().Be("Hello");
    }
#endif
}
