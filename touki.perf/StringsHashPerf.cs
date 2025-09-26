// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StringsHashPerf
{
    // Generate a benchmark that tests random strings (generated from a fixed seed) of 10, 100, 200, and 1000 characters.
    private const int Seed = 12345;
    private string _data = "";

    [Params(10, 20, 30, 40, 100)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        Random random = new(Seed);
        char[] chars = new char[N];
        for (int i = 0; i < N; i++)
        {
            // Generate a random Unicode character
            chars[i] = (char)random.Next(32, ushort.MaxValue);
        }

        _data = new string(chars);
    }

    [Benchmark(Baseline = true)]
    public int BuiltIn()
    {
        return _data.GetHashCode();
    }

    [Benchmark]
    public int Custom()
    {
        return string.GetHashCode(_data.AsSpan());
    }
}
