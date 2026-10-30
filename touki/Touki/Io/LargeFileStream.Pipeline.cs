// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Threading;
using System.Threading.Tasks;

namespace Touki.Io;

public sealed partial class LargeFileStream
{
    private Memory<byte> ActiveBuffer => _active?.Buffer
        ?? throw new InvalidOperationException("No window is active.");

    private bool ContainsPosition() =>
        _active is not null && _position >= _activeOffset && _position - _activeOffset < _activeLength;

    private bool CanBypass(int requested) => !ContainsPosition()
        && (Options.ChunkSize == 0 || requested >= _windowSize);

    private void InitializeConsumerSlots()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] ??= CreateSlot();
        }
    }

    private LargeFileBufferSlot ReserveSlot()
    {
        _slots[0] ??= CreateSlot();
        foreach (LargeFileBufferSlot? slot in _slots)
        {
            if (slot is not null && slot.TryReserve())
            {
                return slot;
            }
        }

        throw new InvalidOperationException("The I/O and consumer slot budgets are inconsistent.");
    }

    private LargeFileBufferSlot CreateSlot()
    {
        LargeFileBufferSlot slot = new(_windowSize, _slotReturned);
        Statistics.AllocatedSlot(_windowSize);
        return slot;
    }

    private void DiscardWindow()
    {
        _active?.ReleaseReservation();
        _active = null;
        _activeLength = 0;
    }

    private void PrepareWindow()
    {
        DiscardWindow();
        _active = ReserveSlot();
        _activeOffset = Options.AccessPattern == LargeFileAccessPattern.Sparse
            ? _position / _windowSize * _windowSize
            : _position;

        _activeLength = 0;
    }

    private void EnsureWindow()
    {
        if (ContainsPosition())
        {
            return;
        }

        PrepareWindow();
        int target = (int)Math.Min(_windowSize, Length - _activeOffset);
        while (_activeLength < target)
        {
            int count = ReadBackend(
                ActiveBuffer[_activeLength..target],
                _activeOffset + _activeLength,
                _lifetime.Token);

            _activeLength += count;
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask EnsureWindowAsync(CancellationToken token)
    {
        if (ContainsPosition())
        {
            return;
        }

        PrepareWindow();
        int target = (int)Math.Min(_windowSize, Length - _activeOffset);
        while (_activeLength < target)
        {
            int count = await ConfigureReadAwait(ReadBackendAsync(
                ActiveBuffer[_activeLength..target],
                _activeOffset + _activeLength,
                token));

            _activeLength += count;
        }
    }

    private LargeFileChunk EndChunk() => new(
        slot: null,
        start: 0,
        length: 0,
        _position,
        isLast: true,
        _generation,
        statistics: null);

    private LargeFileChunk DeliverChunk(CancellationToken token)
    {
        lock (_sync)
        {
            ThrowIfClosing();
            token.ThrowIfCancellationRequested();
            LargeFileBufferSlot slot = _active
                ?? throw new InvalidOperationException("No completed window is available.");

            int start = (int)(_position - _activeOffset);
            int count = _activeLength - start;
            LargeFileChunk chunk = slot.Lease(
                start,
                count,
                _position,
                _position == Length - count,
                _generation,
                Statistics);

            _active = null;
            _activeLength = 0;
            Volatile.Write(ref _position, _position + count);
            Statistics.Delivered(count);
            return chunk;
        }
    }

    private void ReadDirect(Span<byte> buffer, long offset)
    {
        DiscardWindow();
        while (!buffer.IsEmpty)
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            long started = Statistics.StartTiming();
            Statistics.BeginRead();
            int count = 0;
            int requested = buffer.Length;
            try
            {
                count = ValidateRead(_backend.Read(buffer, offset), requested, offset);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Fault(exception, offset);
                throw;
            }
            finally
            {
                Statistics.Waited(started);
                Statistics.EndRead(count, requested);
            }

            offset += count;
            buffer = buffer[count..];
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask ReadDirectAsync(Memory<byte> buffer, long offset, CancellationToken token)
    {
        DiscardWindow();
        while (!buffer.IsEmpty)
        {
            int count = await ConfigureReadAwait(ReadBackendAsync(buffer, offset, token));
            offset += count;
            buffer = buffer[count..];
        }
    }

    private int ReadBackend(Memory<byte> buffer, long offset, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long started = Statistics.StartTiming();
        Statistics.BeginRead();
        int count = 0;
        try
        {
            count = ValidateRead(_backend.Read(buffer.Span, offset), buffer.Length, offset);
            return count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Fault(exception, offset);
            throw;
        }
        finally
        {
            Statistics.Waited(started);
            Statistics.EndRead(count, buffer.Length);
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<int> ReadBackendAsync(Memory<byte> buffer, long offset, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long started = Statistics.StartTiming();
        Statistics.BeginRead();
        int count = 0;
        try
        {
            ValueTask<int> pending = _backend.ReadAsync(buffer, offset, token);
            count = ValidateRead(
                await pending.ConfigureAwait(continueOnCapturedContext: false),
                buffer.Length,
                offset);

            return count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Fault(exception, offset);
            throw;
        }
        finally
        {
            Statistics.Waited(started);
            Statistics.EndRead(count, buffer.Length);
        }
    }

    private static int ValidateRead(int count, int requested, long offset)
    {
        if (count <= 0 || count > requested)
        {
            throw new IOException($"Invalid or premature EOF completion at file offset {offset}: {count} bytes.");
        }

        return count;
    }

}
#endif
