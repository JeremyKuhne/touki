// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using Microsoft.Win32.SafeHandles;
using System.Threading;
using System.Threading.Tasks;

namespace Touki.Io;

/// <summary>
///  Implements the modern offset-based file primitive.
/// </summary>
internal sealed partial class RandomAccessFileReadBackend : DisposableBase, IFileReadBackend
{
    private readonly bool _ownsHandle;
    private readonly SafeFileHandle? _synchronousHandle;

    /// <summary>
    ///  Captures the length of an asynchronous handle, taking ownership only when requested.
    /// </summary>
    /// <param name="handle">The open asynchronous handle.</param>
    /// <param name="ownsHandle">Whether disposal closes the handle.</param>
    /// <param name="options">The effective configuration, or null for a single handle.</param>
    internal RandomAccessFileReadBackend(
        SafeFileHandle handle,
        bool ownsHandle,
        LargeFileStreamOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.IsClosed || handle.IsInvalid || !handle.IsAsync)
        {
            throw new ArgumentException("An open asynchronous file handle is required.", nameof(handle));
        }

        Length = RandomAccess.GetLength(handle);
        if (options?.UseSeparateSynchronousHandle == true)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10))
            {
                throw new PlatformNotSupportedException("A separate synchronous handle requires Windows 10 or later.");
            }

            _synchronousHandle = OpenSynchronousHandle(handle, options.AccessHint);
        }

        Handle = handle;
        _ownsHandle = ownsHandle;
    }

    /// <inheritdoc/>
    public SafeFileHandle Handle { get; }

    /// <inheritdoc/>
    public long Length { get; }

    /// <inheritdoc/>
    public int Read(Span<byte> buffer, long fileOffset)
    {
        ObjectDisposedException.ThrowIf(Disposed || Handle.IsClosed, this);
        if (_synchronousHandle is not null)
        {
            return RandomAccess.Read(_synchronousHandle, buffer, fileOffset);
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            return ReadWindows(buffer, fileOffset);
        }

        return RandomAccess.Read(Handle, buffer, fileOffset);
    }

    /// <inheritdoc/>
    public ValueTask<int> ReadAsync(Memory<byte> buffer, long fileOffset, CancellationToken cancellationToken) =>
        RandomAccess.ReadAsync(Handle, buffer, fileOffset, cancellationToken);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeSyncEvent();
            _synchronousHandle?.Dispose();

            if (_ownsHandle)
            {
                Handle.Dispose();
            }
        }
    }
}
#endif
