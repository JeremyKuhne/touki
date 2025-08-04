// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StoreLong
{
    [Benchmark]
    public long As()
    {
        Value value = 42L;
        return value.As<long>();
    }

    [Benchmark(Baseline = true)]
    public long TryGet()
    {
        Value value = 42L;
        value.TryGetValue(out long result);
        return result;
    }

    [Benchmark]
    public long CastOut()
    {
        Value value = 42L;
        return (long)value;
    }
}
