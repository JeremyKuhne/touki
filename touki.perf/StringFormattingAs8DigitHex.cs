// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StringFormattingAs8DigitHex
{
    private readonly int _value = 42;

    [GlobalSetup]
    public void Setup()
    {
        _ = $"The answer is {_value}.";
    }

    [Benchmark(Baseline = true)]
    public string StringFormat()
    {
        // Using "X8" to format the integer as an 8-digit hexadecimal string.
        return string.Format("The answer is {0:X8}.", _value);
    }

    [Benchmark]
    public string StringsFormat()
    {
        // Using "X8" to format the integer as an 8-digit hexadecimal string.
        return Strings.Format("The answer is {0:X8}.", _value);
    }

    [Benchmark]
    public string StringInterpolation()
    {
        return $"The answer is {_value:X8}.";
    }
}
