// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;
using Touki.Io;
using Touki.Text;
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
        await memory.WriteAsync(new ArraySegment<byte>(data, 2, 2), TestContext.Current.CancellationToken);
        memory.Position = 0;

        byte[] readBuffer = new byte[2];
        int read = await memory.ReadAsync(new ArraySegment<byte>(readBuffer), TestContext.Current.CancellationToken);
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

        await memory.WriteAsync(new ArraySegment<byte>(), TestContext.Current.CancellationToken);
        memory.Length.Should().Be(0);

        byte[] data = [4, 5];
        await memory.WriteAsync(new ArraySegment<byte>(data), TestContext.Current.CancellationToken);

        memory.Position = 0;

        long initial = memory.Position;
        int read = await memory.ReadAsync(new ArraySegment<byte>(), TestContext.Current.CancellationToken);
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

#if NET
    [Fact]
    public void WriteFormatted_StringOverload_WritesUtf16Bytes()
    {
        using MemoryStream stream = new();

        stream.WriteFormatted("Hi");

        stream.ToArray().Should().BeEquivalentTo(Encoding.Unicode.GetBytes("Hi"));
    }
#endif

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
