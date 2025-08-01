// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading;

public static class ThreadTestHelpers
{
    public const int ExpectedTimeoutMilliseconds = 50;
    public const int UnexpectedTimeoutMilliseconds = 1000 * 60;

    // Wait longer for a thread to time out, so that an unexpected timeout in the thread is more likely to expire first and
    // provide a better stack trace for the failure
    public static readonly int s_unexpectedThreadTimeoutMilliseconds =
        UnexpectedTimeoutMilliseconds + /* RemoteExecutor.FailWaitTimeoutMilliseconds */ (60 * 1000);

    public static Thread CreateGuardedThread(out Action waitForThread, Action start) =>
        CreateGuardedThread(out Action checkForThreadErrors, out waitForThread, start);

    public static Thread CreateGuardedThread(out Action checkForThreadErrors, out Action waitForThread, Action start)
    {
        Exception? backgroundEx = null;
        var t =
            new Thread(() =>
            {
                try
                {
                    start();
                }
                catch (Exception ex)
                {
                    backgroundEx = ex;
                    Interlocked.MemoryBarrier();
                }
            });
        Action localCheckForThreadErrors = checkForThreadErrors = // cannot use ref or out parameters in lambda
            () =>
            {
                Interlocked.MemoryBarrier();
                if (backgroundEx != null)
                {
                    throw new AggregateException(backgroundEx);
                }
            };
        waitForThread =
            () =>
            {
                Assert.True(t.Join(s_unexpectedThreadTimeoutMilliseconds));
                localCheckForThreadErrors();
            };
        return t;
    }
}

