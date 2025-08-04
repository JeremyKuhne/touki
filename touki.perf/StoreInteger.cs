// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StoreInteger
{
    [Benchmark]
    public int As()
    {
        Value value = 42;
        return value.As<int>();
    }

    [Benchmark]
    public int TryGet()
    {
        Value value = 42;
        value.TryGetValue(out int result);
        return result;
    }
}
