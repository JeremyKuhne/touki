// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- Git dialect ---

    [Theory]
    // Git has implicit globstar, '/' separator, '\' escape, supports classes, and
    // strips gitignore-style leading '!' / leading '/' / trailing '/' markers.
    // Non-anchored patterns without an internal '/' match at any path depth (the
    // factory prepends `**/` at compile time per the gitignore "match anywhere" rule).
    [InlineData("*.cs", "Foo.cs", true)]
    [InlineData("*.cs", "src/Foo.cs", true)]                   // match-anywhere
    [InlineData("*.cs", "a/b/c/Foo.cs", true)]                 // match-anywhere
    [InlineData("**/*.cs", "src/Foo.cs", true)]
    [InlineData("[abc].txt", "a.txt", true)]
    // Patterns with an internal '/' are treated as anchored to the gitignore root.
    [InlineData("src/*.cs", "src/Foo.cs", true)]
    [InlineData("src/*.cs", "lib/src/Foo.cs", false)]          // anchored: nested doesn't match
    public void IsMatch_Git_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Git)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    // Leading '/' anchors the pattern to the gitignore root; the leading '/' is
    // stripped but the pattern is no longer subject to the "match anywhere" rule.
    [InlineData("/bin", "bin", true)]
    [InlineData("/bin", "src/bin", false)]
    public void IsMatch_Git_RootAnchored(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Git)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    // Leading '!' negates the match.
    [InlineData("!*.log", "Foo.log", false)]
    [InlineData("!*.log", "Foo.txt", true)]
    public void IsMatch_Git_Negation(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Git)
            .IsMatch(input).Should().Be(expected);

    [Fact]
    public void Compile_Git_LeadingBang_SetsNegated()
    {
        GlobSpecification matcher = GlobSpecification.Compile("!*.log", GlobDialect.Git);
        matcher.Negated.Should().BeTrue();
        matcher.RootAnchored.Should().BeFalse();
        matcher.DirectoryOnly.Should().BeFalse();
    }

    [Fact]
    public void Compile_Git_LeadingSlash_SetsRootAnchored()
    {
        GlobSpecification matcher = GlobSpecification.Compile("/bin", GlobDialect.Git);
        matcher.RootAnchored.Should().BeTrue();
        matcher.Negated.Should().BeFalse();
        matcher.DirectoryOnly.Should().BeFalse();
        matcher.IsMatch("bin").Should().BeTrue();
    }

    [Fact]
    public void Compile_Git_TrailingSlash_SetsDirectoryOnly()
    {
        GlobSpecification matcher = GlobSpecification.Compile("logs/", GlobDialect.Git);
        matcher.DirectoryOnly.Should().BeTrue();
        matcher.Negated.Should().BeFalse();
        matcher.RootAnchored.Should().BeFalse();
        matcher.IsMatch("logs").Should().BeTrue();
    }

    [Fact]
    public void Compile_Git_AllThreeMarkers_StrippedAndExposed()
    {
        // !/bin/  ->  Negated, RootAnchored, DirectoryOnly all set; matcher matches "bin".
        GlobSpecification matcher = GlobSpecification.Compile("!/bin/", GlobDialect.Git);
        matcher.Negated.Should().BeTrue();
        matcher.RootAnchored.Should().BeTrue();
        matcher.DirectoryOnly.Should().BeTrue();
        // IsMatch wraps with Negated: stripped pattern "bin" matches "bin" literally,
        // so the negated result is false.
        matcher.IsMatch("bin").Should().BeFalse();
        matcher.IsMatch("logs").Should().BeTrue();
    }

    [Fact]
    public void Compile_Git_SeparatorIsForwardSlash() =>
        GlobSpecification.Compile("*", GlobDialect.Git).Separator.Should().Be('/');

    // --- Negation property (default false on non-Git dialects) ---

    [Theory]
    [InlineData(GlobDialect.Posix, "!*.cs")]
    [InlineData(GlobDialect.PosixPath, "!*.cs")]
    [InlineData(GlobDialect.MSBuild, "!*.cs")]
    [InlineData(GlobDialect.Bash, "!*.cs")]
    [InlineData(GlobDialect.FileSystemGlobbing, "!*.cs")]
    public void Compile_NonGit_LeadingBang_IsLiteral(GlobDialect dialect, string pattern)
    {
        GlobSpecification matcher = GlobSpecification.Compile(pattern, dialect);
        matcher.Negated.Should().BeFalse();
        // Pattern compiled as literal '!*.cs', so "Foo.cs" does NOT match.
        matcher.IsMatch("Foo.cs").Should().BeFalse();
        matcher.IsMatch("!Foo.cs").Should().BeTrue();
    }

    // Regression: StripGitignoreMarkers must not throw IndexOutOfRangeException
    // for patterns that become empty between strips. Reported by Copilot review
    // on PR #160. Each input below crashed before the fix:
    //   "!"   -> strip '!' -> empty, then pattern[0] == '/' threw.
    //   "/"   -> strip '/' -> empty, then pattern[^1] == '/' threw.
    //   "!/"  -> strip '!' -> "/", strip '/' -> empty, then pattern[^1] threw.
    [Theory]
    [InlineData("!", true, false, false)]
    [InlineData("/", false, true, false)]
    [InlineData("!/", true, true, false)]
    public void Compile_Git_PathologicalShortPatterns_DoesNotThrow(
        string pattern,
        bool expectedNegated,
        bool expectedRootAnchored,
        bool expectedDirectoryOnly)
    {
        GlobSpecification matcher = GlobSpecification.Compile(pattern, GlobDialect.Git);
        matcher.Negated.Should().Be(expectedNegated);
        matcher.RootAnchored.Should().Be(expectedRootAnchored);
        matcher.DirectoryOnly.Should().Be(expectedDirectoryOnly);
    }
}
