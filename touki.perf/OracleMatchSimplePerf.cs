// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

using System.IO.Enumeration;
using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Per-pattern-shape match-throughput comparison between
///  <see cref="GlobSpecification"/>(<see cref="GlobDialect.Simple"/>) and the BCL
///  <see cref="FileSystemName.MatchesSimpleExpression(System.ReadOnlySpan{char}, System.ReadOnlySpan{char}, bool)"/>
///  helper. The BCL implementation is the reference for the Simple dialect -
///  every <see cref="Directory.EnumerateFiles(string, string)"/>-style call routes
///  through it.
/// </summary>
/// <remarks>
///  <para>
///   net10 only; <see cref="FileSystemName"/> does not exist on .NET Framework.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class OracleMatchSimplePerf
{
    private const string LiteralPattern = "Program.cs";
    private const string LiteralInput = "Program.cs";
    private const string PrefixPattern = "Test*";
    private const string PrefixInput = "TestThing.cs";
    private const string SuffixPattern = "*.cs";
    private const string SuffixInput = "Program.cs";
    private const string ContainsPattern = "*Brown*";
    private const string ContainsInput = "TheQuickBrownFox.txt";
    private const string GeneralPattern = "a*b?c[de]*";
    private const string GeneralInput = "axxxxbycdef";
    private const string MissInput = "zzzzzzzzzzzzzzzz";

    private GlobSpecification _toukiLiteral = null!;
    private GlobSpecification _toukiPrefix = null!;
    private GlobSpecification _toukiSuffix = null!;
    private GlobSpecification _toukiContains = null!;
    private GlobSpecification _toukiGeneral = null!;

    [GlobalSetup]
    public void Setup()
    {
        _toukiLiteral = GlobSpecification.Compile(LiteralPattern, GlobDialect.Simple);
        _toukiPrefix = GlobSpecification.Compile(PrefixPattern, GlobDialect.Simple);
        _toukiSuffix = GlobSpecification.Compile(SuffixPattern, GlobDialect.Simple);
        _toukiContains = GlobSpecification.Compile(ContainsPattern, GlobDialect.Simple);
        _toukiGeneral = GlobSpecification.Compile(GeneralPattern, GlobDialect.Simple);
    }

    [Benchmark(Baseline = true)]
    public bool Touki_Literal_Hit() => _toukiLiteral.IsMatch(LiteralInput.AsSpan());

    [Benchmark]
    public bool Oracle_Literal_Hit() =>
        FileSystemName.MatchesSimpleExpression(LiteralPattern, LiteralInput, ignoreCase: false);

    [Benchmark]
    public bool Touki_Literal_Miss() => _toukiLiteral.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_Literal_Miss() =>
        FileSystemName.MatchesSimpleExpression(LiteralPattern, MissInput, ignoreCase: false);

    [Benchmark]
    public bool Touki_Prefix_Hit() => _toukiPrefix.IsMatch(PrefixInput.AsSpan());

    [Benchmark]
    public bool Oracle_Prefix_Hit() =>
        FileSystemName.MatchesSimpleExpression(PrefixPattern, PrefixInput, ignoreCase: false);

    [Benchmark]
    public bool Touki_Prefix_Miss() => _toukiPrefix.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_Prefix_Miss() =>
        FileSystemName.MatchesSimpleExpression(PrefixPattern, MissInput, ignoreCase: false);

    [Benchmark]
    public bool Touki_Suffix_Hit() => _toukiSuffix.IsMatch(SuffixInput.AsSpan());

    [Benchmark]
    public bool Oracle_Suffix_Hit() =>
        FileSystemName.MatchesSimpleExpression(SuffixPattern, SuffixInput, ignoreCase: false);

    [Benchmark]
    public bool Touki_Suffix_Miss() => _toukiSuffix.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_Suffix_Miss() =>
        FileSystemName.MatchesSimpleExpression(SuffixPattern, MissInput, ignoreCase: false);

    [Benchmark]
    public bool Touki_Contains_Hit() => _toukiContains.IsMatch(ContainsInput.AsSpan());

    [Benchmark]
    public bool Oracle_Contains_Hit() =>
        FileSystemName.MatchesSimpleExpression(ContainsPattern, ContainsInput, ignoreCase: false);

    [Benchmark]
    public bool Touki_Contains_Miss() => _toukiContains.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_Contains_Miss() =>
        FileSystemName.MatchesSimpleExpression(ContainsPattern, MissInput, ignoreCase: false);

    [Benchmark]
    public bool Touki_General_Hit() => _toukiGeneral.IsMatch(GeneralInput.AsSpan());

    [Benchmark]
    public bool Oracle_General_Hit() =>
        FileSystemName.MatchesSimpleExpression(GeneralPattern, GeneralInput, ignoreCase: false);

    [Benchmark]
    public bool Touki_General_Miss() => _toukiGeneral.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Oracle_General_Miss() =>
        FileSystemName.MatchesSimpleExpression(GeneralPattern, MissInput, ignoreCase: false);
}

#endif
