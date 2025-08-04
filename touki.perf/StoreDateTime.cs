// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
// [SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StoreDateTime
{
    private static readonly DateTime s_now = DateTime.Now;
    private static readonly DateTimeOffset s_offsetUtc = DateTime.UtcNow;
    private static readonly DateTimeOffset s_offset = DateTime.Now;

    [Benchmark(Baseline = true)]
    public DateTime InOutDateTime()
    {
        Value value = s_now;
        value.TryGetValue(out DateTime result);
        return result;
    }

    [Benchmark]
    public DateTimeOffset InOutDateTimeOffsetUTC()
    {
        Value value = s_offsetUtc;
        value.TryGetValue(out DateTimeOffset result);
        return result;
    }

    [Benchmark]
    public DateTimeOffset InOutDateTimeOffset()
    {
        Value value = s_offset;
        value.TryGetValue(out DateTimeOffset result);
        return result;
    }
}
