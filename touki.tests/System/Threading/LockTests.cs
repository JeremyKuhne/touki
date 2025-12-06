// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading;

public static class LockTests
{
#pragma warning disable CS9216 // casting Lock to object
    [Fact]
    public static void LockStatementWithLockVsMonitor()
    {
        Lock lockObj = new();
        lock (lockObj)
        {
            Assert.True(lockObj.IsHeldByCurrentThread);
            Assert.False(Monitor.IsEntered(lockObj));
        }

        lock ((object)lockObj)
        {
            Assert.False(lockObj.IsHeldByCurrentThread);
            Assert.True(Monitor.IsEntered(lockObj));
        }

        LockOnTWhereTIsLock(lockObj);

        static void LockOnTWhereTIsLock<T>(T lockObj) where T : class
        {
            Assert.IsType<Lock>(lockObj);
            lock (lockObj)
            {
                Assert.False(((Lock)(object)lockObj).IsHeldByCurrentThread);
                Assert.True(Monitor.IsEntered(lockObj));
            }
        }
    }
#pragma warning restore CS9216

    // Attempts a single recursive acquisition/release cycle of a newly-created lock.
    [Fact]
    public static void BasicRecursion()
    {
        Lock lockObj = new();
        Assert.True(lockObj.TryEnter());
        Assert.True(lockObj.TryEnter());
        lockObj.Exit();
        Assert.True(lockObj.IsHeldByCurrentThread);
        lockObj.Enter();
        Assert.True(lockObj.IsHeldByCurrentThread);
        lockObj.Exit();

        using (lockObj.EnterScope())
        {
            Assert.True(lockObj.IsHeldByCurrentThread);
        }

        lock (lockObj)
        {
            Assert.True(lockObj.IsHeldByCurrentThread);
        }

        Assert.True(lockObj.IsHeldByCurrentThread);
        lockObj.Exit();
        Assert.False(lockObj.IsHeldByCurrentThread);
    }

    // Attempts to overflow the recursion count of a newly-created lock.
    [Fact]
    public static void DeepRecursion()
    {
        Lock lockObj = new();
        const int successLimit = 10000;

        int i = 0;
        for (; i < successLimit; i++)
        {
            Assert.True(lockObj.TryEnter());
        }

        for (; i > 1; i--)
        {
            lockObj.Exit();
            Assert.True(lockObj.IsHeldByCurrentThread);
        }

        lockObj.Exit();
        Assert.False(lockObj.IsHeldByCurrentThread);
    }

    [Fact]
    public static void IsHeldByCurrentThread()
    {
        Lock lockObj = new();
        Assert.False(lockObj.IsHeldByCurrentThread);
        using (lockObj.EnterScope())
        {
            Assert.True(lockObj.IsHeldByCurrentThread);
        }
        lock (lockObj)
        {
            Assert.True(lockObj.IsHeldByCurrentThread);
        }
        Assert.False(lockObj.IsHeldByCurrentThread);
    }

    [Fact]
    public static void IsHeldByCurrentThread_WhenHeldBySomeoneElse()
    {
        Lock lockObj = new();
        var b = new Barrier(2);

        Task t = Task.Run(() =>
        {
            using (lockObj.EnterScope())
            {
                b.SignalAndWait(TestContext.Current.CancellationToken);
                Assert.True(lockObj.IsHeldByCurrentThread);
                b.SignalAndWait(TestContext.Current.CancellationToken);
            }
        },
        TestContext.Current.CancellationToken);

        b.SignalAndWait(TestContext.Current.CancellationToken);
        Assert.False(lockObj.IsHeldByCurrentThread);
        b.SignalAndWait(TestContext.Current.CancellationToken);

#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
        t.Wait(TestContext.Current.CancellationToken);
#pragma warning restore xUnit1031
    }

    [Fact]
    public static void Exit_Invalid()
    {
        Lock lockObj = new();
        Assert.Throws<SynchronizationLockException>(lockObj.Exit);
        default(Lock.Scope).Dispose();
    }

    [Fact]
    public static void Exit_WhenHeldBySomeoneElse_ThrowsSynchronizationLockException()
    {
        Lock lockObj = new();
        var b = new Barrier(2);

        Lock.Scope lockScopeCopy;
        using (Lock.Scope lockScope = lockObj.EnterScope())
        {
            lockScopeCopy = lockScope;
        }

        Task t = Task.Run(() =>
        {
            using (lockObj.EnterScope())
            {
                b.SignalAndWait(TestContext.Current.CancellationToken);
                b.SignalAndWait(TestContext.Current.CancellationToken);
            }
        },
        TestContext.Current.CancellationToken);

        b.SignalAndWait(TestContext.Current.CancellationToken);

        Assert.Throws<SynchronizationLockException>(lockObj.Exit);

        try
        {
            // Can't use Assert.Throws because lockScopeCopy is a ref struct local that can't be captured by a lambda
            // expression
            lockScopeCopy.Dispose();
            Assert.Fail("Expected SynchronizationLockException but did not get an exception.");
        }
        catch (SynchronizationLockException)
        {
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected SynchronizationLockException but got a different exception instead: {ex}");
        }

        b.SignalAndWait(TestContext.Current.CancellationToken);
#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
        t.Wait(TestContext.Current.CancellationToken);
#pragma warning restore xUnit1031
    }

    [Fact]
    public static void TryEnter_Invalid()
    {
        Lock lockObj = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => lockObj.TryEnter(-2));
        Assert.Throws<ArgumentOutOfRangeException>(() => lockObj.TryEnter(TimeSpan.FromMilliseconds(-2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => lockObj.TryEnter(TimeSpan.FromMilliseconds((double)int.MaxValue + 1)));
    }

    [Fact]
    public static void Enter_HasToWait()
    {
        Lock lockObj = new();

        // When the current thread has the lock, have background threads wait for the lock in various ways. After a short
        // duration, release the lock and allow the background threads to acquire the lock.
        {
            var backgroundTestDelegates = new List<Action>();
            Barrier? readyBarrier = null;

            backgroundTestDelegates.Add(() =>
            {
                readyBarrier!.SignalAndWait();
                lockObj.Enter();
                lockObj.Exit();
            });

            backgroundTestDelegates.Add(() =>
            {
                readyBarrier!.SignalAndWait();
                using (lockObj.EnterScope())
                {
                }
            });

            backgroundTestDelegates.Add(() =>
            {
                readyBarrier!.SignalAndWait();
                Assert.True(lockObj.TryEnter(ThreadTestHelpers.UnexpectedTimeoutMilliseconds));
                lockObj.Exit();
            });

            backgroundTestDelegates.Add(() =>
            {
                readyBarrier!.SignalAndWait();
                Assert.True(lockObj.TryEnter(TimeSpan.FromMilliseconds(ThreadTestHelpers.UnexpectedTimeoutMilliseconds)));
                lockObj.Exit();
            });

            int testCount = backgroundTestDelegates.Count;
            readyBarrier = new Barrier(testCount + 1); // plus main thread
            var waitForThreadArray = new Action[testCount];
            for (int i = 0; i < backgroundTestDelegates.Count; ++i)
            {
                int icopy = i; // for use in delegates
                Thread t =
                    ThreadTestHelpers.CreateGuardedThread(out waitForThreadArray[i],
                        () => backgroundTestDelegates[icopy]());
                t.IsBackground = true;
                t.Start();
            }

            using (lockObj.EnterScope())
            {
                readyBarrier.SignalAndWait(ThreadTestHelpers.UnexpectedTimeoutMilliseconds, TestContext.Current.CancellationToken);
                Thread.Sleep(ThreadTestHelpers.ExpectedTimeoutMilliseconds);
            }
            foreach (Action waitForThread in waitForThreadArray)
                waitForThread();
        }

        // When the current thread has the lock, have background threads wait for the lock in various ways and time out
        // after a short duration
        {
            var backgroundTestDelegates = new List<Action>();
            Barrier? readyBarrier = null;

            backgroundTestDelegates.Add(() =>
            {
                readyBarrier!.SignalAndWait();
                Assert.False(lockObj.TryEnter(ThreadTestHelpers.ExpectedTimeoutMilliseconds));
            });

            backgroundTestDelegates.Add(() =>
            {
                readyBarrier!.SignalAndWait();
                Assert.False(lockObj.TryEnter(TimeSpan.FromMilliseconds(ThreadTestHelpers.ExpectedTimeoutMilliseconds)));
            });

            int testCount = backgroundTestDelegates.Count;
            readyBarrier = new Barrier(testCount + 1); // plus main thread
            var waitForThreadArray = new Action[testCount];
            for (int i = 0; i < backgroundTestDelegates.Count; ++i)
            {
                int icopy = i; // for use in delegates
                Thread t =
                    ThreadTestHelpers.CreateGuardedThread(out waitForThreadArray[i],
                        () => backgroundTestDelegates[icopy]());
                t.IsBackground = true;
                t.Start();
            }

            using (lockObj.EnterScope())
            {
                readyBarrier.SignalAndWait(ThreadTestHelpers.UnexpectedTimeoutMilliseconds, TestContext.Current.CancellationToken);
                foreach (Action waitForThread in waitForThreadArray)
                    waitForThread();
            }
        }
    }

#if NETFRAMEWORK
    [Fact]
    public static void UseTrivialWaits_Constructor()
    {
        // Test that the constructor with useTrivialWaits parameter works correctly
        Lock lockWithTrivialWaits = new(true);
        Lock lockWithoutTrivialWaits = new(false);

        Assert.True(lockWithTrivialWaits.TryEnter());
        lockWithTrivialWaits.Exit();

        Assert.True(lockWithoutTrivialWaits.TryEnter());
        lockWithoutTrivialWaits.Exit();
    }
#endif

    [Fact]
    public static void ContentionCount_IncreasesUnderContention()
    {
        Lock lockObj = new();
        long initialCount = typeof(Lock).TestAccessor.Dynamic.ContentionCount;

        const int threadCount = 5;
        var barrier = new Barrier(threadCount + 1);
        var tasks = new Task[threadCount];

        // First thread will get the lock
        using (lockObj.EnterScope())
        {
            // Start multiple threads that will try to acquire the lock
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait(); // Wait for all threads to be ready
                    lockObj.TryEnter(50); // Try to enter with timeout - will contend with main thread
                    barrier.SignalAndWait(); // Signal completion
                },
                TestContext.Current.CancellationToken);
            }

            barrier.SignalAndWait(TestContext.Current.CancellationToken); // Let all threads try to acquire
            Thread.Sleep(100); // Hold lock while other threads try to acquire it
        }

        barrier.SignalAndWait(TestContext.Current.CancellationToken); // Wait for all threads to complete

#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
        Task.WaitAll(tasks, TestContext.Current.CancellationToken);
#pragma warning restore xUnit1031

        // Contention count should have increased
        Assert.True(typeof(Lock).TestAccessor.Dynamic.ContentionCount > initialCount);
    }

    [Fact]
    public static void TryEnter_Timeout_Precision()
    {
        Lock lockObj = new();
        ManualResetEventSlim backgroundTaskStarted = new(false);

        // First, acquire the lock on this thread
        using (lockObj.EnterScope())
        {
            lockObj.IsHeldByCurrentThread.Should().BeTrue();

            // Then try to acquire it with a timeout from another thread
            Task<(bool, TimeSpan)> durationTask = Task.Run(() =>
            {
                // Signal that the background thread has started and is about to attempt to enter the lock.
                backgroundTaskStarted.Set();

                DateTime start = DateTime.UtcNow;
                const int timeoutMs = 100;
                bool success = lockObj.TryEnter(timeoutMs);
                TimeSpan duration = DateTime.UtcNow - start;
                return (success, duration);
            });

            // Wait here until the background task has signaled that it's running.
            // This guarantees the lock is still held when TryEnter is called.
            backgroundTaskStarted.Wait(TestContext.Current.CancellationToken);

#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
            (bool success, TimeSpan duration) = durationTask.Result;
#pragma warning restore xUnit1031

            success.Should().BeFalse();

            // Verify that the timeout is reasonably close to what was requested
            // Allow some margin for timing variations
            TimeSpan expectedMin = TimeSpan.FromMilliseconds(50);  // Lower bound
            TimeSpan expectedMax = TimeSpan.FromMilliseconds(500); // Upper bound with significant margin

            duration.Should().BeGreaterThanOrEqualTo(expectedMin);
            duration.Should().BeLessThanOrEqualTo(expectedMax);
        }
    }

    [Fact]
    public static void EnterExit_Multiple_Threads_Fairness()
    {
        const int iterations = 100;
        const int threadCount = 5;

        Lock lockObj = new();
        int sharedCounter = 0;
        int[] threadAcquisitions = new int[threadCount];

        var tasks = new Task[threadCount];
        var ready = new CountdownEvent(threadCount);
        var start = new ManualResetEventSlim(false);

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t; // Capture for lambda
            tasks[t] = Task.Run(() =>
            {
                ready.Signal();
                start.Wait();

                for (int i = 0; i < iterations; i++)
                {
                    using (lockObj.EnterScope())
                    {
                        sharedCounter++;
                        threadAcquisitions[threadId]++;
                        Thread.Sleep(1); // Small delay to increase chance of contention
                    }

                    Thread.Yield(); // Give other threads a chance
                }
            },
            TestContext.Current.CancellationToken);
        }

        // Wait for all threads to be ready
        ready.Wait(TestContext.Current.CancellationToken);
        // Start all threads simultaneously
        start.Set();

#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
        Task.WaitAll(tasks, TestContext.Current.CancellationToken);
#pragma warning restore xUnit1031

        // Verify total operations
        Assert.Equal(iterations * threadCount, sharedCounter);

        // Check for reasonable distribution of lock acquisitions
        // Each thread should have gotten approximately the same number
        foreach (int acquisitions in threadAcquisitions)
        {
            Assert.Equal(iterations, acquisitions);
        }
    }
}
