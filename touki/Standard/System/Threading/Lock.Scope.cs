// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading;

public sealed partial class Lock
{
    /// <summary>
    /// A disposable structure that is returned by <see cref="EnterScope()"/>, which when disposed, exits the lock.
    /// </summary>
    public ref struct Scope
    {
        private Lock? _lockObj;
        private ThreadId _currentThreadId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Scope(Lock lockObj, ThreadId currentThreadId)
        {
            _lockObj = lockObj;
            _currentThreadId = currentThreadId;
        }

        /// <summary>
        /// Exits the lock.
        /// </summary>
        /// <remarks>
        /// If the calling thread holds the lock multiple times, such as recursively, the lock is exited only once. The
        /// calling thread should ensure that each enter is matched with an exit.
        /// </remarks>
        /// <exception cref="SynchronizationLockException">
        /// The calling thread does not hold the lock.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Lock? lockObj = _lockObj;
            if (lockObj is not null)
            {
                _lockObj = null;
                lockObj.Exit(_currentThreadId);
            }
        }
    }
}
