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
[TestClass]
public class MultiSuffixGlobStrategyTests
{
    private static bool MatchCore(string pattern, string prefix, string fileName) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan());

    [TestMethod]
    // Two-suffix alternation matches either end. Mirrors the canonical
    // `**/@(*.cs|*.md)` benchmark shape.
    [DataRow("**/@(*.cs|*.md)", "src/", "foo.cs", true)]
    [DataRow("**/@(*.cs|*.md)", "src/", "foo.md", true)]
    [DataRow("**/@(*.cs|*.md)", "src/", "foo.txt", false)]
    [DataRow("**/@(*.cs|*.md)", "", "foo.cs", true)]
    [DataRow("**/@(*.cs|*.md)", "a/b/c/", "foo.md", true)]
    public void MatchCore_TwoSuffixes(string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Eight-suffix alternation (PatternCount=8 benchmark shape). Reproduces a
    // realistic mix of common source extensions.
    [DataRow("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "build.props", true)]
    [DataRow("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "build.targets", true)]
    [DataRow("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "build.user", false)]
    [DataRow("**/@(*.cs|*.md|*.json|*.txt|*.xml|*.yml|*.props|*.targets)", "p/", "Program.cs", true)]
    public void MatchCore_EightSuffixes(string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Leading-dot rule cross-check against the matching N-include MatchSet
    // (`**/*.cs` ∪ `**/*.md`). The specialization must agree with the
    // baseline shape on every input. Encoded as oracle pairs so that any
    // future change to the leading-dot semantics flips both sides together.
    [DataRow("", "foo.cs")]
    [DataRow("", "foo.md")]
    [DataRow("", "foo.txt")]
    [DataRow("", ".cs")]
    [DataRow("", ".md")]
    [DataRow("", ".hidden.cs")]
    [DataRow("src/", ".cs")]
    [DataRow("src/", ".gitignore")]
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

    [TestMethod]
    // Suffix-set with a single alternative behaves like SuffixGlobStrategy for
    // the segment; verifies the N=1 path of the specialization.
    [DataRow("**/@(*.cs)", "src/Io/", "Glob.cs", true)]
    [DataRow("**/@(*.cs)", "src/Io/", "Glob.md", false)]
    public void MatchCore_SingleSuffix(string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Shapes the specialization intentionally rejects fall through to the
    // bytecode interpreter, which must still answer correctly. These tests
    // pin the safety net.
    //   `?(...)`  - wrong extglob kind
    //   `+(...)`  - wrong extglob kind
    //   `*(...)`  - wrong extglob kind
    //   `!(...)`  - wrong extglob kind
    //   mixed-shape body (literal alt next to `*lit` alt)
    [DataRow("**/?(*.cs)", "src/", "foo.cs", true)]
    [DataRow("**/?(*.cs)", "src/", "foo.md", false)]
    [DataRow("**/+(*.cs)", "src/", "foo.cs", true)]
    [DataRow("**/+(*.cs)", "src/", "foo.md", false)]
    [DataRow("**/*(*.cs)", "src/", "foo.cs", true)]
    [DataRow("**/!(skip)", "src/", "keep", true)]
    [DataRow("**/!(skip)", "src/", "skip", false)]
    [DataRow("**/@(*.cs|README)", "src/", "README", true)]
    [DataRow("**/@(*.cs|README)", "src/", "foo.cs", true)]
    public void MatchCore_RejectedShapesStillMatchCorrectly(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);

    [TestMethod]
    // Path-unaware dialects do not see the GlobStarFileNameStrategy
    // specialization; suffix-set extglobs there flow through the bytecode
    // interpreter. Confirm the semantics match.
    [DataRow("@(*.cs|*.md)", "foo.cs", true)]
    [DataRow("@(*.cs|*.md)", "foo.md", true)]
    [DataRow("@(*.cs|*.md)", "foo.txt", false)]
    public void IsMatch_PathUnaware_StillCorrect(string pattern, string input, bool expected) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Posix, GlobOptions.AllowExtGlob)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // IgnoreCase fold paths through MultiSuffixGlobStrategy. The strategy
    // dispatches to the ASCII or Unicode endswith fold based on the dialect
    // semantics; both must agree with the generic bytecode answer.
    [DataRow("**/@(*.CS|*.MD)", "src/", "foo.cs", true)]
    [DataRow("**/@(*.CS|*.MD)", "src/", "foo.MD", true)]
    [DataRow("**/@(*.CS|*.MD)", "src/", "foo.txt", false)]
    public void MatchCore_IgnoreCase(string pattern, string prefix, string fileName, bool expected) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob | GlobOptions.IgnoreCase)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan()).Should().Be(expected);

    [TestMethod]
    // Unicode ignore-case path: the MSBuild dialect routes
    // `MultiSuffixGlobStrategy` through the `IgnoreCaseKind.Unicode`
    // arm of the endswith / equality switch. Uses a Latin-1 accented
    // character so the ordinal fold actually differs from the ASCII fold.
    [DataRow("**/@(*.\u00C9|*.MD)", "src/", "foo.\u00E9", true)]
    [DataRow("**/@(*.\u00C9|*.MD)", "src/", "foo.MD", true)]
    [DataRow("**/@(*.\u00C9|*.MD)", "src/", "foo.txt", false)]
    // Leading-dot input on the Unicode-fold path: forces the EqualsOrdinalIgnoreCase
    // equality branch inside the leading-dot block. Both arms begin with a
    // literal `.` so the precheck allows the dot input through; the equality
    // check then matches under the case-insensitive fold.
    [DataRow("**/@(.\u00C9|.MD)", "src/", ".\u00E9", true)]
    [DataRow("**/@(.\u00C9|.MD)", "src/", ".md", true)]
    [DataRow("**/@(.\u00C9|.MD)", "src/", ".other", false)]
    public void MatchCore_IgnoreCase_Unicode(string pattern, string prefix, string fileName, bool expected) =>
        GlobSpecification
            .Compile(pattern, GlobDialect.MSBuild, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .MatchCore(prefix.AsSpan(), fileName.AsSpan()).Should().Be(expected);

    [TestMethod]
    // Shapes that fall outside `TryCreateMultiSuffixSegmentMatcher`'s selection
    // criteria must still compile and match correctly through the recursive
    // walker. Pins the rejection arms so the factory paths stay covered.
    //
    //   - `*` (bare AnyRun-only alt) - zero suffix length, rejected.
    //   - `*[a]` - bracket class inside the suffix.
    //   - `*?` - `?` inside the suffix (any-char wildcard).
    //   - `foo` - alt missing the leading `*`.
    //   - `*foo|@(bar)` - alt that opens a nested extglob in the suffix.
    [DataRow("**/@(*)", "src/", "anything", true)]
    [DataRow("**/@(*[ab])", "src/", "fooa", true)]
    [DataRow("**/@(*[ab])", "src/", "fooc", false)]
    [DataRow("**/@(*?)", "src/", "ab", true)]
    [DataRow("**/@(foo)", "src/", "foo", true)]
    [DataRow("**/@(foo)", "src/", "bar", false)]
    [DataRow("**/@(*foo|@(bar))", "src/", "xfoo", true)]
    [DataRow("**/@(*foo|@(bar))", "src/", "bar", true)]
    [DataRow("**/@(*foo|@(bar))", "src/", "baz", false)]
    public void MatchCore_NonSpecializedShapesStillMatch(
        string pattern, string prefix, string fileName, bool expected) =>
        MatchCore(pattern, prefix, fileName).Should().Be(expected);
}
