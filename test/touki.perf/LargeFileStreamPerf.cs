// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
public class LargeFileStreamPerf
{
    private string _path = string.Empty;

    [Params(LargeFileReadScenario.Sequential, LargeFileReadScenario.ScriptedSequential, LargeFileReadScenario.Sparse)]
    public LargeFileReadScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = Environment.GetEnvironmentVariable("TOUKI_LARGE_FILE")
            ?? throw new InvalidOperationException("Set TOUKI_LARGE_FILE to a generated reference file.");

        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.TunedFileStream, Scenario);
        LargeFileReadReplay.Run(reader, Scenario);
    }

    [Benchmark(Baseline = true)]
    public long TunedFileStream()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.TunedFileStream, Scenario);
        return LargeFileReadReplay.Run(reader, Scenario);
    }

    [Benchmark]
    public long NaiveFileStream()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.NaiveFileStream, Scenario);
        return LargeFileReadReplay.Run(reader, Scenario);
    }

    [Benchmark]
    public long Raw()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.Raw, Scenario);
        return LargeFileReadReplay.Run(reader, Scenario);
    }

#if NET
    [Benchmark]
    public long LargeFileStreamDualTransport()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.LargeFileStreamDualTransport, Scenario);
        return LargeFileReadReplay.Run(reader, Scenario);
    }

    [Benchmark]
    public long LargeFileStreamDualTimedTransport()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.LargeFileStreamDualTimedTransport, Scenario);
        return LargeFileReadReplay.Run(reader, Scenario);
    }

    [Benchmark]
    public long LargeFileStreamSyncTransport()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.LargeFileStreamSyncTransport, Scenario);
        return LargeFileReadReplay.Run(reader, Scenario);
    }

    [Benchmark]
    public long RawAsyncHandle()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.RawAsyncHandle, Scenario);
        return LargeFileReadReplay.Run(reader, Scenario);
    }

    [Benchmark]
    public long TunedAsyncHandle()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.TunedAsyncHandle, Scenario);
        return LargeFileReadReplay.Run(reader, Scenario);
    }

    [Benchmark]
    public long LargeFileStream()
    {
        using LargeFileReadReferenceReader reader = new(_path, LargeFileReadReference.LargeFileStream, Scenario);
        return LargeFileReadReplay.Run(reader, Scenario);
    }
#endif
}
