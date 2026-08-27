// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading;

public sealed partial class Lock
{
    /// <summary>
    ///  Identifies whether a lock attempt acquired the lock, should spin, or should wait.
    /// </summary>
    private enum TryLockResult
    {
        Locked,
        Spin,
        Wait
    }
}
