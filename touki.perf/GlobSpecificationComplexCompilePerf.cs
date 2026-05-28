// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace touki.perf;

/// <summary>
///  Compile-time cost baseline for complex <see cref="GlobSpecification"/> inputs -
///  the kind that hit the full bytecode encoder, the
///  <c>TryNormalizeRuns</c> per-dialect normalization, the gitignore marker strip,
///  and the segment-bounded globstar emitter. Use this as the regression line for
///  clarity / perf tweaks to <c>GlobSpecificationFactory</c>.
/// </summary>
/// <remarks>
///  <para>
///   The simpler shapes (literal / prefix / suffix / contains / single-class) are
///   already covered by <see cref="GlobSpecificationCompilePerf"/>. This class focuses
///   on the work that actually exercises the factory's path-aware branches and the
///   ValueStringBuilder rental path:
///  </para>
///  <para>
///   - Path-aware globstar emission with surrounding-separator absorption.<br/>
///   - Sequential-separator collapse (MSBuild) and internal-empty-segment rewrite (FSG).<br/>
///   - Multi-asterisk-run collapse (Bash/Git globstar; FSG/Simple/Posix to <c>*</c>).<br/>
///   - Gitignore marker strip + match-anywhere prefix prepend.<br/>
///   - Long patterns that grow ValueStringBuilder off its stack buffer.<br/>
///   - Nested POSIX named character classes that expand inline.
///  </para>
///  <para>
///   All compile paths allocate (the resulting matcher and its program string); the
///   benchmark's value is the <i>delta</i> between runs, not absolute zero.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class GlobSpecificationComplexCompilePerf
{
    // ---- Path-aware globstar (full bytecode + tail-anchor analysis) ----

    [Benchmark]
    public GlobSpecification GlobStar_Bash_Simple() =>
        GlobSpecification.Compile("**/*.cs", GlobDialect.Bash, GlobOptions.AllowGlobStar);

    [Benchmark]
    public GlobSpecification GlobStar_Bash_Anchored() =>
        GlobSpecification.Compile("src/**/*.cs", GlobDialect.Bash, GlobOptions.AllowGlobStar);

    [Benchmark]
    public GlobSpecification GlobStar_Bash_Sandwich() =>
        GlobSpecification.Compile("src/**/test/**/*.cs", GlobDialect.Bash, GlobOptions.AllowGlobStar);

    [Benchmark]
    public GlobSpecification GlobStar_MSBuild_Implicit() =>
        GlobSpecification.Compile("**/*.cs", GlobDialect.MSBuild);

    [Benchmark]
    public GlobSpecification GlobStar_FSG_Implicit() =>
        GlobSpecification.Compile("**/*.cs", GlobDialect.FileSystemGlobbing);

    // ---- Normalization-triggering inputs (TryNormalizeRuns hot path) ----

    [Benchmark]
    public GlobSpecification Normalize_MSBuild_SeparatorRun() =>
        GlobSpecification.Compile("a//**//b/*.cs", GlobDialect.MSBuild);

    [Benchmark]
    public GlobSpecification Normalize_FSG_EmptySegment() =>
        GlobSpecification.Compile("a//b/*.cs", GlobDialect.FileSystemGlobbing);

    [Benchmark]
    public GlobSpecification Normalize_Bash_AsteriskRun() =>
        GlobSpecification.Compile("a***b/***.cs", GlobDialect.Bash, GlobOptions.AllowGlobStar);

    [Benchmark]
    public GlobSpecification Normalize_Git_AsteriskRun() =>
        GlobSpecification.Compile("a***/***/.cs", GlobDialect.Git);

    [Benchmark]
    public GlobSpecification Normalize_MSBuild_NeverMatch() =>
        GlobSpecification.Compile("a***b/*.cs", GlobDialect.MSBuild);

    // ---- Gitignore preprocessing (marker strip + match-anywhere prepend) ----

    [Benchmark]
    public GlobSpecification Git_Negated_Anchored_DirOnly() =>
        GlobSpecification.Compile("!/build/", GlobDialect.Git);

    [Benchmark]
    public GlobSpecification Git_MatchAnywhere_Suffix() =>
        GlobSpecification.Compile("*.log", GlobDialect.Git);

    [Benchmark]
    public GlobSpecification Git_DeepGlobStar() =>
        GlobSpecification.Compile("**/node_modules/**/dist/**/*.js", GlobDialect.Git);

    // ---- Long patterns (force ValueStringBuilder ArrayPool growth) ----

    [Benchmark]
    public GlobSpecification Long_DeepLiteral() =>
        GlobSpecification.Compile(
            "src/a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q/r/s/t/u/v/w/x/y/z/lib/internals/Program.cs",
            GlobDialect.MSBuild);

    [Benchmark]
    public GlobSpecification Long_ManyWildcards() =>
        GlobSpecification.Compile(
            "src/*/lib/*/internals/*/build/*/dist/*/bin/*/obj/*/release/*.cs",
            GlobDialect.MSBuild);

    [Benchmark]
    public GlobSpecification Long_ManySegments_GlobStar() =>
        GlobSpecification.Compile(
            "**/src/**/lib/**/internals/**/build/**/dist/**/release/**/*.cs",
            GlobDialect.MSBuild);

    // ---- Character-class density (POSIX bracket expansion) ----

    [Benchmark]
    public GlobSpecification Classes_Mixed_Inline() =>
        GlobSpecification.Compile("[Tt]est_[0-9][0-9]_[A-Za-z]*.[ch]s", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Classes_NestedPosixNamed() =>
        GlobSpecification.Compile(
            "[[:upper:]][[:alpha:]_]*[[:digit:]][[:xdigit:]]?[[:alnum:]_]*.[ch]s",
            GlobDialect.Posix);

    // ---- Path-unaware specialization fast paths (for ratio context) ----

    [Benchmark]
    public GlobSpecification Specialized_Suffix() =>
        GlobSpecification.Compile("*.cs", GlobDialect.Posix);

    [Benchmark]
    public GlobSpecification Specialized_PrefixSuffix() =>
        GlobSpecification.Compile("Test_*.cs", GlobDialect.Posix);
}
