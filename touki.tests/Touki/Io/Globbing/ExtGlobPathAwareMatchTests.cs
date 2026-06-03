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
[TestClass]
public class ExtGlobPathAwareMatchTests
{
    private static bool MatchCore(string pattern, string prefix, string fileName) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan());

    private static bool IsMatch(string pattern, string input) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .IsMatch(input.AsSpan());

    [TestMethod]
    // Direct regression for the observed failure: `**/@(*.cs)` with a real
    // directory prefix. Pre-fix, the absorbed=6 retry of `**` saw the
    // alternation block already advanced past and answered false.
    [DataRow("**/@(*.cs)", "touki/", "GlobalUsings.cs", true)]
    [DataRow("**/@(*.cs)", "touki/Io/Globbing/", "GlobMatch.cs", true)]
    [DataRow("**/@(*.cs)", "", "foo.cs", true)]
    [DataRow("**/@(*.cs)", "a/b/c/", "x.cs", true)]
    [DataRow("**/@(*.cs)", "touki/", "GlobalUsings.md", false)]
    public void MatchCore_GlobStar_AtConstruct_WithDirectoryPrefix(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Same backtracking shape for every positive extglob kind, ensuring
    // `**/`'s GlobStar retry restores ranges across all DispatchAlternation
    // variants (TryAlternativeOnce, TryAlternativeRepeating).
    [DataRow("**/?(foo.cs)", "touki/", "foo.cs", true)]
    [DataRow("**/?(foo.cs)", "touki/", "", true)]
    [DataRow("**/?(foo.cs)", "touki/", "bar.cs", false)]
    [DataRow("**/*(a|b)", "touki/", "abab", true)]
    [DataRow("**/*(a|b)", "touki/", "", true)]
    [DataRow("**/*(a|b)", "touki/", "abc", false)]
    [DataRow("**/+(a|b)", "touki/", "abab", true)]
    [DataRow("**/+(a|b)", "touki/", "", false)]
    [DataRow("**/+(a|b)", "touki/", "abc", false)]
    [DataRow("**/!(skip)", "touki/", "keep", true)]
    [DataRow("**/!(skip)", "touki/", "skip", false)]
    public void MatchCore_GlobStar_BeforeEachExtGlobKind(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // AnyRun (single `*` outside a path segment) had the same bug shape:
    // mutate-once-then-loop. Triggered by patterns where `*` must backtrack
    // (consume different lengths) across an alternation.
    [DataRow("*@(foo|bar)", "", "xfoo", true)]
    [DataRow("*@(foo|bar)", "", "xxxbar", true)]
    [DataRow("*@(foo|bar)", "", "xbaz", false)]
    [DataRow("prefix*@(a|b)suffix", "", "prefixxxasuffix", true)]
    [DataRow("prefix*@(a|b)suffix", "", "prefixbsuffix", true)]
    [DataRow("prefix*@(a|b)suffix", "", "prefixxxcsuffix", false)]
    public void MatchCore_AnyRun_BeforeExtGlob_BacktracksCorrectly(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Negation followed by a backtracking `**/` or extglob continuation:
    // TryNegation loops L = 0..maxL and re-tries the program's rest on each
    // step, so the rest's ranges must be restored between L values.
    [DataRow("!(skip)/**/*.cs", "", "keep/foo.cs", true)]
    [DataRow("!(skip)/**/*.cs", "", "keep/sub/foo.cs", true)]
    [DataRow("!(skip)/**/*.cs", "", "skip/foo.cs", false)]
    public void MatchCore_Negation_BeforeBacktrackingRest(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Two extglob constructs separated by a `*` that must backtrack: the
    // AnyRun fix has to leave the trailing alternation's ranges intact across
    // each consumed length.
    [DataRow("@(a|b)*@(x|y)", "", "axxx", true)]
    [DataRow("@(a|b)*@(x|y)", "", "bxxxy", true)]
    [DataRow("@(a|b)*@(x|y)", "", "az", false)]
    public void MatchCore_TwoAlternations_AnyRunBetween(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Deeper path prefixes exercise the path-aware backtracking length
    // computation in NextValidGlobStarLength (scanning forward to each
    // separator) combined with extglob's range-list handoff.
    [DataRow("**/@(foo|bar).cs", "a/", "foo.cs", true)]
    [DataRow("**/@(foo|bar).cs", "a/b/", "bar.cs", true)]
    [DataRow("**/@(foo|bar).cs", "a/b/c/", "foo.cs", true)]
    [DataRow("**/@(foo|bar).cs", "a/b/c/", "baz.cs", false)]
    public void MatchCore_GlobStar_MultiSegmentPrefix(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Leading-dot rule with extglob alternations. The non-extglob fast paths
    // gate input on `program[0] == Literal '.'`; the extglob walker has to
    // descend into each alternative so explicit-dot alternatives can match
    // hidden inputs. Regression for a bug where the precheck rejected any
    // `.` input the moment `program[0] == AltStart`, even when an
    // alternative began with a literal dot.
    [DataRow("@(.gitignore|README)", "", ".gitignore", true)]
    [DataRow("@(.gitignore|README)", "", "README", true)]
    [DataRow("@(.gitignore|README)", "", ".cs", false)]
    [DataRow("@(.gitignore|README)", "", "other", false)]
    // Nested AltStart: the leading-dot probe must recurse so the inner
    // explicit-dot alternative is found.
    [DataRow("@(@(.env|.gitignore)|README)", "", ".env", true)]
    [DataRow("@(@(.env|.gitignore)|README)", "", ".gitignore", true)]
    [DataRow("@(@(.env|.gitignore)|README)", "", ".other", false)]
    // Pure-Literal alternatives: literal mismatch fails naturally without
    // consuming the leading `.`.
    [DataRow("@(.cs|README)", "", "README", true)]
    [DataRow("@(.cs|README)", "", ".cs", true)]
    [DataRow("@(.cs|README)", "", ".other", false)]
    // Known limitation: when an alternative begins with `*`/`?`/class/globstar
    // and the input starts with `.`, the recursive walker does not (yet)
    // enforce the leading-dot rule per-alternative. The precheck lets these
    // patterns through because at least one alternative begins with a literal
    // dot; the wildcard-led alternative then matches against the dot input.
    // Tracked separately; tighten when the walker grows MatchLeadingDot
    // awareness.
    [DataRow("@(*.cs|README)", "", "foo.cs", true)]
    [DataRow("@(*.cs|README)", "", "README", true)]
    public void MatchCore_LeadingDotRule_WithExtGlob(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Boundary invariant: the two-span walker addresses the virtual input
    // through `CharAt(first, second, firstLength, i)`, so a separator sitting
    // exactly at the first/second split (the directory-prefix trailing '/')
    // must be seen identically to a separator inside a single contiguous span.
    // Every case here splits the same logical path at the '/' that the
    // enumeration call shape would, and asserts the two-span `MatchCore`
    // answer equals the single-span `IsMatch` answer on the joined input.
    //
    // Extglob constructs whose handlers scan to a separator (AnyRun, GlobStar,
    // negation `maxL`) are the ones most exposed to an off-by-one at the
    // boundary, so each is represented.
    //
    // GlobStar spanning the boundary segment.
    [DataRow("**/@(foo|bar).cs", "a/", "foo.cs")]
    [DataRow("**/@(foo|bar).cs", "a/b/", "bar.cs")]
    [DataRow("**/!(skip)", "a/b/", "keep")]
    [DataRow("**/!(skip)", "a/b/", "skip")]
    // Negation directly at the boundary: the construct's separator-bounded
    // `maxL` is computed by walking `CharAt` from the split, so the trailing
    // prefix separator must terminate the scan at the right index.
    [DataRow("!(skip)/keep.cs", "", "keep/keep.cs")]
    [DataRow("!(skip)/keep.cs", "keep/", "keep.cs")]
    [DataRow("!(skip)/**/*.cs", "keep/", "sub/foo.cs")]
    [DataRow("!(skip)/**/*.cs", "skip/", "sub/foo.cs")]
    // AnyRun bounded by the boundary separator.
    [DataRow("*/@(foo|bar).cs", "dir/", "foo.cs")]
    [DataRow("*/@(foo|bar).cs", "", "dir/bar.cs")]
    public void MatchCore_SeparatorAtFirstSecondBoundary_AgreesWithSingleSpan(
        string pattern, string prefix, string fileName)
    {
        bool twoSpan = MatchCore(pattern, prefix, fileName);
        bool single = IsMatch(pattern, prefix + fileName);
        twoSpan.Should().Be(single);
    }

    [TestMethod]
    // A literal alternative that straddles the first/second boundary: the
    // chosen literal ("keep.cs") is split so its head lives in `first` and its
    // tail in `second`. `LiteralMatchesAt` must compare across the split.
    // Every split point of "keep.cs" must agree with the single-span answer.
    [DataRow("@(keep.cs|other)", "k", "eep.cs", true)]
    [DataRow("@(keep.cs|other)", "ke", "ep.cs", true)]
    [DataRow("@(keep.cs|other)", "keep", ".cs", true)]
    [DataRow("@(keep.cs|other)", "keep.", "cs", true)]
    [DataRow("@(keep.cs|other)", "kee", "p.cx", false)]
    [DataRow("@(keep.cs|other)", "oth", "er", true)]
    public void MatchCore_LiteralStraddlesBoundary_MatchesAcrossSplit(
        string pattern, string prefix, string fileName, bool expected)
    {
        MatchCore(pattern, prefix, fileName).Should().Be(expected);
        // The split must not change the answer relative to one contiguous span.
        IsMatch(pattern, prefix + fileName).Should().Be(expected);
    }

    [TestMethod]
    // Exercises the per-alternative offset table baked into the AltStart
    // header. Earlier theories only reach altCount <= 2, so the third and
    // later offset slots (off_2, off_3, ...) are never read; an off-by-one in
    // the table write or read would slip through. These cases require the
    // matcher to select alternatives at index >= 2 to succeed.
    [DataRow("@(a|b|c)", "", "c", true)]
    [DataRow("@(a|b|c)", "", "b", true)]
    [DataRow("@(a|b|c)", "", "d", false)]
    [DataRow("@(red|green|blue|yellow)", "", "yellow", true)]
    [DataRow("@(red|green|blue|yellow)", "", "blue", true)]
    [DataRow("@(red|green|blue|yellow)", "", "purple", false)]
    [DataRow("+(a|bb|ccc)", "", "cccbbacccbb", true)]
    [DataRow("**/@(foo|bar|baz).cs", "a/b/", "baz.cs", true)]
    [DataRow("**/@(foo|bar|baz).cs", "a/b/", "qux.cs", false)]
    public void MatchCore_OffsetTable_MultipleAlternatives(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // A nested extglob sitting in a NON-first alternative, followed by further
    // alternatives. This is the exact shape that would expose an offset-table
    // bug: the nested construct's own header splice happens between the moment
    // the outer table records its alternative positions and the moment the
    // outer table is spliced in, so a later outer offset (for the alternative
    // after the nested one) must still resolve correctly.
    [DataRow("@(README|@(.env|.gitignore)|LICENSE)", "", "LICENSE", true)]
    [DataRow("@(README|@(.env|.gitignore)|LICENSE)", "", ".env", true)]
    [DataRow("@(README|@(.env|.gitignore)|LICENSE)", "", ".gitignore", true)]
    [DataRow("@(README|@(.env|.gitignore)|LICENSE)", "", "other", false)]
    [DataRow("+(a|@(b|c)|d)", "", "dcba", true)]
    [DataRow("+(a|@(b|c)|d)", "", "abcd", true)]
    [DataRow("+(a|@(b|c)|d)", "", "abxd", false)]
    [DataRow("**/@(keep|@(foo|bar)|skip).cs", "a/b/", "skip.cs", true)]
    [DataRow("**/@(keep|@(foo|bar)|skip).cs", "a/b/", "bar.cs", true)]
    [DataRow("**/@(keep|@(foo|bar)|skip).cs", "a/b/", "nope.cs", false)]
    public void MatchCore_OffsetTable_NestedInNonFirstAlternative(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Empty alternatives inside the repeating constructs (+ and *) drive the
    // "empty alternative collapses to matching the rest" branch in
    // ProduceAlternative: when an alternative body is empty the progress guard
    // refuses to re-enter the block with no input consumed, so the production
    // degenerates to matching the construct's continuation. Placing the empty
    // alternative first guarantees the collapse branch is taken on the first
    // production; a trailing empty alternative reaches it on a later slot.
    [DataRow("*(|a)", "", "", true)]
    [DataRow("*(|a)", "", "a", true)]
    [DataRow("*(|a)", "", "aaa", true)]
    [DataRow("*(|a)", "", "b", false)]
    [DataRow("+(|a)", "", "a", true)]
    [DataRow("+(|a)", "", "aaa", true)]
    [DataRow("+(|a)", "", "b", false)]
    [DataRow("*(a|)", "", "aa", true)]
    [DataRow("+(a|)", "", "aa", true)]
    public void MatchCore_EmptyAlternative_RepeatingConstruct(
        string pattern, string prefix, string fileName, bool expected)
    {
        MatchCore(pattern, prefix, fileName).Should().Be(expected);
        // The two-span split must never change the answer relative to one span.
        IsMatch(pattern, prefix + fileName).Should().Be(expected);
    }
}
