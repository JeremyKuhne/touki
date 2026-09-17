// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace touki.perf;

/// <summary>
///  Baseline measurements for <see cref="StringSegment.CompareTo(string, StringComparison)"/>
///  and <see cref="StringSegment.Equals(StringSegment, StringComparison)"/> under
///  <see cref="StringComparison.OrdinalIgnoreCase"/>. Captured before and after the
///  refactor that pushes the ASCII fast-path into shared <c>SpanExtensions</c>.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class StringSegmentIgnoreCasePerf
{
    private string _a = "";
    private string _b = "";
    private StringSegment _segA;
    private StringSegment _segB;

    [Params(5, 10, 20, 64)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Different-case ASCII so the fold path runs for every char.
        _a = new string('a', Length);
        _b = new string('A', Length);
        _segA = new StringSegment(_a);
        _segB = new StringSegment(_b);
    }

    [Benchmark]
    public int Compare_StringOverload() =>
        _segA.CompareTo(_b, StringComparison.OrdinalIgnoreCase);

    [Benchmark]
    public int Compare_SegmentOverload() =>
        _segA.CompareTo(_segB, StringComparison.OrdinalIgnoreCase);

    [Benchmark]
    public bool Equals_StringComparison() =>
        _segA.Equals(_segB, StringComparison.OrdinalIgnoreCase);
}
