// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace touki.perf;

[MemoryDiagnoser]
public class ValueTaskAwaiterAllocationPerf
{
    private readonly CompletingSource _source = new();

    [Benchmark(Baseline = true)]
    public int Standard() => CompletedResult(RunStandard());

    [Benchmark]
    public int Wrapped() => CompletedResult(RunWrapped());

    private static int CompletedResult(ValueTask<int> pending)
    {
        if (!pending.IsCompleted)
        {
            throw new InvalidOperationException("The diagnostic source must complete inline.");
        }

        return pending.GetAwaiter().GetResult();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<int> RunStandard()
    {
        int result = 0;
        for (int i = 0; i < 100; i++)
        {
            result += await new ValueTask<int>(_source, token: 0).ConfigureAwait(continueOnCapturedContext: false);
        }

        return result;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<int> RunWrapped()
    {
        int result = 0;
        for (int i = 0; i < 100; i++)
        {
            result += await new UnboxedAwaiter(new ValueTask<int>(_source, token: 0));
        }

        return result;
    }

    private readonly struct UnboxedAwaiter(ValueTask<int> value) : ICriticalNotifyCompletion
    {
        private readonly ConfiguredValueTaskAwaitable<int>.ConfiguredValueTaskAwaiter _awaiter =
            value.ConfigureAwait(continueOnCapturedContext: false).GetAwaiter();

        public UnboxedAwaiter GetAwaiter() => this;

        public bool IsCompleted => _awaiter.IsCompleted;

        public int GetResult() => _awaiter.GetResult();

        public void OnCompleted(Action continuation) => _awaiter.OnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation) => _awaiter.UnsafeOnCompleted(continuation);
    }

    private sealed class CompletingSource : IValueTaskSource<int>
    {
        private bool _completed;

        public ValueTaskSourceStatus GetStatus(short token) => _completed
            ? ValueTaskSourceStatus.Succeeded
            : ValueTaskSourceStatus.Pending;

        public int GetResult(short token)
        {
            if (!_completed)
            {
                throw new InvalidOperationException("The diagnostic completion has not run.");
            }

            _completed = false;
            return 1;
        }

        public void OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
            _completed = true;
            continuation(state);
        }
    }
}
#endif
