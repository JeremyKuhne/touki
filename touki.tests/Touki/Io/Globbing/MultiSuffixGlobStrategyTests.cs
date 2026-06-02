// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Correctness coverage for the <see cref="MultiSuffixGlobStrategy"/>
///  specialization the factory selects for <c>**/&#x40;(*lit1|*lit2|...)</c>
///  and other compatible suffix-set shapes. The matching semantics must agree
///  with the equivalent generic bytecode (or, for the common
///  <c>**/&#x40;(*.ext1|*.ext2)</c> shape, with an N-include
///  <see cref="MatchSet"/> of <c>**/*.extN</c>) so the optimization stays
///  invisible to callers.
/// </summary>
/// <remarks>
///  <para>
///   The strategy is the dominant win on .NET Framework 4.8.1 RyuJIT; if these
///   tests fail it is a sign that the factory routed a pattern that does not
///   match the strategy's semantics. The expected behavior is documented on
///   <see cref="MultiSuffixGlobStrategy"/>.
///  </para>
/// </remarks>
public class MultiSuffixGlobStrategyTests
{
    private static bool MatchCore(string pattern, string prefix, string fileName) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan());

    [Test]
    // Two-suffix alternation matches either end. Mirrors the canonical
    // `**/@(*.cs|*.md)` benchmark shape.
    [Arguments("**/@(*.cs|*.md)", "src/", "foo.cs", true)]
    [Arguments("**/@(*.cs|*.md)", "src/", "foo.md", true)]
    [Arguments("**/@(*.cs|*.md)", "src/", "foo.txt", false)]
    [Arguments("**/@(*.cs|*.md)", "", "foo.cs", true)]
    [Arguments("**/@(*.cs|*.md)", "a/b/c/", "foo.md", true)]
    public void MatchCore_TwoSuffixes(string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Test]
    // Eight-suffix alternation (PatternCount=8 benchmark shape). Reproduces a
    // realistic mix of common source extensions.
    [Arguments("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "build.props", true)]
    [Arguments("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "build.targets", true)]
    [Arguments("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "build.user", false)]
    [Arguments("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "Program.cs", true)]
    public void MatchCore_EightSuffixes(string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Test]
    // Leading-dot rule cross-check against the matching N-include MatchSet
    // (`**/*.cs` ∪ `**/*.md`). The specialization must agree with the
    // baseline shape on every input. Encoded as oracle pairs so that any
    // future change to the leading-dot semantics flips both sides together.
    [Arguments("", "foo.cs")]
    [Arguments("", "foo.md")]
    [Arguments("", "foo.txt")]
    [Arguments("", ".cs")]
    [Arguments("", ".md")]
    [Arguments("", ".hidden.cs")]
    [Arguments("src/", ".cs")]
    [Arguments("src/", ".gitignore")]
    public void MatchCore_LeadingDotRule_MatchesMatchSetBaseline(string prefix, string fileName)
    {
        bool extGlob = GlobSpecification
            .Compile("**/@(*.cs|*.md)", GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan());

        bool unionOfSuffixes =
            GlobSpecification.Compile("**/*.cs", GlobDialect.Bash, GlobOptions.AllowGlobStar)
                .MatchCore(prefix.AsSpan(), fileName.AsSpan())
            || GlobSpecification.Compile("**/*.md", GlobDialect.Bash, GlobOptions.AllowGlobStar)
                .MatchCore(prefix.AsSpan(), fileName.AsSpan());

        extGlob.Should().Be(unionOfSuffixes,
            $"prefix='{prefix}' fileName='{fileName}'");
    }

    [Test]
    // Suffix-set with a single alternative behaves like SuffixGlobStrategy for
    // the segment; verifies the N=1 path of the specialization.
    [Arguments("**/@(*.cs)", "src/Io/", "Glob.cs", true)]
    [Arguments("**/@(*.cs)", "src/Io/", "Glob.md", false)]
    public void MatchCore_SingleSuffix(string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Test]
    // Shapes the specialization intentionally rejects fall through to the
    // bytecode interpreter, which must still answer correctly. These tests
    // pin the safety net.
    //   `?(...)`  - wrong extglob kind
    //   `+(...)`  - wrong extglob kind
    //   `*(...)`  - wrong extglob kind
    //   `!(...)`  - wrong extglob kind
    //   mixed-shape body (literal alt next to `*lit` alt)
    [Arguments("**/?(*.cs)", "src/", "foo.cs", true)]
    [Arguments("**/?(*.cs)", "src/", "foo.md", false)]
    [Arguments("**/+(*.cs)", "src/", "foo.cs", true)]
    [Arguments("**/+(*.cs)", "src/", "foo.md", false)]
    [Arguments("**/*(*.cs)", "src/", "foo.cs", true)]
    [Arguments("**/!(skip)", "src/", "keep", true)]
    [Arguments("**/!(skip)", "src/", "skip", false)]
    [Arguments("**/@(*.cs|README)", "src/", "README", true)]
    [Arguments("**/@(*.cs|README)", "src/", "foo.cs", true)]
    public void MatchCore_RejectedShapesStillMatchCorrectly(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Test]
    // Path-unaware dialects do not see the GlobStarFileNameStrategy
    // specialization; suffix-set extglobs there flow through the bytecode
    // interpreter. Confirm the semantics match.
    [Arguments("@(*.cs|*.md)", "foo.cs", true)]
    [Arguments("@(*.cs|*.md)", "foo.md", true)]
    [Arguments("@(*.cs|*.md)", "foo.txt", false)]
    public void IsMatch_PathUnaware_StillCorrect(string pattern, string input, bool expected) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Posix, GlobOptions.AllowExtGlob)
            .IsMatch(input).Should().Be(expected);

    [Test]
    // IgnoreCase fold paths through MultiSuffixGlobStrategy. The strategy
    // dispatches to the ASCII or Unicode endswith fold based on the dialect
    // semantics; both must agree with the generic bytecode answer.
    [Arguments("**/@(*.CS|*.MD)", "src/", "foo.cs", true)]
    [Arguments("**/@(*.CS|*.MD)", "src/", "foo.MD", true)]
    [Arguments("**/@(*.CS|*.MD)", "src/", "foo.txt", false)]
    public void MatchCore_IgnoreCase(string pattern, string prefix, string fileName, bool expected) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob | GlobOptions.IgnoreCase)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan()).Should().Be(expected);

    [Test]
    // Unicode ignore-case path: the MSBuild dialect routes
    // `MultiSuffixGlobStrategy` through the `IgnoreCaseKind.Unicode`
    // arm of the endswith / equality switch. Uses a Latin-1 accented
    // character so the ordinal fold actually differs from the ASCII fold.
    [Arguments("**/@(*.\u00C9|*.MD)", "src/", "foo.\u00E9", true)]
    [Arguments("**/@(*.\u00C9|*.MD)", "src/", "foo.MD", true)]
    [Arguments("**/@(*.\u00C9|*.MD)", "src/", "foo.txt", false)]
    // Leading-dot input on the Unicode-fold path: forces the EqualsOrdinalIgnoreCase
    // equality branch inside the leading-dot block. Both arms begin with a
    // literal `.` so the precheck allows the dot input through; the equality
    // check then matches under the case-insensitive fold.
    [Arguments("**/@(.\u00C9|.MD)", "src/", ".\u00E9", true)]
    [Arguments("**/@(.\u00C9|.MD)", "src/", ".md", true)]
    [Arguments("**/@(.\u00C9|.MD)", "src/", ".other", false)]
    public void MatchCore_IgnoreCase_Unicode(string pattern, string prefix, string fileName, bool expected) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.MSBuild, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan()).Should().Be(expected);

    [Test]
    // Shapes that fall outside `TryCreateMultiSuffixSegmentMatcher`'s selection
    // criteria must still compile and match correctly through the recursive
    // walker. Pins the rejection arms so the factory paths stay covered.
    //
    //   - `*` (bare AnyRun-only alt) - zero suffix length, rejected.
    //   - `*[a]` - bracket class inside the suffix.
    //   - `*?` - `?` inside the suffix (any-char wildcard).
    //   - `foo` - alt missing the leading `*`.
    //   - `*foo|@(bar)` - alt that opens a nested extglob in the suffix.
    [Arguments("**/@(*)", "src/", "anything", true)]
    [Arguments("**/@(*[ab])", "src/", "fooa", true)]
    [Arguments("**/@(*[ab])", "src/", "fooc", false)]
    [Arguments("**/@(*?)", "src/", "ab", true)]
    [Arguments("**/@(foo)", "src/", "foo", true)]
    [Arguments("**/@(foo)", "src/", "bar", false)]
    [Arguments("**/@(*foo|@(bar))", "src/", "xfoo", true)]
    [Arguments("**/@(*foo|@(bar))", "src/", "bar", true)]
    [Arguments("**/@(*foo|@(bar))", "src/", "baz", false)]
    public void MatchCore_NonSpecializedShapesStillMatch(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);
}
