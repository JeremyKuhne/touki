// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Threading;

namespace Touki.Io;

/// <summary>
///  Provides thread-safe accounting snapshots, including after stream disposal.
/// </summary>
public sealed class LargeFileStreamStatistics
{
    // The foreground-operation gate serializes I/O and delivery writers. Lease returns remain multi-writer.
    private long _backendBytes;
    private long _delivered;
    private long _requests;
    private long _shortReads;
    private long _waitingTicks;
    private long _heldTicks;
    private int _outstanding;
    private int _peakOutstanding;
    private int _inFlight;
    private int _peakInFlight;
    private int _bufferBytes;
    private Exception? _unobservedFailure;

    /// <summary>
    ///  Initializes counters with an explicit timing policy.
    /// </summary>
    /// <param name="collectTimings">Whether to collect I/O and lease lifetime timings.</param>
    internal LargeFileStreamStatistics(bool collectTimings = false) => TimingsEnabled = collectTimings;

    /// <summary>
    ///  Gets whether timing is enabled. Exact byte and request counters are independent of this setting.
    /// </summary>
    public bool TimingsEnabled { get; }

    /// <summary>
    ///  Gets the successful backend byte count, excluding buffered replays.
    /// </summary>
    public long BackendBytesRead => Volatile.Read(ref _backendBytes);

    /// <summary>
    ///  Gets the bytes handed to callers, including buffered replays.
    /// </summary>
    public long BytesDelivered => Volatile.Read(ref _delivered);

    /// <summary>
    ///  Gets the number of submitted backend reads.
    /// </summary>
    public long RequestsIssued => Volatile.Read(ref _requests);

    /// <summary>
    ///  Gets the number of positive short reads that were reissued.
    /// </summary>
    public long ShortReads => Volatile.Read(ref _shortReads);

    /// <summary>
    ///  Gets unused speculative bytes. The demand-only reader never issues speculative reads.
    /// </summary>
    public long PrefetchedBytesUnused => 0;

    /// <summary>
    ///  Gets consumer time spent in backend reads, or zero when timing is disabled.
    /// </summary>
    public TimeSpan TimeWaitingForIo => TimeSpan.FromSeconds(
        (double)Volatile.Read(ref _waitingTicks) / Stopwatch.Frequency);

    /// <summary>
    ///  Gets the sum of disposed chunk lifetimes, or zero when timing is disabled.
    /// </summary>
    public TimeSpan TimeChunksHeld => TimeSpan.FromSeconds(
        (double)Interlocked.Read(ref _heldTicks) / Stopwatch.Frequency);

    /// <summary>
    ///  Gets the peak number of simultaneous backend reads.
    /// </summary>
    public int MaxInFlight => Volatile.Read(ref _peakInFlight);

    /// <summary>
    ///  Gets the peak number of live nonempty consumer leases.
    /// </summary>
    public int MaxChunksOutstanding => Volatile.Read(ref _peakOutstanding);

    /// <summary>
    ///  Gets the peak payload capacity allocated in fixed slots, excluding caller-owned buffers.
    /// </summary>
    public int PeakBufferedBytes => Volatile.Read(ref _bufferBytes);

    /// <summary>
    ///  Gets an error recorded during cleanup instead of thrown from disposal.
    /// </summary>
    public Exception? UnobservedFailure => Volatile.Read(ref _unobservedFailure);

    /// <summary>
    ///  Accounts for one submitted read.
    /// </summary>
    internal void BeginRead()
    {
        Volatile.Write(ref _requests, _requests + 1);
        _inFlight++;
        if (_inFlight > _peakInFlight)
        {
            Volatile.Write(ref _peakInFlight, _inFlight);
        }
    }

    /// <summary>
    ///  Accounts for a completed or failed read.
    /// </summary>
    /// <param name="bytes">The successful byte count, or zero on failure.</param>
    /// <param name="requested">The submitted byte count.</param>
    internal void EndRead(int bytes, int requested)
    {
        _inFlight--;
        Volatile.Write(ref _backendBytes, _backendBytes + bytes);
        if (bytes > 0 && bytes < requested)
        {
            Volatile.Write(ref _shortReads, _shortReads + 1);
        }
    }

    /// <summary>
    ///  Accounts for committed delivery.
    /// </summary>
    /// <param name="bytes">The delivered byte count.</param>
    internal void Delivered(int bytes) => Volatile.Write(ref _delivered, _delivered + bytes);

    /// <summary>
    ///  Accounts for the consumer time spent in a backend request.
    /// </summary>
    /// <param name="started">The initial stopwatch timestamp.</param>
    internal void Waited(long started)
    {
        if (started != 0)
        {
            Volatile.Write(ref _waitingTicks, _waitingTicks + Stopwatch.GetTimestamp() - started);
        }
    }

    /// <summary>
    ///  Captures a timestamp only when detailed timing has been requested.
    /// </summary>
    /// <returns>The initial timestamp, or zero when timing is disabled.</returns>
    internal long StartTiming() => TimingsEnabled ? Stopwatch.GetTimestamp() : 0;

    /// <summary>
    ///  Accounts for a new nonempty lease.
    /// </summary>
    internal void LeaseCreated() => SetPeak(ref _peakOutstanding, Interlocked.Increment(ref _outstanding));

    /// <summary>
    ///  Accounts for a fixed slot allocated once during the stream's lifetime.
    /// </summary>
    /// <param name="bytes">The new slot's payload length.</param>
    internal void AllocatedSlot(int bytes) => Volatile.Write(ref _bufferBytes, _bufferBytes + bytes);

    /// <summary>
    ///  Accounts for disposal of a nonempty lease.
    /// </summary>
    /// <param name="started">The lease creation timestamp.</param>
    internal void LeaseReturned(long started)
    {
        Interlocked.Decrement(ref _outstanding);
        if (started != 0)
        {
            Interlocked.Add(ref _heldTicks, Stopwatch.GetTimestamp() - started);
        }
    }

    /// <summary>
    ///  Preserves a cleanup error without throwing from disposal.
    /// </summary>
    /// <param name="exception">The original cleanup error.</param>
    internal void RecordCleanupFailure(Exception exception) =>
        Interlocked.CompareExchange(ref _unobservedFailure, exception, comparand: null);

    private static void SetPeak(ref int peak, int value)
    {
        int observed = Volatile.Read(ref peak);
        while (value > observed)
        {
            int previous = Interlocked.CompareExchange(ref peak, value, observed);
            if (previous == observed)
            {
                return;
            }

            observed = previous;
        }
    }
}
#endif
