// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public partial class StreamExtensionsTests
{
    [TestMethod]
    public void Read_ByteSpanWhenStreamReturnsTooManyBytes_ThrowsIOException()
    {
        using InvalidReadCountStream stream = new();
        Span<byte> buffer = stackalloc byte[1];
        System.IO.IOException? exception = null;

        try
        {
#pragma warning disable CA2022 // Direct Read call exercises the Framework span polyfill's return-value guard.
            stream.Read(buffer);
#pragma warning restore CA2022
        }
        catch (System.IO.IOException caught)
        {
            exception = caught;
        }

        exception.Should().NotBeNull();
    }

    [TestMethod]
    public void Read_EmptyByteSpanOnDisposedMemoryStream_ThrowsObjectDisposedException()
    {
        MemoryStream stream = new();
        stream.Dispose();
        Span<byte> buffer = [];
        ObjectDisposedException? exception = null;

        try
        {
#pragma warning disable CA2022 // Direct Read call verifies the Stream.Read span contract.
            stream.Read(buffer);
#pragma warning restore CA2022
        }
        catch (ObjectDisposedException caught)
        {
            exception = caught;
        }

        exception.Should().NotBeNull();
    }

    [TestMethod]
    public void Write_EmptyByteSpanOnDisposedMemoryStream_ThrowsObjectDisposedException()
    {
        MemoryStream stream = new();
        stream.Dispose();
        ReadOnlySpan<byte> buffer = [];
        ObjectDisposedException? exception = null;

        try
        {
            stream.Write(buffer);
        }
        catch (ObjectDisposedException caught)
        {
            exception = caught;
        }

        exception.Should().NotBeNull();
    }

    [TestMethod]
    public void TryReadExactly_EmptyBuffersOnDisposedStream_ReturnsTrue()
    {
        MemoryStream stream = new();
        stream.Dispose();
        Span<byte> span = [];
        byte[] array = [];

        bool spanResult = stream.TryReadExactly(span);
        bool arrayResult = stream.TryReadExactly(array, offset: 0, count: 0);

        spanResult.Should().BeTrue();
        arrayResult.Should().BeTrue();
    }

    [TestMethod]
    public void TryReadExactly_NonEmptySpanOnDisposedStream_ThrowsObjectDisposedException()
    {
        MemoryStream stream = new();
        stream.Dispose();
        Span<byte> buffer = stackalloc byte[1];
        ObjectDisposedException? exception = null;

        try
        {
            stream.TryReadExactly(buffer);
        }
        catch (ObjectDisposedException caught)
        {
            exception = caught;
        }

        exception.Should().NotBeNull();
    }

    [TestMethod]
    public void TryReadExactly_NonEmptyArrayOnDisposedStream_ThrowsObjectDisposedException()
    {
        MemoryStream stream = new();
        stream.Dispose();
        byte[] buffer = [0];

        Action action = () => stream.TryReadExactly(buffer, offset: 0, count: 1);

        action.Should().Throw<ObjectDisposedException>();
    }

    [TestMethod]
    public void Read_ByteSpanExactMemoryStreamWithOrigin_ReadsRelativeToOrigin()
    {
        byte[] source = [9, 1, 2, 3, 9];
        using MemoryStream stream = new(
            source,
            index: 1,
            count: 3,
            writable: false,
            publiclyVisible: true);
        stream.Position = 1;
        byte[] buffer = [0, 0];

#pragma warning disable CA2022 // Direct Read call verifies the Stream.Read span contract.
        int read = stream.Read(buffer.AsSpan());
#pragma warning restore CA2022

        read.Should().Be(2);
        buffer.Should().Equal(2, 3);
        stream.Position.Should().Be(3);
    }

    [TestMethod]
    public void Read_ByteSpanExactMemoryStreamPastEnd_ReturnsZero()
    {
        byte[] source = [1, 2, 3];
        using MemoryStream stream = new(
            source,
            index: 0,
            count: source.Length,
            writable: false,
            publiclyVisible: true);
        stream.Position = stream.Length + 1;
        byte[] buffer = [9];

#pragma warning disable CA2022 // Direct Read call verifies the Stream.Read span contract.
        int read = stream.Read(buffer.AsSpan());
#pragma warning restore CA2022

        read.Should().Be(0);
        buffer.Should().Equal(9);
        stream.Position.Should().Be(4);
    }

    [TestMethod]
    public void Write_ByteSpanExactMemoryStreamWithOrigin_WritesRelativeToOrigin()
    {
        byte[] backing = [9, 0, 0, 0, 9];
        using MemoryStream stream = new(
            backing,
            index: 1,
            count: 3,
            writable: true,
            publiclyVisible: true);
        stream.Position = 1;
        ReadOnlySpan<byte> buffer = [2, 3];

        stream.Write(buffer);

        backing.Should().Equal(9, 0, 2, 3, 9);
        stream.Position.Should().Be(3);
    }

    [TestMethod]
    public void Write_ByteSpanBeyondLength_ClearsGap()
    {
        using MemoryStream stream = new();
        stream.Write([1, 2, 3, 4], 0, 4);
        stream.SetLength(1);
        stream.Position = 3;
        ReadOnlySpan<byte> buffer = [9];

        stream.Write(buffer);

        stream.ToArray().Should().Equal(1, 0, 0, 9);
        stream.Position.Should().Be(4);
        stream.Length.Should().Be(4);
    }

    [TestMethod]
    public void Write_EmptyByteSpanBeyondLength_DoesNotGrowStream()
    {
        using MemoryStream stream = new();
        stream.WriteByte(1);
        stream.Position = 3;
        ReadOnlySpan<byte> buffer = [];

        stream.Write(buffer);

        stream.ToArray().Should().Equal(1);
        stream.Position.Should().Be(3);
        stream.Length.Should().Be(1);
    }

    [TestMethod]
    public void Write_ByteSpanBeyondFixedCapacity_ThrowsNotSupportedException()
    {
        byte[] backing = [1, 2, 3];
        using MemoryStream stream = new(
            backing,
            index: 0,
            count: backing.Length,
            writable: true,
            publiclyVisible: true);
        stream.Position = stream.Length;
        ReadOnlySpan<byte> buffer = [4];
        NotSupportedException? exception = null;

        try
        {
            stream.Write(buffer);
        }
        catch (NotSupportedException caught)
        {
            exception = caught;
        }

        exception.Should().NotBeNull();
        backing.Should().Equal(1, 2, 3);
        stream.Position.Should().Be(3);
        stream.Length.Should().Be(3);
    }

    [TestMethod]
    public void Write_ByteSpanNonWritableMemoryStream_ThrowsNotSupportedException()
    {
        byte[] backing = [0];
        using MemoryStream stream = new(
            backing,
            index: 0,
            count: backing.Length,
            writable: false,
            publiclyVisible: true);
        ReadOnlySpan<byte> buffer = [1];
        NotSupportedException? exception = null;

        try
        {
            stream.Write(buffer);
        }
        catch (NotSupportedException caught)
        {
            exception = caught;
        }

        exception.Should().NotBeNull();
        backing.Should().Equal(0);
    }

    [TestMethod]
    public void Write_ByteSpanAtMaximumPosition_ThrowsOutOfMemoryException()
    {
        using MemoryStream stream = new();
        stream.Position = 0X7FFFFFC7;
        ReadOnlySpan<byte> buffer = [1];
        OutOfMemoryException? exception = null;

        try
        {
            stream.Write(buffer);
        }
        catch (OutOfMemoryException caught)
        {
            exception = caught;
        }

        exception.Should().NotBeNull();
    }

#if NET
    [TestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    public void TryReadExactly_SpanReadReturnsInvalidCount_ThrowsIOException(int readCount)
    {
        using InvalidSpanReadCountStream stream = new(readCount);
        Span<byte> buffer = stackalloc byte[1];
        System.IO.IOException? exception = null;

        try
        {
            stream.TryReadExactly(buffer);
        }
        catch (System.IO.IOException caught)
        {
            exception = caught;
        }

        exception.Should().NotBeNull();
    }
#endif

#if NETFRAMEWORK && !DEBUG
    [TestMethod]
    public void Read_ByteSpanFrameworkPolyfill_DoesNotAllocateAfterWarmup()
    {
        byte[] source = new byte[16];
        byte[] buffer = new byte[16];
        using MemoryStream stream = new(
            source,
            index: 0,
            count: source.Length,
            writable: false,
            publiclyVisible: true);

        _ = stream.Read(buffer.AsSpan());
        stream.Position = 0;

        using (MemoryWatch.Create)
        {
            _ = stream.Read(buffer.AsSpan());
        }
    }
#endif

    private sealed class InvalidReadCountStream : System.IO.Stream
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

        public override int Read(byte[] buffer, int offset, int count) => count + 1;

        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

#if NET
    private sealed class InvalidSpanReadCountStream(int readCount) : System.IO.Stream
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

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(Span<byte> buffer) => readCount;

        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
#endif
}
