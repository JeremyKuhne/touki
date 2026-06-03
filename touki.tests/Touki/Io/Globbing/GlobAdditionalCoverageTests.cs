// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Targeted coverage tests for branches not exercised by the dialect/oracle suites:
///  the one-shot <see cref="Glob.IsMatch"/> helper, the specialized matcher
///  case-insensitive paths, leading-dot fail paths on Suffix/Contains matchers, the
///  <c>NeverMatchGlobStrategy</c> two-span entry point reached via
///  <see cref="GlobMatch.MatchesFile"/>, and the path-aware
///  <see cref="LiteralGlobStrategy"/> two-span fast path.
/// </summary>
[TestClass]
public class GlobAdditionalCoverageTests
{
    private static string Root => Path.Combine(Path.GetTempPath(), "glob-coverage-root");

    [TestMethod]
    [DataRow("abc", "abc", true)]
    [DataRow("a*c", "axc", true)]
    [DataRow("a*c", "abz", false)]
    public void Glob_IsMatch_OneShotHelper(string pattern, string input, bool expected) =>
        Glob.IsMatch(pattern, input.AsSpan(), GlobDialect.Posix).Should().Be(expected);

    [TestMethod]
    // Path-unaware Posix with IgnoreCase picks the Ascii fold path for Prefix.
    [DataRow("ABC*", "abcdef", true)]
    [DataRow("ABC*", "xabc", false)]
    public void PrefixGlobStrategy_IgnoreCase_Ascii(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Simple dialect with IgnoreCase picks the Unicode fold path for Prefix.
    [DataRow("\u00c9*", "\u00e9foo", true)]   // É* matches éfoo
    public void PrefixGlobStrategy_IgnoreCase_Unicode(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Ascii ignore-case + leading-dot input forces the suffix-equality branch.
    [DataRow("*.HIDDEN", ".hidden", true)]
    [DataRow("*.HIDDEN", "x.hidden", true)]
    [DataRow("*.HIDDEN", ".other", false)]
    public void SuffixGlobStrategy_IgnoreCase_Ascii(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Unicode ignore-case leading-dot equality + normal endswith.
    [DataRow("*.\u00C9", ".\u00e9", true)]
    [DataRow("*.\u00C9", "file.\u00e9", true)]
    [DataRow("*.\u00C9", ".\u00ea", false)]
    public void SuffixGlobStrategy_IgnoreCase_Unicode(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Suffix matcher, leading dot, no fold: requires exact equality of input to suffix.
    [DataRow("*.cs", ".cs", true)]
    [DataRow("*.cs", "a.cs", true)]
    [DataRow("*.cs", ".foo.cs", false)]
    public void SuffixGlobStrategy_LeadingDot_NoFold(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Contains matcher: leading dot path requires needle to start at index 0.
    [DataRow("*foo*", ".foobar", false)]
    [DataRow("*.foo*", ".foobar", true)]
    [DataRow("*foo*", ".x", false)]
    public void ContainsGlobStrategy_LeadingDot_NoFold(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Contains matcher IgnoreCase paths (both leading-dot and non).
    [DataRow("*FOO*", "abcfoo", true)]
    [DataRow("*FOO*", "abcbar", false)]
    [DataRow("*.FOO*", ".foobar", true)]
    public void ContainsGlobStrategy_IgnoreCase(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Contains matcher leading-dot path with input shorter than needle returns false.
    [DataRow("*.foobar*", ".foo", false)]
    public void ContainsGlobStrategy_LeadingDot_InputShorterThanNeedle(
        string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Contains matcher Posix+IgnoreCase: only ASCII letters fold; non-ASCII chars
    // compare ordinally. `*é*` must NOT match `É` (U+00C9 vs U+00E9 differ outside
    // the ASCII range). This pins down the IgnoreCaseKind.Ascii dispatch.
    [DataRow("*\u00E9*", "X\u00E9X", true)]   // exact match of '\u00E9'
    [DataRow("*\u00E9*", "X\u00C9X", false)]  // '\u00C9' (uppercase) must NOT fold to '\u00E9'
    [DataRow("*FOO*", "abcfoo", true)]        // ASCII letters DO fold
    public void ContainsGlobStrategy_PosixIgnoreCase_AsciiOnlyFold(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Contrast with Simple+IgnoreCase (Unicode fold): '\u00E9' DOES fold to '\u00C9'.
    [DataRow("*\u00E9*", "X\u00C9X", true)]
    public void ContainsGlobStrategy_SimpleIgnoreCase_UnicodeFold(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Contains matcher Posix+IgnoreCase leading-dot branch with ASCII-only fold.
    [DataRow("*.FOO*", ".foobar", true)]
    [DataRow("*.\u00E9*", ".\u00C9oo", false)]  // non-ASCII does not fold
    public void ContainsGlobStrategy_PosixIgnoreCase_LeadingDot_AsciiOnlyFold(
        string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // PrefixSuffix matcher Unicode ignore-case branch.
    [DataRow("\u00C9*\u00C9", "\u00e9X\u00e9", true)]
    [DataRow("\u00C9*\u00C9", "\u00eaX\u00e9", false)]
    public void PrefixSuffixGlobStrategy_IgnoreCase_Unicode(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    public void NeverMatchGlobStrategy_MatchesFile_AlwaysReturnsFalse()
    {
        // MSBuild treats `***` (three or more *) as a never-match sentinel. The matcher
        // is path-aware so MatchesFile dispatches to the two-span MatchCore override.
        GlobMatch matcher = GlobSpecification.Compile("***", GlobDialect.MSBuild).CreateMatcher(Root);
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesFile(Root, "anything".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Path.Combine(Root, "sub"), "file.cs".AsSpan()).Should().BeFalse();
        matcher.Dispose();
    }

    [TestMethod]
    public void LiteralGlobStrategy_MatchesFile_TwoSpan_CaseSensitive()
    {
        // PosixPath + literal `a/b/file.cs` → LiteralGlobStrategy; path-aware so
        // MatchesFile uses the two-span override.
        GlobMatch matcher = GlobSpecification.Compile("a/b/file.cs", GlobDialect.PosixPath).CreateMatcher(Root);
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesFile(Path.Combine(Root, "a", "b"), "file.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Path.Combine(Root, "a", "b"), "OTHER.cs".AsSpan()).Should().BeFalse();
        matcher.Dispose();
    }

    [TestMethod]
    public void LiteralGlobStrategy_MatchesFile_TwoSpan_Ascii()
    {
        // PosixPath + IgnoreCase → IgnoreCaseKind.Ascii branch in two-span MatchCore.
        GlobMatch matcher = GlobSpecification.Compile(
            "a/b/FILE.cs",
            GlobDialect.PosixPath,
            GlobOptions.IgnoreCase).CreateMatcher(Root);
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesFile(Path.Combine(Root, "a", "b"), "file.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Path.Combine(Root, "a", "b"), "nope.cs".AsSpan()).Should().BeFalse();
        matcher.Dispose();
    }

    [TestMethod]
    public void LiteralGlobStrategy_MatchesFile_TwoSpan_Unicode()
    {
        // MSBuild dialect's default IgnoreCaseKind is Unicode; a pure literal pattern
        // routes to LiteralGlobStrategy with the Unicode branch in two-span MatchCore.
        GlobMatch matcher = GlobSpecification.Compile("a/b/file.cs", GlobDialect.MSBuild).CreateMatcher(Root);
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesFile(Path.Combine(Root, "a", "b"), "FILE.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Path.Combine(Root, "a", "b"), "different.cs".AsSpan()).Should().BeFalse();
        matcher.Dispose();
    }

    [TestMethod]
    public void LiteralGlobStrategy_MatchesFile_TwoSpan_LengthMismatch()
    {
        // total != _literal.Length short-circuit branch (line 44 of LiteralGlobStrategy).
        GlobMatch matcher = GlobSpecification.Compile("a/b/x", GlobDialect.PosixPath).CreateMatcher(Root);
        IEnumerationMatcher boundary = matcher;

        // File name combined with the cached dir prefix doesn't have matching length.
        boundary.MatchesFile(Path.Combine(Root, "a", "b"), "xy".AsSpan()).Should().BeFalse();
        matcher.Dispose();
    }

    [TestMethod]
    // MSBuild input-side coalesce path: input has a `//` run, exercising
    // ContainsSeparatorRun and CoalesceSeparatorRuns. The pattern collapses
    // to a single segment, but the input's run must be coalesced before match.
    [DataRow("a/b", "a//b", true)]
    [DataRow("a/b", "a///b", true)]
    [DataRow("a/b", "a/b", true)]
    public void GlobSpecification_CoalesceInputSeparators_MSBuild(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.MSBuild).IsMatch(input).Should().Be(expected);

    [TestMethod]
    public void GlobSpecification_DisallowEmptyInput_Simple()
    {
        // Simple dialect's empty-input contract: never matches even when the pattern
        // would otherwise (e.g. `*` accepts everything).
        GlobSpecification.Compile("*", GlobDialect.Simple).IsMatch("".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void TryCompile_InvalidPattern_ReportsError()
    {
        // Dangling escape produces a compile error; the TryCompile entry point
        // exposes the error path that the throwing Compile wraps. (An unterminated
        // '[' is no longer an error - fnmatch semantics treat it as a literal
        // character; see PortedTests.Posix.cs.)
        bool ok = GlobSpecification.TryCompile(
            "abc\\",
            GlobDialect.Posix,
            GlobOptions.None,
            out GlobSpecification? matcher,
            out GlobCompileError error);

        ok.Should().BeFalse();
        matcher.Should().BeNull();
        error.IsError.Should().BeTrue();
        error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
        error.Position.Should().BeGreaterThanOrEqualTo(0);
        error.Message.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public void TryCompile_PatternTooLarge_ReportsError()
    {
        bool ok = GlobSpecification.TryCompile(
            new string('a', 10),
            GlobDialect.Posix,
            GlobOptions.None,
            GlobPathSeparator.DialectDefault,
            maxPatternLength: 4,
            out GlobSpecification? matcher,
            out GlobCompileError error);

        ok.Should().BeFalse();
        matcher.Should().BeNull();
        error.IsError.Should().BeTrue();
        error.Code.Should().Be(GlobCompileErrorCode.PatternTooLarge);
    }

    [TestMethod]
    public void Compile_InvalidPattern_Throws()
    {
        Action act = () => GlobSpecification.Compile("abc\\", GlobDialect.Posix);
        act.Should().Throw<GlobFormatException>();
    }
}
