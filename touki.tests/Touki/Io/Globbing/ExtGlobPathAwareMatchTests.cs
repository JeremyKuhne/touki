// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Two-span <see cref="GlobSpecification.MatchCore"/> regression coverage for
///  extended-glob patterns. Where <see cref="ExtGlobPositiveMatchTests"/> and
///  <see cref="ExtGlobNegationMatchTests"/> exercise the matcher via
///  <see cref="GlobSpecification.IsMatch"/> (single-span input), these tests
///  drive <see cref="GlobSpecification.MatchCore"/> with a non-empty directory
///  prefix to mirror the call shape used by
///  <see cref="GlobMatch.MatchesFile"/> during directory enumeration.
/// </summary>
/// <remarks>
///  <para>
///   Originally added to pin the fix for a bug where the recursive
///   <c>MatchExtGlob</c> walker's <c>GlobStar</c> and <c>AnyRun</c> handlers
///   mutated <c>ranges[0]</c> once before the backtracking loop, then reused
///   the corrupted state on subsequent retries. The visible failure was
///   <c>**/@(*.cs)</c> matching against <c>("touki/", "GlobalUsings.cs")</c>
///   returning <see langword="false"/>: the first <c>absorbed=0</c> attempt
///   advanced <c>ranges[0].Start</c> past the alternation block while
///   dispatching, then the <c>absorbed=6</c> retry saw an empty range and
///   short-circuited on the <c>inputIndex == totalLength</c> end check.
///   <c>IsMatch</c>-style tests passed because they fold the entire input
///   into the second span, never producing the prefix-bearing call shape.
///  </para>
/// </remarks>
public class ExtGlobPathAwareMatchTests
{
    private static bool MatchCore(string pattern, string prefix, string fileName) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan());

    [Theory]
    // Direct regression for the observed failure: `**/@(*.cs)` with a real
    // directory prefix. Pre-fix, the absorbed=6 retry of `**` saw the
    // alternation block already advanced past and answered false.
    [InlineData("**/@(*.cs)", "touki/", "GlobalUsings.cs", true)]
    [InlineData("**/@(*.cs)", "touki/Io/Globbing/", "GlobMatch.cs", true)]
    [InlineData("**/@(*.cs)", "", "foo.cs", true)]
    [InlineData("**/@(*.cs)", "a/b/c/", "x.cs", true)]
    [InlineData("**/@(*.cs)", "touki/", "GlobalUsings.md", false)]
    public void MatchCore_GlobStar_AtConstruct_WithDirectoryPrefix(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Theory]
    // Same backtracking shape for every positive extglob kind, ensuring
    // `**/`'s GlobStar retry restores ranges across all DispatchAlternation
    // variants (TryAlternativeOnce, TryAlternativeRepeating).
    [InlineData("**/?(foo.cs)", "touki/", "foo.cs", true)]
    [InlineData("**/?(foo.cs)", "touki/", "", true)]
    [InlineData("**/?(foo.cs)", "touki/", "bar.cs", false)]
    [InlineData("**/*(a|b)", "touki/", "abab", true)]
    [InlineData("**/*(a|b)", "touki/", "", true)]
    [InlineData("**/*(a|b)", "touki/", "abc", false)]
    [InlineData("**/+(a|b)", "touki/", "abab", true)]
    [InlineData("**/+(a|b)", "touki/", "", false)]
    [InlineData("**/+(a|b)", "touki/", "abc", false)]
    [InlineData("**/!(skip)", "touki/", "keep", true)]
    [InlineData("**/!(skip)", "touki/", "skip", false)]
    public void MatchCore_GlobStar_BeforeEachExtGlobKind(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Theory]
    // AnyRun (single `*` outside a path segment) had the same bug shape:
    // mutate-once-then-loop. Triggered by patterns where `*` must backtrack
    // (consume different lengths) across an alternation.
    [InlineData("*@(foo|bar)", "", "xfoo", true)]
    [InlineData("*@(foo|bar)", "", "xxxbar", true)]
    [InlineData("*@(foo|bar)", "", "xbaz", false)]
    [InlineData("prefix*@(a|b)suffix", "", "prefixxxasuffix", true)]
    [InlineData("prefix*@(a|b)suffix", "", "prefixbsuffix", true)]
    [InlineData("prefix*@(a|b)suffix", "", "prefixxxcsuffix", false)]
    public void MatchCore_AnyRun_BeforeExtGlob_BacktracksCorrectly(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Theory]
    // Negation followed by a backtracking `**/` or extglob continuation:
    // TryNegation loops L = 0..maxL and re-tries the program's rest on each
    // step, so the rest's ranges must be restored between L values.
    [InlineData("!(skip)/**/*.cs", "", "keep/foo.cs", true)]
    [InlineData("!(skip)/**/*.cs", "", "keep/sub/foo.cs", true)]
    [InlineData("!(skip)/**/*.cs", "", "skip/foo.cs", false)]
    public void MatchCore_Negation_BeforeBacktrackingRest(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Theory]
    // Two extglob constructs separated by a `*` that must backtrack: the
    // AnyRun fix has to leave the trailing alternation's ranges intact across
    // each consumed length.
    [InlineData("@(a|b)*@(x|y)", "", "axxx", true)]
    [InlineData("@(a|b)*@(x|y)", "", "bxxxy", true)]
    [InlineData("@(a|b)*@(x|y)", "", "az", false)]
    public void MatchCore_TwoAlternations_AnyRunBetween(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Theory]
    // Deeper path prefixes exercise the path-aware backtracking length
    // computation in NextValidGlobStarLength (scanning forward to each
    // separator) combined with extglob's range-list handoff.
    [InlineData("**/@(foo|bar).cs", "a/", "foo.cs", true)]
    [InlineData("**/@(foo|bar).cs", "a/b/", "bar.cs", true)]
    [InlineData("**/@(foo|bar).cs", "a/b/c/", "foo.cs", true)]
    [InlineData("**/@(foo|bar).cs", "a/b/c/", "baz.cs", false)]
    public void MatchCore_GlobStar_MultiSegmentPrefix(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);
}
