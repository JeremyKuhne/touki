// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Touki.Framework.Resources;

namespace System.Threading;

/// <summary>
/// Provides a way to get mutual exclusion in regions of code between different threads. A lock may be held by one thread at
/// a time.
/// </summary>
/// <remarks>
/// Threads that cannot immediately enter the lock may wait for the lock to be exited or until a specified timeout. A thread
/// that holds a lock may enter the lock repeatedly without exiting it, such as recursively, in which case the thread should
/// eventually exit the lock the same number of times to fully exit the lock and allow other threads to enter the lock.
/// </remarks>
public sealed partial class Lock
{
    private const short DefaultMaxSpinCount = 22;
    private const short DefaultAdaptiveSpinPeriod = 100;
    private const short SpinSleep0Threshold = 10;
    private const ushort MaxDurationMsForPreemptingWaiters = 100;

    private static long s_contentionCount;

    // The field's type is not ThreadId to try to retain the relative order of fields of intrinsic types. The type system
    // appears to place struct fields after fields of other types, in which case there can be a greater chance that
    // _owningThreadId is not in the same cache line as _state.
    private uint _owningThreadId;

    private uint _state; // see State for layout
    private uint _recursionCount;

    // This field serves a few purposes currently:
    // - When positive, it indicates the number of spin-wait iterations that most threads would do upon contention
    // - When zero, it indicates that spin-waiting is to be attempted by a thread to test if it is successful
    // - When negative, it serves as a rough counter for contentions that would increment it towards zero
    //
    // See references to this field and "AdaptiveSpin" in TryEnterSlow for more information.
    private short _spinCount;

    private ushort _waiterStartTimeMs;
    private AutoResetEvent? _waitEvent;

    private static readonly short s_maxSpinCount = DefaultMaxSpinCount;
    private static readonly short s_minSpinCountForAdaptiveSpin = -DefaultAdaptiveSpinPeriod;

    internal ulong OwningOSThreadId => _owningThreadId;

#pragma warning disable CA1822 // can be marked as static - varies between runtimes
    internal int OwningManagedThreadId => 0;
#pragma warning restore CA1822

    private static TryLockResult LazyInitializeOrEnter() => TryLockResult.Spin;

    /// <summary>
    ///  Initializes a new instance of the <see cref="Lock"/> class.
    /// </summary>
    public Lock() => _spinCount = s_maxSpinCount;

    /// <summary>
    ///  Initializes a new instance of the <see cref="Lock"/> class, optionally using trivial waits.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Can't artificially set the <see cref="Thread.ThreadState"/> as that is a runtime thing. This is normally
    ///   an internal constructor, but I've made it public so I can use it for other copied internals that are upstack
    ///   (notably Madowaku).
    ///  </para>
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Lock(bool useTrivialWaits) : this()
    {
        State.InitializeUseTrivialWaits(this, useTrivialWaits);
    }

    /// <summary>
    ///  Enters the lock. Once the method returns, the calling thread would be the only thread that holds the lock.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   If the lock cannot be entered immediately, the calling thread waits for the lock to be exited. If the lock is
    ///   already held by the calling thread, the lock is entered again. The calling thread should exit the lock as many times
    ///   as it had entered the lock to fully exit the lock and allow other threads to enter the lock.
    ///  </para>
    /// </remarks>
    /// <exception cref="LockRecursionException">
    ///  The lock has reached the limit of recursive enters. The limit is implementation-defined, but is expected to be high
    ///  enough that it would typically not be reached when the lock is used properly.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Enter()
    {
        ThreadId currentThreadId = TryEnter_Inlined(timeoutMs: -1);
        Debug.Assert(currentThreadId.IsInitialized);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ThreadId EnterAndGetCurrentThreadId()
    {
        ThreadId currentThreadId = TryEnter_Inlined(timeoutMs: -1);
        Debug.Assert(currentThreadId.IsInitialized);
        Debug.Assert(currentThreadId.Id == _owningThreadId);
        return currentThreadId;
    }

    /// <summary>
    /// Enters the lock and returns a <see cref="Scope"/> that may be disposed to exit the lock. Once the method returns,
    /// the calling thread would be the only thread that holds the lock. This method is intended to be used along with a
    /// language construct that would automatically dispose the <see cref="Scope"/>, such as with the C# <code>using</code>
    /// statement.
    /// </summary>
    /// <returns>
    /// A <see cref="Scope"/> that may be disposed to exit the lock.
    /// </returns>
    /// <remarks>
    /// If the lock cannot be entered immediately, the calling thread waits for the lock to be exited. If the lock is
    /// already held by the calling thread, the lock is entered again. The calling thread should exit the lock, such as by
    /// disposing the returned <see cref="Scope"/>, as many times as it had entered the lock to fully exit the lock and
    /// allow other threads to enter the lock.
    /// </remarks>
    /// <exception cref="LockRecursionException">
    /// The lock has reached the limit of recursive enters. The limit is implementation-defined, but is expected to be high
    /// enough that it would typically not be reached when the lock is used properly.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Scope EnterScope() => new Scope(this, EnterAndGetCurrentThreadId());

    /// <summary>
    /// Tries to enter the lock without waiting. If the lock is entered, the calling thread would be the only thread that
    /// holds the lock.
    /// </summary>
    /// <returns>
    /// <code>true</code> if the lock was entered, <code>false</code> otherwise.
    /// </returns>
    /// <remarks>
    /// If the lock cannot be entered immediately, the method returns <code>false</code>. If the lock is already held by the
    /// calling thread, the lock is entered again. The calling thread should exit the lock as many times as it had entered
    /// the lock to fully exit the lock and allow other threads to enter the lock.
    /// </remarks>
    /// <exception cref="LockRecursionException">
    /// The lock has reached the limit of recursive enters. The limit is implementation-defined, but is expected to be high
    /// enough that it would typically not be reached when the lock is used properly.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TryEnter() => TryEnter_Inlined(timeoutMs: 0).IsInitialized;

    /// <summary>
    /// Tries to enter the lock, waiting for roughly the specified duration. If the lock is entered, the calling thread
    /// would be the only thread that holds the lock.
    /// </summary>
    /// <param name="millisecondsTimeout">
    /// The rough duration in milliseconds for which the method will wait if the lock is not available. A value of
    /// <code>0</code> specifies that the method should not wait, and a value of <see cref="Timeout.Infinite"/> or
    /// <code>-1</code> specifies that the method should wait indefinitely until the lock is entered.
    /// </param>
    /// <returns>
    /// <code>true</code> if the lock was entered, <code>false</code> otherwise.
    /// </returns>
    /// <remarks>
    /// If the lock cannot be entered immediately, the calling thread waits for roughly the specified duration for the lock
    /// to be exited. If the lock is already held by the calling thread, the lock is entered again. The calling thread
    /// should exit the lock as many times as it had entered the lock to fully exit the lock and allow other threads to
    /// enter the lock.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="millisecondsTimeout"/> is less than <code>-1</code>.
    /// </exception>
    /// <exception cref="LockRecursionException">
    /// The lock has reached the limit of recursive enters. The limit is implementation-defined, but is expected to be high
    /// enough that it would typically not be reached when the lock is used properly.
    /// </exception>
    public bool TryEnter(int millisecondsTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(millisecondsTimeout, -1);
        return TryEnter_Outlined(millisecondsTimeout);
    }

    /// <summary>
    /// Tries to enter the lock, waiting for roughly the specified duration. If the lock is entered, the calling thread
    /// would be the only thread that holds the lock.
    /// </summary>
    /// <param name="timeout">
    /// The rough duration for which the method will wait if the lock is not available. The timeout is converted to a number
    /// of milliseconds by casting <see cref="TimeSpan.TotalMilliseconds"/> of the timeout to an integer value. A value
    /// representing <code>0</code> milliseconds specifies that the method should not wait, and a value representing
    /// <see cref="Timeout.Infinite"/> or <code>-1</code> milliseconds specifies that the method should wait indefinitely
    /// until the lock is entered.
    /// </param>
    /// <returns>
    /// <code>true</code> if the lock was entered, <code>false</code> otherwise.
    /// </returns>
    /// <remarks>
    /// If the lock cannot be entered immediately, the calling thread waits for roughly the specified duration for the lock
    /// to be exited. If the lock is already held by the calling thread, the lock is entered again. The calling thread
    /// should exit the lock as many times as it had entered the lock to fully exit the lock and allow other threads to
    /// enter the lock.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeout"/>, after its conversion to an integer millisecond value, represents a value that is less
    /// than <code>-1</code> milliseconds or greater than <see cref="int.MaxValue"/> milliseconds.
    /// </exception>
    /// <exception cref="LockRecursionException">
    /// The lock has reached the limit of recursive enters. The limit is implementation-defined, but is expected to be high
    /// enough that it would typically not be reached when the lock is used properly.
    /// </exception>
    public bool TryEnter(TimeSpan timeout) => TryEnter_Outlined(ToTimeoutMilliseconds(timeout));

    private static int ToTimeoutMilliseconds(TimeSpan timeout)
    {
        long timeoutMilliseconds = (long)timeout.TotalMilliseconds;
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMilliseconds, -1, nameof(timeout));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeoutMilliseconds, int.MaxValue, nameof(timeout));
        return (int)timeoutMilliseconds;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryEnter_Outlined(int timeoutMs) => TryEnter_Inlined(timeoutMs).IsInitialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ThreadId TryEnter_Inlined(int timeoutMs)
    {
        Debug.Assert(timeoutMs >= -1);

        ThreadId currentThreadId = ThreadId.Current_NoInitialize;
        if (currentThreadId.IsInitialized && State.TryLock(this))
        {
            Debug.Assert(!new ThreadId(_owningThreadId).IsInitialized);
            Debug.Assert(_recursionCount == 0);
            _owningThreadId = currentThreadId.Id;
            return currentThreadId;
        }

        return TryEnterSlow(timeoutMs, currentThreadId);
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
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Exit()
    {
        var owningThreadId = new ThreadId(_owningThreadId);
        if (!owningThreadId.IsInitialized || owningThreadId.Id != ThreadId.Current_NoInitialize.Id)
        {
            ThrowHelper.ThrowSynchronizationLockException_LockExit();
        }

        ExitImpl();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Exit(ThreadId currentThreadId)
    {
        Debug.Assert(currentThreadId.IsInitialized);
        Debug.Assert(currentThreadId.Id == ThreadId.Current_NoInitialize.Id);

        if (_owningThreadId != currentThreadId.Id)
        {
            ThrowHelper.ThrowSynchronizationLockException_LockExit();
        }

        ExitImpl();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExitImpl()
    {
        Debug.Assert(new ThreadId(_owningThreadId).IsInitialized);
        Debug.Assert(_owningThreadId == ThreadId.Current_NoInitialize.Id);
        Debug.Assert(new State(this).IsLocked);

        if (_recursionCount == 0)
        {
            _owningThreadId = 0;

            State state = State.Unlock(this);
            if (state.HasAnyWaiters)
            {
                SignalWaiterIfNecessary(state);
            }
        }
        else
        {
            _recursionCount--;
        }
    }

    private static bool IsAdaptiveSpinEnabled(short minSpinCountForAdaptiveSpin) => minSpinCountForAdaptiveSpin <= 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ThreadId TryEnterSlow(int timeoutMs, ThreadId currentThreadId)
    {
        Debug.Assert(timeoutMs >= -1);

        if (!currentThreadId.IsInitialized)
        {
            // The thread info hasn't been initialized yet for this thread, and the fast path hasn't been tried yet. After
            // initializing the thread info, try the fast path first.
            currentThreadId.InitializeForCurrentThread();
            Debug.Assert(_owningThreadId != currentThreadId.Id);
            if (State.TryLock(this))
            {
                goto Locked;
            }
        }
        else if (_owningThreadId == currentThreadId.Id)
        {
            Debug.Assert(new State(this).IsLocked);

            uint newRecursionCount = _recursionCount + 1;
            if (newRecursionCount != 0)
            {
                _recursionCount = newRecursionCount;
                return currentThreadId;
            }

            throw new LockRecursionException(SRF.Lock_Enter_LockRecursionException);
        }

        if (timeoutMs == 0)
        {
            return new ThreadId(0);
        }

        //
        // At this point, a full lock attempt has been made, and it's time to retry or wait for the lock.
        //

        // Notify the debugger that this thread is about to wait for a lock that is likely held by another thread. The
        // debugger may choose to enable other threads to run to help resolve the dependency, or it may choose to abort the
        // FuncEval here. The lock state is consistent here for an abort, whereas letting a FuncEval continue to run could
        // lead to the FuncEval timing out and potentially aborting at an arbitrary place where the lock state may not be
        // consistent.
        Debugger.NotifyOfCrossThreadDependency();

        if (LazyInitializeOrEnter() == TryLockResult.Locked)
        {
            goto Locked;
        }

        short maxSpinCount = s_maxSpinCount;
        if (maxSpinCount == 0)
        {
            goto Wait;
        }

        short minSpinCountForAdaptiveSpin = s_minSpinCountForAdaptiveSpin;
        short spinCount = _spinCount;
        if (spinCount < 0)
        {
            // When negative, the spin count serves as a counter for contentions such that a spin-wait can be attempted
            // periodically to see if it would be beneficial. Increment the spin count and skip spin-waiting.
            Debug.Assert(IsAdaptiveSpinEnabled(minSpinCountForAdaptiveSpin));
            _spinCount = (short)(spinCount + 1);
            goto Wait;
        }

        // Try to acquire the lock, and check if non-waiters should stop preempting waiters. If this thread should not
        // preempt waiters, skip spin-waiting. Upon contention, register a spinner.
        TryLockResult tryLockResult = State.TryLockBeforeSpinLoop(this, spinCount, out bool isFirstSpinner);
        if (tryLockResult != TryLockResult.Spin)
        {
            goto LockedOrWait;
        }

        // Lock was not acquired and a spinner was registered

        if (isFirstSpinner)
        {
            // Whether a full-length spin-wait would be effective is determined by having the first spinner do a full-length
            // spin-wait to see if it is effective. Shorter spin-waits would more often be ineffective just because they are
            // shorter.
            spinCount = maxSpinCount;
        }

        for (short spinIndex = 0; ;)
        {
            LowLevelSpinWaiter.Wait(spinIndex, SpinSleep0Threshold, isSingleProcessor: false);

            if (++spinIndex >= spinCount)
            {
                // The last lock attempt for this spin will be done after the loop
                break;
            }

            // Try to acquire the lock and unregister the spinner
            tryLockResult = State.TryLockInsideSpinLoop(this);
            if (tryLockResult == TryLockResult.Spin)
            {
                continue;
            }

            if (tryLockResult == TryLockResult.Locked)
            {
                if (isFirstSpinner && IsAdaptiveSpinEnabled(minSpinCountForAdaptiveSpin))
                {
                    // Since the first spinner does a full-length spin-wait, and to keep upward and downward changes to the
                    // spin count more balanced, only the first spinner adjusts the spin count
                    spinCount = _spinCount;
                    if (spinCount < maxSpinCount)
                    {
                        _spinCount = (short)(spinCount + 1);
                    }
                }

                goto Locked;
            }

            // The lock was not acquired and the spinner was not unregistered, stop spinning
            Debug.Assert(tryLockResult == TryLockResult.Wait);
            break;
        }

        // Unregister the spinner and try to acquire the lock
        tryLockResult = State.TryLockAfterSpinLoop(this);
        if (isFirstSpinner && IsAdaptiveSpinEnabled(minSpinCountForAdaptiveSpin))
        {
            // Since the first spinner does a full-length spin-wait, and to keep upward and downward changes to the
            // spin count more balanced, only the first spinner adjusts the spin count
            if (tryLockResult == TryLockResult.Locked)
            {
                spinCount = _spinCount;
                if (spinCount < maxSpinCount)
                {
                    _spinCount = (short)(spinCount + 1);
                }
            }
            else
            {
                // If the spin count is already zero, skip spin-waiting for a while, even for the first spinners. After a
                // number of contentions, the first spinner will attempt a spin-wait again to see if it is effective.
                Debug.Assert(tryLockResult == TryLockResult.Wait);
                spinCount = _spinCount;
                _spinCount = spinCount > 0 ? (short)(spinCount - 1) : minSpinCountForAdaptiveSpin;
            }
        }

    LockedOrWait:
        Debug.Assert(tryLockResult != TryLockResult.Spin);
        if (tryLockResult == TryLockResult.Wait)
        {
            goto Wait;
        }

        Debug.Assert(tryLockResult == TryLockResult.Locked);

    Locked:
        Debug.Assert(!new ThreadId(_owningThreadId).IsInitialized);
        Debug.Assert(_recursionCount == 0);
        _owningThreadId = currentThreadId.Id;
        return currentThreadId;

    Wait:
        AutoResetEvent waitEvent = _waitEvent ?? CreateWaitEvent();
        if (State.TryLockBeforeWait(this))
        {
            // Lock was acquired and a waiter was not registered
            goto Locked;
        }

        // Lock was not acquired and a waiter was registered. All following paths need to unregister the waiter, including
        // exceptional paths.
        try
        {
            Interlocked.Increment(ref s_contentionCount);

            using ThreadBlockingInfo.Scope threadBlockingScope = new(this, timeoutMs);

            bool acquiredLock = false;
            int waitStartTimeMs = timeoutMs < 0 ? 0 : Environment.TickCount;
            int remainingTimeoutMs = timeoutMs;
            while (true)
            {
                bool useTrivalWaits = new State(this).UseTrivialWaits;
                if (useTrivalWaits)
                {
                    if (!waitEvent.WaitOneNoCheck(remainingTimeoutMs, useTrivalWaits))
                    {
                        break;
                    }
                }
                else
                {
                    // Our copy of WaitOneNoCheck won't change the ThreadState, so we'll try to avoid it if we can.
                    if (!waitEvent.WaitOne(remainingTimeoutMs))
                    {
                        break;
                    }
                }

                // Spin a bit while trying to acquire the lock. This has a few benefits:
                // - Spinning helps to reduce waiter starvation. Since other non-waiter threads can take the lock while
                //   there are waiters (see State.TryLock()), once a waiter wakes it will be able to better compete with
                //   other spinners for the lock.
                // - If there is another thread that is repeatedly acquiring and releasing the lock, spinning before waiting
                //   again helps to prevent a waiter from repeatedly context-switching in and out
                // - Further in the same situation above, waking up and waiting shortly thereafter deprioritizes this waiter
                //   because events release waiters in FIFO order. Spinning a bit helps a waiter to retain its priority at
                //   least for one spin duration before it gets deprioritized behind all other waiters.
                for (short spinIndex = 0; spinIndex < maxSpinCount; spinIndex++)
                {
                    if (State.TryLockInsideWaiterSpinLoop(this))
                    {
                        acquiredLock = true;
                        break;
                    }

                    LowLevelSpinWaiter.Wait(spinIndex, SpinSleep0Threshold, isSingleProcessor: false);
                }

                if (acquiredLock)
                {
                    break;
                }

                if (State.TryLockAfterWaiterSpinLoop(this))
                {
                    acquiredLock = true;
                    break;
                }

                if (remainingTimeoutMs < 0)
                {
                    continue;
                }

                uint waitDurationMs = (uint)(Environment.TickCount - waitStartTimeMs);
                if (waitDurationMs >= (uint)timeoutMs)
                {
                    break;
                }

                remainingTimeoutMs = timeoutMs - (int)waitDurationMs;
            }

            if (acquiredLock)
            {
                // In NativeAOT, ensure that class construction cycles do not occur after the lock is acquired but before
                // the state is fully updated. Update the state to fully reflect that this thread owns the lock before doing
                // other things.
                Debug.Assert(!new ThreadId(_owningThreadId).IsInitialized);
                Debug.Assert(_recursionCount == 0);
                _owningThreadId = currentThreadId.Id;

                return currentThreadId;
            }
        }
        catch // run this code before exception filters in callers
        {
            State.UnregisterWaiter(this);
            throw;
        }

        State.UnregisterWaiter(this);
        return new ThreadId(0);
    }

    private void ResetWaiterStartTime() => _waiterStartTimeMs = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordWaiterStartTime()
    {
        ushort currentTimeMs = (ushort)Environment.TickCount;
        if (currentTimeMs == 0)
        {
            // Don't record zero, that value is reserved for indicating that a time is not recorded
            currentTimeMs--;
        }
        _waiterStartTimeMs = currentTimeMs;
    }

    private bool ShouldStopPreemptingWaiters
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // If the recorded time is zero, a time has not been recorded yet
            ushort waiterStartTimeMs = _waiterStartTimeMs;
            return
                waiterStartTimeMs != 0 &&
                (ushort)(Environment.TickCount - waiterStartTimeMs) >= MaxDurationMsForPreemptingWaiters;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private AutoResetEvent CreateWaitEvent()
    {
        var newWaitEvent = new AutoResetEvent(false);
        AutoResetEvent? waitEventBeforeUpdate = Interlocked.CompareExchange(ref _waitEvent, newWaitEvent, null);
        if (waitEventBeforeUpdate is null)
        {
            return newWaitEvent;
        }

        newWaitEvent.Dispose();
        return waitEventBeforeUpdate;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SignalWaiterIfNecessary(State state)
    {
        if (State.TrySetIsWaiterSignaledToWake(this, state))
        {
            // Signal a waiter to wake
            bool signaled = _waitEvent!.Set();
            Debug.Assert(signaled);
        }
    }

    /// <summary>
    /// <code>true</code> if the lock is held by the calling thread, <code>false</code> otherwise.
    /// </summary>
    public bool IsHeldByCurrentThread
    {
        get
        {
            var owningThreadId = new ThreadId(_owningThreadId);
            bool isHeld = owningThreadId.IsInitialized && owningThreadId.Id == ThreadId.Current_NoInitialize.Id;
            Debug.Assert(!isHeld || new State(this).IsLocked);
            return isHeld;
        }
    }

    internal static long ContentionCount => s_contentionCount;

    internal void Dispose() => _waitEvent?.Dispose();

    internal nint LockIdForEvents => _waitEvent!.SafeWaitHandle.DangerousGetHandle();

    internal ulong OwningThreadId => _owningThreadId;
}
