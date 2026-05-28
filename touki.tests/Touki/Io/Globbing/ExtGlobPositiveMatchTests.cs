// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Match tests for the four positive extended-glob constructs &#8212;
///  <c>?(...)</c>, <c>*(...)</c>, <c>+(...)</c>, and <c>@(...)</c>. Negation
///  (<c>!(...)</c>) is covered in a later step and intentionally still
///  reports no match here.
/// </summary>
public class ExtGlobPositiveMatchTests
{
    private static bool Match(string pattern, string input, GlobDialect dialect = GlobDialect.Bash) =>
        GlobSpecification.Compile(
            pattern,
            dialect,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob).IsMatch(input);

    // -- @(...) : exactly one alternative must match --------------------------------

    [Theory]
    [InlineData("@(foo)", "foo", true)]
    [InlineData("@(foo)", "bar", false)]
    [InlineData("@(foo|bar)", "foo", true)]
    [InlineData("@(foo|bar)", "bar", true)]
    [InlineData("@(foo|bar)", "baz", false)]
    [InlineData("@(a|b|c)", "a", true)]
    [InlineData("@(a|b|c)", "c", true)]
    [InlineData("@(a|b|c)", "d", false)]
    [InlineData("@(a|b)", "", false)]
    [InlineData("@(a|b)", "ab", false)]
    public void Match_At_ExactlyOneAlternative(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    [Theory]
    // @(...) embedded in surrounding literals.
    [InlineData("foo@(x|y)bar", "fooxbar", true)]
    [InlineData("foo@(x|y)bar", "fooybar", true)]
    [InlineData("foo@(x|y)bar", "foozbar", false)]
    [InlineData("foo@(x|y)bar", "fooxybar", false)]
    [InlineData("foo@(x|y)bar", "foobar", false)]
    public void Match_At_WithSurroundingLiterals(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- ?(...) : zero or one alternative ---------------------------------------------

    [Theory]
    [InlineData("?(foo)", "", true)]
    [InlineData("?(foo)", "foo", true)]
    [InlineData("?(foo)", "bar", false)]
    [InlineData("?(foo)", "foofoo", false)]
    [InlineData("?(a|b)", "", true)]
    [InlineData("?(a|b)", "a", true)]
    [InlineData("?(a|b)", "b", true)]
    [InlineData("?(a|b)", "c", false)]
    [InlineData("foo?(x|y)bar", "fooxbar", true)]
    [InlineData("foo?(x|y)bar", "foobar", true)]
    [InlineData("foo?(x|y)bar", "fooxxbar", false)]
    public void Match_Question_ZeroOrOne(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- +(...) : one or more alternatives --------------------------------------------

    [Theory]
    [InlineData("+(a)", "", false)]
    [InlineData("+(a)", "a", true)]
    [InlineData("+(a)", "aa", true)]
    [InlineData("+(a)", "aaaa", true)]
    [InlineData("+(a)", "ab", false)]
    [InlineData("+(a|b)", "ab", true)]
    [InlineData("+(a|b)", "aabb", true)]
    [InlineData("+(a|b)", "abab", true)]
    [InlineData("+(a|b)", "abc", false)]
    [InlineData("+(a|b)", "", false)]
    [InlineData("foo+(x|y)bar", "fooxbar", true)]
    [InlineData("foo+(x|y)bar", "fooxxbar", true)]
    [InlineData("foo+(x|y)bar", "fooxyxbar", true)]
    [InlineData("foo+(x|y)bar", "foobar", false)]
    public void Match_Plus_OneOrMore(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- *(...) : zero or more alternatives -------------------------------------------

    [Theory]
    [InlineData("*(a)", "", true)]
    [InlineData("*(a)", "a", true)]
    [InlineData("*(a)", "aa", true)]
    [InlineData("*(a)", "aaaa", true)]
    [InlineData("*(a)", "ab", false)]
    [InlineData("*(a|b)", "", true)]
    [InlineData("*(a|b)", "abab", true)]
    [InlineData("*(a|b)", "c", false)]
    [InlineData("foo*(x|y)bar", "foobar", true)]
    [InlineData("foo*(x|y)bar", "fooxbar", true)]
    [InlineData("foo*(x|y)bar", "fooxxxxxbar", true)]
    [InlineData("foo*(x|y)bar", "fooxybar", true)]
    [InlineData("foo*(x|y)bar", "foozbar", false)]
    public void Match_Star_ZeroOrMore(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Multiple alternatives --------------------------------------------------------

    [Theory]
    [InlineData("@(foo|bar|baz)", "foo", true)]
    [InlineData("@(foo|bar|baz)", "bar", true)]
    [InlineData("@(foo|bar|baz)", "baz", true)]
    [InlineData("@(foo|bar|baz)", "qux", false)]
    public void Match_MultipleAlternatives(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Empty alternatives -----------------------------------------------------------

    [Theory]
    [InlineData("@(|)", "", true)]
    [InlineData("@(|)", "x", false)]
    [InlineData("@(|a)", "", true)]
    [InlineData("@(|a)", "a", true)]
    [InlineData("@(a|)", "", true)]
    [InlineData("@(a|)", "a", true)]
    [InlineData("foo@(|x)bar", "foobar", true)]
    [InlineData("foo@(|x)bar", "fooxbar", true)]
    [InlineData("foo@(|x)bar", "fooybar", false)]
    public void Match_EmptyAlternatives(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Inner wildcards inside alternatives ------------------------------------------

    [Theory]
    [InlineData("@(*.cs|*.txt)", "foo.cs", true)]
    [InlineData("@(*.cs|*.txt)", "foo.txt", true)]
    [InlineData("@(*.cs|*.txt)", "foo.json", false)]
    [InlineData("@(a?b)", "axb", true)]
    [InlineData("@(a?b)", "ab", false)]
    [InlineData("@(a?b)", "axyb", false)]
    public void Match_InnerWildcards(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Nested extglob ---------------------------------------------------------------

    [Theory]
    [InlineData("*(a|@(b|c))d", "d", true)]
    [InlineData("*(a|@(b|c))d", "ad", true)]
    [InlineData("*(a|@(b|c))d", "bd", true)]
    [InlineData("*(a|@(b|c))d", "cd", true)]
    [InlineData("*(a|@(b|c))d", "abcd", true)]
    [InlineData("*(a|@(b|c))d", "abxd", false)]
    [InlineData("?(@(foo|bar))", "foo", true)]
    [InlineData("?(@(foo|bar))", "bar", true)]
    [InlineData("?(@(foo|bar))", "", true)]
    [InlineData("?(@(foo|bar))", "baz", false)]
    public void Match_Nested(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Path-aware: inner wildcards don't cross the separator ------------------------

    [Theory]
    [InlineData("@(*.cs|*.txt)", "foo/bar.cs", false)]
    [InlineData("@(*.cs|*.txt)", "foo.cs", true)]
    [InlineData("dir/@(a|b)", "dir/a", true)]
    [InlineData("dir/@(a|b)", "dir/b", true)]
    [InlineData("dir/@(a|b)", "dir/c", false)]
    public void Match_PathAware(string pattern, string input, bool expected) =>
        Match(pattern, input, GlobDialect.Bash).Should().Be(expected);

    // -- IgnoreCase ------------------------------------------------------------------

    [Theory]
    [InlineData("@(FOO|BAR)", "foo", true)]
    [InlineData("@(FOO|BAR)", "bar", true)]
    [InlineData("@(FOO|BAR)", "baz", false)]
    public void Match_IgnoreCase(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob | GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
}
