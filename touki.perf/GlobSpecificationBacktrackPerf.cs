// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Targets the backtracking path of <see cref="GlobSpecification"/> -- the two-savepoint
///  <c>Backtrack</c> helper inside <c>CompiledGlobStrategy</c> -- with patterns that
///  force the matcher to walk the AnyRun / GlobStar slots repeatedly. The mainline
///  <c>MsBuildEnumeratePerf2</c> benchmark mostly hits the tail-anchor fast-fail and
///  rarely calls <c>Backtrack</c>; this one isolates the cost when backtracking actually
///  happens.
/// </summary>
/// <remarks>
///  <para>
///   Each benchmark exercises one of:
///   <list type="bullet">
///    <item>
///     <description>
///      Heavy AnyRun backtracking: <c>*a*b*c*d*e.cs</c> against an input that nearly
///      matches and forces the last AnyRun to walk most of the input before failing or
///      succeeding.
///     </description>
///    </item>
///    <item>
///     <description>
///      GlobStar (path-aware) backtracking: <c>**/foo/**/bar/**/*.cs</c> against deep
///      paths where each <c>**</c> may have to try multiple separator boundaries.
///     </description>
///    </item>
///    <item>
///     <description>
///      Combined AnyRun + GlobStar: <c>**/*a*b*c.cs</c> against deep paths to ensure
///      both savepoint slots are active simultaneously.
///     </description>
///    </item>
///   </list>
///  </para>
///  <para>
///   All <c>IsMatch</c> calls must remain allocation-free.
///  </para>
/// </remarks>
[MemoryDiagnoser]
public class GlobSpecificationBacktrackPerf
{
    // --- AnyRun-only patterns ---

    // *a*b*c*d*e.cs: five AnyRun savepoints, each followed by a single literal char.
    // Hit: trailing ".cs" matches via tail-anchor, then the NFA walks AnyRun slots.
    // Miss: no ".cs" tail forces NFA full walk after fast-fail elimination.
    private const string AnyRunPattern = "*a*b*c*d*e.cs";
    private const string AnyRunHitInput = "xxxaxxxbxxxcxxxdxxxe.cs";
    private const string AnyRunHardHitInput = "aaaaaaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbcccccccccccccdddddddddddeeeeeeeeeeeee.cs";
    private const string AnyRunMissInput = "xxxaxxxbxxxcxxxdxxxe.txt";
    private const string AnyRunPartialMissInput = "xxxaxxxbxxxcxxxdxxx.cs";

    // --- GlobStar (path-aware) patterns ---

    // **/foo/**/bar/**/*.cs: three GlobStar opcodes with literal segments between them.
    // Forces the GlobStar slot to walk separator positions repeatedly when intermediate
    // literals don't match cleanly.
    private const string GlobStarPattern = "**/foo/**/bar/**/*.cs";
    private const string GlobStarHitInput = "a/b/c/foo/d/e/bar/f/g/h.cs";
    private const string GlobStarDeepHitInput = "a/b/c/d/e/f/g/h/i/j/foo/k/l/m/n/o/p/bar/q/r/s/t/u/v/w.cs";
    private const string GlobStarMissInput = "a/b/c/foo/d/e/baz/f/g/h.cs";
    private const string GlobStarDeepMissInput = "a/b/c/d/e/f/g/h/i/j/fox/k/l/m/n/o/p/baz/q/r/s/t/u/v/w.cs";

    // --- Combined AnyRun + GlobStar ---

    private const string CombinedPattern = "**/*a*b*c.cs";
    private const string CombinedHitInput = "x/y/z/aaaa-bbbb-cccc-final-axbxc.cs";
    private const string CombinedMissInput = "x/y/z/aaaa-bbbb-cccc-final-axbxd.cs";

    private GlobSpecification _anyRun = null!;
    private GlobSpecification _globStar = null!;
    private GlobSpecification _combined = null!;

    [GlobalSetup]
    public void Setup()
    {
        _anyRun = GlobSpecification.Compile(AnyRunPattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar);
        _globStar = GlobSpecification.Compile(GlobStarPattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar);
        _combined = GlobSpecification.Compile(CombinedPattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar);
    }

    // --- AnyRun ---

    [Benchmark(Baseline = true)]
    public bool AnyRun_Hit() => _anyRun.IsMatch(AnyRunHitInput.AsSpan());

    [Benchmark]
    public bool AnyRun_HardHit() => _anyRun.IsMatch(AnyRunHardHitInput.AsSpan());

    [Benchmark]
    public bool AnyRun_Miss() => _anyRun.IsMatch(AnyRunMissInput.AsSpan());

    [Benchmark]
    public bool AnyRun_PartialMiss() => _anyRun.IsMatch(AnyRunPartialMissInput.AsSpan());

    // --- GlobStar ---

    [Benchmark]
    public bool GlobStar_Hit() => _globStar.IsMatch(GlobStarHitInput.AsSpan());

    [Benchmark]
    public bool GlobStar_DeepHit() => _globStar.IsMatch(GlobStarDeepHitInput.AsSpan());

    [Benchmark]
    public bool GlobStar_Miss() => _globStar.IsMatch(GlobStarMissInput.AsSpan());

    [Benchmark]
    public bool GlobStar_DeepMiss() => _globStar.IsMatch(GlobStarDeepMissInput.AsSpan());

    // --- Combined ---

    [Benchmark]
    public bool Combined_Hit() => _combined.IsMatch(CombinedHitInput.AsSpan());

    [Benchmark]
    public bool Combined_Miss() => _combined.IsMatch(CombinedMissInput.AsSpan());
}
