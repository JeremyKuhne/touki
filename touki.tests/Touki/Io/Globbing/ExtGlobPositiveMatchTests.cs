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
[TestClass]
public class ExtGlobPositiveMatchTests
{
    private static bool Match(string pattern, string input, GlobDialect dialect = GlobDialect.Bash) =>
        GlobSpecification.Compile(
            pattern,
            dialect,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob).IsMatch(input);

    // -- @(...) : exactly one alternative must match --------------------------------

    [TestMethod]
    [DataRow("@(foo)", "foo", true)]
    [DataRow("@(foo)", "bar", false)]
    [DataRow("@(foo|bar)", "foo", true)]
    [DataRow("@(foo|bar)", "bar", true)]
    [DataRow("@(foo|bar)", "baz", false)]
    [DataRow("@(a|b|c)", "a", true)]
    [DataRow("@(a|b|c)", "c", true)]
    [DataRow("@(a|b|c)", "d", false)]
    [DataRow("@(a|b)", "", false)]
    [DataRow("@(a|b)", "ab", false)]
    public void Match_At_ExactlyOneAlternative(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    [TestMethod]
    // @(...) embedded in surrounding literals.
    [DataRow("foo@(x|y)bar", "fooxbar", true)]
    [DataRow("foo@(x|y)bar", "fooybar", true)]
    [DataRow("foo@(x|y)bar", "foozbar", false)]
    [DataRow("foo@(x|y)bar", "fooxybar", false)]
    [DataRow("foo@(x|y)bar", "foobar", false)]
    public void Match_At_WithSurroundingLiterals(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- ?(...) : zero or one alternative ---------------------------------------------

    [TestMethod]
    [DataRow("?(foo)", "", true)]
    [DataRow("?(foo)", "foo", true)]
    [DataRow("?(foo)", "bar", false)]
    [DataRow("?(foo)", "foofoo", false)]
    [DataRow("?(a|b)", "", true)]
    [DataRow("?(a|b)", "a", true)]
    [DataRow("?(a|b)", "b", true)]
    [DataRow("?(a|b)", "c", false)]
    [DataRow("foo?(x|y)bar", "fooxbar", true)]
    [DataRow("foo?(x|y)bar", "foobar", true)]
    [DataRow("foo?(x|y)bar", "fooxxbar", false)]
    public void Match_Question_ZeroOrOne(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- +(...) : one or more alternatives --------------------------------------------

    [TestMethod]
    [DataRow("+(a)", "", false)]
    [DataRow("+(a)", "a", true)]
    [DataRow("+(a)", "aa", true)]
    [DataRow("+(a)", "aaaa", true)]
    [DataRow("+(a)", "ab", false)]
    [DataRow("+(a|b)", "ab", true)]
    [DataRow("+(a|b)", "aabb", true)]
    [DataRow("+(a|b)", "abab", true)]
    [DataRow("+(a|b)", "abc", false)]
    [DataRow("+(a|b)", "", false)]
    [DataRow("foo+(x|y)bar", "fooxbar", true)]
    [DataRow("foo+(x|y)bar", "fooxxbar", true)]
    [DataRow("foo+(x|y)bar", "fooxyxbar", true)]
    [DataRow("foo+(x|y)bar", "foobar", false)]
    public void Match_Plus_OneOrMore(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- *(...) : zero or more alternatives -------------------------------------------

    [TestMethod]
    [DataRow("*(a)", "", true)]
    [DataRow("*(a)", "a", true)]
    [DataRow("*(a)", "aa", true)]
    [DataRow("*(a)", "aaaa", true)]
    [DataRow("*(a)", "ab", false)]
    [DataRow("*(a|b)", "", true)]
    [DataRow("*(a|b)", "abab", true)]
    [DataRow("*(a|b)", "c", false)]
    [DataRow("foo*(x|y)bar", "foobar", true)]
    [DataRow("foo*(x|y)bar", "fooxbar", true)]
    [DataRow("foo*(x|y)bar", "fooxxxxxbar", true)]
    [DataRow("foo*(x|y)bar", "fooxybar", true)]
    [DataRow("foo*(x|y)bar", "foozbar", false)]
    public void Match_Star_ZeroOrMore(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Multiple alternatives --------------------------------------------------------

    [TestMethod]
    [DataRow("@(foo|bar|baz)", "foo", true)]
    [DataRow("@(foo|bar|baz)", "bar", true)]
    [DataRow("@(foo|bar|baz)", "baz", true)]
    [DataRow("@(foo|bar|baz)", "qux", false)]
    public void Match_MultipleAlternatives(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Empty alternatives -----------------------------------------------------------

    [TestMethod]
    [DataRow("@(|)", "", true)]
    [DataRow("@(|)", "x", false)]
    [DataRow("@(|a)", "", true)]
    [DataRow("@(|a)", "a", true)]
    [DataRow("@(a|)", "", true)]
    [DataRow("@(a|)", "a", true)]
    [DataRow("foo@(|x)bar", "foobar", true)]
    [DataRow("foo@(|x)bar", "fooxbar", true)]
    [DataRow("foo@(|x)bar", "fooybar", false)]
    public void Match_EmptyAlternatives(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Inner wildcards inside alternatives ------------------------------------------

    [TestMethod]
    [DataRow("@(*.cs|*.txt)", "foo.cs", true)]
    [DataRow("@(*.cs|*.txt)", "foo.txt", true)]
    [DataRow("@(*.cs|*.txt)", "foo.json", false)]
    [DataRow("@(a?b)", "axb", true)]
    [DataRow("@(a?b)", "ab", false)]
    [DataRow("@(a?b)", "axyb", false)]
    public void Match_InnerWildcards(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Nested extglob ---------------------------------------------------------------

    [TestMethod]
    [DataRow("*(a|@(b|c))d", "d", true)]
    [DataRow("*(a|@(b|c))d", "ad", true)]
    [DataRow("*(a|@(b|c))d", "bd", true)]
    [DataRow("*(a|@(b|c))d", "cd", true)]
    [DataRow("*(a|@(b|c))d", "abcd", true)]
    [DataRow("*(a|@(b|c))d", "abxd", false)]
    [DataRow("?(@(foo|bar))", "foo", true)]
    [DataRow("?(@(foo|bar))", "bar", true)]
    [DataRow("?(@(foo|bar))", "", true)]
    [DataRow("?(@(foo|bar))", "baz", false)]
    public void Match_Nested(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Path-aware: inner wildcards don't cross the separator ------------------------

    [TestMethod]
    [DataRow("@(*.cs|*.txt)", "foo/bar.cs", false)]
    [DataRow("@(*.cs|*.txt)", "foo.cs", true)]
    [DataRow("dir/@(a|b)", "dir/a", true)]
    [DataRow("dir/@(a|b)", "dir/b", true)]
    [DataRow("dir/@(a|b)", "dir/c", false)]
    public void Match_PathAware(string pattern, string input, bool expected) =>
        Match(pattern, input, GlobDialect.Bash).Should().Be(expected);

    // -- IgnoreCase ------------------------------------------------------------------

    [TestMethod]
    [DataRow("@(FOO|BAR)", "foo", true)]
    [DataRow("@(FOO|BAR)", "bar", true)]
    [DataRow("@(FOO|BAR)", "baz", false)]
    public void Match_IgnoreCase(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob | GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
}
