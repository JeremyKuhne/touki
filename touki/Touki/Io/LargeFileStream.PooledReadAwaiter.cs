// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET11_0
using System.Threading.Tasks;

namespace Touki.Io;

public sealed partial class LargeFileStream
{
    /// <summary>
    ///  Preserves non-capturing pooled awaits without the preview runtime's boxed internal-interface cast.
    /// </summary>
    private readonly struct PooledReadAwaiter(ValueTask pending) : ICriticalNotifyCompletion
    {
        private readonly ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter _awaiter =
            pending.ConfigureAwait(continueOnCapturedContext: false).GetAwaiter();

        public PooledReadAwaiter GetAwaiter() => this;

        public bool IsCompleted => _awaiter.IsCompleted;

        public void GetResult() => _awaiter.GetResult();

        public void OnCompleted(Action continuation) => _awaiter.OnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation) => _awaiter.UnsafeOnCompleted(continuation);
    }
}
#endif
