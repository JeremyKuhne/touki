// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Runtime.Versioning;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Microsoft.Win32.SafeHandles;

namespace Touki.Io;

internal sealed partial class RandomAccessFileReadBackend
{
    private readonly Lock _syncReadGate = new();
    private ManualResetEvent? _syncReadEvent;

    [SupportedOSPlatform("windows10.0")]
    private static SafeFileHandle OpenSynchronousHandle(SafeFileHandle original, LargeFileAccessHint hint)
    {
        bool reference = false;
        try
        {
            original.DangerousAddRef(ref reference);
            HANDLE reopened = PInvoke.ReOpenFile(
                (HANDLE)original.DangerousGetHandle(),
                (uint)GENERIC_ACCESS_RIGHTS.GENERIC_READ,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
                hint switch
                {
                    LargeFileAccessHint.Sequential => FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_SEQUENTIAL_SCAN,
                    LargeFileAccessHint.Random => FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_RANDOM_ACCESS,
                    _ => 0
                });

            if (reopened.IsNull || reopened == (HANDLE)(nint)(-1))
            {
                ThrowReadError((WIN32_ERROR)Marshal.GetLastWin32Error());
            }

            return new(reopened, ownsHandle: true);
        }
        finally
        {
            if (reference)
            {
                original.DangerousRelease();
            }
        }
    }

    [SupportedOSPlatform("windows10.0")]
    private unsafe int ReadWindows(Span<byte> buffer, long fileOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
        lock (_syncReadGate)
        {
            ObjectDisposedException.ThrowIf(Disposed || Handle.IsClosed, this);
            if (buffer.IsEmpty)
            {
                return 0;
            }

            ManualResetEvent signal = _syncReadEvent ??= new(initialState: false);
            bool fileReference = false;
            bool eventReference = false;
            try
            {
                Handle.DangerousAddRef(ref fileReference);
                signal.SafeWaitHandle.DangerousAddRef(ref eventReference);
                NativeOverlapped overlapped = new()
                {
                    OffsetLow = unchecked((int)fileOffset),
                    OffsetHigh = (int)(fileOffset >> 32),

                    // The low bit suppresses IOCP delivery: this stack OVERLAPPED is not CLR-owned.
                    EventHandle = signal.SafeWaitHandle.DangerousGetHandle() | 1
                };

                HANDLE handle = (HANDLE)Handle.DangerousGetHandle();
                fixed (byte* pointer = buffer)
                {
                    if (!PInvoke.ReadFile(handle, pointer, (uint)buffer.Length, lpNumberOfBytesRead: null, &overlapped))
                    {
                        WIN32_ERROR error = (WIN32_ERROR)Marshal.GetLastWin32Error();
                        if (error == WIN32_ERROR.ERROR_HANDLE_EOF)
                        {
                            return 0;
                        }

                        if (error != WIN32_ERROR.ERROR_IO_PENDING)
                        {
                            ThrowReadError(error);
                        }

                        uint read;
                        if (!PInvoke.GetOverlappedResult(handle, &overlapped, &read, bWait: true))
                        {
                            error = (WIN32_ERROR)Marshal.GetLastWin32Error();
                            if (error == WIN32_ERROR.ERROR_HANDLE_EOF)
                            {
                                return 0;
                            }

                            ThrowReadError(error);
                        }

                        return checked((int)read);
                    }

                    // ReadFile resets the event itself; a successful return has already published the byte count.
                    return checked((int)overlapped.InternalHigh);
                }
            }
            finally
            {
                if (eventReference)
                {
                    signal.SafeWaitHandle.DangerousRelease();
                }

                if (fileReference)
                {
                    Handle.DangerousRelease();
                }
            }
        }
    }

    private static void ThrowReadError(WIN32_ERROR error)
    {
        string message = new Win32Exception((int)error).Message;
        if (error == WIN32_ERROR.ERROR_ACCESS_DENIED)
        {
            throw new UnauthorizedAccessException(message);
        }

        throw new IOException(message, unchecked((int)(0x80070000u | (uint)error)));
    }

    private void DisposeSyncEvent()
    {
        lock (_syncReadGate)
        {
            _syncReadEvent?.Dispose();
            _syncReadEvent = null;
        }
    }
}
#endif
