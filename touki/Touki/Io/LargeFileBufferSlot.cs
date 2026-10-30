// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Threading;

namespace Touki.Io;

/// <summary>
///  Owns a fixed buffer shared by the stream, a unique lease, and any outstanding pins.
/// </summary>
internal sealed class LargeFileBufferSlot
{
    private readonly Lock _sync = new();
    private readonly Action _returned;
    private byte[]? _buffer;
    private LargeFileChunk? _owner;
    private bool _streamOwns = true;
    private bool _reserved;
    private bool _returnPending;
    private int _pins;

    /// <summary>
    ///  Allocates one exact-size slot.
    /// </summary>
    /// <param name="size">The buffer length.</param>
    /// <param name="returned">Returns a consumer permit after the lease and its pins are released.</param>
    internal LargeFileBufferSlot(int size, Action returned)
    {
        _buffer = new byte[size];
        _returned = returned;
    }

    /// <summary>
    ///  Gets storage retained by the stream or a lease.
    /// </summary>
    internal Memory<byte> Buffer => _buffer
        ?? throw new ObjectDisposedException(nameof(LargeFileBufferSlot));

    /// <summary>
    ///  Tries to reserve a free slot for the stream's next fill.
    /// </summary>
    /// <returns>Whether the slot was free.</returns>
    internal bool TryReserve()
    {
        lock (_sync)
        {
            if (_reserved || !_streamOwns)
            {
                return false;
            }

            _reserved = true;
            return true;
        }
    }

    /// <summary>
    ///  Releases a completed, unleased window.
    /// </summary>
    internal void ReleaseReservation()
    {
        lock (_sync)
        {
            if (_owner is not null || _pins != 0)
            {
                throw new InvalidOperationException("A leased or pinned slot cannot be released by the stream.");
            }

            _reserved = false;
        }
    }

    /// <summary>
    ///  Transfers an exclusively reserved window to a new lease identity.
    /// </summary>
    /// <param name="start">The first delivered buffer index.</param>
    /// <param name="count">The delivered byte count.</param>
    /// <param name="offset">The absolute delivered file offset.</param>
    /// <param name="isLast">Whether this is the final window.</param>
    /// <param name="generation">The stream generation at delivery.</param>
    /// <param name="statistics">The stream's accounting sink.</param>
    /// <returns>The unique lease.</returns>
    internal LargeFileChunk Lease(
        int start,
        int count,
        long offset,
        bool isLast,
        long generation,
        LargeFileStreamStatistics statistics)
    {
        lock (_sync)
        {
            if (!_streamOwns || !_reserved || _owner is not null || _pins != 0)
            {
                throw new InvalidOperationException("The slot is not exclusively owned by the stream.");
            }

            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)start, (uint)Buffer.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)(Buffer.Length - start));
            _owner = new(this, start, count, offset, isLast, generation, statistics);
            return _owner;
        }
    }

    /// <summary>
    ///  Checks lease identity before exposing its bytes.
    /// </summary>
    /// <param name="lease">The requesting lease.</param>
    /// <param name="start">The first buffer index.</param>
    /// <param name="count">The byte count.</param>
    /// <returns>The leased span.</returns>
    internal Span<byte> GetSpan(LargeFileChunk lease, int start, int count)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(lease.IsDisposed || !ReferenceEquals(_owner, lease), lease);
            return Buffer.Span.Slice(start, count);
        }
    }

    /// <summary>
    ///  Pins storage while retaining the lease's slot.
    /// </summary>
    /// <param name="lease">The requesting lease.</param>
    /// <param name="index">The checked buffer index.</param>
    /// <returns>A pin that releases its slot reference on disposal.</returns>
    internal unsafe MemoryHandle Pin(LargeFileChunk lease, int index)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(lease.IsDisposed || !ReferenceEquals(_owner, lease), lease);
            byte[] buffer = _buffer ?? throw new ObjectDisposedException(nameof(LargeFileBufferSlot));
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)buffer.Length);
            GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            lease.Pins++;
            _pins++;
            return new((byte*)pin.AddrOfPinnedObject() + index, pin, lease);
        }
    }

    /// <summary>
    ///  Releases one pin belonging to a specific lease.
    /// </summary>
    /// <param name="lease">The lease that created the pin.</param>
    internal void Unpin(LargeFileChunk lease)
    {
        bool returned;
        lock (_sync)
        {
            if (lease.Pins == 0)
            {
                throw new InvalidOperationException("The lease has no outstanding pin.");
            }

            lease.Pins--;
            _pins--;
            returned = FinishReturn();
        }

        if (returned)
        {
            _returned();
        }
    }

    /// <summary>
    ///  Returns ownership only if this is still the slot's current lease.
    /// </summary>
    /// <param name="lease">The disposing lease.</param>
    internal void ReleaseOwner(LargeFileChunk lease)
    {
        bool returned;
        lock (_sync)
        {
            if (!ReferenceEquals(Interlocked.CompareExchange(ref _owner, value: null, lease), lease))
            {
                return;
            }

            _returnPending = true;
            returned = FinishReturn();
        }

        if (returned)
        {
            _returned();
        }
    }

    /// <summary>
    ///  Orphans storage retained by leases or pins; otherwise releases it immediately.
    /// </summary>
    internal void ReleaseStream()
    {
        lock (_sync)
        {
            _streamOwns = false;
            if (_owner is null && _pins == 0)
            {
                _buffer = null;
            }
        }
    }

    private bool FinishReturn()
    {
        if (!_returnPending || _pins != 0)
        {
            return false;
        }

        _returnPending = false;
        _reserved = false;
        if (!_streamOwns)
        {
            _buffer = null;
        }

        return true;
    }
}
#endif
