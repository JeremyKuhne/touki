// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

/// <summary>
///  Direct comparison of <c>SpanExtensions.EqualsOrdinalIgnoreCase</c> against the BCL
///  <c>MemoryExtensions.Equals(span, span, StringComparison.OrdinalIgnoreCase)</c> on
///  both .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT. Settles whether the inline
///  ASCII fast-path is worth keeping on each TFM.
/// </summary>
/// <remarks>
///  <para>
///   The hit-with-different-case variant is the worst case for the framework dispatch
///   path: the BCL must do its full OrdinalIgnoreCase compare. The miss-with-length
///   variant is the best case for both: a single length compare. Mid lengths (5, 10,
///   20) cover the typical glob-pattern range; 64 covers the threshold where
///   vectorization should start to dominate.
///  </para>
/// </remarks>
[MemoryDiagnoser]
// Re-enable `[DisassemblyDiagnoser(maxDepth: 3, printSource: true, exportGithubMarkdown: true)]`
// when investigating codegen. Captured disasm for both TFMs lives in
// docs/bcl-ignorecase-valley-rca.md (the analysis writeup).
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class AsciiIgnoreCasePerf
{
    [Params(5, 10, 20, 64)]
    public int Length { get; set; }

    private string _a = null!;
    private string _b = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Same content, mixed cases: forces an actual compare, both sides ASCII letters.
        _a = new string('a', Length);
        _b = new string('A', Length);
    }

    [Benchmark(Baseline = true)]
    public bool Helper_HitDifferentCase() =>
        _a.AsSpan().EqualsOrdinalIgnoreCase(_b.AsSpan());

    [Benchmark]
    public bool Bcl_HitDifferentCase() =>
        _a.AsSpan().Equals(_b.AsSpan(), StringComparison.OrdinalIgnoreCase);

    [Benchmark]
    public bool Bcl_StartsWithDifferentCase() =>
        _a.AsSpan().StartsWith(_b.AsSpan(), StringComparison.OrdinalIgnoreCase);

    [Benchmark]
    public bool Bcl_EndsWithDifferentCase() =>
        _a.AsSpan().EndsWith(_b.AsSpan(), StringComparison.OrdinalIgnoreCase);

    [Benchmark]
    public int Bcl_IndexOfDifferentCase() =>
        _a.AsSpan().IndexOf(_b.AsSpan(), StringComparison.OrdinalIgnoreCase);
}
