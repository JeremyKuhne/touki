// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StoreEnum
{
    [Benchmark(Baseline = true)]
    public DayOfWeek TryOut()
    {
        Value value = Value.Create(DayOfWeek.Monday);
        value.TryGetValue(out DayOfWeek result);
        return result;
    }
}
