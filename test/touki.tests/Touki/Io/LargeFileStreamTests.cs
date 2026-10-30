// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using Microsoft.Win32.SafeHandles;
using touki.perf;

namespace Touki.Io;

[TestClass]
public partial class LargeFileStreamTests
{
    private const int Window = 64 << 10;

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(Window - 1)]
    [DataRow(Window)]
    [DataRow(Window + 1)]
    [DataRow(3 * Window + 17)]
    public async Task Read_VariedLengthsAndBuffers_MatchesEveryReferenceByte(int length)
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "pattern.bin");
        LargeFileReadPattern.Create(path, length, blocks: false);
        foreach (int chunkSize in new[] { 0, Window })
        {
            foreach (LargeFileAccessPattern pattern in Enum.GetValues<LargeFileAccessPattern>())
            {
                foreach (int size in new[] { 1, 777, Window, 2 * Window })
                {
                    using LargeFileStream stream = LargeFileStream.Open(path, new()
                    {
                        AccessPattern = pattern,
                        ChunkSize = chunkSize
                    });

                    byte[] expected = new byte[size];
                    byte[] actual = new byte[size];
                    long offset = 0;
                    while (true)
                    {
                        int count = await stream.ReadAsync(actual).ConfigureAwait(continueOnCapturedContext: false);
                        if (count == 0)
                        {
                            break;
                        }

                        LargeFileReadPattern.Fill(expected.AsSpan(0, count), offset, length, blocks: false);
                        actual.AsSpan(0, count).SequenceEqual(expected.AsSpan(0, count)).Should().BeTrue();
                        offset += count;
                        stream.Position.Should().Be(offset);
                    }

                    offset.Should().Be(length);
                    stream.ReadByte().Should().Be(-1);
                }
            }
        }
    }

    [TestMethod]
    public void Read_PositiveShortCompletions_FillsWindowAndCountsRequests()
    {
        using TestFileReadBackend backend = new(Window + 7) { MaxReadSize = 1001 };
        using LargeFileStream stream = Open(backend);
        using LargeFileChunk chunk = stream.ReadChunk();
        byte[] expected = new byte[Window];
        LargeFileReadPattern.Fill(expected, 0, backend.Length, blocks: false);
        chunk.Memory.ToArray().Should().Equal(expected);
        stream.Statistics.BackendBytesRead.Should().Be(Window);
        stream.Statistics.RequestsIssued.Should().Be(66);
        stream.Statistics.ShortReads.Should().Be(65);
        stream.Statistics.MaxInFlight.Should().Be(1);
    }

    [TestMethod]
    public void Read_PrematureEof_FaultsWithoutMovingPosition()
    {
        using TestFileReadBackend backend = new(2 * Window) { MaxReadSize = 1001, PrematureAt = 1001 };
        using LargeFileStream stream = Open(backend);
        Action read = () => stream.ReadByte();
        read.Should().Throw<IOException>();
        stream.Position.Should().Be(0);
        stream.FailureFileOffset.Should().Be(1001);
        long requests = stream.Statistics.RequestsIssued;
        Action seek = () => stream.Seek(0, SeekOrigin.Begin);
        seek.Should().Throw<IOException>();
        read.Should().Throw<IOException>();
        stream.Statistics.RequestsIssued.Should().Be(requests);
    }

    [TestMethod]
    public void Read_BackendFailure_PreservesPrimaryException()
    {
        IOException expected = new("Injected failure.");
        using TestFileReadBackend backend = new(Window) { Failure = expected };
        using LargeFileStream stream = Open(backend);
        Action read = () => stream.ReadByte();
        read.Should().Throw<IOException>().Which.Should().BeSameAs(expected);
        read.Should().Throw<IOException>().Which.Should().BeSameAs(expected);
        stream.Position.Should().Be(0);
    }

    [TestMethod]
    public void Read_LargeBufferAndSparseDemandOnly_DoesNotStageOrSpeculate()
    {
        using TestFileReadBackend backend = new(8 * Window);
        using LargeFileStream stream = LargeFileStream.Open(backend, new()
        {
            AccessPattern = LargeFileAccessPattern.Sparse
        });

        stream.Position = 123;
        byte[] buffer = new byte[72];
        stream.Read(buffer).Should().Be(72);
        backend.LastOffset.Should().Be(123);
        backend.LastCount.Should().Be(72);
        stream.Statistics.BackendBytesRead.Should().Be(72);
        stream.Statistics.PrefetchedBytesUnused.Should().Be(0);
        stream.Statistics.RequestsIssued.Should().Be(1);
        stream.Seek(10000, SeekOrigin.Begin);
        stream.Read([]).Should().Be(0);
        stream.Statistics.RequestsIssued.Should().Be(1);
    }

    [TestMethod]
    public void Read_LargeCallerBuffer_BypassesWindow()
    {
        using TestFileReadBackend backend = new(8 * Window);
        using LargeFileStream stream = Open(backend);
        byte[] buffer = new byte[2 * Window];
        stream.Read(buffer).Should().Be(buffer.Length);
        backend.LastCount.Should().Be(buffer.Length);
        stream.Statistics.BackendBytesRead.Should().Be(buffer.Length);
    }

    [TestMethod]
    public void Seek_InsideActiveWindow_ReplaysWithoutIo()
    {
        using TestFileReadBackend backend = new(3 * Window);
        using LargeFileStream stream = Open(backend);
        byte[] first = new byte[91];
        byte[] replay = new byte[first.Length];
        stream.Read(first).Should().Be(first.Length);
        stream.Seek(0, SeekOrigin.Begin).Should().Be(0);
        stream.Read(replay).Should().Be(replay.Length);
        replay.Should().Equal(first);
        stream.Statistics.RequestsIssued.Should().Be(1);
        stream.Statistics.BytesDelivered.Should().Be(182);
    }

    [TestMethod]
    public void Seek_InvalidAndOverflowingOrigins_LeavePositionUnchanged()
    {
        using TestFileReadBackend backend = new(2 * Window);
        using LargeFileStream stream = Open(backend);
        stream.Seek(17, SeekOrigin.Begin);
        Action negative = () => stream.Seek(-18, SeekOrigin.Current);
        Action overflow = () => stream.Seek(long.MaxValue, SeekOrigin.End);
        Action invalid = () => stream.Seek(0, (SeekOrigin)999);
        negative.Should().Throw<IOException>();
        overflow.Should().Throw<IOException>();
        invalid.Should().Throw<ArgumentException>();
        stream.Position.Should().Be(17);
        stream.Seek(-1, SeekOrigin.End).Should().Be(backend.Length - 1);
        stream.Seek(1, SeekOrigin.Current).Should().Be(backend.Length);
        stream.Position = long.MaxValue;
        stream.ReadByte().Should().Be(-1);
        stream.Position.Should().Be(long.MaxValue);
    }

    [TestMethod]
    public void Seek_AboveFourGiB_PreservesOffsetsAndExactBytes()
    {
        long length = (1L << 32) + 2 * Window;
        using TestFileReadBackend backend = new(length);
        using LargeFileStream stream = Open(backend);
        foreach (long offset in new[] { (1L << 31) - 17, (1L << 32) - 17, (1L << 32) + 17 })
        {
            stream.Position = offset;
            using LargeFileChunk chunk = stream.ReadChunk();
            chunk.FileOffset.Should().Be(offset);
            byte[] expected = new byte[chunk.Memory.Length];
            LargeFileReadPattern.Fill(expected, offset, length, blocks: false);
            chunk.Memory.ToArray().Should().Equal(expected);
        }
    }

    [TestMethod]
    public async Task ReadAsync_Cancellation_DrainsAndLeavesReaderUsable()
    {
        using TestFileReadBackend backend = new(2 * Window) { HoldReads = true };
        using LargeFileStream stream = Open(backend);
        using CancellationTokenSource cancellation = new();
        Task<int> pending = stream.ReadAsync(new byte[71], cancellation.Token).AsTask();
        await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
        cancellation.Cancel();
        Func<Task> read = async () => await pending.ConfigureAwait(continueOnCapturedContext: false);
        OperationCanceledException exception = (await read.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(continueOnCapturedContext: false)).Which;
        exception.CancellationToken.Should().Be(cancellation.Token);
        stream.Position.Should().Be(0);
        backend.HoldReads = false;
        stream.ReadByte().Should().Be(0);
        stream.Position.Should().Be(1);
    }

    [TestMethod]
    public async Task ReadAsync_AlreadyCancelledEmptyRead_ThrowsWithCallerToken()
    {
        using TestFileReadBackend backend = new(0);
        using LargeFileStream stream = Open(backend);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Func<Task> read = async () => await stream.ReadAsync(Memory<byte>.Empty, cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
        OperationCanceledException exception = (await read.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(continueOnCapturedContext: false)).Which;
        exception.CancellationToken.Should().Be(cancellation.Token);
        stream.Statistics.RequestsIssued.Should().Be(0);
    }

    [TestMethod]
    public async Task ReadAsync_OverlappingCalls_RejectsRatherThanQueues()
    {
        using TestFileReadBackend backend = new(2 * Window) { HoldReads = true };
        using LargeFileStream stream = Open(backend);
        Task<int> pending = stream.ReadAsync(new byte[71]).AsTask();
        await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
        Action read = () => stream.ReadByte();
        Action seek = () => stream.Seek(0, SeekOrigin.Begin);
        read.Should().Throw<InvalidOperationException>();
        seek.Should().Throw<InvalidOperationException>();
        backend.Release.TrySetResult();
        (await pending.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false)).Should().Be(71);
    }

    [TestMethod]
    public async Task DisposeAsync_PendingUncancellableRead_DrainsBeforeReleasingBackend()
    {
        using TestFileReadBackend backend = new(2 * Window) { HoldReads = true, RespectCancellation = false };
        using LargeFileStream stream = Open(backend);
        try
        {
            Task<int> pending = stream.ReadAsync(new byte[71]).AsTask();
            await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
            Task disposed = stream.DisposeAsync().AsTask();
            Task disposedAgain = stream.DisposeAsync().AsTask();
            disposed.IsCompleted.Should().BeFalse();
            backend.WasDisposed.Should().BeFalse();
            backend.Release.TrySetResult();
            Func<Task> read = async () => await pending.ConfigureAwait(continueOnCapturedContext: false);
            await read.Should().ThrowAsync<ObjectDisposedException>().ConfigureAwait(continueOnCapturedContext: false);
            await Task.WhenAll(disposed, disposedAgain).WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(continueOnCapturedContext: false);

            backend.WasDisposed.Should().BeTrue();
            stream.Position.Should().Be(0);
            stream.CanRead.Should().BeFalse();
        }
        finally
        {
            backend.Release.TrySetResult();
        }
    }

    [TestMethod]
    public void Dispose_UnobservedCleanupFailure_RecordsWithoutThrowing()
    {
        IOException expected = new("Cleanup failure.");
        using TestFileReadBackend backend = new(0) { DisposeFailure = expected };
        using LargeFileStream stream = Open(backend);
        stream.Dispose();
        stream.Statistics.UnobservedFailure.Should().BeSameAs(expected);
    }

    [TestMethod]
    public async Task DisposeAsync_ThrowingCancellationCallback_StillDrainsAndRecordsFailure()
    {
        IOException expected = new("Cancellation callback failure.");
        using TestFileReadBackend backend = new(Window) { HoldReads = true, CancellationFailure = expected };
        using LargeFileStream stream = Open(backend);
        Task<int> pending = stream.ReadAsync(new byte[71]).AsTask();
        await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(continueOnCapturedContext: false);
        await stream.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(continueOnCapturedContext: false);

        Func<Task> read = async () => await pending.ConfigureAwait(continueOnCapturedContext: false);
        await read.Should().ThrowAsync<ObjectDisposedException>().ConfigureAwait(continueOnCapturedContext: false);
        backend.WasDisposed.Should().BeTrue();
        stream.Statistics.UnobservedFailure.Should().BeOfType<AggregateException>()
            .Which.InnerExceptions.Should().Contain(expected);
    }

    [TestMethod]
    public async Task CopyToAsync_DefaultBuffer_UsesDirectWindowSizedReads()
    {
        using TestFileReadBackend backend = new(3 * Window + 17);
        using LargeFileStream stream = Open(backend);
        using MemoryStream destination = new();
        await stream.CopyToAsync(destination).ConfigureAwait(continueOnCapturedContext: false);
        byte[] expected = new byte[backend.Length];
        LargeFileReadPattern.Fill(expected, 0, backend.Length, blocks: false);
        destination.ToArray().Should().Equal(expected);
        stream.Statistics.BackendBytesRead.Should().Be(backend.Length);
    }

    [TestMethod]
    public void Open_HandleOwnership_ClosesOnlyOwnedHandles()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "pattern.bin");
        LargeFileReadPattern.Create(path, Window, blocks: false);
        using SafeFileHandle handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
        using (LargeFileStream borrowed = LargeFileStream.Open(handle, ownsHandle: false))
        {
            borrowed.ReadByte().Should().Be(0);
        }

        handle.IsClosed.Should().BeFalse();
        using (LargeFileStream owner = LargeFileStream.Open(handle, ownsHandle: true))
        {
            owner.ReadByte().Should().Be(0);
        }

        handle.IsClosed.Should().BeTrue();
    }

    [TestMethod]
    public void Open_InvalidOptions_RejectsBeforeTakingBackendOwnership()
    {
        using TestFileReadBackend backend = new(Window);
        LargeFileStreamOptions[] invalid =
        [
            new() { ChunkSize = -1 },
            new() { ChunkSize = Window - 1 },
            new() { ReadAheadDepth = 1 },
            new() { MaxOutstandingChunks = 0 },
            new() { MaxOutstandingChunks = int.MaxValue },
            new() { MaxBufferedBytes = -1 },
            new() { ChunkSize = Window, MaxBufferedBytes = 2 * Window - 1 },
            new() { AccessPattern = (LargeFileAccessPattern)999 },
            new() { AccessHint = (LargeFileAccessHint)999 }
        ];

        foreach (LargeFileStreamOptions options in invalid)
        {
            Action open = () => LargeFileStream.Open(backend, options);
            open.Should().Throw<ArgumentException>();
            backend.WasDisposed.Should().BeFalse();
        }
    }

    [TestMethod]
    public void Open_ExactCapacity_ResolvesImmutableEffectiveOptions()
    {
        using TestFileReadBackend backend = new(Window);
        using LargeFileStream stream = LargeFileStream.Open(backend, new()
        {
            ChunkSize = Window,
            MaxBufferedBytes = 2 * Window
        });

        stream.Options.ChunkSize.Should().Be(Window);
        stream.Options.AccessHint.Should().Be(LargeFileAccessHint.Sequential);
        stream.Options.MaxOutstandingChunks.Should().Be(1);
        stream.Options.MaxBufferedBytes.Should().Be(2 * Window);
        stream.Statistics.RequestsIssued.Should().Be(0);
        stream.CanWrite.Should().BeFalse();
        Action write = () => stream.WriteByte(1);
        write.Should().Throw<NotSupportedException>();
    }

    private static LargeFileStream Open(IFileReadBackend backend, int outstanding = 1, bool collectTimings = false) =>
        LargeFileStream.Open(backend, new()
        {
            ChunkSize = Window,
            MaxOutstandingChunks = outstanding,
            CollectTimings = collectTimings,
            MaxBufferedBytes = checked(Window * (1 + outstanding))
        });

    private sealed class TestFileReadBackend(long length) : DisposableBase, IFileReadBackend
    {
        private CancellationTokenRegistration _cancellationRegistration;

        public SafeFileHandle Handle { get; } = new(nint.Zero, ownsHandle: false);
        public long Length { get; } = length;
        public int MaxReadSize { get; set; } = int.MaxValue;
        public long? PrematureAt { get; set; }
        public IOException? Failure { get; set; }
        public IOException? DisposeFailure { get; set; }
        public IOException? CancellationFailure { get; set; }
        public bool HoldReads { get; set; }
        public bool RespectCancellation { get; set; } = true;
        public bool WasDisposed { get; private set; }
        public long LastOffset { get; private set; }
        public int LastCount { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Read(Span<byte> buffer, long fileOffset)
        {
            ObjectDisposedException.ThrowIf(WasDisposed, this);
            LastOffset = fileOffset;
            LastCount = buffer.Length;
            if (Failure is not null)
            {
                throw Failure;
            }

            if (PrematureAt is { } end && fileOffset >= end)
            {
                return 0;
            }

            int count = (int)Math.Min(Math.Min(buffer.Length, MaxReadSize), Length - fileOffset);
            LargeFileReadPattern.Fill(buffer[..count], fileOffset, Length, blocks: false);
            return count;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, long fileOffset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancellationFailure is { } failure)
            {
                _cancellationRegistration = cancellationToken.Register(
                    static state => throw (IOException)(state ?? throw new InvalidOperationException()),
                    failure);
            }

            return HoldReads
                ? ReadHeldAsync(buffer, fileOffset, cancellationToken)
                : new(Read(buffer.Span, fileOffset));
        }

        private async ValueTask<int> ReadHeldAsync(Memory<byte> buffer, long offset, CancellationToken token)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(RespectCancellation ? token : CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false);

            return Read(buffer.Span, offset);
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            _cancellationRegistration.Dispose();
            Handle.Dispose();
            if (DisposeFailure is not null)
            {
                throw DisposeFailure;
            }
        }
    }
}
#endif
