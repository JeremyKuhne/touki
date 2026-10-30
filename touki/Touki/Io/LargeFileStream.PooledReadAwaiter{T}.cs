// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET11_0
using System.Threading.Tasks;

namespace Touki.Io;

public sealed partial class LargeFileStream
{
    /// <summary>
    ///  Preserves typed non-capturing pooled awaits without boxing the configured awaiter.
    /// </summary>
    /// <typeparam name="T">The operation result.</typeparam>
    private readonly struct PooledReadAwaiter<T>(ValueTask<T> pending) : ICriticalNotifyCompletion
    {
        private readonly ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter _awaiter =
            pending.ConfigureAwait(continueOnCapturedContext: false).GetAwaiter();

        public PooledReadAwaiter<T> GetAwaiter() => this;

        public bool IsCompleted => _awaiter.IsCompleted;

        public T GetResult() => _awaiter.GetResult();

        public void OnCompleted(Action continuation) => _awaiter.OnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation) => _awaiter.UnsafeOnCompleted(continuation);
    }
}
#endif
