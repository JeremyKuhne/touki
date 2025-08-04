// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StringFormattingDateTime
{
    private readonly DateTime _value = DateTime.Now;

    [Benchmark(Baseline = true)]
    public string StringFormat()
    {
        return string.Format("The time was {0}.", _value);
    }

    [Benchmark]
    public string StringInterpolation()
    {
        return $"The time was {_value}.";
    }

    [Benchmark]
    public string StringsFormat()
    {
        return Strings.Format("The time was {0}.", _value);
    }
}
