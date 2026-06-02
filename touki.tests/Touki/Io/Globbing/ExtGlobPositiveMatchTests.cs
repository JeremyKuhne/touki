// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Match tests for the four positive extended-glob constructs -
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

    [Test]
    [Arguments("@(foo)", "foo", true)]
    [Arguments("@(foo)", "bar", false)]
    [Arguments("@(foo|bar)", "foo", true)]
    [Arguments("@(foo|bar)", "bar", true)]
    [Arguments("@(foo|bar)", "baz", false)]
    [Arguments("@(a|b|c)", "a", true)]
    [Arguments("@(a|b|c)", "c", true)]
    [Arguments("@(a|b|c)", "d", false)]
    [Arguments("@(a|b)", "", false)]
    [Arguments("@(a|b)", "ab", false)]
    public void Match_At_ExactlyOneAlternative(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    [Test]
    // @(...) embedded in surrounding literals.
    [Arguments("foo@(x|y)bar", "fooxbar", true)]
    [Arguments("foo@(x|y)bar", "fooybar", true)]
    [Arguments("foo@(x|y)bar", "foozbar", false)]
    [Arguments("foo@(x|y)bar", "fooxybar", false)]
    [Arguments("foo@(x|y)bar", "foobar", false)]
    public void Match_At_WithSurroundingLiterals(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- ?(...) : zero or one alternative ---------------------------------------------

    [Test]
    [Arguments("?(foo)", "", true)]
    [Arguments("?(foo)", "foo", true)]
    [Arguments("?(foo)", "bar", false)]
    [Arguments("?(foo)", "foofoo", false)]
    [Arguments("?(a|b)", "", true)]
    [Arguments("?(a|b)", "a", true)]
    [Arguments("?(a|b)", "b", true)]
    [Arguments("?(a|b)", "c", false)]
    [Arguments("foo?(x|y)bar", "fooxbar", true)]
    [Arguments("foo?(x|y)bar", "foobar", true)]
    [Arguments("foo?(x|y)bar", "fooxxbar", false)]
    public void Match_Question_ZeroOrOne(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- +(...) : one or more alternatives --------------------------------------------

    [Test]
    [Arguments("+(a)", "", false)]
    [Arguments("+(a)", "a", true)]
    [Arguments("+(a)", "aa", true)]
    [Arguments("+(a)", "aaaa", true)]
    [Arguments("+(a)", "ab", false)]
    [Arguments("+(a|b)", "ab", true)]
    [Arguments("+(a|b)", "aabb", true)]
    [Arguments("+(a|b)", "abab", true)]
    [Arguments("+(a|b)", "abc", false)]
    [Arguments("+(a|b)", "", false)]
    [Arguments("foo+(x|y)bar", "fooxbar", true)]
    [Arguments("foo+(x|y)bar", "fooxxbar", true)]
    [Arguments("foo+(x|y)bar", "fooxyxbar", true)]
    [Arguments("foo+(x|y)bar", "foobar", false)]
    public void Match_Plus_OneOrMore(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- *(...) : zero or more alternatives -------------------------------------------

    [Test]
    [Arguments("*(a)", "", true)]
    [Arguments("*(a)", "a", true)]
    [Arguments("*(a)", "aa", true)]
    [Arguments("*(a)", "aaaa", true)]
    [Arguments("*(a)", "ab", false)]
    [Arguments("*(a|b)", "", true)]
    [Arguments("*(a|b)", "abab", true)]
    [Arguments("*(a|b)", "c", false)]
    [Arguments("foo*(x|y)bar", "foobar", true)]
    [Arguments("foo*(x|y)bar", "fooxbar", true)]
    [Arguments("foo*(x|y)bar", "fooxxxxxbar", true)]
    [Arguments("foo*(x|y)bar", "fooxybar", true)]
    [Arguments("foo*(x|y)bar", "foozbar", false)]
    public void Match_Star_ZeroOrMore(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Multiple alternatives --------------------------------------------------------

    [Test]
    [Arguments("@(foo|bar|baz)", "foo", true)]
    [Arguments("@(foo|bar|baz)", "bar", true)]
    [Arguments("@(foo|bar|baz)", "baz", true)]
    [Arguments("@(foo|bar|baz)", "qux", false)]
    public void Match_MultipleAlternatives(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Empty alternatives -----------------------------------------------------------

    [Test]
    [Arguments("@(|)", "", true)]
    [Arguments("@(|)", "x", false)]
    [Arguments("@(|a)", "", true)]
    [Arguments("@(|a)", "a", true)]
    [Arguments("@(a|)", "", true)]
    [Arguments("@(a|)", "a", true)]
    [Arguments("foo@(|x)bar", "foobar", true)]
    [Arguments("foo@(|x)bar", "fooxbar", true)]
    [Arguments("foo@(|x)bar", "fooybar", false)]
    public void Match_EmptyAlternatives(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Inner wildcards inside alternatives ------------------------------------------

    [Test]
    [Arguments("@(*.cs|*.txt)", "foo.cs", true)]
    [Arguments("@(*.cs|*.txt)", "foo.txt", true)]
    [Arguments("@(*.cs|*.txt)", "foo.json", false)]
    [Arguments("@(a?b)", "axb", true)]
    [Arguments("@(a?b)", "ab", false)]
    [Arguments("@(a?b)", "axyb", false)]
    public void Match_InnerWildcards(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Nested extglob ---------------------------------------------------------------

    [Test]
    [Arguments("*(a|@(b|c))d", "d", true)]
    [Arguments("*(a|@(b|c))d", "ad", true)]
    [Arguments("*(a|@(b|c))d", "bd", true)]
    [Arguments("*(a|@(b|c))d", "cd", true)]
    [Arguments("*(a|@(b|c))d", "abcd", true)]
    [Arguments("*(a|@(b|c))d", "abxd", false)]
    [Arguments("?(@(foo|bar))", "foo", true)]
    [Arguments("?(@(foo|bar))", "bar", true)]
    [Arguments("?(@(foo|bar))", "", true)]
    [Arguments("?(@(foo|bar))", "baz", false)]
    public void Match_Nested(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Path-aware: inner wildcards don't cross the separator ------------------------

    [Test]
    [Arguments("@(*.cs|*.txt)", "foo/bar.cs", false)]
    [Arguments("@(*.cs|*.txt)", "foo.cs", true)]
    [Arguments("dir/@(a|b)", "dir/a", true)]
    [Arguments("dir/@(a|b)", "dir/b", true)]
    [Arguments("dir/@(a|b)", "dir/c", false)]
    public void Match_PathAware(string pattern, string input, bool expected) =>
        Match(pattern, input, GlobDialect.Bash).Should().Be(expected);

    // -- IgnoreCase ------------------------------------------------------------------

    [Test]
    [Arguments("@(FOO|BAR)", "foo", true)]
    [Arguments("@(FOO|BAR)", "bar", true)]
    [Arguments("@(FOO|BAR)", "baz", false)]
    public void Match_IgnoreCase(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob | GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
}
