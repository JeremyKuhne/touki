// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;
using Touki.Text;

namespace Touki.Io;

[TestClass]
public class TextWriterExtensionsTests
{
    [TestMethod]
    public void Write_ReadOnlySpan_AppendsToStringWriter()
    {
        System.IO.StringWriter writer = new();
        ReadOnlySpan<char> span = "Hello".AsSpan();

        writer.Write(span);

        writer.ToString().Should().Be("Hello");
    }

    [TestMethod]
    public void Write_ReadOnlySpan_Empty_DoesNothing()
    {
        System.IO.StringWriter writer = new();

        writer.Write([]);

        writer.ToString().Should().BeEmpty();
    }

    [TestMethod]
    public void WriteLine_ReadOnlySpan_AppendsAndAddsNewLine()
    {
        System.IO.StringWriter writer = new();
        ReadOnlySpan<char> span = "Hello".AsSpan();

        writer.WriteLine(span);

        writer.ToString().Should().Be($"Hello{Environment.NewLine}");
    }

    [TestMethod]
    public void WriteLine_ReadOnlySpan_Empty_WritesOnlyNewLine()
    {
        System.IO.StringWriter writer = new();

        writer.WriteLine([]);

        writer.ToString().Should().Be(Environment.NewLine);
    }

    [TestMethod]
    public void Write_StringSegment_WritesSegmentContent()
    {
        System.IO.StringWriter writer = new();
        StringSegment segment = new("Hello World", 6, 5);

        writer.Write(segment.AsSpan());

        writer.ToString().Should().Be("World");
    }

    [TestMethod]
    public void WriteLine_StringSegment_WritesSegmentContentAndNewLine()
    {
        System.IO.StringWriter writer = new();
        StringSegment segment = new("Hello World", 0, 5);

        writer.WriteLine(segment.AsSpan());

        writer.ToString().Should().Be($"Hello{Environment.NewLine}");
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void WriteFormatted_RentedBuilder_DisposesBuilder()
    {
        using System.IO.StringWriter writer = new();
        ValueStringBuilder builder = new(initialCapacity: 32);
        try
        {
            builder.Append("Hello");

            writer.WriteFormatted(ref builder);

            builder.Capacity.Should().Be(0);
            writer.ToString().Should().Be("Hello");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void WriteFormatted_WriterThrows_DisposesBuilder()
    {
        using ThrowingTextWriter writer = new();
        ValueStringBuilder builder = new(initialCapacity: 32);
        InvalidOperationException? exception = null;
        try
        {
            builder.Append("Hello");

            try
            {
                writer.WriteFormatted(ref builder);
            }
            catch (InvalidOperationException caught)
            {
                exception = caught;
            }

            builder.Capacity.Should().Be(0);
            exception.Should().NotBeNull();
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void WriteFormatted_CustomWriter_UsesVirtualStringOverload()
    {
        using RecordingTextWriter writer = new();
        int value = 42;

        writer.WriteFormatted($"Value: {value}");

        writer.StringOverloadCalled.Should().BeTrue();
        writer.Captured.Should().Be("Value: 42");
    }

    [TestMethod]
    public void WriteFormatted_DisposedStringWriterWithContent_Throws()
    {
        System.IO.StringWriter writer = new();
        try
        {
            writer.Dispose();
            int value = 42;
            ObjectDisposedException? exception = null;

            try
            {
                writer.WriteFormatted($"Value: {value}");
            }
            catch (ObjectDisposedException caught)
            {
                exception = caught;
            }

            exception.Should().NotBeNull();
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteFormatted_DisposedStringWriterWithEmptyContent_Throws()
    {
        System.IO.StringWriter writer = new();
        try
        {
            writer.Dispose();
            string value = string.Empty;
            ObjectDisposedException? exception = null;

            try
            {
                writer.WriteFormatted($"{value}");
            }
            catch (ObjectDisposedException caught)
            {
                exception = caught;
            }

            exception.Should().NotBeNull();
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteFormatted_DisposedStreamWriterWithContent_MatchesStringOverload()
    {
        (Type? ExceptionType, long StreamLength) expected =
            WriteToDisposedStreamWriter("Value: 42", formatted: false);
        (Type? ExceptionType, long StreamLength) actual =
            WriteToDisposedStreamWriter("Value: 42", formatted: true);

        actual.Should().Be(expected);
    }

    [TestMethod]
    public void WriteFormatted_DisposedStreamWriterWithEmptyContent_MatchesStringOverload()
    {
        (Type? ExceptionType, long StreamLength) expected =
            WriteToDisposedStreamWriter(string.Empty, formatted: false);
        (Type? ExceptionType, long StreamLength) actual =
            WriteToDisposedStreamWriter(string.Empty, formatted: true);

        actual.Should().Be(expected);
    }

    [TestMethod]
    public void WriteFormatted_StreamWriterAutoFlush_MatchesStringOverload()
    {
        (Type? ExceptionType, int FlushCount, string Content) expected =
            WriteToAutoFlushStreamWriter("Value: 42", formatted: false, throwOnFlush: false);
        (Type? ExceptionType, int FlushCount, string Content) actual =
            WriteToAutoFlushStreamWriter("Value: 42", formatted: true, throwOnFlush: false);

        actual.Should().Be(expected);
    }

    [TestMethod]
    public void WriteFormatted_StreamWriterThrowingFlush_MatchesStringOverload()
    {
        (Type? ExceptionType, int FlushCount, string Content) expected =
            WriteToAutoFlushStreamWriter("Value: 42", formatted: false, throwOnFlush: true);
        (Type? ExceptionType, int FlushCount, string Content) actual =
            WriteToAutoFlushStreamWriter("Value: 42", formatted: true, throwOnFlush: true);

        actual.Should().Be(expected);
    }

    private static (Type? ExceptionType, long StreamLength) WriteToDisposedStreamWriter(
        string value,
        bool formatted)
    {
        MemoryStream stream = new();
        System.IO.StreamWriter writer = new(stream, Encoding.UTF8, 1024, leaveOpen: true);
        try
        {
            writer.Dispose();
            Exception? exception = null;

            try
            {
                if (formatted)
                {
                    writer.WriteFormatted($"{value}");
                }
                else
                {
                    writer.Write(value);
                }
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            return (exception?.GetType(), stream.Length);
        }
        finally
        {
            writer.Dispose();
            stream.Dispose();
        }
    }

    private static (Type? ExceptionType, int FlushCount, string Content) WriteToAutoFlushStreamWriter(
        string value,
        bool formatted,
        bool throwOnFlush)
    {
        TrackingMemoryStream stream = new();
        System.IO.StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            1024,
            leaveOpen: true);
        try
        {
            writer.AutoFlush = true;
            stream.ResetFlushCount();
            stream.ThrowOnFlush = throwOnFlush;
            Exception? exception = null;

            try
            {
                if (formatted)
                {
                    writer.WriteFormatted($"{value}");
                }
                else
                {
                    writer.Write(value);
                }
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            return (
                exception?.GetType(),
                stream.FlushCount,
                Encoding.UTF8.GetString(stream.ToArray()));
        }
        finally
        {
            stream.ThrowOnFlush = false;
            writer.Dispose();
            stream.Dispose();
        }
    }

#if NET
    [TestMethod]
    public void WriteFormatted_StringOverload_WritesLiteralWithoutBuilder()
    {
        System.IO.StringWriter writer = new();

        writer.WriteFormatted("Hello");

        writer.ToString().Should().Be("Hello");
    }

    [TestMethod]
    public void WriteFormatted_StringOverload_CustomWriterUsesVirtualStringOverload()
    {
        using RecordingTextWriter writer = new();

        writer.WriteFormatted("Hello");

        writer.StringOverloadCalled.Should().BeTrue();
        writer.Captured.Should().Be("Hello");
    }
#endif

    [TestMethod]
    public void Write_StringSegmentOverload_WritesSegmentContent()
    {
        System.IO.StringWriter writer = new();
        StringSegment segment = new("Hello World", 6, 5);

        TextWriterExtensions.Write(writer, segment);

        writer.ToString().Should().Be("World");
    }

    [TestMethod]
    public void WriteLine_StringSegmentOverload_WritesSegmentContentAndNewLine()
    {
        System.IO.StringWriter writer = new();
        StringSegment segment = new("Hello World", 0, 5);

        TextWriterExtensions.WriteLine(writer, segment);

        writer.ToString().Should().Be($"Hello{Environment.NewLine}");
    }

    private sealed class ThrowingTextWriter : System.IO.TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string? value) => throw new InvalidOperationException();

#if NET
        public override void Write(ReadOnlySpan<char> buffer) => throw new InvalidOperationException();
#else
        public override void Write(char[] buffer, int index, int count) => throw new InvalidOperationException();
#endif
    }

    private sealed class RecordingTextWriter : System.IO.TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public bool StringOverloadCalled { get; private set; }

        public string? Captured { get; private set; }

        public override void Write(string? value)
        {
            StringOverloadCalled = true;
            Captured = value;
        }

#if NET
        public override void Write(ReadOnlySpan<char> buffer) =>
            throw new InvalidOperationException("The span overload should not be used for a custom writer.");
#else
        public override void Write(char[] buffer, int index, int count) =>
            throw new InvalidOperationException("The buffer overload should not be used for a custom writer.");
#endif
    }

    private sealed class TrackingMemoryStream : MemoryStream
    {
        public int FlushCount { get; private set; }

        public bool ThrowOnFlush { get; set; }

        public override void Flush()
        {
            FlushCount++;
            if (ThrowOnFlush)
            {
                throw new InvalidOperationException();
            }

            base.Flush();
        }

        public void ResetFlushCount() => FlushCount = 0;
    }
}
