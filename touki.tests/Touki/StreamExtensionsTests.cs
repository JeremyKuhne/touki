// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;
using Touki.Text;

namespace Touki;

[TestClass]
public partial class StreamExtensionsTests
{
    [TestMethod]
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

    [TestMethod]
    public async Task ReadAsync_WriteAsync_ArraySegment()
    {
        using MemoryStream memory = new();
        byte[] data = [6, 7, 8, 9, 10];

        // Write asynchronously using ArraySegment
        await memory.WriteAsync(new ArraySegment<byte>(data, 2, 2), CancellationToken.None).ConfigureAwait(false);
        memory.Position = 0;

        byte[] readBuffer = new byte[2];
        int read = await memory.ReadAsync(new ArraySegment<byte>(readBuffer), CancellationToken.None).ConfigureAwait(false);
        read.Should().Be(2);
        readBuffer.Should().BeEquivalentTo([8, 9]);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task DefaultSegmentAsync_IsIgnored()
    {
        using MemoryStream memory = new();

        await memory.WriteAsync(new ArraySegment<byte>(), CancellationToken.None).ConfigureAwait(false);
        memory.Length.Should().Be(0);

        byte[] data = [4, 5];
        await memory.WriteAsync(new ArraySegment<byte>(data), CancellationToken.None).ConfigureAwait(false);

        memory.Position = 0;

        long initial = memory.Position;
        int read = await memory.ReadAsync(new ArraySegment<byte>(), CancellationToken.None).ConfigureAwait(false);
        read.Should().Be(0);
        memory.Position.Should().Be(initial);
    }

    [TestMethod]
    public void TryReadExactly_SpanWithPartialReads_FillsBufferAndReturnsTrue()
    {
        using ChunkedReadStream stream = new([1, 2, 3, 4], maximumReadSize: 1);
        byte[] buffer = [0, 0, 0, 0];

        bool result = stream.TryReadExactly(buffer.AsSpan());

        result.Should().BeTrue();
        buffer.Should().Equal(1, 2, 3, 4);
        stream.ReadCallCount.Should().Be(4);
    }

    [TestMethod]
    public void TryReadExactly_SpanEndsEarly_ReturnsFalseWithReadPrefix()
    {
        using ChunkedReadStream stream = new([1, 2], maximumReadSize: 1);
        byte[] buffer = [9, 9, 9];

        bool result = stream.TryReadExactly(buffer.AsSpan());

        result.Should().BeFalse();
        buffer.Should().Equal(1, 2, 9);
        stream.ReadCallCount.Should().Be(3);
    }

    [TestMethod]
    public void TryReadExactly_EmptySpan_ReturnsTrueWithoutReading()
    {
        using ChunkedReadStream stream = new([], maximumReadSize: 1);
        Span<byte> buffer = [];

        bool result = stream.TryReadExactly(buffer);

        result.Should().BeTrue();
        stream.ReadCallCount.Should().Be(0);
    }

    [TestMethod]
    public void TryReadExactly_ArrayRangeWithPartialReads_FillsRangeAndReturnsTrue()
    {
        using ChunkedReadStream stream = new([1, 2, 3], maximumReadSize: 1);
        byte[] buffer = [9, 9, 9, 9, 9];

        bool result = stream.TryReadExactly(buffer, offset: 1, count: 3);

        result.Should().BeTrue();
        buffer.Should().Equal(9, 1, 2, 3, 9);
    }

    [TestMethod]
    public void TryReadExactly_ArrayRangeEndsEarly_ReturnsFalseWithReadPrefix()
    {
        using ChunkedReadStream stream = new([1, 2], maximumReadSize: 1);
        byte[] buffer = [9, 9, 9, 9, 9];

        bool result = stream.TryReadExactly(buffer, offset: 1, count: 3);

        result.Should().BeFalse();
        buffer.Should().Equal(9, 1, 2, 9, 9);
    }

    [TestMethod]
    public void TryReadExactly_NullArray_ThrowsArgumentNullException()
    {
        using ChunkedReadStream stream = new([], maximumReadSize: 1);

        Action action = () => stream.TryReadExactly(null!, offset: 0, count: 0);

        action.Should().Throw<ArgumentNullException>().WithParameterName("buffer");
    }

    [TestMethod]
    public void TryReadExactly_NullStream_ThrowsArgumentNullException()
    {
        System.IO.Stream stream = null!;
        byte[] buffer = [];

        Action action = () => stream.TryReadExactly(buffer, offset: 0, count: 0);

        action.Should().Throw<ArgumentNullException>().WithParameterName("stream");
    }

    [TestMethod]
    [DataRow(-1, 0, "offset")]
    [DataRow(0, -1, "count")]
    [DataRow(2, 2, "count")]
    [DataRow(4, 0, "count")]
    public void TryReadExactly_InvalidArrayRange_ThrowsArgumentOutOfRangeException(
        int offset,
        int count,
        string parameterName)
    {
        using ChunkedReadStream stream = new([], maximumReadSize: 1);
        byte[] buffer = new byte[3];

        Action action = () => stream.TryReadExactly(buffer, offset, count);

        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameterName);
    }

    [TestMethod]
    public void TryReadExactly_ReadThrows_PropagatesException()
    {
        InvalidOperationException expected = new();
        using ThrowingReadStream stream = new(expected);
        byte[] buffer = [0];

        Action action = () => stream.TryReadExactly(buffer, offset: 0, count: 1);

        action.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expected);
    }

    [TestMethod]
    public void Read_ByteSpanWithPartialRead_CopiesOnlyBytesRead()
    {
        using ChunkedReadStream stream = new([1, 2, 3], maximumReadSize: 1);
        byte[] buffer = [9, 9, 9];

        int read = stream.Read(buffer.AsSpan());

        read.Should().Be(1);
        buffer.Should().Equal(1, 9, 9);
    }

    [TestMethod]
    public void Read_ByteSpanDerivedMemoryStream_UsesArrayOverride()
    {
        byte[] source = [1, 2, 3];
        using TrackingMemoryStream stream = new(source);
        byte[] buffer = [0, 0, 0];

        int read = stream.Read(buffer.AsSpan());

        read.Should().Be(3);
        buffer.Should().Equal(source);
        stream.ArrayReadCallCount.Should().Be(1);
    }

    [TestMethod]
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
    [TestMethod]
    public void WriteFormatted_StringOverload_WritesUtf16Bytes()
    {
        using MemoryStream stream = new();

        stream.WriteFormatted("Hi");

        stream.ToArray().Should().BeEquivalentTo(Encoding.Unicode.GetBytes("Hi"));
    }
#endif

    [TestMethod]
    public void WriteFormatted_EmptyBuilder_WritesNothing()
    {
        using MemoryStream stream = new();
        ValueStringBuilder builder = new();
        stream.WriteFormatted(ref builder);

        stream.Length.Should().Be(0);
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void Write_ReadOnlySpan_WritesToTextWriter()
    {
        StringWriter writer = new();
        ReadOnlySpan<char> span = "Hello Span World".AsSpan();

        writer.Write(span);

        string result = writer.ToString();
        result.Should().Be("Hello Span World");
    }

    [TestMethod]
    public void Write_EmptyReadOnlySpan_WritesNothing()
    {
        StringWriter writer = new();
        ReadOnlySpan<char> span = [];

        writer.Write(span);

        string result = writer.ToString();
        result.Should().BeEmpty();
    }

    [TestMethod]
    public void WriteLine_ReadOnlySpan_WritesToTextWriterWithNewLine()
    {
        StringWriter writer = new();
        ReadOnlySpan<char> span = "Hello Span Line".AsSpan();

        writer.WriteLine(span);

        string result = writer.ToString();
        result.Should().Be($"Hello Span Line{Environment.NewLine}");
    }

    [TestMethod]
    public void WriteLine_EmptyReadOnlySpan_WritesOnlyNewLine()
    {
        StringWriter writer = new();
        ReadOnlySpan<char> span = [];

        writer.WriteLine(span);

        string result = writer.ToString();
        result.Should().Be(Environment.NewLine);
    }

    [TestMethod]
    public void Write_ByteSpan_AppendsToEmptyStream()
    {
        using MemoryStream memory = new();
        ReadOnlySpan<byte> data = [1, 2, 3, 4, 5];

        memory.Write(data);

        memory.Position.Should().Be(5);
        memory.Length.Should().Be(5);
        byte[] expected = [1, 2, 3, 4, 5];
        memory.ToArray().Should().Equal(expected);
    }

    [TestMethod]
    public void Write_ByteSpan_OverwritesWithinLength()
    {
        using MemoryStream memory = new();
        memory.Write([1, 2, 3, 4, 5], 0, 5);
        memory.Position = 1;

        ReadOnlySpan<byte> data = [9, 9];
        memory.Write(data);

        memory.Position.Should().Be(3);
        memory.Length.Should().Be(5);
        byte[] expected = [1, 9, 9, 4, 5];
        memory.ToArray().Should().Equal(expected);
    }

    [TestMethod]
    public void Write_ByteSpan_OverwritesAndExtends()
    {
        using MemoryStream memory = new();
        memory.Write([1, 2, 3], 0, 3);
        memory.Position = 2;

        ReadOnlySpan<byte> data = [7, 8, 9];
        memory.Write(data);

        memory.Position.Should().Be(5);
        memory.Length.Should().Be(5);
        byte[] expected = [1, 2, 7, 8, 9];
        memory.ToArray().Should().Equal(expected);
    }

    [TestMethod]
    public void Write_ByteSpan_GrowsBeyondCapacity()
    {
        using MemoryStream memory = new(2);
        ReadOnlySpan<byte> data = [1, 2, 3, 4, 5, 6, 7, 8];

        memory.Write(data);

        memory.Length.Should().Be(8);
        byte[] expected = [1, 2, 3, 4, 5, 6, 7, 8];
        memory.ToArray().Should().Equal(expected);
    }

    [TestMethod]
    public void Write_ByteSpan_Empty_DoesNothing()
    {
        using MemoryStream memory = new();
        memory.Write([1, 2, 3], 0, 3);
        memory.Position = 3;

        ReadOnlySpan<byte> empty = [];
        memory.Write(empty);

        memory.Position.Should().Be(3);
        memory.Length.Should().Be(3);
    }

    [TestMethod]
    public void Write_ByteSpan_NonExpandableBackedStream_UsesFallback()
    {
        // A MemoryStream over a caller-owned array is not publicly visible, so TryGetBuffer fails and
        // the rent/copy fallback path handles the write.
        byte[] backing = new byte[5];
        using MemoryStream memory = new(backing);
        ReadOnlySpan<byte> data = [1, 2, 3, 4, 5];

        memory.Write(data);

        memory.Position.Should().Be(5);
        byte[] expected = [1, 2, 3, 4, 5];
        backing.Should().Equal(expected);
    }

    [TestMethod]
    public void Write_ByteSpanDerivedMemoryStream_UsesArrayOverride()
    {
        byte[] backing = new byte[3];
        using TrackingMemoryStream stream = new(backing);
        ReadOnlySpan<byte> data = [1, 2, 3];

        stream.Write(data);

        backing.Should().Equal(1, 2, 3);
        stream.ArrayWriteCallCount.Should().Be(1);
    }

#if NETFRAMEWORK
    [TestMethod]
    public void Write_LegacyStaticEntryPoint_ForwardsToSystemIOPolyfill()
    {
        using MemoryStream stream = new();
        ReadOnlySpan<byte> buffer = [1, 2, 3];

        Touki.Io.StreamExtensions.Write(stream, buffer);

        stream.ToArray().Should().Equal(1, 2, 3);
    }
#endif

    private sealed class ChunkedReadStream(byte[] source, int maximumReadSize) : System.IO.Stream
    {
        private int _position;

        public int ReadCallCount { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => source.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCallCount++;
            int read = Math.Min(Math.Min(count, maximumReadSize), source.Length - _position);
            source.AsSpan(_position, read).CopyTo(buffer.AsSpan(offset, read));
            _position += read;
            return read;
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowingReadStream(Exception exception) : System.IO.Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => 0;

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw exception;

        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class TrackingMemoryStream(byte[] buffer)
        : MemoryStream(buffer, index: 0, count: buffer.Length, writable: true, publiclyVisible: true)
    {
        public int ArrayReadCallCount { get; private set; }

        public int ArrayWriteCallCount { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArrayReadCallCount++;
            return base.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArrayWriteCallCount++;
            base.Write(buffer, offset, count);
        }
    }
}
