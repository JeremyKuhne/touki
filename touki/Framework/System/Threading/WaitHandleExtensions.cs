// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Win32.SafeHandles;
using Touki.Exceptions;
using Touki.Framework.Resources;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace System.Threading;

internal static class WaitHandleExtensions
{
    internal static unsafe bool WaitOneNoCheck(
         this WaitHandle target,
         int millisecondsTimeout,
         bool useTrivialWaits = false,
         object? associatedObject = null)
    {
        Debug.Assert(millisecondsTimeout >= -1);

        // The field value is modifiable via the public <see cref="WaitHandle.SafeWaitHandle"/> property, save it locally
        // to ensure that one instance is used in all places in this method
        SafeWaitHandle? waitHandle = target.SafeWaitHandle;
        ObjectDisposed.ThrowIf(waitHandle is null, target);

        bool success = false;
        try
        {
            waitHandle.DangerousAddRef(ref success);

            WAIT_EVENT waitResult = WAIT_EVENT.WAIT_FAILED;

            // Check if the wait should be forwarded to a SynchronizationContext wait override. Trivial waits don't allow
            // reentrance or interruption, and are not forwarded.
            bool usedSyncContextWait = false;
            if (!useTrivialWaits && SynchronizationContext.Current is { } context && context.IsWaitNotificationRequired())
            {
                usedSyncContextWait = true;
                waitResult = (WAIT_EVENT)context.Wait([waitHandle.DangerousGetHandle()], waitAll: false, millisecondsTimeout);
            }

            if (!usedSyncContextWait)
            {
                waitResult = WaitOneCore((void*)waitHandle.DangerousGetHandle(), millisecondsTimeout);
            }

            return waitResult == WAIT_EVENT.WAIT_ABANDONED ? throw new AbandonedMutexException() : waitResult != WAIT_EVENT.WAIT_TIMEOUT;
        }
        finally
        {
            if (success)
            {
                waitHandle.DangerousRelease();
            }
        }

        static unsafe WAIT_EVENT WaitOneCore(void* handle, int millisecondsTimeout)
        {
            return WaitForMultipleObjectsIgnoringSyncContext(&handle, 1, false, millisecondsTimeout);
        }

        static unsafe WAIT_EVENT WaitForMultipleObjectsIgnoringSyncContext(
            void** pHandles,
            int numHandles,
            bool waitAll,
            int millisecondsTimeout)
        {
            Debug.Assert(millisecondsTimeout >= -1);

            // Normalize waitAll
            if (numHandles == 1)
                waitAll = false;

            Thread currentThread = Thread.CurrentThread;

            // This flag can't be explicitly set.
            // currentThread.SetWaitSleepJoinState();

            WAIT_EVENT result = PInvoke.WaitForMultipleObjectsEx(
                (uint)numHandles,
                (HANDLE*)pHandles,
                waitAll,
                (uint)millisecondsTimeout,
                false);

            // currentThread.ClearWaitSleepJoinState();

            if (result == WAIT_EVENT.WAIT_FAILED)
            {
                WIN32_ERROR errorCode = (WIN32_ERROR)Marshal.GetLastWin32Error();
                if (waitAll && errorCode == WIN32_ERROR.ERROR_INVALID_PARAMETER)
                {
                    // Check for duplicate handles. This is a brute force O(n^2) search, which is intended since the typical
                    // array length is short enough that this would actually be faster than using a hash set. Also, the worst
                    // case is not so bad considering that the array length is limited by
                    // <see cref="WaitHandle.MaxWaitHandles"/>.
                    for (int i = 1; i < numHandles; ++i)
                    {
                        void* handle = pHandles[i];
                        for (int j = 0; j < i; ++j)
                        {
                            if (pHandles[j] == handle)
                            {
                                throw new DuplicateWaitObjectException($"waitHandles[{i}]");
                            }
                        }
                    }
                }

                ThrowWaitFailedException(errorCode);
            }

            return result;
        }

        static void ThrowWaitFailedException(WIN32_ERROR errorCode)
        {
            throw errorCode switch
            {
                WIN32_ERROR.ERROR_INVALID_HANDLE => new InvalidOperationHResultException(HRESULT.E_HANDLE, SRF.InvalidOperation_InvalidHandle),
                WIN32_ERROR.ERROR_INVALID_PARAMETER => new ArgumentException(),
                WIN32_ERROR.ERROR_ACCESS_DENIED => new UnauthorizedAccessException(),
                WIN32_ERROR.ERROR_NOT_ENOUGH_MEMORY => new OutOfMemoryException(),

                // Only applicable to <see cref="WaitHandle.SignalAndWait(WaitHandle, WaitHandle)"/>. Note however, that
                // if the semahpore already has the maximum signal count, the Windows SignalObjectAndWait function does not
                // return an error, but this code is kept for historical reasons and to convey the intent, since ideally,
                // that should be an error.
                WIN32_ERROR.ERROR_TOO_MANY_POSTS => new InvalidOperationException(SRF.Threading_WaitHandleTooManyPosts),

                // Only applicable to <see cref="WaitHandle.SignalAndWait(WaitHandle, WaitHandle)"/> when signaling a mutex
                // that is locked by a different thread. Note that if the mutex is already unlocked, the Windows
                // SignalObjectAndWait function does not return an error.
                WIN32_ERROR.ERROR_NOT_OWNER => new ApplicationException(SRF.Arg_SynchronizationLockException),
                WIN32_ERROR.ERROR_MUTANT_LIMIT_EXCEEDED => new OverflowException(SRF.Overflow_MutexReacquireCount),
                _ => new HResultException(new((int)errorCode)),
            };
        }
    }
}
