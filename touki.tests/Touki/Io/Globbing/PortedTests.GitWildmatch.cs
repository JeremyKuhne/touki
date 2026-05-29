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
    [Theory]
    [InlineData("foo", "foo", true)]
    [InlineData("foo", "bar", false)]
    [InlineData("", "", true)]
    [InlineData("foo", "???", true)]
    [InlineData("foo", "??", false)]
    [InlineData("foo", "*", true)]
    [InlineData("foo", "f*", true)]
    [InlineData("foo", "*f", false)]
    [InlineData("foo", "*foo*", true)]
    [InlineData("foobar", "*ob*a*r*", true)]
    [InlineData("aaaaaaabababab", "*ab", true)]
    [InlineData("foo*", "foo\\*", true)]
    [InlineData("foobar", "foo\\*bar", false)]
    [InlineData("f\\oo", "f\\\\oo", true)]
    [InlineData("ball", "*[al]?", true)]
    [InlineData("ten", "[ten]", false)]
    [InlineData("ten", "**[!te]", true)]
    [InlineData("ten", "**[!ten]", false)]
    [InlineData("ten", "t[a-g]n", true)]
    [InlineData("ten", "t[!a-g]n", false)]
    [InlineData("ton", "t[!a-g]n", true)]
    [InlineData("ton", "t[^a-g]n", true)]
    [InlineData("a]b", "a[]]b", true)]
    [InlineData("a-b", "a[]-]b", true)]
    [InlineData("a]b", "a[]-]b", true)]
    [InlineData("aab", "a[]-]b", false)]
    [InlineData("aab", "a[]a-]b", true)]
    [InlineData("]", "]", true)]
    public void IsMatch_GitWildmatch_BasicFeatures(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Extended slash-matching features (WM_PATHNAME on) -----
    [Theory]
    [InlineData("foo/baz/bar", "foo*bar", false)]
    [InlineData("foo/baz/bar", "foo**bar", false)]
    [InlineData("foobazbar", "foo**bar", true)]
    [InlineData("foo/baz/bar", "foo/**/bar", true)]
    [InlineData("foo/baz/bar", "foo/**/**/bar", true)]
    [InlineData("foo/b/a/z/bar", "foo/**/bar", true)]
    [InlineData("foo/b/a/z/bar", "foo/**/**/bar", true)]
    [InlineData("foo/bar", "foo/**/bar", true)]
    [InlineData("foo/bar", "foo/**/**/bar", true)]
    [InlineData("foo/bar", "foo?bar", false)]
    [InlineData("foo/bar", "foo[/]bar", false)]
    [InlineData("foo/bar", "foo[^a-z]bar", false)]
    [InlineData("foo/bar", "f[^eiu][^eiu][^eiu][^eiu][^eiu]r", false)]
    [InlineData("foo-bar", "f[^eiu][^eiu][^eiu][^eiu][^eiu]r", true)]
    [InlineData("foo", "**/foo", true)]
    [InlineData("XXX/foo", "**/foo", true)]
    [InlineData("bar/baz/foo", "**/foo", true)]
    [InlineData("bar/baz/foo", "*/foo", false)]
    [InlineData("foo/bar/baz", "**/bar*", false)]
    [InlineData("deep/foo/bar/baz", "**/bar/*", true)]
    [InlineData("deep/foo/bar", "**/bar/*", false)]
    [InlineData("foo/bar/baz", "**/bar**", false)]
    [InlineData("foo/bar/baz/x", "*/bar/**", true)]
    [InlineData("deep/foo/bar/baz/x", "*/bar/**", false)]
    [InlineData("deep/foo/bar/baz/x", "**/bar/*/*", true)]
    public void IsMatch_GitWildmatch_ExtendedSlash(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Various additional tests -----
    [Theory]
    [InlineData("acrt", "a[c-c]st", false)]
    [InlineData("acrt", "a[c-c]rt", true)]
    [InlineData("]", "[!]-]", false)]
    [InlineData("a", "[!]-]", true)]
    [InlineData("XXX/\\", "*/\\\\", true)]
    [InlineData("foo", "foo", true)]
    [InlineData("@foo", "@foo", true)]
    [InlineData("foo", "@foo", false)]
    [InlineData("[ab]", "\\[ab]", true)]
    [InlineData("[ab]", "[[]ab]", true)]
    [InlineData("[ab]", "[[:]ab]", true)]
    [InlineData("[ab]", "[[:digit]ab]", true)]
    [InlineData("[ab]", "[\\[:]ab]", true)]
    [InlineData("?a?b", "\\??\\?b", true)]
    [InlineData("abc", "\\a\\b\\c", true)]
    [InlineData("foo/bar/baz/to", "**/t[o]", true)]
    public void IsMatch_GitWildmatch_VariousAdditional(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Character class tests -----
    [Theory]
    [InlineData("a1B", "[[:alpha:]][[:digit:]][[:upper:]]", true)]
    [InlineData("A", "[[:digit:][:upper:][:space:]]", true)]
    [InlineData("1", "[[:digit:][:upper:][:space:]]", true)]
    [InlineData("1", "[[:digit:][:upper:][:spaci:]]", false)]
    [InlineData(" ", "[[:digit:][:upper:][:space:]]", true)]
    [InlineData(".", "[[:digit:][:upper:][:space:]]", false)]
    [InlineData(".", "[[:digit:][:punct:][:space:]]", true)]
    [InlineData("5", "[[:xdigit:]]", true)]
    [InlineData("f", "[[:xdigit:]]", true)]
    [InlineData("D", "[[:xdigit:]]", true)]
    [InlineData(
        "_",
        "[[:alnum:][:alpha:][:blank:][:cntrl:][:digit:][:graph:][:lower:][:print:][:punct:][:space:][:upper:][:xdigit:]]",
        true)]
    [InlineData("5", "[a-c[:digit:]x-z]", true)]
    [InlineData("b", "[a-c[:digit:]x-z]", true)]
    [InlineData("y", "[a-c[:digit:]x-z]", true)]
    [InlineData("q", "[a-c[:digit:]x-z]", false)]
    public void IsMatch_GitWildmatch_CharacterClasses(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Malformed / misc bracket-class rows -----
    [Theory]
    [InlineData("]", "[\\\\-^]", true)]
    [InlineData("[", "[\\\\-^]", false)]
    [InlineData("ab", "a[]b", false)]
    [InlineData("ab", "[!", false)]
    [InlineData("ab", "[-", false)]
    [InlineData("-", "[-]", true)]
    [InlineData("-", "[a-", false)]
    [InlineData("-", "[!a-", false)]
    [InlineData("-", "[--A]", true)]
    [InlineData("5", "[--A]", true)]
    [InlineData(" ", "[ --]", true)]
    [InlineData("$", "[ --]", true)]
    [InlineData("-", "[ --]", true)]
    [InlineData("0", "[ --]", false)]
    [InlineData("-", "[---]", true)]
    [InlineData("-", "[------]", true)]
    [InlineData("j", "[a-e-n]", false)]
    [InlineData("-", "[a-e-n]", true)]
    [InlineData("a", "[!------]", true)]
    [InlineData("[", "[]-a]", false)]
    [InlineData("^", "[]-a]", true)]
    [InlineData("^", "[!]-a]", false)]
    [InlineData("[", "[!]-a]", true)]
    [InlineData("^", "[a^bc]", true)]
    [InlineData("-b]", "[a-]b]", true)]
    [InlineData("\\", "[\\\\]", true)]
    [InlineData("\\", "[!\\\\]", false)]
    [InlineData("G", "[A-\\\\]", true)]
    [InlineData("aaabbb", "b*a", false)]
    [InlineData("aabcaa", "*ba*", false)]
    [InlineData(",", "[,]", true)]
    [InlineData(",", "[\\\\,]", true)]
    [InlineData("\\", "[\\\\,]", true)]
    [InlineData("-", "[,-.]", true)]
    [InlineData("+", "[,-.]", false)]
    [InlineData("-.]", "[,-.]", false)]
    public void IsMatch_GitWildmatch_MalformedAndMisc(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Test recursion (deep paths and X-style font names) -----
    [Theory]
    [InlineData(
        "-adobe-courier-bold-o-normal--12-120-75-75-m-70-iso8859-1",
        "-*-*-*-*-*-*-12-*-*-*-m-*-*-*",
        true)]
    [InlineData(
        "-adobe-courier-bold-o-normal--12-120-75-75-X-70-iso8859-1",
        "-*-*-*-*-*-*-12-*-*-*-m-*-*-*",
        false)]
    [InlineData(
        "-adobe-courier-bold-o-normal--12-120-75-75-/-70-iso8859-1",
        "-*-*-*-*-*-*-12-*-*-*-m-*-*-*",
        false)]
    [InlineData(
        "XXX/adobe/courier/bold/o/normal//12/120/75/75/m/70/iso8859/1",
        "XXX/*/*/*/*/*/*/12/*/*/*/m/*/*/*",
        true)]
    [InlineData(
        "XXX/adobe/courier/bold/o/normal//12/120/75/75/X/70/iso8859/1",
        "XXX/*/*/*/*/*/*/12/*/*/*/m/*/*/*",
        false)]
    [InlineData(
        "abcd/abcdefg/abcdefghijk/abcdefghijklmnop.txt",
        "**/*a*b*g*n*t",
        true)]
    [InlineData(
        "abcd/abcdefg/abcdefghijk/abcdefghijklmnop.txtz",
        "**/*a*b*g*n*t",
        false)]
    [InlineData("foo", "*/*/*", false)]
    [InlineData("foo/bar", "*/*/*", false)]
    [InlineData("foo/bba/arr", "*/*/*", true)]
    [InlineData("foo/bb/aa/rr", "*/*/*", false)]
    [InlineData("foo/bb/aa/rr", "**/**/**", true)]
    [InlineData("abcXdefXghi", "*X*i", true)]
    [InlineData("ab/cXd/efXg/hi", "*X*i", false)]
    [InlineData("ab/cXd/efXg/hi", "*/*X*/*/*i", true)]
    [InlineData("ab/cXd/efXg/hi", "**/*X*/**/*i", true)]
    public void IsMatch_GitWildmatch_Recursion(string input, string pattern, bool expected) =>
        Wm(pattern).IsMatch(input).Should().Be(expected);

    // ----- Extra pathmatch rows (wildmatch column = WM_PATHNAME on) -----
    [Theory]
    [InlineData("foo", "fo", false)]
    [InlineData("foo/bar", "foo/bar", true)]
    [InlineData("foo/bar", "foo/*", true)]
    [InlineData("foo/bba/arr", "foo/*", false)]
    [InlineData("foo/bba/arr", "foo/**", true)]
    [InlineData("foo/bba/arr", "foo*", false)]
    [InlineData("foo/bba/arr", "foo/*arr", false)]
    [InlineData("foo/bba/arr", "foo/**arr", false)]
    [InlineData("foo/bba/arr", "foo/*z", false)]
    [InlineData("foo/bba/arr", "foo/**z", false)]
    [InlineData("foo/bar", "foo?bar", false)]
    [InlineData("foo/bar", "foo[/]bar", false)]
    [InlineData("foo/bar", "foo[^a-z]bar", false)]
    [InlineData("ab/cXd/efXg/hi", "*Xg*i", false)]
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
