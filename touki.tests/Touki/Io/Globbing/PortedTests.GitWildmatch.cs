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
public class PortedTests_GitWildmatch
{
    // ----- Basic wildmatch features (column 1 from t3070) -----
    [Test]
    [Arguments("foo", "foo", true)]
    [Arguments("foo", "bar", false)]
    [Arguments("", "", true)]
    [Arguments("foo", "???", true)]
    [Arguments("foo", "??", false)]
    [Arguments("foo", "*", true)]
    [Arguments("foo", "f*", true)]
    [Arguments("foo", "*f", false)]
    [Arguments("foo", "*foo*", true)]
    [Arguments("foobar", "*ob*a*r*", true)]
    [Arguments("aaaaaaabababab", "*ab", true)]
    [Arguments("foo*", "foo\\*", true)]
    [Arguments("foobar", "foo\\*bar", false)]
    [Arguments("f\\oo", "f\\\\oo", true)]
    [Arguments("ball", "*[al]?", true)]
    [Arguments("ten", "[ten]", false)]
    [Arguments("ten", "**[!te]", true)]
    [Arguments("ten", "**[!ten]", false)]
    [Arguments("ten", "t[a-g]n", true)]
    [Arguments("ten", "t[!a-g]n", false)]
    [Arguments("ton", "t[!a-g]n", true)]
    [Arguments("ton", "t[^a-g]n", true)]
    [Arguments("a]b", "a[]]b", true)]
    [Arguments("a-b", "a[]-]b", true)]
    [Arguments("a]b", "a[]-]b", true)]
    [Arguments("aab", "a[]-]b", false)]
    [Arguments("aab", "a[]a-]b", true)]
    [Arguments("]", "]", true)]
    public void IsMatch_GitWildmatch_BasicFeatures(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Extended slash-matching features (WM_PATHNAME on) -----
    [Test]
    [Arguments("foo/baz/bar", "foo*bar", false)]
    [Arguments("foo/baz/bar", "foo**bar", false)]
    [Arguments("foobazbar", "foo**bar", true)]
    [Arguments("foo/baz/bar", "foo/**/bar", true)]
    [Arguments("foo/baz/bar", "foo/**/**/bar", true)]
    [Arguments("foo/b/a/z/bar", "foo/**/bar", true)]
    [Arguments("foo/b/a/z/bar", "foo/**/**/bar", true)]
    [Arguments("foo/bar", "foo/**/bar", true)]
    [Arguments("foo/bar", "foo/**/**/bar", true)]
    [Arguments("foo/bar", "foo?bar", false)]
    [Arguments("foo/bar", "foo[/]bar", false)]
    [Arguments("foo/bar", "foo[^a-z]bar", false)]
    [Arguments("foo/bar", "f[^eiu][^eiu][^eiu][^eiu][^eiu]r", false)]
    [Arguments("foo-bar", "f[^eiu][^eiu][^eiu][^eiu][^eiu]r", true)]
    [Arguments("foo", "**/foo", true)]
    [Arguments("XXX/foo", "**/foo", true)]
    [Arguments("bar/baz/foo", "**/foo", true)]
    [Arguments("bar/baz/foo", "*/foo", false)]
    [Arguments("foo/bar/baz", "**/bar*", false)]
    [Arguments("deep/foo/bar/baz", "**/bar/*", true)]
    [Arguments("deep/foo/bar", "**/bar/*", false)]
    [Arguments("foo/bar/baz", "**/bar**", false)]
    [Arguments("foo/bar/baz/x", "*/bar/**", true)]
    [Arguments("deep/foo/bar/baz/x", "*/bar/**", false)]
    [Arguments("deep/foo/bar/baz/x", "**/bar/*/*", true)]
    public void IsMatch_GitWildmatch_ExtendedSlash(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Various additional tests -----
    [Test]
    [Arguments("acrt", "a[c-c]st", false)]
    [Arguments("acrt", "a[c-c]rt", true)]
    [Arguments("]", "[!]-]", false)]
    [Arguments("a", "[!]-]", true)]
    [Arguments("XXX/\\", "*/\\\\", true)]
    [Arguments("foo", "foo", true)]
    [Arguments("@foo", "@foo", true)]
    [Arguments("foo", "@foo", false)]
    [Arguments("[ab]", "\\[ab]", true)]
    [Arguments("[ab]", "[[]ab]", true)]
    [Arguments("[ab]", "[[:]ab]", true)]
    [Arguments("[ab]", "[[:digit]ab]", true)]
    [Arguments("[ab]", "[\\[:]ab]", true)]
    [Arguments("?a?b", "\\??\\?b", true)]
    [Arguments("abc", "\\a\\b\\c", true)]
    [Arguments("foo/bar/baz/to", "**/t[o]", true)]
    public void IsMatch_GitWildmatch_VariousAdditional(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Character class tests -----
    [Test]
    [Arguments("a1B", "[[:alpha:]][[:digit:]][[:upper:]]", true)]
    [Arguments("A", "[[:digit:][:upper:][:space:]]", true)]
    [Arguments("1", "[[:digit:][:upper:][:space:]]", true)]
    [Arguments("1", "[[:digit:][:upper:][:spaci:]]", false)]
    [Arguments(" ", "[[:digit:][:upper:][:space:]]", true)]
    [Arguments(".", "[[:digit:][:upper:][:space:]]", false)]
    [Arguments(".", "[[:digit:][:punct:][:space:]]", true)]
    [Arguments("5", "[[:xdigit:]]", true)]
    [Arguments("f", "[[:xdigit:]]", true)]
    [Arguments("D", "[[:xdigit:]]", true)]
    [Arguments(
        "_",
        "[[:alnum:][:alpha:][:blank:][:cntrl:][:digit:][:graph:][:lower:][:print:][:punct:][:space:][:upper:][:xdigit:]]",
        true)]
    [Arguments("5", "[a-c[:digit:]x-z]", true)]
    [Arguments("b", "[a-c[:digit:]x-z]", true)]
    [Arguments("y", "[a-c[:digit:]x-z]", true)]
    [Arguments("q", "[a-c[:digit:]x-z]", false)]
    public void IsMatch_GitWildmatch_CharacterClasses(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Malformed / misc bracket-class rows -----
    [Test]
    [Arguments("]", "[\\\\-^]", true)]
    [Arguments("[", "[\\\\-^]", false)]
    [Arguments("ab", "a[]b", false)]
    [Arguments("ab", "[!", false)]
    [Arguments("ab", "[-", false)]
    [Arguments("-", "[-]", true)]
    [Arguments("-", "[a-", false)]
    [Arguments("-", "[!a-", false)]
    [Arguments("-", "[--A]", true)]
    [Arguments("5", "[--A]", true)]
    [Arguments(" ", "[ --]", true)]
    [Arguments("$", "[ --]", true)]
    [Arguments("-", "[ --]", true)]
    [Arguments("0", "[ --]", false)]
    [Arguments("-", "[---]", true)]
    [Arguments("-", "[------]", true)]
    [Arguments("j", "[a-e-n]", false)]
    [Arguments("-", "[a-e-n]", true)]
    [Arguments("a", "[!------]", true)]
    [Arguments("[", "[]-a]", false)]
    [Arguments("^", "[]-a]", true)]
    [Arguments("^", "[!]-a]", false)]
    [Arguments("[", "[!]-a]", true)]
    [Arguments("^", "[a^bc]", true)]
    [Arguments("-b]", "[a-]b]", true)]
    [Arguments("\\", "[\\\\]", true)]
    [Arguments("\\", "[!\\\\]", false)]
    [Arguments("G", "[A-\\\\]", true)]
    [Arguments("aaabbb", "b*a", false)]
    [Arguments("aabcaa", "*ba*", false)]
    [Arguments(",", "[,]", true)]
    [Arguments(",", "[\\\\,]", true)]
    [Arguments("\\", "[\\\\,]", true)]
    [Arguments("-", "[,-.]", true)]
    [Arguments("+", "[,-.]", false)]
    [Arguments("-.]", "[,-.]", false)]
    public void IsMatch_GitWildmatch_MalformedAndMisc(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Test recursion (deep paths and X-style font names) -----
    [Test]
    [Arguments(
        "-adobe-courier-bold-o-normal--12-120-75-75-m-70-iso8859-1",
        "-*-*-*-*-*-*-12-*-*-*-m-*-*-*",
        true)]
    [Arguments(
        "-adobe-courier-bold-o-normal--12-120-75-75-X-70-iso8859-1",
        "-*-*-*-*-*-*-12-*-*-*-m-*-*-*",
        false)]
    [Arguments(
        "-adobe-courier-bold-o-normal--12-120-75-75-/-70-iso8859-1",
        "-*-*-*-*-*-*-12-*-*-*-m-*-*-*",
        false)]
    [Arguments(
        "XXX/adobe/courier/bold/o/normal//12/120/75/75/m/70/iso8859/1",
        "XXX/*/*/*/*/*/*/12/*/*/*/m/*/*/*",
        true)]
    [Arguments(
        "XXX/adobe/courier/bold/o/normal//12/120/75/75/X/70/iso8859/1",
        "XXX/*/*/*/*/*/*/12/*/*/*/m/*/*/*",
        false)]
    [Arguments(
        "abcd/abcdefg/abcdefghijk/abcdefghijklmnop.txt",
        "**/*a*b*g*n*t",
        true)]
    [Arguments(
        "abcd/abcdefg/abcdefghijk/abcdefghijklmnop.txtz",
        "**/*a*b*g*n*t",
        false)]
    [Arguments("foo", "*/*/*", false)]
    [Arguments("foo/bar", "*/*/*", false)]
    [Arguments("foo/bba/arr", "*/*/*", true)]
    [Arguments("foo/bb/aa/rr", "*/*/*", false)]
    [Arguments("foo/bb/aa/rr", "**/**/**", true)]
    [Arguments("abcXdefXghi", "*X*i", true)]
    [Arguments("ab/cXd/efXg/hi", "*X*i", false)]
    [Arguments("ab/cXd/efXg/hi", "*/*X*/*/*i", true)]
    [Arguments("ab/cXd/efXg/hi", "**/*X*/**/*i", true)]
    public void IsMatch_GitWildmatch_Recursion(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Extra pathmatch rows (wildmatch column = WM_PATHNAME on) -----
    [Test]
    [Arguments("foo", "fo", false)]
    [Arguments("foo/bar", "foo/bar", true)]
    [Arguments("foo/bar", "foo/*", true)]
    [Arguments("foo/bba/arr", "foo/*", false)]
    [Arguments("foo/bba/arr", "foo/**", true)]
    [Arguments("foo/bba/arr", "foo*", false)]
    [Arguments("foo/bba/arr", "foo/*arr", false)]
    [Arguments("foo/bba/arr", "foo/**arr", false)]
    [Arguments("foo/bba/arr", "foo/*z", false)]
    [Arguments("foo/bba/arr", "foo/**z", false)]
    [Arguments("foo/bar", "foo?bar", false)]
    [Arguments("foo/bar", "foo[/]bar", false)]
    [Arguments("foo/bar", "foo[^a-z]bar", false)]
    [Arguments("ab/cXd/efXg/hi", "*Xg*i", false)]
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
