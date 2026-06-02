// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    [Test]
    [Arguments("", "", true)]
    [Arguments("", "a", false)]
    [Arguments("abc", "abc", true)]
    [Arguments("abc", "abd", false)]
    [Arguments("abc", "ab", false)]
    [Arguments("abc", "abcd", false)]
    public void IsMatch_Literal_PosixCaseSensitive(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Test]
    [Arguments("*", "", true)]
    [Arguments("*", "abc", true)]
    [Arguments("*", ".hidden", false)]
    [Arguments("a*", "abc", true)]
    [Arguments("a*", "bbc", false)]
    [Arguments("*c", "abc", true)]
    [Arguments("*c", "abd", false)]
    [Arguments("a*c", "abc", true)]
    [Arguments("a*c", "ac", true)]
    [Arguments("a*c", "aXYZc", true)]
    [Arguments("a*c", "abz", false)]
    [Arguments("a*b*c", "axbyc", true)]
    [Arguments("a*b*c", "abc", true)]
    [Arguments("a*b*c", "abxc", true)]
    [Arguments("a*b*c", "axc", false)]
    [Arguments("**", "anything", true)]
    public void IsMatch_Star_Posix(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Test]
    [Arguments("?", "a", true)]
    [Arguments("?", "", false)]
    [Arguments("?", "ab", false)]
    [Arguments("???", "abc", true)]
    [Arguments("a?c", "abc", true)]
    [Arguments("a?c", "ac", false)]
    [Arguments("a?c", "axc", true)]
    [Arguments("?", ".", false)]
    public void IsMatch_Question_Posix(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Test]
    [Arguments("[abc]", "a", true)]
    [Arguments("[abc]", "b", true)]
    [Arguments("[abc]", "c", true)]
    [Arguments("[abc]", "d", false)]
    [Arguments("[a-z]", "m", true)]
    [Arguments("[a-z]", "M", false)]
    [Arguments("[!abc]", "d", true)]
    [Arguments("[!abc]", "a", false)]
    [Arguments("[^abc]", "d", true)]
    [Arguments("[]]", "]", true)]
    [Arguments("[a-c]x", "bx", true)]
    [Arguments("[a-c]x", "dx", false)]
    public void IsMatch_CharacterClass_Posix(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Test]
    [Arguments("ABC", "abc", true)]
    [Arguments("abc", "ABC", true)]
    [Arguments("A*C", "axxxc", true)]
    [Arguments("[A-Z]", "m", true)]
    [Arguments("[a-z]", "M", true)]
    public void IsMatch_IgnoreCase_Ascii(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
    }

    [Test]
    // Posix dialect uses ASCII-only case folding. Non-ASCII letter pairs must NOT
    // compare equal under IgnoreCase even though they are Unicode case-fold equivalents.
    [Arguments("caf\u00e9", "CAF\u00c9", false)]   // café vs CAFÉ
    [Arguments("\u00fcber", "\u00dcBER", false)]   // über vs ÜBER
    // ASCII letters in the same pattern still fold correctly.
    [Arguments("caf\u00e9", "caf\u00e9", true)]    // identical
    [Arguments("CAF\u00e9", "caf\u00e9", true)]    // ASCII prefix folds, non-ASCII matches ordinal
    public void IsMatch_IgnoreCase_Posix_AsciiFoldOnly(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [Test]
    // Simple dialect uses full Unicode ordinal IC (matches FileSystemName.MatchesSimpleExpression).
    // Non-ASCII letter pairs DO compare equal under IgnoreCase.
    [Arguments("caf\u00e9", "CAF\u00c9", true)]    // café vs CAFÉ
    [Arguments("\u00fcber", "\u00dcBER", true)]    // über vs ÜBER
    [Arguments("caf\u00e9", "caf\u00ea", false)]   // é vs ê genuinely differ
    public void IsMatch_IgnoreCase_Simple_UnicodeFold(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [Test]
    [Arguments(".hidden", ".hidden", true)]
    [Arguments("*hidden", ".hidden", false)]
    [Arguments("?hidden", ".hidden", false)]
    [Arguments("[.]hidden", ".hidden", false)]
    [Arguments(".*", ".hidden", true)]
    public void IsMatch_LeadingDot_Default_PosixStrict(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Test]
    [Arguments("*hidden", ".hidden", true)]
    [Arguments("?hidden", ".hidden", true)]
    public void IsMatch_LeadingDot_AllowedWhenOptionSet(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.MatchLeadingDot)
            .IsMatch(input).Should().Be(expected);
    }

    [Test]
    [Arguments(@"\*", "*", true)]
    [Arguments(@"\*", "a", false)]
    [Arguments(@"\?", "?", true)]
    [Arguments(@"\[abc\]", "[abc]", true)]
    public void IsMatch_Escape_Honored(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Test]
    [Arguments(@"\*", "\\*", true)]
    [Arguments(@"\*", "*", false)]
    public void IsMatch_Escape_DisabledWithNoEscape(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.NoEscape)
            .IsMatch(input).Should().Be(expected);
    }

    [Test]
    public void Compile_DanglingEscape_Throws()
    {
        Action act = () => GlobSpecification.Compile(@"abc\", GlobDialect.Posix);
        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [Test]
    public void Compile_UnterminatedClass_TreatedAsLiteral()
    {
        // Per fnmatch / glibc semantics: an unterminated '[' is treated as a
        // literal character rather than rejected. See PortedTests.Posix.cs for
        // the row that pinned this down (B.6 031: "/[", "\\/[", 0).
        GlobSpecification matcher = GlobSpecification.Compile("[abc", GlobDialect.Posix);
        matcher.IsMatch("[abc").Should().BeTrue();
        matcher.IsMatch("abc").Should().BeFalse();
    }

    [Test]
    public void TryCompile_DanglingEscape_ReturnsFalse()
    {
        bool ok = GlobSpecification.TryCompile(@"abc\", GlobDialect.Posix, GlobOptions.None,
            out GlobSpecification? result, out GlobCompileError error);
        ok.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [Test]
    [Arguments("*.cs", "Program.cs", true)]
    [Arguments("*.cs", "Program.vb", false)]
    [Arguments("Test*.cs", "TestThing.cs", true)]
    public void IsMatch_Simple_BasicCases(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Simple).IsMatch(input).Should().Be(expected);
    }

    [Test]
    public void IsMatch_Simple_DoesNotParseCharClass()
    {
        // Simple dialect treats '[' as a literal.
        GlobSpecification.Compile("[abc]", GlobDialect.Simple).IsMatch("[abc]").Should().BeTrue();
        GlobSpecification.Compile("[abc]", GlobDialect.Simple).IsMatch("a").Should().BeFalse();
    }

    [Test]
    // PowerShell -like / WildcardPattern: *, ?, bracket classes work as in POSIX, but the
    // FNM_PERIOD rule does NOT apply (a leading '.' may be matched by a wildcard).
    [Arguments("*", ".hidden", true)]
    [Arguments("?hidden", ".hidden", true)]
    [Arguments("[.]hidden", ".hidden", true)]
    [Arguments("*.cs", "Program.cs", true)]
    [Arguments("*.cs", "Program.vb", false)]
    [Arguments("[a-c]x", "bx", true)]
    [Arguments("[a-c]x", "dx", false)]
    public void IsMatch_PowerShell_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PowerShell).IsMatch(input).Should().Be(expected);

    [Test]
    // PowerShell escape character is backtick, not backslash.
    [Arguments("a`*b", "a*b", true)]         // backtick escapes '*' -> literal '*'
    [Arguments("a`*b", "axb", false)]
    [Arguments("a`?b", "a?b", true)]
    [Arguments("a`?b", "axb", false)]
    [Arguments("a`[b", "a[b", true)]         // backtick escapes '[' -> literal
    [Arguments("a\\b", "a\\b", true)]        // backslash is literal under PowerShell
    [Arguments("a\\b", "axb", false)]
    public void IsMatch_PowerShell_BacktickEscape(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PowerShell).IsMatch(input).Should().Be(expected);

    [Test]
    public void Compile_PowerShell_DanglingBacktick_Throws()
    {
        bool ok = GlobSpecification.TryCompile("ab`", GlobDialect.PowerShell, GlobOptions.None,
            out GlobSpecification? result, out GlobCompileError error);
        ok.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [Test]
    [Arguments("ABC", "abc", true)]
    [Arguments("[A-Z]", "m", true)]
    public void IsMatch_IgnoreCase_PowerShell_Unicode(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PowerShell, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [Test]
    public void Compile_UnsupportedDialect_Throws()
    {
        // No GlobDialect value is unimplemented today; cast an out-of-range value to
        // exercise the defensive FeatureNotEnabled branch in TryCompile.
        Action act = () => GlobSpecification.Compile("*", (GlobDialect)999);
        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.FeatureNotEnabled);
    }

    [Test]
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

    [Test]
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

    [Test]
    // '*' does not cross '/' in path-aware dialects.
    [Arguments("*", "abc", true)]
    [Arguments("*", "a/b", false)]
    [Arguments("*.cs", "Program.cs", true)]
    [Arguments("*.cs", "src/Program.cs", false)]
    [Arguments("src/*.cs", "src/Program.cs", true)]
    [Arguments("src/*.cs", "src/sub/Program.cs", false)]
    // '?' does not match '/'.
    [Arguments("a?b", "a/b", false)]
    [Arguments("a?b", "aXb", true)]
    // Multi-segment literal patterns still match exactly.
    [Arguments("a/b/c", "a/b/c", true)]
    [Arguments("a/b/c", "a/b/d", false)]
    // Contains-shape under path-aware: '*foo*' must route through CompiledGlobStrategy
    // and reject inputs whose required wildcard span would have to cross '/'.
    [Arguments("*foo*", "afoob", true)]
    [Arguments("*foo*", "x/foo/y", false)]
    [Arguments("a*b", "axb", true)]
    [Arguments("a*b", "a/b", false)]
    // Character classes also reject the separator.
    [Arguments("a[/x]b", "a/b", false)]
    [Arguments("a[/x]b", "axb", true)]
    public void IsMatch_PosixPath_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath).IsMatch(input).Should().Be(expected);

    [Test]
    // GS_None: bare '**' matches anything, including the empty string and paths
    // containing separators.
    [Arguments("**", "", true)]
    [Arguments("**", "a", true)]
    [Arguments("**", "a/b/c", true)]
    // GS_R (leading '**/'): zero-or-more dirs followed by the trailing segment.
    [Arguments("**/foo", "foo", true)]
    [Arguments("**/foo", "bar/foo", true)]
    [Arguments("**/foo", "a/b/foo", true)]
    [Arguments("**/foo", "barfoo", false)]
    [Arguments("**/foo", "xfoox", false)]
    [Arguments("**/foo", "foo/bar", false)]
    // GS_L (trailing '/**'): the leading segment followed by zero-or-more dirs.
    [Arguments("foo/**", "foo", true)]
    [Arguments("foo/**", "foo/x", true)]
    [Arguments("foo/**", "foo/x/y", true)]
    [Arguments("foo/**", "foox", false)]
    [Arguments("foo/**", "fooz/y", false)]
    // GS_LT (middle '/**/'): two literal anchors with zero-or-more dirs between.
    [Arguments("a/**/b", "a/b", true)]
    [Arguments("a/**/b", "a/x/b", true)]
    [Arguments("a/**/b", "a/x/y/b", true)]
    [Arguments("a/**/b", "ab", false)]
    [Arguments("a/**/b", "a/xb", false)]
    [Arguments("a/**/b", "a/b/c", false)]
    // Mixed with '*' inside segments.
    [Arguments("**/*.cs", "Foo.cs", true)]
    [Arguments("**/*.cs", "src/Foo.cs", true)]
    [Arguments("**/*.cs", "src/sub/Foo.cs", true)]
    [Arguments("**/*.cs", "Foo.txt", false)]
    // Two globstars; the second pads zero or more trailing dirs.
    [Arguments("**/foo/**", "foo", true)]
    [Arguments("**/foo/**", "foo/bar", true)]
    [Arguments("**/foo/**", "a/foo/b/c", true)]
    [Arguments("**/foo/**", "bar", false)]
    public void IsMatch_PosixPath_GlobStar(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar)
            .IsMatch(input).Should().Be(expected);

    [Test]
    // '**' not in its own segment must behave like '*' (no separator crossing).
    [Arguments("**foo", "foo", true)]
    [Arguments("**foo", "xfoo", true)]
    [Arguments("**foo", "a/foo", false)]
    [Arguments("foo**", "foo", true)]
    [Arguments("foo**", "foox", true)]
    [Arguments("foo**", "foo/x", false)]
    [Arguments("a**b", "axb", true)]
    [Arguments("a**b", "a/b", false)]
    public void IsMatch_PosixPath_GlobStarNotEligible_TreatedAsStar(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar)
            .IsMatch(input).Should().Be(expected);

    [Test]
    // Path-aware dialects skip the simple Prefix/Suffix/Contains/PrefixSuffix matchers
    // because none of those types track the separator yet. All wildcard patterns route
    // through CompiledGlobStrategy; pure literals still hit LiteralGlobStrategy.
    [Arguments("", typeof(LiteralGlobStrategy))]
    [Arguments("abc", typeof(LiteralGlobStrategy))]
    [Arguments("a/b/c", typeof(LiteralGlobStrategy))]
    [Arguments("*", typeof(CompiledGlobStrategy))]
    [Arguments("*.cs", typeof(CompiledGlobStrategy))]
    [Arguments("a*", typeof(CompiledGlobStrategy))]
    [Arguments("*foo*", typeof(CompiledGlobStrategy))]
    [Arguments("a*b", typeof(CompiledGlobStrategy))]
    [Arguments("a?b", typeof(CompiledGlobStrategy))]
    public void Compile_PosixPath_ChoosesStrategy(string pattern, Type expectedType)
    {
        GlobSpecification spec = GlobSpecification.Compile(pattern, GlobDialect.PosixPath);
        object strategy = spec.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType(expectedType);
    }

    [Test]
    // The `**/<segment>` shape (path-aware globstar + single trailing segment without
    // further separators) routes to the GlobStarFileNameStrategy specialization for
    // any segment that the path-unaware specialization or the literal short-circuit
    // can satisfy. Segments containing classes or question marks still fall through
    // to CompiledGlobStrategy (when the dialect honors classes).
    [Arguments(GlobDialect.FileSystemGlobbing, "**/*.cs", typeof(GlobStarFileNameStrategy))]
    [Arguments(GlobDialect.FileSystemGlobbing, "**/file.cs", typeof(GlobStarFileNameStrategy))]
    [Arguments(GlobDialect.FileSystemGlobbing, "**/abc*", typeof(GlobStarFileNameStrategy))]
    [Arguments(GlobDialect.FileSystemGlobbing, "**/*foo*", typeof(GlobStarFileNameStrategy))]
    [Arguments(GlobDialect.FileSystemGlobbing, "**/a*b", typeof(GlobStarFileNameStrategy))]
    [Arguments(GlobDialect.FileSystemGlobbing, "**/*", typeof(GlobStarFileNameStrategy))]
    [Arguments(GlobDialect.MSBuild, "**/*.cs", typeof(GlobStarFileNameStrategy))]
    [Arguments(GlobDialect.Git, "**/*.cs", typeof(GlobStarFileNameStrategy))]
    [Arguments(GlobDialect.PosixPath, "**/a?b", typeof(CompiledGlobStrategy))]
    [Arguments(GlobDialect.PosixPath, "**/[abc]", typeof(CompiledGlobStrategy))]
    [Arguments(GlobDialect.FileSystemGlobbing, "**/sub/*.cs", typeof(CompiledGlobStrategy))]
    public void Compile_GlobStarFileNameSpecialization(GlobDialect dialect, string pattern, Type expectedType)
    {
        GlobSpecification spec = GlobSpecification.Compile(pattern, dialect, GlobOptions.AllowGlobStar);
        object strategy = spec.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType(expectedType);
    }

    [Test]
    // Functional parity against the bytecode interpreter for the new specialization.
    [Arguments("**/*.cs", "a.cs", true)]
    [Arguments("**/*.cs", "src/a.cs", true)]
    [Arguments("**/*.cs", "src/sub/a.cs", true)]
    [Arguments("**/*.cs", "a.txt", false)]
    [Arguments("**/*.cs", "src/a.txt", false)]
    [Arguments("**/file.cs", "file.cs", true)]
    [Arguments("**/file.cs", "src/file.cs", true)]
    [Arguments("**/file.cs", "src/other.cs", false)]
    [Arguments("**/foo*", "foobar", true)]
    [Arguments("**/foo*", "dir/foobar", true)]
    [Arguments("**/foo*", "dir/bar", false)]
    [Arguments("**/*foo*", "barfoobaz", true)]
    [Arguments("**/*foo*", "dir/barfoobaz", true)]
    public void IsMatch_GlobStarFileNameSpecialization_FunctionalParity(
        string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing).IsMatch(input).Should().Be(expected);

    [Test]
    public void Compile_PosixPath_SeparatorIsForwardSlash() =>
        GlobSpecification.Compile("*", GlobDialect.PosixPath).Separator.Should().Be('/');

    [Test]
    public void Compile_Posix_SeparatorIsZero() =>
        GlobSpecification.Compile("*", GlobDialect.Posix).Separator.Should().Be('\0');

    [Test]
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

    [Test]
    [Arguments("", typeof(LiteralGlobStrategy))]
    [Arguments("abc", typeof(LiteralGlobStrategy))]
    [Arguments("*", typeof(AnyGlobStrategy))]
    [Arguments("**", typeof(AnyGlobStrategy))]
    [Arguments("abc*", typeof(PrefixGlobStrategy))]
    [Arguments("*.cs", typeof(SuffixGlobStrategy))]
    [Arguments("*foo*", typeof(ContainsGlobStrategy))]
    [Arguments("a*b", typeof(PrefixSuffixGlobStrategy))]
    [Arguments("a?b", typeof(CompiledGlobStrategy))]
    [Arguments("[abc]", typeof(CompiledGlobStrategy))]
    [Arguments("a*b*c", typeof(CompiledGlobStrategy))]
    public void Compile_ChoosesSimplestStrategy(string pattern, Type expectedType)
    {
        GlobSpecification matcher = GlobSpecification.Compile(pattern, GlobDialect.Posix);
        object strategy = matcher.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType(expectedType);
    }
}
