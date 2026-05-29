// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    [Theory]
    [InlineData("", "", true)]
    [InlineData("", "a", false)]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abd", false)]
    [InlineData("abc", "ab", false)]
    [InlineData("abc", "abcd", false)]
    public void IsMatch_Literal_PosixCaseSensitive(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("*", "", true)]
    [InlineData("*", "abc", true)]
    [InlineData("*", ".hidden", false)]
    [InlineData("a*", "abc", true)]
    [InlineData("a*", "bbc", false)]
    [InlineData("*c", "abc", true)]
    [InlineData("*c", "abd", false)]
    [InlineData("a*c", "abc", true)]
    [InlineData("a*c", "ac", true)]
    [InlineData("a*c", "aXYZc", true)]
    [InlineData("a*c", "abz", false)]
    [InlineData("a*b*c", "axbyc", true)]
    [InlineData("a*b*c", "abc", true)]
    [InlineData("a*b*c", "abxc", true)]
    [InlineData("a*b*c", "axc", false)]
    [InlineData("**", "anything", true)]
    public void IsMatch_Star_Posix(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("?", "a", true)]
    [InlineData("?", "", false)]
    [InlineData("?", "ab", false)]
    [InlineData("???", "abc", true)]
    [InlineData("a?c", "abc", true)]
    [InlineData("a?c", "ac", false)]
    [InlineData("a?c", "axc", true)]
    [InlineData("?", ".", false)]
    public void IsMatch_Question_Posix(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("[abc]", "a", true)]
    [InlineData("[abc]", "b", true)]
    [InlineData("[abc]", "c", true)]
    [InlineData("[abc]", "d", false)]
    [InlineData("[a-z]", "m", true)]
    [InlineData("[a-z]", "M", false)]
    [InlineData("[!abc]", "d", true)]
    [InlineData("[!abc]", "a", false)]
    [InlineData("[^abc]", "d", true)]
    [InlineData("[]]", "]", true)]
    [InlineData("[a-c]x", "bx", true)]
    [InlineData("[a-c]x", "dx", false)]
    public void IsMatch_CharacterClass_Posix(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("ABC", "abc", true)]
    [InlineData("abc", "ABC", true)]
    [InlineData("A*C", "axxxc", true)]
    [InlineData("[A-Z]", "m", true)]
    [InlineData("[a-z]", "M", true)]
    public void IsMatch_IgnoreCase_Ascii(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
    }

    [Theory]
    // Posix dialect uses ASCII-only case folding. Non-ASCII letter pairs must NOT
    // compare equal under IgnoreCase even though they are Unicode case-fold equivalents.
    [InlineData("caf\u00e9", "CAF\u00c9", false)]   // café vs CAFÉ
    [InlineData("\u00fcber", "\u00dcBER", false)]   // über vs ÜBER
    // ASCII letters in the same pattern still fold correctly.
    [InlineData("caf\u00e9", "caf\u00e9", true)]    // identical
    [InlineData("CAF\u00e9", "caf\u00e9", true)]    // ASCII prefix folds, non-ASCII matches ordinal
    public void IsMatch_IgnoreCase_Posix_AsciiFoldOnly(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    // Simple dialect uses full Unicode ordinal IC (matches FileSystemName.MatchesSimpleExpression).
    // Non-ASCII letter pairs DO compare equal under IgnoreCase.
    [InlineData("caf\u00e9", "CAF\u00c9", true)]    // café vs CAFÉ
    [InlineData("\u00fcber", "\u00dcBER", true)]    // über vs ÜBER
    [InlineData("caf\u00e9", "caf\u00ea", false)]   // é vs ê genuinely differ
    public void IsMatch_IgnoreCase_Simple_UnicodeFold(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    [InlineData(".hidden", ".hidden", true)]
    [InlineData("*hidden", ".hidden", false)]
    [InlineData("?hidden", ".hidden", false)]
    [InlineData("[.]hidden", ".hidden", false)]
    [InlineData(".*", ".hidden", true)]
    public void IsMatch_LeadingDot_Default_PosixStrict(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("*hidden", ".hidden", true)]
    [InlineData("?hidden", ".hidden", true)]
    public void IsMatch_LeadingDot_AllowedWhenOptionSet(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.MatchLeadingDot)
            .IsMatch(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\*", "*", true)]
    [InlineData(@"\*", "a", false)]
    [InlineData(@"\?", "?", true)]
    [InlineData(@"\[abc\]", "[abc]", true)]
    public void IsMatch_Escape_Honored(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\*", "\\*", true)]
    [InlineData(@"\*", "*", false)]
    public void IsMatch_Escape_DisabledWithNoEscape(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.NoEscape)
            .IsMatch(input).Should().Be(expected);
    }

    [Fact]
    public void Compile_DanglingEscape_Throws()
    {
        Action act = () => GlobSpecification.Compile(@"abc\", GlobDialect.Posix);
        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [Fact]
    public void Compile_UnterminatedClass_TreatedAsLiteral()
    {
        // Per fnmatch / glibc semantics: an unterminated '[' is treated as a
        // literal character rather than rejected. See PortedTests.Posix.cs for
        // the row that pinned this down (B.6 031: "/[", "\\/[", 0).
        GlobSpecification matcher = GlobSpecification.Compile("[abc", GlobDialect.Posix);
        matcher.IsMatch("[abc").Should().BeTrue();
        matcher.IsMatch("abc").Should().BeFalse();
    }

    [Fact]
    public void TryCompile_DanglingEscape_ReturnsFalse()
    {
        bool ok = GlobSpecification.TryCompile(@"abc\", GlobDialect.Posix, GlobOptions.None,
            out GlobSpecification? result, out GlobCompileError error);
        ok.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [Theory]
    [InlineData("*.cs", "Program.cs", true)]
    [InlineData("*.cs", "Program.vb", false)]
    [InlineData("Test*.cs", "TestThing.cs", true)]
    public void IsMatch_Simple_BasicCases(string pattern, string input, bool expected)
    {
        GlobSpecification.Compile(pattern, GlobDialect.Simple).IsMatch(input).Should().Be(expected);
    }

    [Fact]
    public void IsMatch_Simple_DoesNotParseCharClass()
    {
        // Simple dialect treats '[' as a literal.
        GlobSpecification.Compile("[abc]", GlobDialect.Simple).IsMatch("[abc]").Should().BeTrue();
        GlobSpecification.Compile("[abc]", GlobDialect.Simple).IsMatch("a").Should().BeFalse();
    }

    [Theory]
    // PowerShell -like / WildcardPattern: *, ?, bracket classes work as in POSIX, but the
    // FNM_PERIOD rule does NOT apply (a leading '.' may be matched by a wildcard).
    [InlineData("*", ".hidden", true)]
    [InlineData("?hidden", ".hidden", true)]
    [InlineData("[.]hidden", ".hidden", true)]
    [InlineData("*.cs", "Program.cs", true)]
    [InlineData("*.cs", "Program.vb", false)]
    [InlineData("[a-c]x", "bx", true)]
    [InlineData("[a-c]x", "dx", false)]
    public void IsMatch_PowerShell_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PowerShell).IsMatch(input).Should().Be(expected);

    [Theory]
    // PowerShell escape character is backtick, not backslash.
    [InlineData("a`*b", "a*b", true)]         // backtick escapes '*' -> literal '*'
    [InlineData("a`*b", "axb", false)]
    [InlineData("a`?b", "a?b", true)]
    [InlineData("a`?b", "axb", false)]
    [InlineData("a`[b", "a[b", true)]         // backtick escapes '[' -> literal
    [InlineData("a\\b", "a\\b", true)]        // backslash is literal under PowerShell
    [InlineData("a\\b", "axb", false)]
    public void IsMatch_PowerShell_BacktickEscape(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PowerShell).IsMatch(input).Should().Be(expected);

    [Fact]
    public void Compile_PowerShell_DanglingBacktick_Throws()
    {
        bool ok = GlobSpecification.TryCompile("ab`", GlobDialect.PowerShell, GlobOptions.None,
            out GlobSpecification? result, out GlobCompileError error);
        ok.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [Theory]
    [InlineData("ABC", "abc", true)]
    [InlineData("[A-Z]", "m", true)]
    public void IsMatch_IgnoreCase_PowerShell_Unicode(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PowerShell, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);

    [Fact]
    public void Compile_UnsupportedDialect_Throws()
    {
#pragma warning disable CS0618 // GlobDialect.Win32 is obsolete; this test pins its not-implemented behavior.
        Action act = () => GlobSpecification.Compile("*", GlobDialect.Win32);
#pragma warning restore CS0618
        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.FeatureNotEnabled);
    }

    [Fact]
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

    [Fact]
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

    [Theory]
    // '*' does not cross '/' in path-aware dialects.
    [InlineData("*", "abc", true)]
    [InlineData("*", "a/b", false)]
    [InlineData("*.cs", "Program.cs", true)]
    [InlineData("*.cs", "src/Program.cs", false)]
    [InlineData("src/*.cs", "src/Program.cs", true)]
    [InlineData("src/*.cs", "src/sub/Program.cs", false)]
    // '?' does not match '/'.
    [InlineData("a?b", "a/b", false)]
    [InlineData("a?b", "aXb", true)]
    // Multi-segment literal patterns still match exactly.
    [InlineData("a/b/c", "a/b/c", true)]
    [InlineData("a/b/c", "a/b/d", false)]
    // Contains-shape under path-aware: '*foo*' must route through CompiledGlobStrategy
    // and reject inputs whose required wildcard span would have to cross '/'.
    [InlineData("*foo*", "afoob", true)]
    [InlineData("*foo*", "x/foo/y", false)]
    [InlineData("a*b", "axb", true)]
    [InlineData("a*b", "a/b", false)]
    // Character classes also reject the separator.
    [InlineData("a[/x]b", "a/b", false)]
    [InlineData("a[/x]b", "axb", true)]
    public void IsMatch_PosixPath_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath).IsMatch(input).Should().Be(expected);

    [Theory]
    // GS_None: bare '**' matches anything, including the empty string and paths
    // containing separators.
    [InlineData("**", "", true)]
    [InlineData("**", "a", true)]
    [InlineData("**", "a/b/c", true)]
    // GS_R (leading '**/'): zero-or-more dirs followed by the trailing segment.
    [InlineData("**/foo", "foo", true)]
    [InlineData("**/foo", "bar/foo", true)]
    [InlineData("**/foo", "a/b/foo", true)]
    [InlineData("**/foo", "barfoo", false)]
    [InlineData("**/foo", "xfoox", false)]
    [InlineData("**/foo", "foo/bar", false)]
    // GS_L (trailing '/**'): the leading segment followed by zero-or-more dirs.
    [InlineData("foo/**", "foo", true)]
    [InlineData("foo/**", "foo/x", true)]
    [InlineData("foo/**", "foo/x/y", true)]
    [InlineData("foo/**", "foox", false)]
    [InlineData("foo/**", "fooz/y", false)]
    // GS_LT (middle '/**/'): two literal anchors with zero-or-more dirs between.
    [InlineData("a/**/b", "a/b", true)]
    [InlineData("a/**/b", "a/x/b", true)]
    [InlineData("a/**/b", "a/x/y/b", true)]
    [InlineData("a/**/b", "ab", false)]
    [InlineData("a/**/b", "a/xb", false)]
    [InlineData("a/**/b", "a/b/c", false)]
    // Mixed with '*' inside segments.
    [InlineData("**/*.cs", "Foo.cs", true)]
    [InlineData("**/*.cs", "src/Foo.cs", true)]
    [InlineData("**/*.cs", "src/sub/Foo.cs", true)]
    [InlineData("**/*.cs", "Foo.txt", false)]
    // Two globstars; the second pads zero or more trailing dirs.
    [InlineData("**/foo/**", "foo", true)]
    [InlineData("**/foo/**", "foo/bar", true)]
    [InlineData("**/foo/**", "a/foo/b/c", true)]
    [InlineData("**/foo/**", "bar", false)]
    public void IsMatch_PosixPath_GlobStar(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    // '**' not in its own segment must behave like '*' (no separator crossing).
    [InlineData("**foo", "foo", true)]
    [InlineData("**foo", "xfoo", true)]
    [InlineData("**foo", "a/foo", false)]
    [InlineData("foo**", "foo", true)]
    [InlineData("foo**", "foox", true)]
    [InlineData("foo**", "foo/x", false)]
    [InlineData("a**b", "axb", true)]
    [InlineData("a**b", "a/b", false)]
    public void IsMatch_PosixPath_GlobStarNotEligible_TreatedAsStar(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    // Path-aware dialects skip the simple Prefix/Suffix/Contains/PrefixSuffix matchers
    // because none of those types track the separator yet. All wildcard patterns route
    // through CompiledGlobStrategy; pure literals still hit LiteralGlobStrategy.
    [InlineData("", typeof(LiteralGlobStrategy))]
    [InlineData("abc", typeof(LiteralGlobStrategy))]
    [InlineData("a/b/c", typeof(LiteralGlobStrategy))]
    [InlineData("*", typeof(CompiledGlobStrategy))]
    [InlineData("*.cs", typeof(CompiledGlobStrategy))]
    [InlineData("a*", typeof(CompiledGlobStrategy))]
    [InlineData("*foo*", typeof(CompiledGlobStrategy))]
    [InlineData("a*b", typeof(CompiledGlobStrategy))]
    [InlineData("a?b", typeof(CompiledGlobStrategy))]
    public void Compile_PosixPath_ChoosesStrategy(string pattern, Type expectedType)
    {
        GlobSpecification spec = GlobSpecification.Compile(pattern, GlobDialect.PosixPath);
        object strategy = spec.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType(expectedType);
    }

    [Theory]
    // The `**/<segment>` shape (path-aware globstar + single trailing segment without
    // further separators) routes to the GlobStarFileNameStrategy specialization for
    // any segment that the path-unaware specialization or the literal short-circuit
    // can satisfy. Segments containing classes or question marks still fall through
    // to CompiledGlobStrategy (when the dialect honors classes).
    [InlineData(GlobDialect.FileSystemGlobbing, "**/*.cs", typeof(GlobStarFileNameStrategy))]
    [InlineData(GlobDialect.FileSystemGlobbing, "**/file.cs", typeof(GlobStarFileNameStrategy))]
    [InlineData(GlobDialect.FileSystemGlobbing, "**/abc*", typeof(GlobStarFileNameStrategy))]
    [InlineData(GlobDialect.FileSystemGlobbing, "**/*foo*", typeof(GlobStarFileNameStrategy))]
    [InlineData(GlobDialect.FileSystemGlobbing, "**/a*b", typeof(GlobStarFileNameStrategy))]
    [InlineData(GlobDialect.FileSystemGlobbing, "**/*", typeof(GlobStarFileNameStrategy))]
    [InlineData(GlobDialect.MSBuild, "**/*.cs", typeof(GlobStarFileNameStrategy))]
    [InlineData(GlobDialect.Git, "**/*.cs", typeof(GlobStarFileNameStrategy))]
    [InlineData(GlobDialect.PosixPath, "**/a?b", typeof(CompiledGlobStrategy))]
    [InlineData(GlobDialect.PosixPath, "**/[abc]", typeof(CompiledGlobStrategy))]
    [InlineData(GlobDialect.FileSystemGlobbing, "**/sub/*.cs", typeof(CompiledGlobStrategy))]
    public void Compile_GlobStarFileNameSpecialization(GlobDialect dialect, string pattern, Type expectedType)
    {
        GlobSpecification spec = GlobSpecification.Compile(pattern, dialect, GlobOptions.AllowGlobStar);
        object strategy = spec.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType(expectedType);
    }

    [Theory]
    // Functional parity against the bytecode interpreter for the new specialization.
    [InlineData("**/*.cs", "a.cs", true)]
    [InlineData("**/*.cs", "src/a.cs", true)]
    [InlineData("**/*.cs", "src/sub/a.cs", true)]
    [InlineData("**/*.cs", "a.txt", false)]
    [InlineData("**/*.cs", "src/a.txt", false)]
    [InlineData("**/file.cs", "file.cs", true)]
    [InlineData("**/file.cs", "src/file.cs", true)]
    [InlineData("**/file.cs", "src/other.cs", false)]
    [InlineData("**/foo*", "foobar", true)]
    [InlineData("**/foo*", "dir/foobar", true)]
    [InlineData("**/foo*", "dir/bar", false)]
    [InlineData("**/*foo*", "barfoobaz", true)]
    [InlineData("**/*foo*", "dir/barfoobaz", true)]
    public void IsMatch_GlobStarFileNameSpecialization_FunctionalParity(
        string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing).IsMatch(input).Should().Be(expected);

    [Fact]
    public void Compile_PosixPath_SeparatorIsForwardSlash() =>
        GlobSpecification.Compile("*", GlobDialect.PosixPath).Separator.Should().Be('/');

    [Fact]
    public void Compile_Posix_SeparatorIsZero() =>
        GlobSpecification.Compile("*", GlobDialect.Posix).Separator.Should().Be('\0');

    [Fact]
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

    [Theory]
    [InlineData("", typeof(LiteralGlobStrategy))]
    [InlineData("abc", typeof(LiteralGlobStrategy))]
    [InlineData("*", typeof(AnyGlobStrategy))]
    [InlineData("**", typeof(AnyGlobStrategy))]
    [InlineData("abc*", typeof(PrefixGlobStrategy))]
    [InlineData("*.cs", typeof(SuffixGlobStrategy))]
    [InlineData("*foo*", typeof(ContainsGlobStrategy))]
    [InlineData("a*b", typeof(PrefixSuffixGlobStrategy))]
    [InlineData("a?b", typeof(CompiledGlobStrategy))]
    [InlineData("[abc]", typeof(CompiledGlobStrategy))]
    [InlineData("a*b*c", typeof(CompiledGlobStrategy))]
    public void Compile_ChoosesSimplestStrategy(string pattern, Type expectedType)
    {
        GlobSpecification matcher = GlobSpecification.Compile(pattern, GlobDialect.Posix);
        object strategy = matcher.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType(expectedType);
    }
}
