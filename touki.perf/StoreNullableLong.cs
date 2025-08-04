// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StoreNullableLong
{
    [Benchmark(Baseline = true)]
    public long? InOutNullable()
    {
        long? @long = 42;
        Value value = @long;
        value.TryGetValue(out long? result);
        return result;
    }

    [Benchmark]
    public long? InLongOutNullable()
    {
        long @long = 42;
        Value value = @long;
        value.TryGetValue(out long? result);
        return result;
    }

    [Benchmark]
    public long InNullableOut()
    {
        long? @long = 42;
        Value value = @long;
        value.TryGetValue(out long result);
        return result;
    }

    [Benchmark]
    public long? CastInNullableOutNullable()
    {
        long? @long = 42;
        Value value = @long;
        return (long?)value;
    }

    [Benchmark]
    public long? CastInOutNullable()
    {
        long @long = 42;
        Value value = @long;
        return (long?)value;
    }

    [Benchmark]
    public long CastInNullableOut()
    {
        long? @long = 42;
        Value value = @long;
        return (long)value;
    }
}
