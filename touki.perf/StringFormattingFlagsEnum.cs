// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using BenchmarkDotNet.Attributes;
using Touki;

namespace touki.perf;

[MemoryDiagnoser]
public class StringFormattingFlagsEnum
{
    private readonly FileAttributes _value = FileAttributes.ReadOnly | FileAttributes.Hidden;

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

    [Benchmark]
    public string StringsFormat()
    {
        return Strings.Format("The answer is {0}.", _value);
    }
}
