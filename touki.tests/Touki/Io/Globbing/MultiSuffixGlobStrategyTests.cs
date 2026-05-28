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

    [Theory]
    // Two-suffix alternation matches either end. Mirrors the canonical
    // `**/@(*.cs|*.md)` benchmark shape.
    [InlineData("**/@(*.cs|*.md)", "src/", "foo.cs", true)]
    [InlineData("**/@(*.cs|*.md)", "src/", "foo.md", true)]
    [InlineData("**/@(*.cs|*.md)", "src/", "foo.txt", false)]
    [InlineData("**/@(*.cs|*.md)", "", "foo.cs", true)]
    [InlineData("**/@(*.cs|*.md)", "a/b/c/", "foo.md", true)]
    public void MatchCore_TwoSuffixes(string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Theory]
    // Eight-suffix alternation (PatternCount=8 benchmark shape). Reproduces a
    // realistic mix of common source extensions.
    [InlineData("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "build.props", true)]
    [InlineData("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "build.targets", true)]
    [InlineData("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "build.user", false)]
    [InlineData("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "Program.cs", true)]
    public void MatchCore_EightSuffixes(string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Theory]
    // Leading-dot rule cross-check against the matching N-include MatchSet
    // (`**/*.cs` ∪ `**/*.md`). The specialization must agree with the
    // baseline shape on every input. Encoded as oracle pairs so that any
    // future change to the leading-dot semantics flips both sides together.
    [InlineData("", "foo.cs")]
    [InlineData("", "foo.md")]
    [InlineData("", "foo.txt")]
    [InlineData("", ".cs")]
    [InlineData("", ".md")]
    [InlineData("", ".hidden.cs")]
    [InlineData("src/", ".cs")]
    [InlineData("src/", ".gitignore")]
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

    [Theory]
    // Suffix-set with a single alternative behaves like SuffixGlobStrategy for
    // the segment; verifies the N=1 path of the specialization.
    [InlineData("**/@(*.cs)", "src/Io/", "Glob.cs", true)]
    [InlineData("**/@(*.cs)", "src/Io/", "Glob.md", false)]
    public void MatchCore_SingleSuffix(string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Theory]
    // Shapes the specialization intentionally rejects fall through to the
    // bytecode interpreter, which must still answer correctly. These tests
    // pin the safety net.
    //   `?(...)`  - wrong extglob kind
    //   `+(...)`  - wrong extglob kind
    //   `*(...)`  - wrong extglob kind
    //   `!(...)`  - wrong extglob kind
    //   mixed-shape body (literal alt next to `*lit` alt)
    [InlineData("**/?(*.cs)", "src/", "foo.cs", true)]
    [InlineData("**/?(*.cs)", "src/", "foo.md", false)]
    [InlineData("**/+(*.cs)", "src/", "foo.cs", true)]
    [InlineData("**/+(*.cs)", "src/", "foo.md", false)]
    [InlineData("**/*(*.cs)", "src/", "foo.cs", true)]
    [InlineData("**/!(skip)", "src/", "keep", true)]
    [InlineData("**/!(skip)", "src/", "skip", false)]
    [InlineData("**/@(*.cs|README)", "src/", "README", true)]
    [InlineData("**/@(*.cs|README)", "src/", "foo.cs", true)]
    public void MatchCore_RejectedShapesStillMatchCorrectly(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [Theory]
    // Path-unaware dialects do not see the GlobStarFileNameStrategy
    // specialization; suffix-set extglobs there flow through the bytecode
    // interpreter. Confirm the semantics match.
    [InlineData("@(*.cs|*.md)", "foo.cs", true)]
    [InlineData("@(*.cs|*.md)", "foo.md", true)]
    [InlineData("@(*.cs|*.md)", "foo.txt", false)]
    public void IsMatch_PathUnaware_StillCorrect(string pattern, string input, bool expected) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Posix, GlobOptions.AllowExtGlob)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    // IgnoreCase fold paths through MultiSuffixGlobStrategy. The strategy
    // dispatches to the ASCII or Unicode endswith fold based on the dialect
    // semantics; both must agree with the generic bytecode answer.
    [InlineData("**/@(*.CS|*.MD)", "src/", "foo.cs", true)]
    [InlineData("**/@(*.CS|*.MD)", "src/", "foo.MD", true)]
    [InlineData("**/@(*.CS|*.MD)", "src/", "foo.txt", false)]
    public void MatchCore_IgnoreCase(string pattern, string prefix, string fileName, bool expected) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob | GlobOptions.IgnoreCase)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan()).Should().Be(expected);

    [Theory]
    // Unicode ignore-case path: the MSBuild dialect routes
    // `MultiSuffixGlobStrategy` through the `IgnoreCaseKind.Unicode`
    // arm of the endswith / equality switch. Uses a Latin-1 accented
    // character so the ordinal fold actually differs from the ASCII fold.
    [InlineData("**/@(*.\u00C9|*.MD)", "src/", "foo.\u00E9", true)]
    [InlineData("**/@(*.\u00C9|*.MD)", "src/", "foo.MD", true)]
    [InlineData("**/@(*.\u00C9|*.MD)", "src/", "foo.txt", false)]
    // Leading-dot input on the Unicode-fold path: forces the EqualsOrdinalIgnoreCase
    // equality branch inside the leading-dot block. Both arms begin with a
    // literal `.` so the precheck allows the dot input through; the equality
    // check then matches under the case-insensitive fold.
    [InlineData("**/@(.\u00C9|.MD)", "src/", ".\u00E9", true)]
    [InlineData("**/@(.\u00C9|.MD)", "src/", ".md", true)]
    [InlineData("**/@(.\u00C9|.MD)", "src/", ".other", false)]
    public void MatchCore_IgnoreCase_Unicode(string pattern, string prefix, string fileName, bool expected) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.MSBuild, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan()).Should().Be(expected);

    [Theory]
    // Shapes that fall outside `TryCreateMultiSuffixSegmentMatcher`'s selection
    // criteria must still compile and match correctly through the recursive
    // walker. Pins the rejection arms so the factory paths stay covered.
    //
    //   - `*` (bare AnyRun-only alt) - zero suffix length, rejected.
    //   - `*[a]` - bracket class inside the suffix.
    //   - `*?` - `?` inside the suffix (any-char wildcard).
    //   - `foo` - alt missing the leading `*`.
    //   - `*foo|@(bar)` - alt that opens a nested extglob in the suffix.
    [InlineData("**/@(*)", "src/", "anything", true)]
    [InlineData("**/@(*[ab])", "src/", "fooa", true)]
    [InlineData("**/@(*[ab])", "src/", "fooc", false)]
    [InlineData("**/@(*?)", "src/", "ab", true)]
    [InlineData("**/@(foo)", "src/", "foo", true)]
    [InlineData("**/@(foo)", "src/", "bar", false)]
    [InlineData("**/@(*foo|@(bar))", "src/", "xfoo", true)]
    [InlineData("**/@(*foo|@(bar))", "src/", "bar", true)]
    [InlineData("**/@(*foo|@(bar))", "src/", "baz", false)]
    public void MatchCore_NonSpecializedShapesStillMatch(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);
}
