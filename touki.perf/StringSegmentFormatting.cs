// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StringSegmentFormatting
{
    private readonly StringSegment _valueOne = new("Hello World!", 0, 5);
    private readonly StringSegment _valueTwo = new("Hello Universe!", 6, 9);

    [Benchmark(Baseline = true)]
    public string StringFormat()
    {
        return string.Format("{0} {1}", _valueOne, _valueTwo);
    }

    [Benchmark]
    public string StringInterpolation()
    {
        return $"{_valueOne} {_valueTwo}";
    }

    [Benchmark]
    public string StringsFormat()
    {
        return Strings.Format("{0} {1}", _valueOne, _valueTwo);
    }
}
