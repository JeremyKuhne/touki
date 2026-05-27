// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace touki.perf;

/// <summary>
///  Compares <c>System.Text.Ascii.EqualsIgnoreCase</c> (BCL on net10, touki polyfill on
///  net481) against <c>SpanExtensions.EqualsOrdinalIgnoreCase</c> to confirm both are
///  acceptable for use as the standard ASCII case-insensitive primitive.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class AsciiBclVsHelperPerf
{
    private string _a = "";
    private string _b = "";

    [Params(5, 10, 16, 20, 64)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _a = new string('a', Length);
        _b = new string('A', Length);
    }

    [Benchmark(Baseline = true)]
    public bool TouSpan_Helper() =>
        _a.AsSpan().EqualsOrdinalIgnoreCase(_b.AsSpan());

    [Benchmark]
    public bool Ascii_BclOrPolyfill() =>
        Ascii.EqualsIgnoreCase(_a.AsSpan(), _b.AsSpan());
}
