// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Targets the recursive extglob walker (<c>TryMatchRanges</c> in
///  <c>CompiledGlobStrategy</c>) directly via <see cref="GlobSpecification.IsMatch"/>.
///  Unlike <c>GlobSpecificationBacktrackPerf</c> (which exercises the non-extglob
///  two-savepoint NFA) every pattern here contains an <c>AltStart</c> opcode and is
///  not the pure <c>*literal</c> suffix-set shape, so each call flows through the
///  recursive bytecode interpreter rather than a specialized strategy.
/// </summary>
/// <remarks>
///  <para>
///   The common-path benchmarks (single-iteration <c>@(...)</c>, prefix alternation,
///   negation) measure the per-call overhead of any walker instrumentation. The
///   bounded-backtracking benchmarks (<c>+(...)</c> with overlapping alternatives)
///   exercise the path that, without memoization, degrades super-linearly; they use
///   inputs short enough to remain fast even on the un-memoized walker so the
///   benchmark itself terminates quickly.
///  </para>
///  <para>
///   All <c>IsMatch</c> calls must remain allocation-free on the common path.
///  </para>
/// </remarks>
[MemoryDiagnoser]
public class GlobExtGlobMatchPerf
{
    // Prefix alternation over literals followed by a globstar tail. Each segment
    // tries `bin`/`obj` then descends; flows through the walker (not MultiSuffix).
    private const string PrefixAltPattern = "@(bin|obj)/**/*.cs";
    private const string PrefixAltHitInput = "bin/a/b/c/file.cs";
    private const string PrefixAltMissInput = "src/a/b/c/file.cs";

    // Negation at the first segment: not bin/obj, then any descendant `.cs`.
    private const string NegationPattern = "!(bin|obj)/**/*.cs";
    private const string NegationHitInput = "src/a/b/c/file.cs";
    private const string NegationMissInput = "bin/a/b/c/file.cs";

    // Single-iteration alternation of `*literal` bodies that is NOT lowered to
    // MultiSuffix because of the surrounding literal prefix; stays on the walker.
    private const string AtAltPattern = "src/@(*.cs|*.md|*.txt)";
    private const string AtAltHitInput = "src/readme.md";
    private const string AtAltMissInput = "src/readme.rst";

    // Bounded repeating alternation with overlapping alternatives. Without
    // memoization this is the shape that grows quickly; inputs kept short so the
    // un-memoized baseline still completes in microseconds.
    private const string RepeatPattern = "+(ab|a|b)c.cs";
    private const string RepeatHitInput = "ababababc.cs";
    private const string RepeatMissInput = "ababababd.cs";

    private GlobSpecification _prefixAlt = null!;
    private GlobSpecification _negation = null!;
    private GlobSpecification _atAlt = null!;
    private GlobSpecification _repeat = null!;

    [GlobalSetup]
    public void Setup()
    {
        GlobOptions options = GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob;
        _prefixAlt = GlobSpecification.Compile(PrefixAltPattern, GlobDialect.Bash, options);
        _negation = GlobSpecification.Compile(NegationPattern, GlobDialect.Bash, options);
        _atAlt = GlobSpecification.Compile(AtAltPattern, GlobDialect.Bash, options);
        _repeat = GlobSpecification.Compile(RepeatPattern, GlobDialect.Bash, options);
    }

    [Benchmark(Baseline = true)]
    public bool PrefixAlt_Hit() => _prefixAlt.IsMatch(PrefixAltHitInput.AsSpan());

    [Benchmark]
    public bool PrefixAlt_Miss() => _prefixAlt.IsMatch(PrefixAltMissInput.AsSpan());

    [Benchmark]
    public bool Negation_Hit() => _negation.IsMatch(NegationHitInput.AsSpan());

    [Benchmark]
    public bool Negation_Miss() => _negation.IsMatch(NegationMissInput.AsSpan());

    [Benchmark]
    public bool AtAlt_Hit() => _atAlt.IsMatch(AtAltHitInput.AsSpan());

    [Benchmark]
    public bool AtAlt_Miss() => _atAlt.IsMatch(AtAltMissInput.AsSpan());

    [Benchmark]
    public bool Repeat_Hit() => _repeat.IsMatch(RepeatHitInput.AsSpan());

    [Benchmark]
    public bool Repeat_Miss() => _repeat.IsMatch(RepeatMissInput.AsSpan());
}
