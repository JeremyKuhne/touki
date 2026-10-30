// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Threading.Tasks;

namespace touki.perf;

[MemoryDiagnoser]
public class LargeFileStreamAsyncPerf
{
    private string _path = string.Empty;

    [Params(LargeFileReadScenario.Sequential, LargeFileReadScenario.ScriptedSequential, LargeFileReadScenario.Sparse)]
    public LargeFileReadScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = Environment.GetEnvironmentVariable("TOUKI_LARGE_FILE")
            ?? throw new InvalidOperationException("Set TOUKI_LARGE_FILE to a generated reference file.");

        using LargeFileReadReferenceReader warm = new(_path, LargeFileReadReference.TunedFileStream, Scenario);
        LargeFileReadReplay.Run(warm, Scenario);
    }

    [Benchmark(Baseline = true)]
    public async Task<long> TunedFileStreamAsync()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.TunedAsyncHandle, Scenario);
        return await LargeFileReadReplay.RunAsync(reader, Scenario).ConfigureAwait(continueOnCapturedContext: false);
    }

    [Benchmark]
    public async Task<long> LargeFileStreamAsync()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.LargeFileStream, Scenario);
        return await LargeFileReadReplay.RunAsync(reader, Scenario).ConfigureAwait(continueOnCapturedContext: false);
    }
}
#endif
