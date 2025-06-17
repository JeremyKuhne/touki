// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using BenchmarkDotNet.Attributes;

namespace touki.perf;

[MemoryDiagnoser]
public class StringFormatting
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
        return string.Format("The answer is {0}.", _value);
    }

    [Benchmark]
    public string StringInterpolation()
    {
        return $"The answer is {_value}.";
    }
}
