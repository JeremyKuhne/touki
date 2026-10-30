// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable IDE0005 // Also linked into touki.tests, whose global usings differ.
using System.Diagnostics;
using Touki;
using FileStream = System.IO.FileStream;
#if NET
using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Touki.Io;
#endif
#if NETFRAMEWORK
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
#endif
#pragma warning restore IDE0005

namespace touki.perf;

internal sealed class LargeFileReadReferenceReader : Touki.DisposableBase
{
    private readonly FileStream? _stream;
#if NET
    private readonly LargeFileStream? _candidate;
#endif
    private readonly LargeFileReadReference _reference;
    private readonly byte[]? _window;
    private long _windowOffset;
    private int _windowLength;
    private long _backendBytesRead;
    private long _requestsIssued;

    internal LargeFileReadReferenceReader(string path, LargeFileReadReference reference, LargeFileReadScenario scenario)
    {
        if (!Enum.IsDefined(typeof(LargeFileReadReference), reference))
        {
            throw new ArgumentOutOfRangeException(nameof(reference));
        }

        bool sparse = scenario == LargeFileReadScenario.Sparse;
        _reference = reference;
#if NET
        if (reference is LargeFileReadReference.LargeFileStream
            or LargeFileReadReference.LargeFileStreamSyncTransport
            or LargeFileReadReference.LargeFileStreamDualTransport
            or LargeFileReadReference.LargeFileStreamDualTimedTransport)
        {
            LargeFileStreamOptions options = new()
            {
                AccessPattern = sparse ? LargeFileAccessPattern.Sparse : LargeFileAccessPattern.Sequential,
                ChunkSize = sparse ? 0 : LargeFileReadPattern.WindowSize,
                UseSeparateSynchronousHandle = reference is LargeFileReadReference.LargeFileStreamDualTransport
                    or LargeFileReadReference.LargeFileStreamDualTimedTransport,
                CollectTimings = reference == LargeFileReadReference.LargeFileStreamDualTimedTransport
            };

            _candidate = reference switch
            {
                LargeFileReadReference.LargeFileStreamSyncTransport =>
                    LargeFileStream.Open(new SynchronousTransport(path, sparse), options),
                _ => LargeFileStream.Open(path, options)
            };

            Length = _candidate.Length;
            return;
        }
#endif

        FileOptions fileOptions = sparse ? FileOptions.RandomAccess : FileOptions.SequentialScan;
#if NET
        if (reference is LargeFileReadReference.RawAsyncHandle or LargeFileReadReference.TunedAsyncHandle)
        {
            fileOptions.SetFlags(FileOptions.Asynchronous);
        }
#endif

        _stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            reference == LargeFileReadReference.NaiveFileStream ? 4096 : 1,
            fileOptions);

        Length = _stream.Length;
        bool windowed = reference == LargeFileReadReference.TunedFileStream;
#if NET
        windowed |= reference == LargeFileReadReference.TunedAsyncHandle;
#endif

        if (windowed
            && scenario is LargeFileReadScenario.ScriptedSequential or LargeFileReadScenario.BlockScan)
        {
            _window = new byte[LargeFileReadPattern.WindowSize];
        }
    }

    internal long Length { get; }

    internal long BackendBytesRead
    {
        get
        {
#if NET
            if (_candidate is not null)
            {
                return _candidate.Statistics.BackendBytesRead;
            }
#endif

            return _backendBytesRead;
        }
    }

    internal long RequestsIssued
    {
        get
        {
#if NET
            if (_candidate is not null)
            {
                return _candidate.Statistics.RequestsIssued;
            }
#endif

            return _requestsIssued;
        }
    }

    internal long FirstByteTimestamp { get; private set; }

    internal int Capacity
    {
        get
        {
#if NET
            if (_candidate is not null)
            {
                return _candidate.Statistics.PeakBufferedBytes;
            }
#endif

            return (_window?.Length ?? 0) + (_reference == LargeFileReadReference.NaiveFileStream ? 4096 : 0);
        }
    }

    internal void ReadExactly(long offset, byte[] destination, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, destination.Length);
        if (offset > Length || count > Length - offset)
        {
            throw new EndOfStreamException();
        }

        int copied = 0;
#if NET
        if (_candidate is not null)
        {
            if (_candidate.Position != offset)
            {
                _candidate.Position = offset;
            }

            while (copied < count)
            {
                int read = _candidate.Read(destination.AsSpan(copied, count - copied));
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                copied += read;
                if (FirstByteTimestamp == 0)
                {
                    FirstByteTimestamp = Stopwatch.GetTimestamp();
                }
            }

            return;
        }
#endif

        while (copied < count)
        {
            if (_window is null)
            {
                int read = ReadCore(offset, destination, copied, count - copied);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += read;
                copied += read;
                continue;
            }

            if (offset < _windowOffset || offset - _windowOffset >= _windowLength)
            {
                _windowOffset = offset / _window.Length * _window.Length;
                _windowLength = 0;
                int target = (int)Math.Min(_window.Length, Length - _windowOffset);
                while (_windowLength < target)
                {
                    int read = ReadCore(
                        _windowOffset + _windowLength,
                        _window,
                        _windowLength,
                        target - _windowLength);

                    if (read == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    _windowLength += read;
                }
            }

            int start = (int)(offset - _windowOffset);
            int available = Math.Min(count - copied, _windowLength - start);
            _window.AsSpan(start, available).CopyTo(destination.AsSpan(copied));
            offset += available;
            copied += available;
        }
    }

#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    internal async ValueTask ReadExactlyAsync(
        long offset,
        byte[] destination,
        int count,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, destination.Length);
        if (offset > Length || count > Length - offset)
        {
            throw new EndOfStreamException();
        }

        int copied = 0;
        if (_candidate is not null)
        {
            if (_candidate.Position != offset)
            {
                _candidate.Position = offset;
            }

            while (copied < count)
            {
                int read = await _candidate.ReadAsync(
                    destination.AsMemory(copied, count - copied),
                    cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                copied += read;
                if (FirstByteTimestamp == 0)
                {
                    FirstByteTimestamp = Stopwatch.GetTimestamp();
                }
            }

            return;
        }

        while (copied < count)
        {
            if (_window is null)
            {
                int read = await ReadCoreAsync(offset, destination, copied, count - copied, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += read;
                copied += read;
                continue;
            }

            if (offset < _windowOffset || offset - _windowOffset >= _windowLength)
            {
                _windowOffset = offset / _window.Length * _window.Length;
                _windowLength = 0;
                int target = (int)Math.Min(_window.Length, Length - _windowOffset);
                while (_windowLength < target)
                {
                    int read = await ReadCoreAsync(
                        _windowOffset + _windowLength,
                        _window,
                        _windowLength,
                        target - _windowLength,
                        cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

                    if (read == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    _windowLength += read;
                }
            }

            int start = (int)(offset - _windowOffset);
            int available = Math.Min(count - copied, _windowLength - start);
            _window.AsMemory(start, available).CopyTo(destination.AsMemory(copied));
            offset += available;
            copied += available;
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<int> ReadCoreAsync(
        long offset,
        byte[] buffer,
        int start,
        int count,
        CancellationToken token)
    {
        FileStream stream = _stream ?? throw new InvalidOperationException("No stream reference.");
        _requestsIssued++;
        int read;
        if (_reference is LargeFileReadReference.Raw or LargeFileReadReference.RawAsyncHandle)
        {
            read = await RandomAccess.ReadAsync(stream.SafeFileHandle, buffer.AsMemory(start, count), offset, token)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        else
        {
            if (stream.Position != offset)
            {
                stream.Position = offset;
            }

            read = await stream.ReadAsync(buffer.AsMemory(start, count), token)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        _backendBytesRead += read;
        if (read > 0 && FirstByteTimestamp == 0)
        {
            FirstByteTimestamp = Stopwatch.GetTimestamp();
        }

        return read;
    }
#endif

    private int ReadCore(long offset, byte[] buffer, int start, int count)
    {
        _requestsIssued++;
        int read;
        bool raw = _reference == LargeFileReadReference.Raw;
#if NET
        raw |= _reference == LargeFileReadReference.RawAsyncHandle;
#endif

        if (raw)
        {
#if NETFRAMEWORK
            read = ReadNative(offset, buffer, start, count);
#else
            FileStream stream = _stream ?? throw new InvalidOperationException("No raw reference handle.");
            read = RandomAccess.Read(stream.SafeFileHandle, buffer.AsSpan(start, count), offset);
#endif
        }
        else
        {
            FileStream stream = _stream ?? throw new InvalidOperationException("No stream reference.");
            if (stream.Position != offset)
            {
                stream.Position = offset;
            }

            read = stream.Read(buffer, start, count);
        }

        _backendBytesRead += read;
        if (read > 0 && FirstByteTimestamp == 0)
        {
            FirstByteTimestamp = Stopwatch.GetTimestamp();
        }

        return read;
    }

#if NETFRAMEWORK
    private unsafe int ReadNative(long offset, byte[] buffer, int start, int count)
    {
        NativeOverlapped overlapped = new()
        {
            OffsetLow = unchecked((int)offset),
            OffsetHigh = (int)(offset >> 32)
        };

        FileStream stream = _stream ?? throw new InvalidOperationException("No raw reference handle.");
        uint read;
        fixed (byte* pointer = &buffer[start])
        {
            if (!PInvoke.ReadFile(
                (HANDLE)stream.SafeFileHandle.DangerousGetHandle(),
                pointer,
                (uint)count,
                &read,
                &overlapped))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        return checked((int)read);
    }
#endif

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
#if NET
            _candidate?.Dispose();
#endif

            _stream?.Dispose();
        }
    }

#if NET
    // Diagnostic only: force immediate completions while retaining all production cursor/slot bookkeeping.
    // This blocks inside ReadAsync and is not an implementation of the production asynchronous-I/O contract.
    private sealed class SynchronousTransport : DisposableBase, IFileReadBackend
    {
        internal SynchronousTransport(string path, bool sparse)
        {
            Handle = File.OpenHandle(
                path,
                options: sparse ? FileOptions.RandomAccess : FileOptions.SequentialScan);

            try
            {
                Length = RandomAccess.GetLength(Handle);
            }
            catch
            {
                Handle.Dispose();
                throw;
            }
        }

        public SafeFileHandle Handle { get; }

        public long Length { get; }

        public int Read(Span<byte> buffer, long fileOffset) => RandomAccess.Read(Handle, buffer, fileOffset);

        public ValueTask<int> ReadAsync(Memory<byte> buffer, long fileOffset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(Read(buffer.Span, fileOffset));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Handle.Dispose();
            }
        }
    }
#endif
}
