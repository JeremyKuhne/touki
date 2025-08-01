// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Framework.Touki;
using Touki.Exceptions;

namespace System.Threading;

public sealed partial class Lock
{
    private struct State : IEquatable<State>
    {
        // Layout constants for Lock._state
        private const uint IsLockedMask = (uint)1 << 0; // bit 0
        private const uint ShouldNotPreemptWaitersMask = (uint)1 << 1; // bit 1
        private const uint SpinnerCountIncrement = (uint)1 << 2; // bits 2-4
        private const uint SpinnerCountMask = (uint)0x7 << 2;
        private const uint IsWaiterSignaledToWakeMask = (uint)1 << 5; // bit 5
        private const uint UseTrivialWaitsMask = (uint)1 << 6; // bit 6
        private const uint WaiterCountIncrement = (uint)1 << 7; // bits 7-31

        private uint _state;

        public State(Lock lockObj) : this(lockObj._state) { }
        private State(uint state) => _state = state;

        public static uint InitialStateValue => 0;
        public static uint LockedStateValue => IsLockedMask;
        private static uint Neg(uint state) => (uint)-(int)state;
        public readonly bool IsInitialState => this == default;
        public readonly bool IsLocked => (_state & IsLockedMask) != 0;

        private void SetIsLocked()
        {
            Debug.Assert(!IsLocked);
            _state += IsLockedMask;
        }

        private readonly bool ShouldNotPreemptWaiters => (_state & ShouldNotPreemptWaitersMask) != 0;

        private void SetShouldNotPreemptWaiters()
        {
            Debug.Assert(!ShouldNotPreemptWaiters);
            Debug.Assert(HasAnyWaiters);

            _state += ShouldNotPreemptWaitersMask;
        }

        private void ClearShouldNotPreemptWaiters()
        {
            Debug.Assert(ShouldNotPreemptWaiters);
            _state -= ShouldNotPreemptWaitersMask;
        }

        private readonly bool ShouldNonWaiterAttemptToAcquireLock
        {
            get
            {
                Debug.Assert(HasAnyWaiters || !ShouldNotPreemptWaiters);
                return (_state & (IsLockedMask | ShouldNotPreemptWaitersMask)) == 0;
            }
        }

        private readonly bool HasAnySpinners => (_state & SpinnerCountMask) != 0;

        private bool TryIncrementSpinnerCount()
        {
            uint newState = _state + SpinnerCountIncrement;
            if (new State(newState).HasAnySpinners) // overflow check
            {
                _state = newState;
                return true;
            }
            return false;
        }

        private void DecrementSpinnerCount()
        {
            Debug.Assert(HasAnySpinners);
            _state -= SpinnerCountIncrement;
        }

        private readonly bool IsWaiterSignaledToWake => (_state & IsWaiterSignaledToWakeMask) != 0;

        private void SetIsWaiterSignaledToWake()
        {
            Debug.Assert(HasAnyWaiters);
            Debug.Assert(NeedToSignalWaiter);

            _state += IsWaiterSignaledToWakeMask;
        }

        private void ClearIsWaiterSignaledToWake()
        {
            Debug.Assert(IsWaiterSignaledToWake);
            _state -= IsWaiterSignaledToWakeMask;
        }

        // Trivial waits are:
        // - Not interruptible by Thread.Interrupt
        // - Don't allow reentrance through APCs or message pumping
        // - Not forwarded to SynchronizationContext wait overrides
        public readonly bool UseTrivialWaits => (_state & UseTrivialWaitsMask) != 0;

        public static void InitializeUseTrivialWaits(Lock lockObj, bool useTrivialWaits)
        {
            Debug.Assert(lockObj._state == 0);

            if (useTrivialWaits)
            {
                lockObj._state = UseTrivialWaitsMask;
            }
        }

        public readonly bool HasAnyWaiters => _state >= WaiterCountIncrement;

        private bool TryIncrementWaiterCount()
        {
            uint newState = _state + WaiterCountIncrement;
            if (new State(newState).HasAnyWaiters) // overflow check
            {
                _state = newState;
                return true;
            }
            return false;
        }

        private void DecrementWaiterCount()
        {
            Debug.Assert(HasAnyWaiters);
            _state -= WaiterCountIncrement;
        }

        public readonly bool NeedToSignalWaiter
        {
            get
            {
                Debug.Assert(HasAnyWaiters);
                return (_state & (SpinnerCountMask | IsWaiterSignaledToWakeMask)) == 0;
            }
        }

        public static bool operator ==(State state1, State state2) => state1._state == state2._state;
        public static bool operator !=(State state1, State state2) => !(state1 == state2);

        readonly bool IEquatable<State>.Equals(State other) => this == other;
        public override readonly bool Equals(object? obj) => obj is State other && this == other;
        public override readonly int GetHashCode() => (int)_state;

        private static State CompareExchange(Lock lockObj, State toState, State fromState) =>
            new State(Interlock.CompareExchange(ref lockObj._state, toState._state, fromState._state));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryLock(Lock lockObj)
        {
            // The lock is mostly fair to release waiters in a typically FIFO order (though the order is not guaranteed).
            // However, it allows non-waiters to acquire the lock if it's available to avoid lock convoys.
            //
            // Lock convoys can be detrimental to performance in scenarios where work is being done on multiple threads and
            // the work involves periodically taking a particular lock for a short time to access shared resources. With a
            // lock convoy, once there is a waiter for the lock (which is not uncommon in such scenarios), a worker thread
            // would be forced to context-switch on the subsequent attempt to acquire the lock, often long before the worker
            // thread exhausts its time slice. This process repeats as long as the lock has a waiter, forcing every worker
            // to context-switch on each attempt to acquire the lock, killing performance and creating a positive feedback
            // loop that makes it more likely for the lock to have waiters. To avoid the lock convoy, each worker needs to
            // be allowed to acquire the lock multiple times in sequence despite there being a waiter for the lock in order
            // to have the worker continue working efficiently during its time slice as long as the lock is not contended.
            //
            // This scheme has the possibility to starve waiters. Waiter starvation is mitigated by other means, see
            // TryLockBeforeSpinLoop() and references to ShouldNotPreemptWaiters.

            var state = new State(lockObj);
            if (!state.ShouldNonWaiterAttemptToAcquireLock)
            {
                return false;
            }

            State newState = state;
            newState.SetIsLocked();

            return CompareExchange(lockObj, newState, state) == state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static State Unlock(Lock lockObj)
        {
            Debug.Assert(IsLockedMask == 1);

            var state = new State(Interlock.Decrement(ref lockObj._state));
            Debug.Assert(!state.IsLocked);
            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TryLockResult TryLockBeforeSpinLoop(Lock lockObj, short spinCount, out bool isFirstSpinner)
        {
            // Normally, threads are allowed to preempt waiters to acquire the lock in order to avoid creating lock convoys,
            // see TryLock(). There can be cases where waiters can be easily starved as a result. For example, a thread that
            // holds a lock for a significant amount of time (much longer than the time it takes to do a context switch),
            // then releases and reacquires the lock in quick succession, and repeats. Though a waiter would be woken upon
            // lock release, usually it will not have enough time to context-switch-in and take the lock, and can be starved
            // for an unreasonably long duration.
            //
            // In order to prevent such starvation and force a bit of fair forward progress, it is sometimes necessary to
            // change the normal policy and disallow threads from preempting waiters. ShouldNotPreemptWaiters() indicates
            // the current state of the policy and this method determines whether the policy should be changed to disallow
            // non-waiters from preempting waiters.
            //   - When the first waiter begins waiting, it records the current time as a "waiter starvation start time".
            //     That is a point in time after which no forward progress has occurred for waiters. When a waiter acquires
            //     the lock, the time is updated to the current time.
            //   - This method checks whether the starvation duration has crossed a threshold and if so, sets
            //     ShouldNotPreemptWaitersMask
            //
            // When unreasonable starvation is occurring, the lock will be released occasionally and if caused by spinners,
            // those threads may start to spin again.
            //   - Before starting to spin this method is called. If ShouldNotPreemptWaitersMask is set, the spinner will
            //     skip spinning and wait instead. Spinners that are already registered at the time
            //     ShouldNotPreemptWaitersMask is set will stop spinning as necessary. Eventually, all spinners will drain
            //     and no new ones will be registered.
            //   - Upon releasing a lock, if there are no spinners, a waiter will be signaled to wake. On that path,
            //     TrySetIsWaiterSignaledToWake() is called.
            //   - Eventually, after spinners have drained, only a waiter will be able to acquire the lock. When a waiter
            //     acquires the lock, or when the last waiter unregisters itself, ShouldNotPreemptWaitersMask is cleared to
            //     restore the normal policy.

            Debug.Assert(spinCount >= 0);

            isFirstSpinner = false;
            var state = new State(lockObj);
            while (true)
            {
                State newState = state;
                TryLockResult result = TryLockResult.Spin;
                if (newState.HasAnyWaiters)
                {
                    if (newState.ShouldNotPreemptWaiters)
                    {
                        return TryLockResult.Wait;
                    }
                    if (lockObj.ShouldStopPreemptingWaiters)
                    {
                        newState.SetShouldNotPreemptWaiters();
                        result = TryLockResult.Wait;
                    }
                }

                if (result == TryLockResult.Spin)
                {
                    Debug.Assert(!newState.ShouldNotPreemptWaiters);
                    if (!newState.IsLocked)
                    {
                        newState.SetIsLocked();
                        result = TryLockResult.Locked;
                    }
                    else if ((newState.HasAnySpinners && spinCount == 0) || !newState.TryIncrementSpinnerCount())
                    {
                        return TryLockResult.Wait;
                    }
                }

                State stateBeforeUpdate = CompareExchange(lockObj, newState, state);
                if (stateBeforeUpdate == state)
                {
                    if (result == TryLockResult.Spin && !state.HasAnySpinners)
                    {
                        isFirstSpinner = true;
                    }
                    return result;
                }

                state = stateBeforeUpdate;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TryLockResult TryLockInsideSpinLoop(Lock lockObj)
        {
            // This method is called from inside a spin loop, it must unregister the spinner if the lock is acquired

            var state = new State(lockObj);
            while (true)
            {
                Debug.Assert(state.HasAnySpinners);
                if (!state.ShouldNonWaiterAttemptToAcquireLock)
                {
                    return state.ShouldNotPreemptWaiters ? TryLockResult.Wait : TryLockResult.Spin;
                }

                State newState = state;
                newState.SetIsLocked();
                newState.DecrementSpinnerCount();

                State stateBeforeUpdate = CompareExchange(lockObj, newState, state);
                if (stateBeforeUpdate == state)
                {
                    return TryLockResult.Locked;
                }

                state = stateBeforeUpdate;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TryLockResult TryLockAfterSpinLoop(Lock lockObj)
        {
            // This method is called at the end of a spin loop, it must unregister the spinner always and acquire the lock
            // if it's available. If the lock is available, a spinner must acquire the lock along with unregistering itself,
            // because a lock releaser does not wake a waiter when there is a spinner registered.

            var state = new State(Interlock.Add(ref lockObj._state, Neg(SpinnerCountIncrement)));
            Debug.Assert(new State(state._state + SpinnerCountIncrement).HasAnySpinners);

            while (true)
            {
                Debug.Assert(state.HasAnyWaiters || !state.ShouldNotPreemptWaiters);
                if (state.IsLocked)
                {
                    return TryLockResult.Wait;
                }

                State newState = state;
                newState.SetIsLocked();

                State stateBeforeUpdate = CompareExchange(lockObj, newState, state);
                if (stateBeforeUpdate == state)
                {
                    return TryLockResult.Locked;
                }

                state = stateBeforeUpdate;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryLockBeforeWait(Lock lockObj)
        {
            // This method is called before waiting. It must either acquire the lock or register a waiter. It also keeps
            // track of the waiter starvation start time.

            var state = new State(lockObj);
            bool waiterStartTimeWasReset = false;
            while (true)
            {
                State newState = state;
                if (newState.ShouldNonWaiterAttemptToAcquireLock)
                {
                    newState.SetIsLocked();
                }
                else
                {
                    if (!newState.TryIncrementWaiterCount())
                    {
                        ThrowHelper.ThrowOutOfMemoryException_LockEnter_WaiterCountOverflow();
                    }

                    if (!state.HasAnyWaiters && !waiterStartTimeWasReset)
                    {
                        // This would be the first waiter. Once the waiter is registered, another thread may check the
                        // waiter starvation start time and the previously recorded value may be stale, causing
                        // ShouldNotPreemptWaitersMask to be set unnecessarily. Reset the start time before registering the
                        // waiter.
                        waiterStartTimeWasReset = true;
                        lockObj.ResetWaiterStartTime();
                    }
                }

                State stateBeforeUpdate = CompareExchange(lockObj, newState, state);
                if (stateBeforeUpdate == state)
                {
                    if (state.ShouldNonWaiterAttemptToAcquireLock)
                    {
                        return true;
                    }

                    Debug.Assert(state.HasAnyWaiters || waiterStartTimeWasReset);
                    if (!state.HasAnyWaiters || waiterStartTimeWasReset)
                    {
                        // This was the first waiter or the waiter start time was reset, record the waiter start time
                        lockObj.RecordWaiterStartTime();
                    }
                    return false;
                }

                state = stateBeforeUpdate;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryLockInsideWaiterSpinLoop(Lock lockObj)
        {
            // This method is called from inside the waiter's spin loop and should observe the wake signal only if the lock
            // is taken, to prevent a lock releaser from waking another waiter while one is already spinning to acquire the
            // lock

            bool waiterStartTimeWasRecorded = false;
            var state = new State(lockObj);
            while (true)
            {
                Debug.Assert(state.HasAnyWaiters);
                Debug.Assert(state.IsWaiterSignaledToWake);

                if (state.IsLocked)
                {
                    return false;
                }

                State newState = state;
                newState.SetIsLocked();
                newState.ClearIsWaiterSignaledToWake();
                newState.DecrementWaiterCount();
                if (newState.ShouldNotPreemptWaiters)
                {
                    newState.ClearShouldNotPreemptWaiters();

                    if (newState.HasAnyWaiters && !waiterStartTimeWasRecorded)
                    {
                        // Update the waiter starvation start time. The time must be recorded before
                        // ShouldNotPreemptWaitersMask is cleared, as once that is cleared, another thread may check the
                        // waiter starvation start time and the previously recorded value may be stale, causing
                        // ShouldNotPreemptWaitersMask to be set again unnecessarily.
                        waiterStartTimeWasRecorded = true;
                        lockObj.RecordWaiterStartTime();
                    }
                }

                State stateBeforeUpdate = CompareExchange(lockObj, newState, state);
                if (stateBeforeUpdate == state)
                {
                    if (newState.HasAnyWaiters)
                    {
                        Debug.Assert(!state.ShouldNotPreemptWaiters || waiterStartTimeWasRecorded);
                        if (!waiterStartTimeWasRecorded)
                        {
                            // Since the lock was acquired successfully by a waiter, update the waiter starvation start time
                            lockObj.RecordWaiterStartTime();
                        }
                    }
                    return true;
                }

                state = stateBeforeUpdate;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryLockAfterWaiterSpinLoop(Lock lockObj)
        {
            // This method is called at the end of the waiter's spin loop. It must observe the wake signal always, and if
            // the lock is available, it must acquire the lock and unregister the waiter. If the lock is available, a waiter
            // must acquire the lock along with observing the wake signal, because a lock releaser does not wake a waiter
            // when a waiter was signaled but the wake signal has not been observed. If the lock is acquired, the waiter
            // starvation start time is also updated.

            var state = new State(Interlock.Add(ref lockObj._state, Neg(IsWaiterSignaledToWakeMask)));
            Debug.Assert(new State(state._state + IsWaiterSignaledToWakeMask).IsWaiterSignaledToWake);

            bool waiterStartTimeWasRecorded = false;
            while (true)
            {
                Debug.Assert(state.HasAnyWaiters);

                if (state.IsLocked)
                {
                    return false;
                }

                State newState = state;
                newState.SetIsLocked();
                newState.DecrementWaiterCount();
                if (newState.ShouldNotPreemptWaiters)
                {
                    newState.ClearShouldNotPreemptWaiters();

                    if (newState.HasAnyWaiters && !waiterStartTimeWasRecorded)
                    {
                        // Update the waiter starvation start time. The time must be recorded before
                        // ShouldNotPreemptWaitersMask is cleared, as once that is cleared, another thread may check the
                        // waiter starvation start time and the previously recorded value may be stale, causing
                        // ShouldNotPreemptWaitersMask to be set again unnecessarily.
                        waiterStartTimeWasRecorded = true;
                        lockObj.RecordWaiterStartTime();
                    }
                }

                State stateBeforeUpdate = CompareExchange(lockObj, newState, state);
                if (stateBeforeUpdate == state)
                {
                    if (newState.HasAnyWaiters)
                    {
                        Debug.Assert(!state.ShouldNotPreemptWaiters || waiterStartTimeWasRecorded);
                        if (!waiterStartTimeWasRecorded)
                        {
                            // Since the lock was acquired successfully by a waiter, update the waiter starvation start time
                            lockObj.RecordWaiterStartTime();
                        }
                    }
                    return true;
                }

                state = stateBeforeUpdate;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UnregisterWaiter(Lock lockObj)
        {
            // This method is called upon an exception while waiting, or when a wait has timed out. It must unregister the
            // waiter, and if it's the last waiter, clear ShouldNotPreemptWaitersMask to allow other threads to acquire the
            // lock.

            var state = new State(lockObj);
            while (true)
            {
                Debug.Assert(state.HasAnyWaiters);

                State newState = state;
                newState.DecrementWaiterCount();
                if (newState.ShouldNotPreemptWaiters && !newState.HasAnyWaiters)
                {
                    newState.ClearShouldNotPreemptWaiters();
                }

                State stateBeforeUpdate = CompareExchange(lockObj, newState, state);
                if (stateBeforeUpdate == state)
                {
                    return;
                }

                state = stateBeforeUpdate;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetIsWaiterSignaledToWake(Lock lockObj, State state)
        {
            // Determine whether we must signal a waiter to wake. Keep track of whether a thread has been signaled to wake
            // but has not yet woken from the wait. IsWaiterSignaledToWakeMask is cleared when a signaled thread wakes up by
            // observing a signal. Since threads can preempt waiting threads and acquire the lock (see TryLock()), it allows
            // for example, one thread to acquire and release the lock multiple times while there are multiple waiting
            // threads. In such a case, we don't want that thread to signal a waiter every time it releases the lock, as
            // that will cause unnecessary context switches with more and more signaled threads waking up, finding that the
            // lock is still locked, and going back into a wait state. So, signal only one waiting thread at a time.

            Debug.Assert(state.HasAnyWaiters);

            while (true)
            {
                if (!state.NeedToSignalWaiter)
                {
                    return false;
                }

                State newState = state;
                newState.SetIsWaiterSignaledToWake();
                if (!newState.ShouldNotPreemptWaiters && lockObj.ShouldStopPreemptingWaiters)
                {
                    newState.SetShouldNotPreemptWaiters();
                }

                State stateBeforeUpdate = CompareExchange(lockObj, newState, state);
                if (stateBeforeUpdate == state)
                {
                    return true;
                }

                if (!stateBeforeUpdate.HasAnyWaiters)
                {
                    return false;
                }

                state = stateBeforeUpdate;
            }
        }
    }
}
