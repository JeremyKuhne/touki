// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Extensions.FileSystemGlobbing;
using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Per-pattern-shape match-throughput comparison between
///  <see cref="GlobSpecification"/>(<see cref="GlobDialect.FileSystemGlobbing"/>) and
///  <see cref="Matcher"/>. <c>Matcher</c> requires per-call setup of a fresh
///  include set; the <c>Oracle_*</c> benchmarks compile that into the matcher once
///  and call <see cref="MatcherExtensions.Match(Matcher, string)"/> per input.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class OracleMatchFileSystemGlobbingPerf
{
    private const string LiteralInput = "Program.cs";
    private const string SuffixInput = "Program.cs";
    private const string GlobStarInput = "src/lib/Program.cs";
    private const string DeepGlobStarInput = "a/b/c/d/e/Program.cs";
    private const string MissInput = "zzzzzzzzzzzzzzzz";

    private GlobSpecification _toukiLiteral = null!;
    private GlobSpecification _toukiSuffix = null!;
    private GlobSpecification _toukiGlobStar = null!;
    private GlobSpecification _toukiDeepGlobStar = null!;

    private Matcher _oracleLiteral = null!;
    private Matcher _oracleSuffix = null!;
    private Matcher _oracleGlobStar = null!;
    private Matcher _oracleDeepGlobStar = null!;

    [GlobalSetup]
    public void Setup()
    {
        _toukiLiteral = GlobSpecification.Compile("Program.cs", GlobDialect.FileSystemGlobbing);
        _toukiSuffix = GlobSpecification.Compile("*.cs", GlobDialect.FileSystemGlobbing);
        _toukiGlobStar = GlobSpecification.Compile("**/*.cs", GlobDialect.FileSystemGlobbing);
        _toukiDeepGlobStar = GlobSpecification.Compile("a/**/*.cs", GlobDialect.FileSystemGlobbing);

        _oracleLiteral = MakeMatcher("Program.cs");
        _oracleSuffix = MakeMatcher("*.cs");
        _oracleGlobStar = MakeMatcher("**/*.cs");
        _oracleDeepGlobStar = MakeMatcher("a/**/*.cs");
    }

    private static Matcher MakeMatcher(string pattern)
    {
        Matcher m = new(StringComparison.Ordinal);
        m.AddInclude(pattern);
        return m;
    }

    [Benchmark(Baseline = true)]
    public bool Touki_Literal_Hit() => _toukiLiteral.IsMatch(LiteralInput.AsSpan());

    [Benchmark]
    public bool Oracle_Literal_Hit() => MatcherExtensions.Match(_oracleLiteral, LiteralInput).HasMatches;

    [Benchmark]
    public bool Touki_Literal_Miss() => _toukiLiteral.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_Literal_Miss() => MatcherExtensions.Match(_oracleLiteral, MissInput).HasMatches;

    [Benchmark]
    public bool Touki_Suffix_Hit() => _toukiSuffix.IsMatch(SuffixInput.AsSpan());

    [Benchmark]
    public bool Oracle_Suffix_Hit() => MatcherExtensions.Match(_oracleSuffix, SuffixInput).HasMatches;

    [Benchmark]
    public bool Touki_Suffix_Miss() => _toukiSuffix.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_Suffix_Miss() => MatcherExtensions.Match(_oracleSuffix, MissInput).HasMatches;

    [Benchmark]
    public bool Touki_GlobStar_Hit() => _toukiGlobStar.IsMatch(GlobStarInput.AsSpan());

    [Benchmark]
    public bool Oracle_GlobStar_Hit() => MatcherExtensions.Match(_oracleGlobStar, GlobStarInput).HasMatches;

    [Benchmark]
    public bool Touki_GlobStar_Miss() => _toukiGlobStar.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_GlobStar_Miss() => MatcherExtensions.Match(_oracleGlobStar, MissInput).HasMatches;

    [Benchmark]
    public bool Touki_DeepGlobStar_Hit() => _toukiDeepGlobStar.IsMatch(DeepGlobStarInput.AsSpan());

    [Benchmark]
    public bool Oracle_DeepGlobStar_Hit() => MatcherExtensions.Match(_oracleDeepGlobStar, DeepGlobStarInput).HasMatches;

    [Benchmark]
    public bool Touki_DeepGlobStar_Miss() => _toukiDeepGlobStar.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_DeepGlobStar_Miss() => MatcherExtensions.Match(_oracleDeepGlobStar, MissInput).HasMatches;
}
