// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  A seekable resource stream that can block reads and records disposal.
/// </summary>
internal sealed class BlockingSeekableResourceStream : Stream
{
    private readonly byte[] _content;
    private readonly ManualResetEventSlim _readStarted;
    private readonly ManualResetEventSlim _continueRead;
    private readonly ManualResetEventSlim _disposed;
    private int _position;

    internal BlockingSeekableResourceStream(
        byte[] content,
        ManualResetEventSlim readStarted,
        ManualResetEventSlim continueRead,
        ManualResetEventSlim disposed)
    {
        _content = content;
        _readStarted = readStarted;
        _continueRead = continueRead;
        _disposed = disposed;
    }

    internal bool BlockReads { get; set; }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _content.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, _content.Length);
            _position = (int)value;
        }
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        WaitIfBlocked();
        int read = Math.Min(count, _content.Length - _position);
        _content.AsSpan(_position, read).CopyTo(buffer.AsSpan(offset, read));
        _position += read;
        return read;
    }

#if NET
    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        WaitIfBlocked();
        int read = Math.Min(buffer.Length, _content.Length - _position);
        _content.AsSpan(_position, read).CopyTo(buffer);
        _position += read;
        return read;
    }
#endif

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        long position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _content.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        Position = position;
        return position;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _disposed.Set();
        base.Dispose(disposing);
    }

    private void WaitIfBlocked()
    {
        if (!BlockReads)
        {
            return;
        }

        _readStarted.Set();
        _continueRead.Wait();
    }
}
