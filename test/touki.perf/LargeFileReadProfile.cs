// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;
#if NET
using System.Threading.Tasks;
#endif

namespace touki.perf;

internal static class LargeFileReadProfile
{
    internal static void Run(string path, LargeFileReadReference reference, LargeFileReadScenario scenario, int iterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        if (!Enum.IsDefined(typeof(LargeFileReadScenario), scenario))
        {
            throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        using (LargeFileReadReferenceReader warm = new(path, reference, scenario))
        {
            LargeFileReadReplay.Run(warm, scenario);
        }

        Console.WriteLine(RunIterations(path, reference, scenario, iterations));
    }

#if NET
    internal static async Task RunAsync(
        string path,
        LargeFileReadReference reference,
        LargeFileReadScenario scenario,
        int iterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        if (!Enum.IsDefined(scenario))
        {
            throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        using (LargeFileReadReferenceReader warm = new(path, reference, scenario))
        {
            await LargeFileReadReplay.RunAsync(warm, scenario).ConfigureAwait(continueOnCapturedContext: false);
        }

        Console.WriteLine(await RunIterationsAsync(path, reference, scenario, iterations)
            .ConfigureAwait(continueOnCapturedContext: false));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<long> RunIterationsAsync(
        string path,
        LargeFileReadReference reference,
        LargeFileReadScenario scenario,
        int iterations)
    {
        long result = 0;
        for (int i = 0; i < iterations; i++)
        {
            using LargeFileReadReferenceReader reader = new(path, reference, scenario);
            long value = await LargeFileReadReplay.RunAsync(reader, scenario)
                .ConfigureAwait(continueOnCapturedContext: false);

            result = unchecked(result * 31 + value);
        }

        return result;
    }
#endif

    // Keep an unambiguous trace root without changing any benchmark or production method's inlining.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long RunIterations(
        string path,
        LargeFileReadReference reference,
        LargeFileReadScenario scenario,
        int iterations)
    {
        long result = 0;
        for (int i = 0; i < iterations; i++)
        {
            using LargeFileReadReferenceReader reader = new(path, reference, scenario);
            result = unchecked(result * 31 + LargeFileReadReplay.Run(reader, scenario));
        }

        return result;
    }
}
