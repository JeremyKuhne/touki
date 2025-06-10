// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Threading;

internal sealed class Lock
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Scope EnterScope() => new(this);

    public readonly ref struct Scope
    {
        private readonly Lock _lock;

        public Scope(Lock @lock)
        {
            _lock = @lock;
#pragma warning disable CS9216 // A value of type 'System.Threading.Lock' converted to a different type will use likely unintended monitor-based locking in 'lock' statement.
            Monitor.Enter(_lock);
        }

        public void Dispose()
        {
            Monitor.Exit(_lock);
#pragma warning restore CS9216
        }
    }
}
