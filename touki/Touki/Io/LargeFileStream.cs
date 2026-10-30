// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using Microsoft.Win32.SafeHandles;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Touki.Io;

/// <summary>
///  A read-only, demand-driven stream over an immutable large file, with zero-copy chunk leases.
/// </summary>
/// <remarks>
///  <para>
///   Length is captured at open. Premature EOF faults the reader instead of silently shortening the file.
///   Only one foreground operation is allowed at a time; overlapping calls throw.
///  </para>
///  <para>
///   A chunk consumes the remaining bytes of the current window. Mixing stream and chunk reads never
///   redelivers already consumed bytes. Seeking within the current window reuses its contents.
///  </para>
///  <para>
///   Chunk reads apply backpressure at MaxOutstandingChunks. Dispose leases promptly and do not batch
///   enumeration ahead of processing. Pins retain their consumer slot until released.
///  </para>
/// </remarks>
[SuppressMessage("Design", "TOUKI0012", Justification = "Stream inheritance provides parser compatibility.")]
public sealed partial class LargeFileStream : Stream
{
    private readonly Lock _sync = new();
    private readonly IFileReadBackend _backend;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _chunkPermits;
    private readonly LargeFileBufferSlot?[] _slots;
    private readonly Action _slotReturned;
    private readonly int _windowSize;
    private LargeFileBufferSlot? _active;
    private long _activeOffset;
    private int _activeLength;
    private long _position;
    private long _generation;
    private bool _busy;
    private bool _closing;
    private ExceptionDispatchInfo? _fault;
    private long? _failureFileOffset;
    private TaskCompletionSource? _quiescent;
    private TaskCompletionSource? _disposed;

    private LargeFileStream(IFileReadBackend backend, LargeFileStreamOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(backend.Length);
        _backend = backend;
        Options = options;
        Statistics = new(options.CollectTimings);
        Length = backend.Length;
        _windowSize = options.ChunkSize == 0 ? LargeFileStreamOptions.DefaultChunkSize : options.ChunkSize;
        _chunkPermits = new(options.MaxOutstandingChunks, options.MaxOutstandingChunks);
        _slots = new LargeFileBufferSlot[checked(1 + options.MaxOutstandingChunks)];
        _slotReturned = ReturnConsumerPermit;
    }

    /// <summary>
    ///  Opens an immutable file with an asynchronous handle and the configured access hint.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <param name="options">The configuration, or null for defaults.</param>
    /// <returns>The reader.</returns>
    public static LargeFileStream Open(string path, LargeFileStreamOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        LargeFileStreamOptions resolved = (options ?? new()).Resolve();
        FileOptions hint = resolved.AccessHint switch
        {
            LargeFileAccessHint.Sequential => FileOptions.SequentialScan,
            LargeFileAccessHint.Random => FileOptions.RandomAccess,
            _ => FileOptions.None
        };

        hint.SetFlags(FileOptions.Asynchronous);
        SafeFileHandle handle = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            resolved.Share,
            hint);

        try
        {
            return OpenResolved(new RandomAccessFileReadBackend(handle, ownsHandle: true, resolved), resolved);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>
    ///  Opens an existing asynchronous, readable, cached file handle.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   A borrowed handle must remain open until the stream has been disposed. Access hints and sharing
    ///   cannot be changed on an existing handle. Ownership transfers after successful backend validation.
    ///  </para>
    /// </remarks>
    /// <param name="handle">The open asynchronous handle.</param>
    /// <param name="ownsHandle">Whether the stream closes the handle on disposal.</param>
    /// <param name="options">The configuration, or null for defaults.</param>
    /// <returns>The reader.</returns>
    public static LargeFileStream Open(
        SafeFileHandle handle,
        bool ownsHandle,
        LargeFileStreamOptions? options = null)
    {
        LargeFileStreamOptions resolved = (options ?? new()).Resolve();
        return OpenResolved(new RandomAccessFileReadBackend(handle, ownsHandle, resolved), resolved);
    }

    /// <summary>
    ///  Creates a reader over the deterministic backend seam, transferring ownership after validation.
    /// </summary>
    /// <param name="backend">The backend to own.</param>
    /// <param name="options">The configuration, or null for defaults.</param>
    /// <returns>The reader.</returns>
    internal static LargeFileStream Open(IFileReadBackend backend, LargeFileStreamOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        return OpenResolved(backend, (options ?? new()).Resolve());
    }

    private static LargeFileStream OpenResolved(IFileReadBackend backend, LargeFileStreamOptions options)
    {
        try
        {
            return new(backend, options);
        }
        catch
        {
            backend.Dispose();
            throw;
        }
    }

    /// <summary>
    ///  Gets the effective, immutable options.
    /// </summary>
    public LargeFileStreamOptions Options { get; }

    /// <summary>
    ///  Gets accounting snapshots that remain accessible after disposal.
    /// </summary>
    public LargeFileStreamStatistics Statistics { get; }

    /// <summary>
    ///  Gets the backend handle. Do not change its mode, cursor, or lifetime while the stream uses it.
    /// </summary>
    public SafeFileHandle SafeFileHandle => _backend.Handle;

    /// <inheritdoc/>
    public override bool CanRead
    {
        get
        {
            lock (_sync)
            {
                return !_closing;
            }
        }
    }

    /// <inheritdoc/>
    public override bool CanSeek => CanRead;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length { get; }

    /// <inheritdoc/>
    public override long Position
    {
        get => Volatile.Read(ref _position);
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <summary>
    ///  Gets the first backend failure's offset.
    /// </summary>
    internal long? FailureFileOffset => _failureFileOffset;

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        Enter();
        bool completed = false;
        try
        {
            if (buffer.IsEmpty || _position >= Length)
            {
                CompleteRead(0);
                completed = true;
                return 0;
            }

            int count;
            if (CanBypass(buffer.Length))
            {
                count = (int)Math.Min(buffer.Length, Length - _position);
                ReadDirect(buffer[..count], _position);
            }
            else
            {
                EnsureWindow();
                int start = (int)(_position - _activeOffset);
                count = Math.Min(buffer.Length, _activeLength - start);
                ActiveBuffer.Span.Slice(start, count).CopyTo(buffer);
            }

            CompleteRead(count);
            completed = true;
            return count;
        }
        catch (OperationCanceledException)
        {
            CancelWindow();
            ThrowCallCancellation(CancellationToken.None);
            throw;
        }
        finally
        {
            if (!completed)
            {
                Exit();
            }
        }
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using CancellationTokenSource? linked = Link(cancellationToken);
            CancellationToken token = linked?.Token ?? _lifetime.Token;
            if (buffer.IsEmpty || _position >= Length)
            {
                CheckCommit(cancellationToken);
                return 0;
            }

            int count;
            if (CanBypass(buffer.Length))
            {
                count = (int)Math.Min(buffer.Length, Length - _position);
                await ConfigureReadAwait(ReadDirectAsync(buffer[..count], _position, token));
            }
            else
            {
                await ConfigureReadAwait(EnsureWindowAsync(token));
                int start = (int)(_position - _activeOffset);
                count = Math.Min(buffer.Length, _activeLength - start);
                ActiveBuffer.Slice(start, count).CopyTo(buffer);
            }

            Commit(count, cancellationToken);
            return count;
        }
        catch (OperationCanceledException)
        {
            CancelWindow();
            ThrowCallCancellation(cancellationToken);
            throw;
        }
        finally
        {
            Exit();
        }
    }

    /// <inheritdoc/>
    public override int ReadByte()
    {
        Span<byte> value = stackalloc byte[1];
        return Read(value) == 0 ? -1 : value[0];
    }

    /// <summary>
    ///  Consumes a window, blocking for I/O or for the configured consumer budget.
    /// </summary>
    /// <returns>The remaining window, or an empty lease at EOF.</returns>
    public LargeFileChunk ReadChunk()
    {
        Enter();
        bool permit = false;
        try
        {
            if (_position >= Length)
            {
                CheckCommit(CancellationToken.None);
                return EndChunk();
            }

            _chunkPermits.Wait(_lifetime.Token);
            permit = true;
            InitializeConsumerSlots();
            EnsureWindow();
            LargeFileChunk chunk = DeliverChunk(CancellationToken.None);
            permit = false;
            return chunk;
        }
        catch (OperationCanceledException)
        {
            CancelWindow();
            ThrowCallCancellation(CancellationToken.None);
            throw;
        }
        finally
        {
            if (permit)
            {
                ReturnConsumerPermit();
            }

            Exit();
        }
    }

    /// <summary>
    ///  Consumes a window, asynchronously waiting for I/O or for the configured consumer budget.
    /// </summary>
    /// <param name="cancellationToken">The token governing this call, including an otherwise empty read.</param>
    /// <returns>The remaining window, or an empty lease at EOF.</returns>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<LargeFileChunk> ReadChunkAsync(CancellationToken cancellationToken = default)
    {
        Enter();
        bool permit = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using CancellationTokenSource? linked = Link(cancellationToken);
            CancellationToken token = linked?.Token ?? _lifetime.Token;
            if (_position >= Length)
            {
                CheckCommit(cancellationToken);
                return EndChunk();
            }

            await _chunkPermits.WaitAsync(token).ConfigureAwait(continueOnCapturedContext: false);
            permit = true;
            InitializeConsumerSlots();
            await ConfigureReadAwait(EnsureWindowAsync(token));
            LargeFileChunk chunk = DeliverChunk(cancellationToken);
            permit = false;
            return chunk;
        }
        catch (OperationCanceledException)
        {
            CancelWindow();
            ThrowCallCancellation(cancellationToken);
            throw;
        }
        finally
        {
            if (permit)
            {
                ReturnConsumerPermit();
            }

            Exit();
        }
    }

    /// <summary>
    ///  Consumes an already-filled window if a consumer permit is available, without submitting I/O.
    /// </summary>
    /// <param name="chunk">The completed lease, if one is available.</param>
    /// <returns>Whether a completed chunk was delivered.</returns>
    public bool TryReadChunk([NotNullWhen(returnValue: true)] out LargeFileChunk? chunk)
    {
        Enter();
        bool permit = false;
        try
        {
            chunk = null;
            if (_position >= Length || !ContainsPosition() || !_chunkPermits.Wait(0))
            {
                return false;
            }

            permit = true;
            InitializeConsumerSlots();
            chunk = DeliverChunk(CancellationToken.None);
            permit = false;
            return true;
        }
        finally
        {
            if (permit)
            {
                ReturnConsumerPermit();
            }

            Exit();
        }
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        lock (_sync)
        {
            EnterCore();
            try
            {
                long position;
                try
                {
                    position = origin switch
                    {
                        SeekOrigin.Begin => offset,
                        SeekOrigin.Current => checked(_position + offset),
                        SeekOrigin.End => checked(Length + offset),
                        _ => throw new ArgumentException("Invalid seek origin.", nameof(origin))
                    };
                }
                catch (OverflowException exception)
                {
                    throw new IOException("The seek position overflowed.", exception);
                }

                if (position < 0)
                {
                    throw new IOException("Cannot seek before the beginning of the file.");
                }

                _generation++;
                Volatile.Write(ref _position, position);
                if (!ContainsPosition())
                {
                    DiscardWindow();
                }

                return position;
            }
            finally
            {
                _busy = false;
                _quiescent?.TrySetResult();
            }
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        lock (_sync)
        {
            ThrowIfClosing();
        }
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Flush();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override void CopyTo(Stream destination, int bufferSize) =>
        base.CopyTo(destination, Math.Max(ValidateCopyBufferSize(bufferSize), _windowSize));

    /// <inheritdoc/>
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
        base.CopyToAsync(destination, Math.Max(ValidateCopyBufferSize(bufferSize), _windowSize), cancellationToken);

    private static int ValidateCopyBufferSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        return size;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException("The stream is read-only.");

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The stream is read-only.");

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException("The stream is read-only.");

    /// <inheritdoc/>
    public override void WriteByte(byte value) => throw new NotSupportedException("The stream is read-only.");

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException("The stream is read-only."));

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromException(new NotSupportedException("The stream is read-only."));

    private void Enter()
    {
        lock (_sync)
        {
            EnterCore();
        }
    }

    private void EnterCore()
    {
        ThrowIfClosing();
        _fault?.Throw();
        if (_busy)
        {
            throw new InvalidOperationException("Another foreground operation is already in progress.");
        }

        _busy = true;
    }

    private void Exit()
    {
        lock (_sync)
        {
            _busy = false;
            _quiescent?.TrySetResult();
        }
    }

    private void ThrowIfClosing() => ObjectDisposedException.ThrowIf(_closing, this);

    private void CheckCommit(CancellationToken token)
    {
        lock (_sync)
        {
            ThrowIfClosing();
            token.ThrowIfCancellationRequested();
        }
    }

    private void Commit(int count, CancellationToken token)
    {
        lock (_sync)
        {
            ThrowIfClosing();
            token.ThrowIfCancellationRequested();
            Volatile.Write(ref _position, _position + count);
            Statistics.Delivered(count);
        }
    }

    private void CompleteRead(int count)
    {
        lock (_sync)
        {
            ThrowIfClosing();
            Volatile.Write(ref _position, _position + count);
            Statistics.Delivered(count);
            _busy = false;
            _quiescent?.TrySetResult();
        }
    }

    private CancellationTokenSource? Link(CancellationToken caller) => caller.CanBeCanceled
        ? CancellationTokenSource.CreateLinkedTokenSource(caller, _lifetime.Token)
        : null;

    private void ThrowCallCancellation(CancellationToken caller)
    {
        lock (_sync)
        {
            ThrowIfClosing();
        }

        caller.ThrowIfCancellationRequested();
    }

    private void CancelWindow()
    {
        _generation++;
        DiscardWindow();
    }

    private void Fault(Exception exception, long offset)
    {
        lock (_sync)
        {
            if (_fault is null)
            {
                _fault = ExceptionDispatchInfo.Capture(exception);
                _failureFileOffset = offset;
            }
        }
    }

    private void ReturnConsumerPermit()
    {
        lock (_sync)
        {
            if (!_closing)
            {
                _chunkPermits.Release();
            }
        }
    }

    private Task BeginDispose()
    {
        Task wait;
        TaskCompletionSource completion;
        lock (_sync)
        {
            if (_disposed is not null)
            {
                return _disposed.Task;
            }

            _closing = true;
            completion = _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_busy)
            {
                _quiescent = new(TaskCreationOptions.RunContinuationsAsynchronously);
                wait = _quiescent.Task;
            }
            else
            {
                wait = Task.CompletedTask;
            }
        }

        try
        {
            _lifetime.Cancel();
        }
        catch (AggregateException exception)
        {
            Statistics.RecordCleanupFailure(exception);
        }

        _ = DrainAndCloseAsync(wait, completion);
        return completion.Task;
    }

    private async Task DrainAndCloseAsync(Task wait, TaskCompletionSource completion)
    {
        try
        {
            await wait.ConfigureAwait(continueOnCapturedContext: false);
            foreach (LargeFileBufferSlot? slot in _slots)
            {
                slot?.ReleaseStream();
            }

            _active = null;
            try
            {
                _backend.Dispose();
            }
            catch (IOException exception)
            {
                Statistics.RecordCleanupFailure(exception);
            }
            finally
            {
                _chunkPermits.Dispose();
                _lifetime.Dispose();
            }

            GC.SuppressFinalize(this);
            completion.SetResult();
        }
        catch (Exception exception)
        {
            completion.SetException(exception);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            BeginDispose().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override ValueTask DisposeAsync() => new(BeginDispose());
}
#endif
