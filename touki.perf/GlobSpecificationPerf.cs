// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.RegularExpressions;
using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Measures <see cref="GlobSpecification"/> match throughput and allocation per strategy
///  shape on .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT, against three
///  <see cref="Regex"/> baselines: interpreted, <see cref="RegexOptions.Compiled"/>,
///  and (on modern .NET) <c>[GeneratedRegex]</c> source-generated.
/// </summary>
/// <remarks>
///  <para>
///   Match-time allocations must be zero for every <c>Touki_*</c> benchmark. The
///   <c>Regex_*</c> benchmarks exist purely as comparison points; their allocation
///   profile is not a goal of this library.
///  </para>
///  <para>
///   The <c>RegexGen_*</c> benchmarks only run on modern .NET because
///   <c>[GeneratedRegex]</c> requires the C# source generator that ships with
///   .NET 7+.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public partial class GlobSpecificationPerf
{
    private const string LiteralInput = "Program.cs";
    private const string PrefixInput = "TestThing.cs";
    private const string SuffixInput = "Program.cs";
    private const string ContainsInput = "TheQuickBrownFox.txt";
    private const string PrefixSuffixInput = "alpha-middle-omega";
    private const string CompiledInput = "axxxxbycdef";
    private const string CompiledTailInput = "aXXXbYcd.txt";
    private const string MissInput = "zzzzzzzzzzzzzzzzzz";

    private const string LiteralPattern = @"^Program\.cs$";
    private const string SuffixPattern = @"^.*\.cs$";
    private const string CompiledPattern = @"^a.*b.c[de].*$";

    /// <summary>
    ///  Whether to compile specifications with <see cref="GlobOptions.IgnoreCase"/>. The ASCII
    ///  fold path is exercised regardless of input casing, so both states matter.
    /// </summary>
    [Params(false, true)]
    public bool IgnoreCase { get; set; }

    private GlobSpecification _literal = null!;
    private GlobSpecification _prefix = null!;
    private GlobSpecification _suffix = null!;
    private GlobSpecification _contains = null!;
    private GlobSpecification _prefixSuffix = null!;
    private GlobSpecification _compiled = null!;
    private GlobSpecification _compiledTail = null!;
    private GlobSpecification _any = null!;

    private Regex _literalRegex = null!;
    private Regex _suffixRegex = null!;
    private Regex _compiledRegex = null!;
    private Regex _literalRegexCompiled = null!;
    private Regex _suffixRegexCompiled = null!;
    private Regex _compiledRegexCompiled = null!;

    [GlobalSetup]
    public void Setup()
    {
        GlobOptions options = IgnoreCase ? GlobOptions.IgnoreCase : GlobOptions.None;
        _literal = GlobSpecification.Compile("Program.cs", GlobDialect.Posix, options);
        _prefix = GlobSpecification.Compile("Test*", GlobDialect.Posix, options);
        _suffix = GlobSpecification.Compile("*.cs", GlobDialect.Posix, options);
        _contains = GlobSpecification.Compile("*Brown*", GlobDialect.Posix, options);
        _prefixSuffix = GlobSpecification.Compile("alpha*omega", GlobDialect.Posix, options);
        _compiled = GlobSpecification.Compile("a*b?c[de]*", GlobDialect.Posix, options);
        _compiledTail = GlobSpecification.Compile("a*b?c[de].txt", GlobDialect.Posix, options);
        _any = GlobSpecification.Compile("*", GlobDialect.Posix, options);

        RegexOptions regexOptions = RegexOptions.CultureInvariant
            | (IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

#pragma warning disable SYSLIB1045 // Use GeneratedRegexAttribute; we want both shapes here.
        _literalRegex = new Regex(LiteralPattern, regexOptions);
        _suffixRegex = new Regex(SuffixPattern, regexOptions);
        _compiledRegex = new Regex(CompiledPattern, regexOptions);

        _literalRegexCompiled = new Regex(LiteralPattern, regexOptions | RegexOptions.Compiled);
        _suffixRegexCompiled = new Regex(SuffixPattern, regexOptions | RegexOptions.Compiled);
        _compiledRegexCompiled = new Regex(CompiledPattern, regexOptions | RegexOptions.Compiled);
#pragma warning restore SYSLIB1045
    }

    // --- Touki match-time throughput ---

    [Benchmark(Baseline = true)]
    public bool Touki_Literal_Hit() => _literal.IsMatch(LiteralInput.AsSpan());

    [Benchmark]
    public bool Touki_Literal_Miss() => _literal.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Touki_Prefix_Hit() => _prefix.IsMatch(PrefixInput.AsSpan());

    [Benchmark]
    public bool Touki_Prefix_Miss() => _prefix.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Touki_Suffix_Hit() => _suffix.IsMatch(SuffixInput.AsSpan());

    [Benchmark]
    public bool Touki_Suffix_Miss() => _suffix.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Touki_Contains_Hit() => _contains.IsMatch(ContainsInput.AsSpan());

    [Benchmark]
    public bool Touki_Contains_Miss() => _contains.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Touki_PrefixSuffix_Hit() => _prefixSuffix.IsMatch(PrefixSuffixInput.AsSpan());

    [Benchmark]
    public bool Touki_PrefixSuffix_Miss() => _prefixSuffix.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Touki_Compiled_Hit() => _compiled.IsMatch(CompiledInput.AsSpan());

    [Benchmark]
    public bool Touki_Compiled_Miss() => _compiled.IsMatch(MissInput.AsSpan());

    [Benchmark]
    public bool Touki_Any_Hit() => _any.IsMatch(LiteralInput.AsSpan());

    [Benchmark]
    public bool Touki_CompiledTail_Hit() => _compiledTail.IsMatch(CompiledTailInput.AsSpan());

    [Benchmark]
    public bool Touki_CompiledTail_Miss() => _compiledTail.IsMatch(MissInput.AsSpan());

    // --- Regex: interpreted ---

    [Benchmark]
    public bool Regex_Literal_Hit() => _literalRegex.IsMatch(LiteralInput);

    [Benchmark]
    public bool Regex_Suffix_Hit() => _suffixRegex.IsMatch(SuffixInput);

    [Benchmark]
    public bool Regex_Compiled_Hit() => _compiledRegex.IsMatch(CompiledInput);

    // --- Regex: RegexOptions.Compiled ---

    [Benchmark]
    public bool RegexCompiled_Literal_Hit() => _literalRegexCompiled.IsMatch(LiteralInput);

    [Benchmark]
    public bool RegexCompiled_Suffix_Hit() => _suffixRegexCompiled.IsMatch(SuffixInput);

    [Benchmark]
    public bool RegexCompiled_Compiled_Hit() => _compiledRegexCompiled.IsMatch(CompiledInput);

#if NET
    // --- Regex: [GeneratedRegex] source-generated (modern .NET only) ---

    [GeneratedRegex(LiteralPattern, RegexOptions.CultureInvariant)]
    private static partial Regex LiteralRegexGen();

    [GeneratedRegex(SuffixPattern, RegexOptions.CultureInvariant)]
    private static partial Regex SuffixRegexGen();

    [GeneratedRegex(CompiledPattern, RegexOptions.CultureInvariant)]
    private static partial Regex CompiledRegexGen();

    [Benchmark]
    public bool RegexGen_Literal_Hit() => LiteralRegexGen().IsMatch(LiteralInput);

    [Benchmark]
    public bool RegexGen_Suffix_Hit() => SuffixRegexGen().IsMatch(SuffixInput);

    [Benchmark]
    public bool RegexGen_Compiled_Hit() => CompiledRegexGen().IsMatch(CompiledInput);
#endif
}

/// <summary>
///  Measures the cost of <see cref="GlobSpecification.Compile(Touki.Text.StringSegment, Touki.Io.Globbing.GlobDialect, Touki.Io.Globbing.GlobOptions, Touki.Io.Globbing.GlobPathSeparator, int)"/> for each pattern shape.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class GlobSpecificationCompilePerf
{
    [Benchmark]
    public GlobSpecification Compile_Literal() => GlobSpecification.Compile("Program.cs", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Compile_Prefix() => GlobSpecification.Compile("Test*", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Compile_Suffix() => GlobSpecification.Compile("*.cs", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Compile_Contains() => GlobSpecification.Compile("*Brown*", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Compile_PrefixSuffix() => GlobSpecification.Compile("alpha*omega", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Compile_Any() => GlobSpecification.Compile("*", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Compile_General() => GlobSpecification.Compile("a*b?c[de]*", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Compile_ManyClasses() =>
        GlobSpecification.Compile("[Tt]est_[0-9][0-9]_*.[ch]s", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Compile_PosixNamedClass() =>
        GlobSpecification.Compile("[[:alpha:]_][[:alnum:]_]*", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Compile_MixedClassesAndNamed() =>
        GlobSpecification.Compile("[[:upper:]][a-z]*_[[:digit:]][0-9].[ch]s", GlobDialect.Posix);
}
