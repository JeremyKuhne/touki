// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Threading;

namespace Touki.Io;

/// <summary>
///  A unique, zero-copy lease over one delivered file window.
/// </summary>
/// <remarks>
///  <para>
///   Dispose the lease before acquiring more than the configured consumer budget. Previously obtained
///   memory rejects fresh span and pin accesses after disposal; a span already obtained cannot be revoked.
///  </para>
///  <para>
///   Chunks remain usable after the stream is disposed. Outstanding pins retain their storage until unpinned
///   and occupy the consumer budget even if the corresponding lease has been disposed.
///  </para>
/// </remarks>
[SuppressMessage("Design", "TOUKI0012", Justification = "MemoryManager inheritance guards retained memory access.")]
public sealed class LargeFileChunk : MemoryManager<byte>
{
    private readonly LargeFileBufferSlot? _slot;
    private readonly LargeFileStreamStatistics? _statistics;
    private readonly int _start;
    private readonly int _length;
    private readonly long _created;
    private int _disposed;

    /// <summary>
    ///  Captures a unique ownership identity and immutable window coordinates.
    /// </summary>
    /// <param name="slot">The slot, or null for an empty EOF lease.</param>
    /// <param name="start">The first delivered buffer index.</param>
    /// <param name="length">The delivered byte count.</param>
    /// <param name="offset">The absolute file offset.</param>
    /// <param name="isLast">Whether this is the final chunk.</param>
    /// <param name="generation">The stream generation at delivery.</param>
    /// <param name="statistics">The accounting sink, or null for EOF.</param>
    internal LargeFileChunk(
        LargeFileBufferSlot? slot,
        int start,
        int length,
        long offset,
        bool isLast,
        long generation,
        LargeFileStreamStatistics? statistics)
    {
        _slot = slot;
        _start = start;
        _length = length;
        FileOffset = offset;
        IsLast = isLast;
        Generation = generation;
        _statistics = statistics;
        _created = statistics?.StartTiming() ?? 0;
        statistics?.LeaseCreated();
    }

    /// <summary>
    ///  Gets the delivered bytes, backed by this lease rather than directly by the slot's array.
    /// </summary>
    public new ReadOnlyMemory<byte> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return CreateMemory(_length);
        }
    }

    /// <summary>
    ///  Gets the absolute file offset of the first delivered byte.
    /// </summary>
    public long FileOffset { get; }

    /// <summary>
    ///  Gets whether this chunk ends at the captured end of the file.
    /// </summary>
    public bool IsLast { get; }

    /// <summary>
    ///  Gets the generation captured when this lease was delivered.
    /// </summary>
    internal long Generation { get; }

    /// <summary>
    ///  Gets whether this identity has been released.
    /// </summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    ///  Gets or sets the pin count, exclusively under the slot lock.
    /// </summary>
    internal int Pins { get; set; }

    /// <inheritdoc/>
    public override Span<byte> GetSpan()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return _slot is null ? [] : _slot.GetSpan(this, _start, _length);
    }

    /// <inheritdoc/>
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)elementIndex, (uint)_length);
        return _slot is null
            ? default
            : _slot.Pin(this, checked(_start + elementIndex));
    }

    /// <inheritdoc/>
    public override void Unpin() => _slot?.Unpin(this);

    /// <summary>
    ///  Releases this unique lease once. Storage is reusable only after its pins are also released.
    /// </summary>
    public void Dispose() => ((IDisposable)this).Dispose();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _statistics?.LeaseReturned(_created);
        _slot?.ReleaseOwner(this);
    }
}
#endif
