// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Buffers;
using System.Buffers.Binary;
using touki.perf;

namespace Touki.Io;

public partial class LargeFileStreamTests
{
    private LargeFileChunk? _allocationSink;

    [TestMethod]
    public void ReadChunk_MixedStreamReads_ReturnsOnlyUnconsumedBytes()
    {
        using TestFileReadBackend backend = new(2 * Window + 17);
        using LargeFileStream stream = Open(backend);
        byte[] prefix = new byte[31];
        stream.Read(prefix).Should().Be(prefix.Length);
        using (LargeFileChunk chunk = stream.ReadChunk())
        {
            chunk.FileOffset.Should().Be(prefix.Length);
            chunk.Memory.Length.Should().Be(Window - prefix.Length);
            byte[] expected = new byte[chunk.Memory.Length];
            LargeFileReadPattern.Fill(expected, prefix.Length, backend.Length, blocks: false);
            chunk.Memory.ToArray().Should().Equal(expected);
            chunk.IsLast.Should().BeFalse();
        }

        stream.Position.Should().Be(Window);
        stream.ReadByte().Should().Be(0);
        stream.Position = 2 * Window;
        using (LargeFileChunk tail = stream.ReadChunk())
        {
            tail.Memory.Length.Should().Be(17);
            tail.IsLast.Should().BeTrue();
        }

        using LargeFileChunk end = stream.ReadChunk();
        end.Memory.IsEmpty.Should().BeTrue();
        end.IsLast.Should().BeTrue();
        stream.TryReadChunk(out LargeFileChunk? unavailable).Should().BeFalse();
        unavailable.Should().BeNull();
    }

    [TestMethod]
    public async Task ReadChunkAsync_ExhaustedConsumerBudget_WaitsForRelease()
    {
        using TestFileReadBackend backend = new(4 * Window);
        using LargeFileStream stream = Open(backend);
        using LargeFileChunk first = stream.ReadChunk();
        Task<LargeFileChunk> waiting = stream.ReadChunkAsync().AsTask();
        waiting.IsCompleted.Should().BeFalse();
        first.Dispose();
        using LargeFileChunk second = await waiting.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
        second.FileOffset.Should().Be(Window);
        stream.Statistics.MaxChunksOutstanding.Should().Be(1);
    }

    [TestMethod]
    public void ReadChunk_OutOfOrderReleaseAndStaleDispose_PreserveCurrentOwner()
    {
        using TestFileReadBackend backend = new(8 * Window);
        using LargeFileStream stream = Open(backend, outstanding: 2);
        using LargeFileChunk first = stream.ReadChunk();
        using LargeFileChunk second = stream.ReadChunk();
        second.Dispose();
        using LargeFileChunk replacement = stream.ReadChunk();
        second.Dispose();
        first.Dispose();
        using LargeFileChunk next = stream.ReadChunk();
        byte[] expected = new byte[Window];
        LargeFileReadPattern.Fill(expected, replacement.FileOffset, backend.Length, blocks: false);
        replacement.Memory.ToArray().Should().Equal(expected);
        next.FileOffset.Should().Be(3 * Window);
        stream.Statistics.MaxChunksOutstanding.Should().Be(2);
    }

    [TestMethod]
    public void TryReadChunk_WindowMissAndFullBudget_DoesNotSubmitIo()
    {
        using TestFileReadBackend backend = new(4 * Window);
        using LargeFileStream stream = Open(backend);
        stream.TryReadChunk(out LargeFileChunk? miss).Should().BeFalse();
        miss.Should().BeNull();
        stream.Statistics.RequestsIssued.Should().Be(0);
        stream.ReadByte();
        stream.TryReadChunk(out LargeFileChunk? ready).Should().BeTrue();
        using LargeFileChunk lease = ready ?? throw new InvalidOperationException("Expected a lease.");
        stream.ReadByte();
        long requests = stream.Statistics.RequestsIssued;
        stream.TryReadChunk(out LargeFileChunk? blocked).Should().BeFalse();
        blocked.Should().BeNull();
        stream.Statistics.RequestsIssued.Should().Be(requests);
        lease.Dispose();
        stream.TryReadChunk(out LargeFileChunk? next).Should().BeTrue();
        using LargeFileChunk delivered = next ?? throw new InvalidOperationException("Expected a lease.");
        delivered.FileOffset.Should().Be(Window + 1);
    }

    [TestMethod]
    public void Dispose_ChunksOutliveStream_RetainedMemoryRemainsValidUntilLeaseDisposal()
    {
        using TestFileReadBackend backend = new(3 * Window);
        using LargeFileStream stream = Open(backend, collectTimings: true);
        stream.Position = 8;
        using LargeFileChunk lease = stream.ReadChunk();
        ReadOnlyMemory<byte> retained = lease.Memory;
        stream.Dispose();
        BinaryPrimitives.ReadInt64LittleEndian(retained.Span).Should().Be(8);
        lease.Dispose();
        Action memory = () => ReadFirstWord(retained);
        memory.Should().Throw<ObjectDisposedException>();
        Action pin = () => PinAndDispose(lease, 0);
        pin.Should().Throw<ObjectDisposedException>();
        stream.Statistics.MaxChunksOutstanding.Should().Be(1);
        stream.Statistics.TimeChunksHeld.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [TestMethod]
    public async Task Dispose_PinOutlivesLease_DoesNotReturnBudgetUntilUnpinned()
    {
        using TestFileReadBackend backend = new(3 * Window);
        using LargeFileStream stream = Open(backend);
        using LargeFileChunk lease = stream.ReadChunk();
        Task<LargeFileChunk> waiting;
        using (MemoryHandle pin = lease.Pin())
        {
            lease.Dispose();
            waiting = stream.ReadChunkAsync().AsTask();
            waiting.IsCompleted.Should().BeFalse();
        }

        using LargeFileChunk next = await waiting.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
        next.FileOffset.Should().Be(Window);
    }

    [TestMethod]
    public unsafe void Dispose_StreamAndLeaseWithLivePin_KeepPinnedBytesAlive()
    {
        using TestFileReadBackend backend = new(Window);
        using LargeFileStream stream = Open(backend);
        stream.Position = 8;
        using LargeFileChunk lease = stream.ReadChunk();
        using MemoryHandle pin = lease.Pin();
        stream.Dispose();
        lease.Dispose();
        BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(pin.Pointer, 8)).Should().Be(8);
    }

    [TestMethod]
    public async Task ReadChunkAsync_CancellationWhileWaiting_LeavesCursorUnchanged()
    {
        using TestFileReadBackend backend = new(3 * Window);
        using LargeFileStream stream = Open(backend);
        using LargeFileChunk first = stream.ReadChunk();
        using CancellationTokenSource cancellation = new();
        Task<LargeFileChunk> waiting = stream.ReadChunkAsync(cancellation.Token).AsTask();
        cancellation.Cancel();
        Func<Task> read = async () => await waiting.ConfigureAwait(continueOnCapturedContext: false);
        OperationCanceledException exception = (await read.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(continueOnCapturedContext: false)).Which;
        exception.CancellationToken.Should().Be(cancellation.Token);
        stream.Position.Should().Be(Window);
        first.Dispose();
        using LargeFileChunk next = stream.ReadChunk();
        next.FileOffset.Should().Be(Window);
    }

    [TestMethod]
    public async Task DisposeAsync_WaitingForLease_DoesNotWaitForConsumer()
    {
        using TestFileReadBackend backend = new(3 * Window);
        using LargeFileStream stream = Open(backend);
        using LargeFileChunk first = stream.ReadChunk();
        Task<LargeFileChunk> waiting = stream.ReadChunkAsync().AsTask();
        await stream.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
        Func<Task> read = async () => await waiting.ConfigureAwait(continueOnCapturedContext: false);
        await read.Should().ThrowAsync<ObjectDisposedException>().ConfigureAwait(continueOnCapturedContext: false);
        first.Memory.Length.Should().Be(Window);
    }

    [TestMethod]
    public async Task ReadChunk_CustomSynchronizationContext_DoesNotCaptureContext()
    {
        using BlockingSynchronousBackend backend = new();
        using LargeFileStream stream = Open(backend);
        Task read = Task.Run(() =>
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new RejectingContext());
            try
            {
                using LargeFileChunk chunk = stream.ReadChunk();
                chunk.Memory.Length.Should().Be(Window);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        });

        try
        {
            await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            backend.Release.TrySetResult();
        }

        await read.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public Task ReadAsync_CustomSynchronizationContext_DoesNotCaptureContext() =>
        CheckAsyncContext(useChunk: false);

    [TestMethod]
    public Task ReadChunkAsync_CustomSynchronizationContext_DoesNotCaptureContext() =>
        CheckAsyncContext(useChunk: true);

    private static async Task CheckAsyncContext(bool useChunk)
    {
        using TestFileReadBackend backend = new(Window) { HoldReads = true };
        using LargeFileStream stream = Open(backend);
        Task<LargeFileChunk>? chunkRead = null;
        Task<int>? byteRead = null;
        await Task.Run(() =>
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new RejectingContext());
            try
            {
                if (useChunk)
                {
                    chunkRead = stream.ReadChunkAsync().AsTask();
                }
                else
                {
                    byteRead = stream.ReadAsync(new byte[71]).AsTask();
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }).ConfigureAwait(continueOnCapturedContext: false);

        await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
        backend.Release.TrySetResult();
        if (chunkRead is not null)
        {
            using LargeFileChunk chunk = await chunkRead.WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(continueOnCapturedContext: false);

            chunk.Memory.Length.Should().Be(Window);
        }
        else
        {
            Task<int> pending = byteRead ?? throw new InvalidOperationException("No read submitted.");
            int count = await pending.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
            count.Should().Be(71);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_PendingSynchronousWindow_DrainsBeforeReleasingStorage()
    {
        using BlockingSynchronousBackend backend = new();
        using LargeFileStream stream = Open(backend);
        Task<LargeFileChunk> read = Task.Run(stream.ReadChunk);
        try
        {
            await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
            Task disposed = stream.DisposeAsync().AsTask();
            disposed.IsCompleted.Should().BeFalse();
            backend.WasDisposed.Should().BeFalse();
            backend.Release.TrySetResult();
            Func<Task> completeRead = async () => await read.ConfigureAwait(continueOnCapturedContext: false);
            await completeRead.Should().ThrowAsync<ObjectDisposedException>()
                .ConfigureAwait(continueOnCapturedContext: false);

            await disposed.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
            stream.Position.Should().Be(0);
            backend.WasDisposed.Should().BeTrue();
        }
        finally
        {
            backend.Release.TrySetResult();
        }
    }

#if !DEBUG
    [TestMethod]
    [DoNotParallelize]
    public void Read_ImmediatelyCompletingBackend_WarmStreamLoopDoesNotAllocate()
    {
        using TestFileReadBackend backend = new(8 * Window);
        using LargeFileStream stream = Open(backend);
        byte[] destination = new byte[255];
        stream.Read(destination).Should().Be(destination.Length);
        int delivered = 0;
        // Concurrent collections perturb this counter even across an empty SpinWait interval.
        GC.TryStartNoGCRegion(1 << 20).Should().BeTrue();
        try
        {
            using (MemoryWatch.Create)
            {
                for (int i = 0; i < 1000; i++)
                {
                    delivered += stream.Read(destination);
                }
            }
        }
        finally
        {
            GC.EndNoGCRegion();
        }

        delivered.Should().BeGreaterThan(3 * Window);
    }

    [TestMethod]
    [DoNotParallelize]
    public void ReadChunk_ImmediatelyCompletingBackend_AllocatesExactlyOneFreshLeasePerWindow()
    {
        const int Iterations = 100;
        using TestFileReadBackend backend = new((Iterations + 1L) * Window);
        using LargeFileStream stream = Open(backend);
        stream.ReadChunk().Dispose();
        stream.Position = 0;
        long leaseBytes;
        long actual;
        // Keep exact per-thread accounting independent of collection-time allocation-context adjustment.
        GC.TryStartNoGCRegion(1 << 20).Should().BeTrue();
        try
        {
            long started = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Iterations; i++)
            {
                _allocationSink = new(slot: null, 0, 0, 0, isLast: true, 0, statistics: null);
                _allocationSink.Dispose();
            }

            leaseBytes = GC.GetAllocatedBytesForCurrentThread() - started;
            started = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Iterations; i++)
            {
                using LargeFileChunk chunk = stream.ReadChunk();
                _allocationSink = chunk;
            }

            actual = GC.GetAllocatedBytesForCurrentThread() - started;
        }
        finally
        {
            GC.EndNoGCRegion();
        }

        leaseBytes.Should().BeGreaterThan(0);
        actual.Should().Be(leaseBytes);
    }
#endif

    private static long ReadFirstWord(ReadOnlyMemory<byte> memory) =>
        BinaryPrimitives.ReadInt64LittleEndian(memory.Span);

    private static void PinAndDispose(LargeFileChunk chunk, int index)
    {
        using MemoryHandle pin = chunk.Pin(index);
    }

    private sealed class BlockingSynchronousBackend : DisposableBase, IFileReadBackend
    {
        private readonly TestFileReadBackend _inner = new(Window);

        public Microsoft.Win32.SafeHandles.SafeFileHandle Handle => _inner.Handle;

        public long Length => _inner.Length;

        public bool WasDisposed => _inner.WasDisposed;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Read(Span<byte> buffer, long fileOffset)
        {
            Started.TrySetResult();
            Release.Task.GetAwaiter().GetResult();
            return _inner.Read(buffer, fileOffset);
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, long fileOffset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(Read(buffer.Span, fileOffset));
        }

        protected override void Dispose(bool disposing) => _inner.Dispose();
    }

    private sealed class RejectingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            throw new InvalidOperationException("The read captured the synchronization context.");
    }
}
#endif
