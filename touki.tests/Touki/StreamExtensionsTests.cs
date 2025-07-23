// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;
using Touki.IO;
#if NETFRAMEWORK
using System.IO;
#endif

namespace Touki;

public class StreamExtensionsTests
{
    [Fact]
    public void Read_Write_ArraySegment()
    {
        using MemoryStream memory = new();
        byte[] data = [1, 2, 3, 4, 5];

        // Write using ArraySegment
        memory.Write(new ArraySegment<byte>(data, 1, 3));
        memory.Position = 0;

        byte[] readBuffer = new byte[3];
        int read = memory.Read(new ArraySegment<byte>(readBuffer));
        read.Should().Be(3);
        readBuffer.Should().BeEquivalentTo([2, 3, 4]);
    }

    [Fact]
    public async Task ReadAsync_WriteAsync_ArraySegment()
    {
        using MemoryStream memory = new();
        byte[] data = [6, 7, 8, 9, 10];

        // Write asynchronously using ArraySegment
        await memory.WriteAsync(new ArraySegment<byte>(data, 2, 2));
        memory.Position = 0;

        byte[] readBuffer = new byte[2];
        int read = await memory.ReadAsync(new ArraySegment<byte>(readBuffer));
        read.Should().Be(2);
        readBuffer.Should().BeEquivalentTo([8, 9]);
    }

    [Fact]
    public void DefaultSegment_IsIgnored()
    {
        using MemoryStream memory = new();

        memory.Write(default);
        memory.Length.Should().Be(0);

        byte[] data = [1, 2, 3];
        memory.Write(data, 0, data.Length);
        memory.Position = 0;

        long initial = memory.Position;
        int read = memory.Read(new ArraySegment<byte>());
        read.Should().Be(0);
        memory.Position.Should().Be(initial);
    }

    [Fact]
    public async Task DefaultSegmentAsync_IsIgnored()
    {
        using MemoryStream memory = new();

        await memory.WriteAsync(new ArraySegment<byte>());
        memory.Length.Should().Be(0);

        byte[] data = [4, 5];
        await memory.WriteAsync(new ArraySegment<byte>(data));

        memory.Position = 0;

        long initial = memory.Position;
        int read = await memory.ReadAsync(new ArraySegment<byte>());
        read.Should().Be(0);
        memory.Position.Should().Be(initial);
    }

    [Fact]
    public void WriteFormatted_SimpleString_WritesToMemoryStream()
    {
        using MemoryStream stream = new();
        stream.WriteFormatted($"Hello World!");
        stream.Position = 0;

        using StreamReader reader = new(stream, Encoding.Unicode);
        string result = reader.ReadToEnd();
        result.Should().Be("Hello World!");
    }

    [Fact]
    public void WriteFormatted_EmptyBuilder_WritesNothing()
    {
        using MemoryStream stream = new();
        ValueStringBuilder builder = new();
        stream.WriteFormatted(ref builder);

        stream.Length.Should().Be(0);
    }

    [Fact]
    public void WriteFormatted_InterpolatedString_WritesToMemoryStream()
    {
        using MemoryStream stream = new();
        string name = "Touki";
        int version = 42;

        stream.WriteFormatted($"Library: {name}, Version: {version}");
        stream.Position = 0;

        using StreamReader reader = new(stream, Encoding.Unicode);
        string result = reader.ReadToEnd();
        result.Should().Be("Library: Touki, Version: 42");
    }

    [Fact]
    public void WriteFormatted_MultipleWrites_AppendToStream()
    {
        using MemoryStream stream = new();

        stream.WriteFormatted($"First part. ");
        stream.WriteFormatted($"Second part.");
        stream.Position = 0;

        using StreamReader reader = new(stream, Encoding.Unicode);
        string result = reader.ReadToEnd();
        result.Should().Be("First part. Second part.");
    }

    [Fact]
    public void WriteFormatted_StreamWriterUnicode_WritesCorrectly()
    {
        using MemoryStream stream = new();
        StreamWriter writer = new(stream, Encoding.Unicode);

#pragma warning disable IDE0082 // 'typeof' can be converted to 'nameof'
        writer.WriteFormatted($"Hello from {typeof(StreamWriter).Name}!");
#pragma warning restore IDE0082
        writer.Flush();

        stream.Position = 0;
        StreamReader reader = new(stream, Encoding.Unicode);
        string result = reader.ReadToEnd();

        result.Should().Be("Hello from StreamWriter!");
    }

    [Fact]
    public void WriteFormatted_StreamWriterUTF8_WritesCorrectly()
    {
        using MemoryStream stream = new();
        StreamWriter writer = new(stream, Encoding.UTF8);

        writer.WriteFormatted($"UTF-{8} Text");
        writer.Flush();

        stream.Position = 0;
        StreamReader reader = new(stream, Encoding.UTF8);
        string result = reader.ReadToEnd();

        result.Should().Be("UTF-8 Text");
    }

    [Fact]
    public void WriteFormatted_StreamWriterInterpolatedValues_WritesCorrectly()
    {
        using MemoryStream stream = new();
        StreamWriter writer = new(stream, Encoding.UTF8);

        string name = "StreamWriter";
        int value = 123;
        double pi = 3.14159;

        writer.WriteFormatted($"Name: {name}, Value: {value}, Pi: {pi:F2}");
        writer.Flush();

        stream.Position = 0;
        StreamReader reader = new(stream, Encoding.UTF8);
        string result = reader.ReadToEnd();

        result.Should().Be("Name: StreamWriter, Value: 123, Pi: 3.14");
    }

    [Fact]
    public void WriteFormatted_StreamWriterMultipleWrites_AppendCorrectly()
    {
        using MemoryStream stream = new();
        StreamWriter writer = new(stream, Encoding.Unicode);

        writer.WriteFormatted($"Part one. ");
        writer.WriteFormatted($"Part two.");
        writer.Flush();

        stream.Position = 0;
        StreamReader reader = new(stream, Encoding.Unicode);
        string result = reader.ReadToEnd();

        result.Should().Be("Part one. Part two.");
    }

    [Fact]
    public void WriteFormatted_StreamWriterEmptyBuilder_WritesNothing()
    {
        using MemoryStream stream = new();
        StreamWriter writer = new(stream, Encoding.UTF8);
        writer.Flush();
        long length = stream.Length;

        ValueStringBuilder builder = new();
        writer.WriteFormatted(ref builder);
        writer.Flush();

        stream.Length.Should().Be(length);
    }

    [Fact]
    public void Write_ReadOnlySpan_WritesToTextWriter()
    {
        StringWriter writer = new();
        ReadOnlySpan<char> span = "Hello Span World".AsSpan();

        writer.Write(span);

        string result = writer.ToString();
        result.Should().Be("Hello Span World");
    }

    [Fact]
    public void Write_EmptyReadOnlySpan_WritesNothing()
    {
        StringWriter writer = new();
        ReadOnlySpan<char> span = [];

        writer.Write(span);

        string result = writer.ToString();
        result.Should().BeEmpty();
    }

    [Fact]
    public void WriteLine_ReadOnlySpan_WritesToTextWriterWithNewLine()
    {
        StringWriter writer = new();
        ReadOnlySpan<char> span = "Hello Span Line".AsSpan();

        writer.WriteLine(span);

        string result = writer.ToString();
        result.Should().Be($"Hello Span Line{Environment.NewLine}");
    }

    [Fact]
    public void WriteLine_EmptyReadOnlySpan_WritesOnlyNewLine()
    {
        StringWriter writer = new();
        ReadOnlySpan<char> span = [];

        writer.WriteLine(span);

        string result = writer.ToString();
        result.Should().Be(Environment.NewLine);
    }
}
