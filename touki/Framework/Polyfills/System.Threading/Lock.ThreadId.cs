// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32;

namespace System.Threading;

public sealed partial class Lock
{
    internal partial struct ThreadId
    {
        [ThreadStatic]
#pragma warning disable IDE1006 // Naming Styles
        private static uint t_threadId;
#pragma warning restore IDE1006

        private uint _id;

        public ThreadId(uint id) => _id = id;

        public readonly uint Id => _id;

        public readonly bool IsInitialized => _id != 0;

        public static ThreadId Current_NoInitialize => new ThreadId(t_threadId);

        public void InitializeForCurrentThread()
        {
            Debug.Assert(!IsInitialized);
            Debug.Assert(t_threadId == 0);

            uint id = PInvoke.GetCurrentThreadId();

            if (id == 0)
            {
                id--;
            }

            t_threadId = _id = id;
            Debug.Assert(IsInitialized);
        }
    }
}
