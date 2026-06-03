// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    [TestMethod]
    [DataRow("", "", true)]
    [DataRow("", "a", false)]
    [DataRow("abc", "abc", true)]
    [DataRow("abc", "abd", false)]
    [DataRow("abc", "ab", false)]
    [DataRow("abc", "abcd", false)]
    public void IsMatch_Literal_PosixCaseSensitive(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("*", "", true)]
    [DataRow("*", "abc", true)]
    [DataRow("*", ".hidden", false)]
    [DataRow("a*", "abc", true)]
    [DataRow("a*", "bbc", false)]
    [DataRow("*c", "abc", true)]
    [DataRow("*c", "abd", false)]
    [DataRow("a*c", "abc", true)]
    [DataRow("a*c", "ac", true)]
    [DataRow("a*c", "aXYZc", true)]
    [DataRow("a*c", "abz", false)]
    [DataRow("a*b*c", "axbyc", true)]
    [DataRow("a*b*c", "abc", true)]
    [DataRow("a*b*c", "abxc", true)]
    [DataRow("a*b*c", "axc", false)]
    [DataRow("**", "anything", true)]
    public void IsMatch_Star_Posix(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("?", "a", true)]
    [DataRow("?", "", false)]
    [DataRow("?", "ab", false)]
    [DataRow("???", "abc", true)]
    [DataRow("a?c", "abc", true)]
    [DataRow("a?c", "ac", false)]
    [DataRow("a?c", "axc", true)]
    [DataRow("?", ".", false)]
    public void IsMatch_Question_Posix(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("[abc]", "a", true)]
    [DataRow("[abc]", "b", true)]
    [DataRow("[abc]", "c", true)]
    [DataRow("[abc]", "d", false)]
    [DataRow("[a-z]", "m", true)]
    [DataRow("[a-z]", "M", false)]
    [DataRow("[!abc]", "d", true)]
    [DataRow("[!abc]", "a", false)]
    [DataRow("[^abc]", "d", true)]
    [DataRow("[]]", "]", true)]
    [DataRow("[a-c]x", "bx", true)]
    [DataRow("[a-c]x", "dx", false)]
    public void IsMatch_CharacterClass_Posix(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("ABC", "abc", true)]
    [DataRow("abc", "ABC", true)]
    [DataRow("A*C", "axxxc", true)]
    [DataRow("[A-Z]", "m", true)]
    [DataRow("[a-z]", "M", true)]
    public void IsMatch_IgnoreCase_Ascii(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    // Posix dialect uses ASCII-only case folding. Non-ASCII letter pairs must NOT
    // compare equal under IgnoreCase even though they are Unicode case-fold equivalents.
    [DataRow("caf\u00e9", "CAF\u00c9", false)]   // café vs CAFÉ
    [DataRow("\u00fcber", "\u00dcBER", false)]   // über vs ÜBER
    // ASCII letters in the same pattern still fold correctly.
    [DataRow("caf\u00e9", "caf\u00e9", true)]    // identical
    [DataRow("CAF\u00e9", "caf\u00e9", true)]    // ASCII prefix folds, non-ASCII matches ordinal
    public void IsMatch_IgnoreCase_Posix_AsciiFoldOnly(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Simple dialect uses full Unicode ordinal IC (matches FileSystemName.MatchesSimpleExpression).
    // Non-ASCII letter pairs DO compare equal under IgnoreCase.
    [DataRow("caf\u00e9", "CAF\u00c9", true)]    // café vs CAFÉ
    [DataRow("\u00fcber", "\u00dcBER", true)]    // über vs ÜBER
    [DataRow("caf\u00e9", "caf\u00ea", false)]   // é vs ê genuinely differ
    public void IsMatch_IgnoreCase_Simple_UnicodeFold(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    [DataRow(".hidden", ".hidden", true)]
    [DataRow("*hidden", ".hidden", false)]
    [DataRow("?hidden", ".hidden", false)]
    [DataRow("[.]hidden", ".hidden", false)]
    [DataRow(".*", ".hidden", true)]
    public void IsMatch_LeadingDot_Default_PosixStrict(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("*hidden", ".hidden", true)]
    [DataRow("?hidden", ".hidden", true)]
    public void IsMatch_LeadingDot_AllowedWhenOptionSet(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.MatchLeadingDot)
            .IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(@"\*", "*", true)]
    [DataRow(@"\*", "a", false)]
    [DataRow(@"\?", "?", true)]
    [DataRow(@"\[abc\]", "[abc]", true)]
    public void IsMatch_Escape_Honored(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    [DataRow(@"\*", "\\*", true)]
    [DataRow(@"\*", "*", false)]
    public void IsMatch_Escape_DisabledWithNoEscape(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.NoEscape)
            .IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    public void Compile_DanglingEscape_Throws()
    {
        Action act = () => GlobSpecification.Compile(@"abc\", GlobDialect.Posix);
        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [TestMethod]
    public void Compile_UnterminatedClass_TreatedAsLiteral()
    {
        // Per fnmatch / glibc semantics: an unterminated '[' is treated as a
        // literal character rather than rejected. See PortedTests.Posix.cs for
        // the row that pinned this down (B.6 031: "/[", "\\/[", 0).
        GlobSpecification matcher = GlobSpecification.Compile("[abc", GlobDialect.Posix);
        matcher.IsMatch("[abc").Should().BeTrue();
        matcher.IsMatch("abc").Should().BeFalse();
    }

    [TestMethod]
    public void TryCompile_DanglingEscape_ReturnsFalse()
    {
        bool ok = GlobSpecification.TryCompile(@"abc\", GlobDialect.Posix, GlobOptions.None,
            out GlobSpecification? result, out GlobCompileError error);
        ok.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [TestMethod]
    [DataRow("*.cs", "Program.cs", true)]
    [DataRow("*.cs", "Program.vb", false)]
    [DataRow("Test*.cs", "TestThing.cs", true)]
    public void IsMatch_Simple_BasicCases(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Simple).IsMatch(input).Should().Be(expected);
    }

    [TestMethod]
    public void IsMatch_Simple_DoesNotParseCharClass()
    {
        // Simple dialect treats '[' as a literal.
        GlobSpecification.Compile("[abc]", GlobDialect.Simple).IsMatch("[abc]").Should().BeTrue();
        GlobSpecification.Compile("[abc]", GlobDialect.Simple).IsMatch("a").Should().BeFalse();
    }

    [TestMethod]
    // PowerShell -like / WildcardPattern: *, ?, bracket classes work as in POSIX, but the
    // FNM_PERIOD rule does NOT apply (a leading '.' may be matched by a wildcard).
    [DataRow("*", ".hidden", true)]
    [DataRow("?hidden", ".hidden", true)]
    [DataRow("[.]hidden", ".hidden", true)]
    [DataRow("*.cs", "Program.cs", true)]
    [DataRow("*.cs", "Program.vb", false)]
    [DataRow("[a-c]x", "bx", true)]
    [DataRow("[a-c]x", "dx", false)]
    public void IsMatch_PowerShell_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PowerShell).IsMatch(input).Should().Be(expected);

    [TestMethod]
    // PowerShell escape character is backtick, not backslash.
    [DataRow("a`*b", "a*b", true)]         // backtick escapes '*' -> literal '*'
    [DataRow("a`*b", "axb", false)]
    [DataRow("a`?b", "a?b", true)]
    [DataRow("a`?b", "axb", false)]
    [DataRow("a`[b", "a[b", true)]         // backtick escapes '[' -> literal
    [DataRow("a\\b", "a\\b", true)]        // backslash is literal under PowerShell
    [DataRow("a\\b", "axb", false)]
    public void IsMatch_PowerShell_BacktickEscape(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PowerShell).IsMatch(input).Should().Be(expected);

    [TestMethod]
    public void Compile_PowerShell_DanglingBacktick_Throws()
    {
        bool ok = GlobSpecification.TryCompile("ab`", GlobDialect.PowerShell, GlobOptions.None,
            out GlobSpecification? result, out GlobCompileError error);
        ok.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [TestMethod]
    [DataRow("ABC", "abc", true)]
    [DataRow("[A-Z]", "m", true)]
    public void IsMatch_IgnoreCase_PowerShell_Unicode(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PowerShell, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    public void Compile_UnsupportedDialect_Throws()
    {
        // No GlobDialect value is unimplemented today; cast an out-of-range value to
        // exercise the defensive FeatureNotEnabled branch in TryCompile.
        Action act = () => GlobSpecification.Compile("*", (GlobDialect)999);
        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.FeatureNotEnabled);
    }

    [TestMethod]
    public void Compile_AllowGlobStar_PosixPath_Succeeds()
    {
        // F2.2: AllowGlobStar is now supported for path-aware dialects.
        GlobSpecification matcher = GlobSpecification.Compile(
            "**/*.cs",
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar);

        matcher.IsMatch("Foo.cs").Should().BeTrue();
        matcher.IsMatch("src/Foo.cs").Should().BeTrue();
        matcher.IsMatch("a/b/Foo.cs").Should().BeTrue();
        matcher.IsMatch("Foo.txt").Should().BeFalse();
    }

    [TestMethod]
    public void Compile_AllowGlobStar_PathUnaware_NoOp()
    {
        // Path-unaware dialects have no segments; '**' collapses to '*' and AllowGlobStar
        // is silently accepted.
        GlobSpecification matcher = GlobSpecification.Compile(
            "**foo**",
            GlobDialect.Posix,
            GlobOptions.AllowGlobStar);

        matcher.IsMatch("foo").Should().BeTrue();
        matcher.IsMatch("xfooy").Should().BeTrue();
        matcher.IsMatch("x/foo/y").Should().BeTrue();
        matcher.IsMatch("bar").Should().BeFalse();
    }

    // -- PosixPath (path-aware) ----------------------------------------------------

    [TestMethod]
    // '*' does not cross '/' in path-aware dialects.
    [DataRow("*", "abc", true)]
    [DataRow("*", "a/b", false)]
    [DataRow("*.cs", "Program.cs", true)]
    [DataRow("*.cs", "src/Program.cs", false)]
    [DataRow("src/*.cs", "src/Program.cs", true)]
    [DataRow("src/*.cs", "src/sub/Program.cs", false)]
    // '?' does not match '/'.
    [DataRow("a?b", "a/b", false)]
    [DataRow("a?b", "aXb", true)]
    // Multi-segment literal patterns still match exactly.
    [DataRow("a/b/c", "a/b/c", true)]
    [DataRow("a/b/c", "a/b/d", false)]
    // Contains-shape under path-aware: '*foo*' must route through CompiledGlobStrategy
    // and reject inputs whose required wildcard span would have to cross '/'.
    [DataRow("*foo*", "afoob", true)]
    [DataRow("*foo*", "x/foo/y", false)]
    [DataRow("a*b", "axb", true)]
    [DataRow("a*b", "a/b", false)]
    // Character classes also reject the separator.
    [DataRow("a[/x]b", "a/b", false)]
    [DataRow("a[/x]b", "axb", true)]
    public void IsMatch_PosixPath_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath).IsMatch(input).Should().Be(expected);

    [TestMethod]
    // GS_None: bare '**' matches anything, including the empty string and paths
    // containing separators.
    [DataRow("**", "", true)]
    [DataRow("**", "a", true)]
    [DataRow("**", "a/b/c", true)]
    // GS_R (leading '**/'): zero-or-more dirs followed by the trailing segment.
    [DataRow("**/foo", "foo", true)]
    [DataRow("**/foo", "bar/foo", true)]
    [DataRow("**/foo", "a/b/foo", true)]
    [DataRow("**/foo", "barfoo", false)]
    [DataRow("**/foo", "xfoox", false)]
    [DataRow("**/foo", "foo/bar", false)]
    // GS_L (trailing '/**'): the leading segment followed by zero-or-more dirs.
    [DataRow("foo/**", "foo", true)]
    [DataRow("foo/**", "foo/x", true)]
    [DataRow("foo/**", "foo/x/y", true)]
    [DataRow("foo/**", "foox", false)]
    [DataRow("foo/**", "fooz/y", false)]
    // GS_LT (middle '/**/'): two literal anchors with zero-or-more dirs between.
    [DataRow("a/**/b", "a/b", true)]
    [DataRow("a/**/b", "a/x/b", true)]
    [DataRow("a/**/b", "a/x/y/b", true)]
    [DataRow("a/**/b", "ab", false)]
    [DataRow("a/**/b", "a/xb", false)]
    [DataRow("a/**/b", "a/b/c", false)]
    // Mixed with '*' inside segments.
    [DataRow("**/*.cs", "Foo.cs", true)]
    [DataRow("**/*.cs", "src/Foo.cs", true)]
    [DataRow("**/*.cs", "src/sub/Foo.cs", true)]
    [DataRow("**/*.cs", "Foo.txt", false)]
    // Two globstars; the second pads zero or more trailing dirs.
    [DataRow("**/foo/**", "foo", true)]
    [DataRow("**/foo/**", "foo/bar", true)]
    [DataRow("**/foo/**", "a/foo/b/c", true)]
    [DataRow("**/foo/**", "bar", false)]
    public void IsMatch_PosixPath_GlobStar(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // '**' not in its own segment must behave like '*' (no separator crossing).
    [DataRow("**foo", "foo", true)]
    [DataRow("**foo", "xfoo", true)]
    [DataRow("**foo", "a/foo", false)]
    [DataRow("foo**", "foo", true)]
    [DataRow("foo**", "foox", true)]
    [DataRow("foo**", "foo/x", false)]
    [DataRow("a**b", "axb", true)]
    [DataRow("a**b", "a/b", false)]
    public void IsMatch_PosixPath_GlobStarNotEligible_TreatedAsStar(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Path-aware dialects skip the simple Prefix/Suffix/Contains/PrefixSuffix matchers
    // because none of those types track the separator yet. All wildcard patterns route
    // through CompiledGlobStrategy; pure literals still hit LiteralGlobStrategy.
    [DataRow("", typeof(LiteralGlobStrategy))]
    [DataRow("abc", typeof(LiteralGlobStrategy))]
    [DataRow("a/b/c", typeof(LiteralGlobStrategy))]
    [DataRow("*", typeof(CompiledGlobStrategy))]
    [DataRow("*.cs", typeof(CompiledGlobStrategy))]
    [DataRow("a*", typeof(CompiledGlobStrategy))]
    [DataRow("*foo*", typeof(CompiledGlobStrategy))]
    [DataRow("a*b", typeof(CompiledGlobStrategy))]
    [DataRow("a?b", typeof(CompiledGlobStrategy))]
    public void Compile_PosixPath_ChoosesStrategy(string pattern, Type expectedType)
    {
        GlobSpecification spec = GlobSpecification.Compile(pattern, GlobDialect.PosixPath);
        object strategy = spec.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType(expectedType);
    }

    [TestMethod]
    // The `**/<segment>` shape (path-aware globstar + single trailing segment without
    // further separators) routes to the GlobStarFileNameStrategy specialization for
    // any segment that the path-unaware specialization or the literal short-circuit
    // can satisfy. Segments containing classes or question marks still fall through
    // to CompiledGlobStrategy (when the dialect honors classes).
    [DataRow(GlobDialect.FileSystemGlobbing, "**/*.cs", typeof(GlobStarFileNameStrategy))]
    [DataRow(GlobDialect.FileSystemGlobbing, "**/file.cs", typeof(GlobStarFileNameStrategy))]
    [DataRow(GlobDialect.FileSystemGlobbing, "**/abc*", typeof(GlobStarFileNameStrategy))]
    [DataRow(GlobDialect.FileSystemGlobbing, "**/*foo*", typeof(GlobStarFileNameStrategy))]
    [DataRow(GlobDialect.FileSystemGlobbing, "**/a*b", typeof(GlobStarFileNameStrategy))]
    [DataRow(GlobDialect.FileSystemGlobbing, "**/*", typeof(GlobStarFileNameStrategy))]
    [DataRow(GlobDialect.MSBuild, "**/*.cs", typeof(GlobStarFileNameStrategy))]
    [DataRow(GlobDialect.Git, "**/*.cs", typeof(GlobStarFileNameStrategy))]
    [DataRow(GlobDialect.PosixPath, "**/a?b", typeof(CompiledGlobStrategy))]
    [DataRow(GlobDialect.PosixPath, "**/[abc]", typeof(CompiledGlobStrategy))]
    [DataRow(GlobDialect.FileSystemGlobbing, "**/sub/*.cs", typeof(CompiledGlobStrategy))]
    public void Compile_GlobStarFileNameSpecialization(GlobDialect dialect, string pattern, Type expectedType)
    {
        GlobSpecification spec = GlobSpecification.Compile(pattern, dialect, GlobOptions.AllowGlobStar);
        object strategy = spec.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType(expectedType);
    }

    [TestMethod]
    // Functional parity against the bytecode interpreter for the new specialization.
    [DataRow("**/*.cs", "a.cs", true)]
    [DataRow("**/*.cs", "src/a.cs", true)]
    [DataRow("**/*.cs", "src/sub/a.cs", true)]
    [DataRow("**/*.cs", "a.txt", false)]
    [DataRow("**/*.cs", "src/a.txt", false)]
    [DataRow("**/file.cs", "file.cs", true)]
    [DataRow("**/file.cs", "src/file.cs", true)]
    [DataRow("**/file.cs", "src/other.cs", false)]
    [DataRow("**/foo*", "foobar", true)]
    [DataRow("**/foo*", "dir/foobar", true)]
    [DataRow("**/foo*", "dir/bar", false)]
    [DataRow("**/*foo*", "barfoobaz", true)]
    [DataRow("**/*foo*", "dir/barfoobaz", true)]
    public void IsMatch_GlobStarFileNameSpecialization_FunctionalParity(
        string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing).IsMatch(input).Should().Be(expected);

    [TestMethod]
    public void Compile_PosixPath_SeparatorIsForwardSlash() =>
        GlobSpecification.Compile("*", GlobDialect.PosixPath).Separator.Should().Be('/');

    [TestMethod]
    public void Compile_Posix_SeparatorIsZero() =>
        GlobSpecification.Compile("*", GlobDialect.Posix).Separator.Should().Be('\0');

    [TestMethod]
    public void IsMatch_DoesNotAllocate()
    {
        GlobSpecification matcher = GlobSpecification.Compile("a*b?c[de]", GlobDialect.Posix);
        string input = "axxxxbycd";

        // Warm up.
        matcher.IsMatch(input);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            matcher.IsMatch(input);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        (after - before).Should().Be(0);
    }

    [TestMethod]
    [DataRow("", typeof(LiteralGlobStrategy))]
    [DataRow("abc", typeof(LiteralGlobStrategy))]
    [DataRow("*", typeof(AnyGlobStrategy))]
    [DataRow("**", typeof(AnyGlobStrategy))]
    [DataRow("abc*", typeof(PrefixGlobStrategy))]
    [DataRow("*.cs", typeof(SuffixGlobStrategy))]
    [DataRow("*foo*", typeof(ContainsGlobStrategy))]
    [DataRow("a*b", typeof(PrefixSuffixGlobStrategy))]
    [DataRow("a?b", typeof(CompiledGlobStrategy))]
    [DataRow("[abc]", typeof(CompiledGlobStrategy))]
    [DataRow("a*b*c", typeof(CompiledGlobStrategy))]
    public void Compile_ChoosesSimplestStrategy(string pattern, Type expectedType)
    {
        GlobSpecification matcher = GlobSpecification.Compile(pattern, GlobDialect.Posix);
        object strategy = matcher.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType(expectedType);
    }
}
