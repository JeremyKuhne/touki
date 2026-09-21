// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  A stream that limits each read, optionally supports seeking, and records disposal.
/// </summary>
internal sealed class StringResourceManagerTestStream : System.IO.Stream
{
    private readonly byte[] _content;
    private readonly int _maximumReadSize;
    private readonly bool _canSeek;
    private readonly bool _throwOnSecondDispose;
    private int _position;

    /// <summary>
    ///  Initializes the stream.
    /// </summary>
    /// <param name="content">The bytes to expose.</param>
    /// <param name="maximumReadSize">The maximum bytes returned by one read.</param>
    /// <param name="canSeek">Whether the stream supports seeking.</param>
    /// <param name="throwOnSecondDispose">Whether a second disposal throws.</param>
    internal StringResourceManagerTestStream(
        byte[] content,
        int maximumReadSize,
        bool canSeek = false,
        bool throwOnSecondDispose = false)
    {
        _content = content;
        _maximumReadSize = maximumReadSize;
        _canSeek = canSeek;
        _throwOnSecondDispose = throwOnSecondDispose;
    }

    /// <summary>
    ///  Whether the stream was disposed.
    /// </summary>
    internal bool IsDisposed { get; private set; }

    /// <summary>
    ///  The number of disposal attempts.
    /// </summary>
    internal int DisposeCount { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => _canSeek;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _canSeek ? _content.Length : throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => _canSeek ? _position : throw new NotSupportedException();
        set
        {
            if (!_canSeek)
            {
                throw new NotSupportedException();
            }

            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, _content.Length);
            _position = (int)value;
        }
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        int remaining = _content.Length - _position;
        int read = Math.Min(Math.Min(count, _maximumReadSize), remaining);
        _content.AsSpan(_position, read).CopyTo(buffer.AsSpan(offset, read));
        _position += read;
        return read;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!_canSeek)
        {
            throw new NotSupportedException();
        }

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
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        DisposeCount++;
        if (_throwOnSecondDispose && DisposeCount > 1)
        {
            throw new InvalidOperationException("The stream was disposed more than once.");
        }

        IsDisposed = true;
        base.Dispose(disposing);
    }
}