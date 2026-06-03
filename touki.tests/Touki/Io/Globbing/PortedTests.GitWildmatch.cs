// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Pattern-level scenarios ported from git's <c>t/t3070-wildmatch.sh</c>
///  test corpus.
/// </summary>
/// <remarks>
///  <para>
///   Source:
///   <see href="https://github.com/git/git/blob/master/t/t3070-wildmatch.sh"><c>git/t/t3070-wildmatch.sh</c></see>.
///   Licensed under GPL-2.0. The upstream <c>match</c> helper asserts four
///   columns per row:
///   <list type="number">
///    <item><description><c>wildmatch</c> - <c>WM_PATHNAME</c> on (case-sensitive).</description></item>
///    <item><description><c>iwildmatch</c> - <c>WM_PATHNAME</c> on (case-insensitive).</description></item>
///    <item><description><c>pathmatch</c> - <c>WM_PATHNAME</c> off (case-sensitive).</description></item>
///    <item><description><c>ipathmatch</c> - <c>WM_PATHNAME</c> off (case-insensitive).</description></item>
///   </list>
///   This port mines the <c>wildmatch</c> column (column 1 / argument 1) -
///   the <c>WM_PATHNAME</c>-on, case-sensitive variant - because git's own
///   gitignore and pathspec evaluation use that mode
///   (<c>core_wildmatch_flags = WM_PATHNAME</c> in git's source). Rows are
///   asserted against <see cref="GlobSpecification.IsMatch"/> on
///   <see cref="GlobDialect.PosixPath"/> with
///   <see cref="GlobOptions.AllowGlobStar"/> +
///   <see cref="GlobOptions.MatchLeadingDot"/> (wildmatch defaults to
///   matching a leading dot; the <c>WM_PERIOD</c> flag git uses for the
///   period-aware variant is not enabled here).
///  </para>
///  <para>
///   Touki's existing live Git oracle suites
///   (<c>SequentialSeparatorGitOracleTests</c>,
///   <c>MultipleAsteriskOracleTests.Git</c>) cover the gitignore-layer
///   semantics via <c>LibGit2Sharp.Repository.Ignore.IsPathIgnored</c>; this
///   port instead pins the underlying wildmatch engine on every CI runner
///   without needing the native LibGit2Sharp binary or a real git binary on
///   PATH.
///  </para>
/// </remarks>
[TestClass]
public class PortedTests_GitWildmatch
{
    // ----- Basic wildmatch features (column 1 from t3070) -----
    [TestMethod]
    [DataRow("foo", "foo", true)]
    [DataRow("foo", "bar", false)]
    [DataRow("", "", true)]
    [DataRow("foo", "???", true)]
    [DataRow("foo", "??", false)]
    [DataRow("foo", "*", true)]
    [DataRow("foo", "f*", true)]
    [DataRow("foo", "*f", false)]
    [DataRow("foo", "*foo*", true)]
    [DataRow("foobar", "*ob*a*r*", true)]
    [DataRow("aaaaaaabababab", "*ab", true)]
    [DataRow("foo*", "foo\\*", true)]
    [DataRow("foobar", "foo\\*bar", false)]
    [DataRow("f\\oo", "f\\\\oo", true)]
    [DataRow("ball", "*[al]?", true)]
    [DataRow("ten", "[ten]", false)]
    [DataRow("ten", "**[!te]", true)]
    [DataRow("ten", "**[!ten]", false)]
    [DataRow("ten", "t[a-g]n", true)]
    [DataRow("ten", "t[!a-g]n", false)]
    [DataRow("ton", "t[!a-g]n", true)]
    [DataRow("ton", "t[^a-g]n", true)]
    [DataRow("a]b", "a[]]b", true)]
    [DataRow("a-b", "a[]-]b", true)]
    [DataRow("a]b", "a[]-]b", true)]
    [DataRow("aab", "a[]-]b", false)]
    [DataRow("aab", "a[]a-]b", true)]
    [DataRow("]", "]", true)]
    public void IsMatch_GitWildmatch_BasicFeatures(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Extended slash-matching features (WM_PATHNAME on) -----
    [TestMethod]
    [DataRow("foo/baz/bar", "foo*bar", false)]
    [DataRow("foo/baz/bar", "foo**bar", false)]
    [DataRow("foobazbar", "foo**bar", true)]
    [DataRow("foo/baz/bar", "foo/**/bar", true)]
    [DataRow("foo/baz/bar", "foo/**/**/bar", true)]
    [DataRow("foo/b/a/z/bar", "foo/**/bar", true)]
    [DataRow("foo/b/a/z/bar", "foo/**/**/bar", true)]
    [DataRow("foo/bar", "foo/**/bar", true)]
    [DataRow("foo/bar", "foo/**/**/bar", true)]
    [DataRow("foo/bar", "foo?bar", false)]
    [DataRow("foo/bar", "foo[/]bar", false)]
    [DataRow("foo/bar", "foo[^a-z]bar", false)]
    [DataRow("foo/bar", "f[^eiu][^eiu][^eiu][^eiu][^eiu]r", false)]
    [DataRow("foo-bar", "f[^eiu][^eiu][^eiu][^eiu][^eiu]r", true)]
    [DataRow("foo", "**/foo", true)]
    [DataRow("XXX/foo", "**/foo", true)]
    [DataRow("bar/baz/foo", "**/foo", true)]
    [DataRow("bar/baz/foo", "*/foo", false)]
    [DataRow("foo/bar/baz", "**/bar*", false)]
    [DataRow("deep/foo/bar/baz", "**/bar/*", true)]
    [DataRow("deep/foo/bar", "**/bar/*", false)]
    [DataRow("foo/bar/baz", "**/bar**", false)]
    [DataRow("foo/bar/baz/x", "*/bar/**", true)]
    [DataRow("deep/foo/bar/baz/x", "*/bar/**", false)]
    [DataRow("deep/foo/bar/baz/x", "**/bar/*/*", true)]
    public void IsMatch_GitWildmatch_ExtendedSlash(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Various additional tests -----
    [TestMethod]
    [DataRow("acrt", "a[c-c]st", false)]
    [DataRow("acrt", "a[c-c]rt", true)]
    [DataRow("]", "[!]-]", false)]
    [DataRow("a", "[!]-]", true)]
    [DataRow("XXX/\\", "*/\\\\", true)]
    [DataRow("foo", "foo", true)]
    [DataRow("@foo", "@foo", true)]
    [DataRow("foo", "@foo", false)]
    [DataRow("[ab]", "\\[ab]", true)]
    [DataRow("[ab]", "[[]ab]", true)]
    [DataRow("[ab]", "[[:]ab]", true)]
    [DataRow("[ab]", "[[:digit]ab]", true)]
    [DataRow("[ab]", "[\\[:]ab]", true)]
    [DataRow("?a?b", "\\??\\?b", true)]
    [DataRow("abc", "\\a\\b\\c", true)]
    [DataRow("foo/bar/baz/to", "**/t[o]", true)]
    public void IsMatch_GitWildmatch_VariousAdditional(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Character class tests -----
    [TestMethod]
    [DataRow("a1B", "[[:alpha:]][[:digit:]][[:upper:]]", true)]
    [DataRow("A", "[[:digit:][:upper:][:space:]]", true)]
    [DataRow("1", "[[:digit:][:upper:][:space:]]", true)]
    [DataRow("1", "[[:digit:][:upper:][:spaci:]]", false)]
    [DataRow(" ", "[[:digit:][:upper:][:space:]]", true)]
    [DataRow(".", "[[:digit:][:upper:][:space:]]", false)]
    [DataRow(".", "[[:digit:][:punct:][:space:]]", true)]
    [DataRow("5", "[[:xdigit:]]", true)]
    [DataRow("f", "[[:xdigit:]]", true)]
    [DataRow("D", "[[:xdigit:]]", true)]
    [DataRow(
        "_",
        "[[:alnum:][:alpha:][:blank:][:cntrl:][:digit:][:graph:][:lower:][:print:][:punct:][:space:][:upper:][:xdigit:]]",
        true)]
    [DataRow("5", "[a-c[:digit:]x-z]", true)]
    [DataRow("b", "[a-c[:digit:]x-z]", true)]
    [DataRow("y", "[a-c[:digit:]x-z]", true)]
    [DataRow("q", "[a-c[:digit:]x-z]", false)]
    public void IsMatch_GitWildmatch_CharacterClasses(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Malformed / misc bracket-class rows -----
    [TestMethod]
    [DataRow("]", "[\\\\-^]", true)]
    [DataRow("[", "[\\\\-^]", false)]
    [DataRow("ab", "a[]b", false)]
    [DataRow("ab", "[!", false)]
    [DataRow("ab", "[-", false)]
    [DataRow("-", "[-]", true)]
    [DataRow("-", "[a-", false)]
    [DataRow("-", "[!a-", false)]
    [DataRow("-", "[--A]", true)]
    [DataRow("5", "[--A]", true)]
    [DataRow(" ", "[ --]", true)]
    [DataRow("$", "[ --]", true)]
    [DataRow("-", "[ --]", true)]
    [DataRow("0", "[ --]", false)]
    [DataRow("-", "[---]", true)]
    [DataRow("-", "[------]", true)]
    [DataRow("j", "[a-e-n]", false)]
    [DataRow("-", "[a-e-n]", true)]
    [DataRow("a", "[!------]", true)]
    [DataRow("[", "[]-a]", false)]
    [DataRow("^", "[]-a]", true)]
    [DataRow("^", "[!]-a]", false)]
    [DataRow("[", "[!]-a]", true)]
    [DataRow("^", "[a^bc]", true)]
    [DataRow("-b]", "[a-]b]", true)]
    [DataRow("\\", "[\\\\]", true)]
    [DataRow("\\", "[!\\\\]", false)]
    [DataRow("G", "[A-\\\\]", true)]
    [DataRow("aaabbb", "b*a", false)]
    [DataRow("aabcaa", "*ba*", false)]
    [DataRow(",", "[,]", true)]
    [DataRow(",", "[\\\\,]", true)]
    [DataRow("\\", "[\\\\,]", true)]
    [DataRow("-", "[,-.]", true)]
    [DataRow("+", "[,-.]", false)]
    [DataRow("-.]", "[,-.]", false)]
    public void IsMatch_GitWildmatch_MalformedAndMisc(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Test recursion (deep paths and X-style font names) -----
    [TestMethod]
    [DataRow(
        "-adobe-courier-bold-o-normal--12-120-75-75-m-70-iso8859-1",
        "-*-*-*-*-*-*-12-*-*-*-m-*-*-*",
        true)]
    [DataRow(
        "-adobe-courier-bold-o-normal--12-120-75-75-X-70-iso8859-1",
        "-*-*-*-*-*-*-12-*-*-*-m-*-*-*",
        false)]
    [DataRow(
        "-adobe-courier-bold-o-normal--12-120-75-75-/-70-iso8859-1",
        "-*-*-*-*-*-*-12-*-*-*-m-*-*-*",
        false)]
    [DataRow(
        "XXX/adobe/courier/bold/o/normal//12/120/75/75/m/70/iso8859/1",
        "XXX/*/*/*/*/*/*/12/*/*/*/m/*/*/*",
        true)]
    [DataRow(
        "XXX/adobe/courier/bold/o/normal//12/120/75/75/X/70/iso8859/1",
        "XXX/*/*/*/*/*/*/12/*/*/*/m/*/*/*",
        false)]
    [DataRow(
        "abcd/abcdefg/abcdefghijk/abcdefghijklmnop.txt",
        "**/*a*b*g*n*t",
        true)]
    [DataRow(
        "abcd/abcdefg/abcdefghijk/abcdefghijklmnop.txtz",
        "**/*a*b*g*n*t",
        false)]
    [DataRow("foo", "*/*/*", false)]
    [DataRow("foo/bar", "*/*/*", false)]
    [DataRow("foo/bba/arr", "*/*/*", true)]
    [DataRow("foo/bb/aa/rr", "*/*/*", false)]
    [DataRow("foo/bb/aa/rr", "**/**/**", true)]
    [DataRow("abcXdefXghi", "*X*i", true)]
    [DataRow("ab/cXd/efXg/hi", "*X*i", false)]
    [DataRow("ab/cXd/efXg/hi", "*/*X*/*/*i", true)]
    [DataRow("ab/cXd/efXg/hi", "**/*X*/**/*i", true)]
    public void IsMatch_GitWildmatch_Recursion(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Extra pathmatch rows (wildmatch column = WM_PATHNAME on) -----
    [TestMethod]
    [DataRow("foo", "fo", false)]
    [DataRow("foo/bar", "foo/bar", true)]
    [DataRow("foo/bar", "foo/*", true)]
    [DataRow("foo/bba/arr", "foo/*", false)]
    [DataRow("foo/bba/arr", "foo/**", true)]
    [DataRow("foo/bba/arr", "foo*", false)]
    [DataRow("foo/bba/arr", "foo/*arr", false)]
    [DataRow("foo/bba/arr", "foo/**arr", false)]
    [DataRow("foo/bba/arr", "foo/*z", false)]
    [DataRow("foo/bba/arr", "foo/**z", false)]
    [DataRow("foo/bar", "foo?bar", false)]
    [DataRow("foo/bar", "foo[/]bar", false)]
    [DataRow("foo/bar", "foo[^a-z]bar", false)]
    [DataRow("ab/cXd/efXg/hi", "*Xg*i", false)]
    public void IsMatch_GitWildmatch_ExtraPathmatch(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // Helper: compile under PosixPath with globstar + leading-dot-matches
    // (wildmatch with WM_PATHNAME, without WM_PERIOD).
    private static GlobSpecification Wm(string pattern) =>
        GlobSpecification.Compile(
            pattern,
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar | GlobOptions.MatchLeadingDot);
}
