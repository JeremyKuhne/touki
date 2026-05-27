// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Build.Globbing;
using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Per-pattern-shape match-throughput comparison between
///  <see cref="GlobSpecification"/>(<see cref="GlobDialect.MSBuild"/>) and
///  <see cref="MSBuildGlob"/> — the oracle used by the multi-asterisk and
///  sequential-separator suites. Reads a compiled matcher's <c>IsMatch</c> per
///  invocation; setup cost is paid once.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class OracleMatchMSBuildPerf
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

    private MSBuildGlob _oracleLiteral = null!;
    private MSBuildGlob _oracleSuffix = null!;
    private MSBuildGlob _oracleGlobStar = null!;
    private MSBuildGlob _oracleDeepGlobStar = null!;

    [GlobalSetup]
    public void Setup()
    {
        string root = Directory.GetCurrentDirectory();

        _toukiLiteral = GlobSpecification.Compile("Program.cs", GlobDialect.MSBuild);
        _toukiSuffix = GlobSpecification.Compile("*.cs", GlobDialect.MSBuild);
        _toukiGlobStar = GlobSpecification.Compile("**/*.cs", GlobDialect.MSBuild);
        _toukiDeepGlobStar = GlobSpecification.Compile("a/**/*.cs", GlobDialect.MSBuild);

        _oracleLiteral = MSBuildGlob.Parse(root, "Program.cs");
        _oracleSuffix = MSBuildGlob.Parse(root, "*.cs");
        _oracleGlobStar = MSBuildGlob.Parse(root, "**/*.cs");
        _oracleDeepGlobStar = MSBuildGlob.Parse(root, "a/**/*.cs");
    }

    [Benchmark(Baseline = true)]
    public bool Touki_Literal_Hit() => _toukiLiteral.IsMatch(LiteralInput.AsSpan());

    [Benchmark]
    public bool Oracle_Literal_Hit() => _oracleLiteral.IsMatch(LiteralInput);

    [Benchmark]
    public bool Touki_Literal_Miss() => _toukiLiteral.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_Literal_Miss() => _oracleLiteral.IsMatch(MissInput);

    [Benchmark]
    public bool Touki_Suffix_Hit() => _toukiSuffix.IsMatch(SuffixInput.AsSpan());

    [Benchmark]
    public bool Oracle_Suffix_Hit() => _oracleSuffix.IsMatch(SuffixInput);

    [Benchmark]
    public bool Touki_Suffix_Miss() => _toukiSuffix.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_Suffix_Miss() => _oracleSuffix.IsMatch(MissInput);

    [Benchmark]
    public bool Touki_GlobStar_Hit() => _toukiGlobStar.IsMatch(GlobStarInput.AsSpan());

    [Benchmark]
    public bool Oracle_GlobStar_Hit() => _oracleGlobStar.IsMatch(GlobStarInput);

    [Benchmark]
    public bool Touki_GlobStar_Miss() => _toukiGlobStar.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_GlobStar_Miss() => _oracleGlobStar.IsMatch(MissInput);

    [Benchmark]
    public bool Touki_DeepGlobStar_Hit() => _toukiDeepGlobStar.IsMatch(DeepGlobStarInput.AsSpan());

    [Benchmark]
    public bool Oracle_DeepGlobStar_Hit() => _oracleDeepGlobStar.IsMatch(DeepGlobStarInput);

    [Benchmark]
    public bool Touki_DeepGlobStar_Miss() => _toukiDeepGlobStar.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_DeepGlobStar_Miss() => _oracleDeepGlobStar.IsMatch(MissInput);
}
