// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Pattern-level scenarios ported from the upstream <c>dotnet/msbuild</c>
///  <c>FileMatcher</c> test suite.
/// </summary>
/// <remarks>
///  <para>
///   Sources:
///  </para>
///  <para>
///   - <see href="https://github.com/dotnet/msbuild/blob/main/src/Framework.UnitTests/FileMatcher_Tests.cs"><c>dotnet/msbuild/src/Framework.UnitTests/FileMatcher_Tests.cs</c></see>
///     (<c>WildcardMatching</c> and <c>GetFilesPatternMatching</c>).<br/>
///   - <see href="https://github.com/dotnet/msbuild/blob/main/src/Build.UnitTests/Globbing/MSBuildGlob_Tests.cs"><c>dotnet/msbuild/src/Build.UnitTests/Globbing/MSBuildGlob_Tests.cs</c></see>
///     (the small set of pattern-level rows; the rest exercise the
///     <c>MSBuildGlob</c> root / fixed / wildcard structural pieces that touki
///     does not model).
///  </para>
///  <para>
///   Licensed under the MIT license to the .NET Foundation.
///  </para>
///  <para>
///   Each row asserts <see cref="GlobSpecification.IsMatch"/> on
///   <see cref="GlobDialect.MSBuild"/>. Upstream tests on
///   <c>FileMatcher.IsMatch</c> validate that the match is case-insensitive
///   on both the pattern and the input (MSBuild defaults to
///   <see cref="GlobOptions.IgnoreCase"/>), so each row is exercised four
///   times: as-is, uppercased input, uppercased pattern, and both upper.
///  </para>
/// </remarks>
public class PortedTests_MSBuild
{
    // From FileMatcher_Tests.WildcardMatching.
    // Tuple order in the upstream source is (input, pattern, expected).
    [Test]
    // No wildcards
    [Arguments("a", "a", true)]
    [Arguments("a", "", false)]
    [Arguments("", "a", false)]

    // Non-ASCII characters
    [Arguments("šđčćž", "šđčćž", true)]

    // '*' wildcard
    [Arguments("abc", "*bc", true)]
    [Arguments("abc", "a*c", true)]
    [Arguments("abc", "ab*", true)]
    [Arguments("ab", "*ab", true)]
    [Arguments("ab", "a*b", true)]
    [Arguments("ab", "ab*", true)]
    [Arguments("aba", "ab*ba", false)]
    [Arguments("", "*", true)]

    // '?' wildcard
    [Arguments("abc", "?bc", true)]
    [Arguments("abc", "a?c", true)]
    [Arguments("abc", "ab?", true)]
    [Arguments("ab", "?ab", false)]
    [Arguments("ab", "a?b", false)]
    [Arguments("ab", "ab?", false)]
    [Arguments("", "?", false)]

    // Mixed '*' / '?'
    [Arguments("a", "*?", true)]
    [Arguments("a", "?*", true)]
    [Arguments("ab", "*?", true)]
    [Arguments("ab", "?*", true)]
    [Arguments("abc", "*?", true)]
    [Arguments("abc", "?*", true)]

    // Multiple mixed wildcards
    [Arguments("a", "??", false)]
    [Arguments("ab", "?*?", true)]
    [Arguments("ab", "*?*?*", true)]
    [Arguments("abc", "?**?*?", true)]
    [Arguments("abc", "?**?*c?", false)]
    [Arguments("abcd", "?b*??", true)]
    [Arguments("abcd", "?a*??", false)]
    [Arguments("abcd", "?**?c?", true)]
    [Arguments("abcd", "?**?d?", false)]
    [Arguments("abcde", "?*b*?*d*?", true)]

    // '?' literal in the input string
    [Arguments("?", "?", true)]
    [Arguments("?a", "?a", true)]
    [Arguments("a?", "a?", true)]
    [Arguments("a?b", "a?", false)]
    [Arguments("a?ab", "a?aab", false)]
    [Arguments("aa?bbbc?d", "aa?bbc?dd", false)]

    // '*' literal in the input string
    [Arguments("*", "*", true)]
    [Arguments("*a", "*a", true)]
    [Arguments("a*", "a*", true)]
    [Arguments("a*b", "a*", true)]
    [Arguments("a*ab", "a*aab", false)]
    [Arguments("a*abab", "a*b", true)]
    [Arguments("aa*bbbc*d", "aa*bbc*dd", false)]
    [Arguments("aa*bbbc*d", "a*bbc*d", true)]
    public void IsMatch_WildcardMatching(string input, string pattern, bool expected)
    {
        GlobSpecification matcher = GlobSpecification.Compile(pattern, GlobDialect.MSBuild);

        // Upstream FileMatcher.IsMatch validates four casings; MSBuild is
        // case-insensitive by default.
        matcher.IsMatch(input).Should().Be(expected, "pattern '{0}' vs input '{1}'", pattern, input);
        matcher.IsMatch(input.ToUpperInvariant()).Should().Be(
            expected, "pattern '{0}' vs uppercased input '{1}'", pattern, input);

        GlobSpecification upperPatternMatcher = GlobSpecification.Compile(
            pattern.ToUpperInvariant(), GlobDialect.MSBuild);
        upperPatternMatcher.IsMatch(input).Should().Be(
            expected, "uppercased pattern '{0}' vs input '{1}'", pattern, input);
        upperPatternMatcher.IsMatch(input.ToUpperInvariant()).Should().Be(
            expected, "uppercased pattern and input '{0}'", pattern);
    }

    // From FileMatcher_Tests.GetFilesPatternMatching. Upstream asserts a match
    // count over a fixed file set; this port asserts the per-file verdict so
    // each row stays pattern-level.
    //
    // Files in the upstream set:
    //   Foo.cs, Foo2.cs, file.txt, file1.txt, file1.txtother, fie1.txt,
    //   fire1.txt, file.bak.txt.
    [Test]
    // *.txt - 5 matches
    [Arguments("*.txt", "file.txt", true)]
    [Arguments("*.txt", "file1.txt", true)]
    [Arguments("*.txt", "fie1.txt", true)]
    [Arguments("*.txt", "fire1.txt", true)]
    [Arguments("*.txt", "file.bak.txt", true)]
    [Arguments("*.txt", "Foo.cs", false)]
    [Arguments("*.txt", "Foo2.cs", false)]
    [Arguments("*.txt", "file1.txtother", false)]

    // ???.cs - 1 match (exactly 3 chars before the .cs)
    [Arguments("???.cs", "Foo.cs", true)]
    [Arguments("???.cs", "Foo2.cs", false)]

    // ????.cs - 1 match
    [Arguments("????.cs", "Foo2.cs", true)]
    [Arguments("????.cs", "Foo.cs", false)]

    // file?.txt - 1 match
    [Arguments("file?.txt", "file1.txt", true)]
    [Arguments("file?.txt", "file.txt", false)]
    [Arguments("file?.txt", "file1.txtother", false)]

    // fi?e?.txt - 2 matches
    [Arguments("fi?e?.txt", "file1.txt", true)]
    [Arguments("fi?e?.txt", "fire1.txt", true)]
    [Arguments("fi?e?.txt", "file.txt", false)]

    // ???.* - 1 match
    [Arguments("???.*", "Foo.cs", true)]
    [Arguments("???.*", "Foo2.cs", false)]

    // ????.* - 4 matches
    [Arguments("????.*", "Foo2.cs", true)]
    [Arguments("????.*", "fie1.txt", true)]
    [Arguments("????.*", "file.txt", true)]
    [Arguments("????.*", "Foo.cs", false)]

    // *.??? - 5 matches
    [Arguments("*.???", "Foo.cs", false)]  // .cs is two chars, not three
    [Arguments("*.???", "file.txt", true)]
    [Arguments("*.???", "file.bak.txt", true)]

    // f??e1.txt - 2 matches
    [Arguments("f??e1.txt", "file1.txt", true)]
    [Arguments("f??e1.txt", "fire1.txt", true)]
    [Arguments("f??e1.txt", "fie1.txt", false)]

    // file.*.txt - 1 match
    [Arguments("file.*.txt", "file.bak.txt", true)]
    [Arguments("file.*.txt", "file.txt", false)]
    public void IsMatch_GetFilesPatternMatching(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.MSBuild).IsMatch(input).Should().Be(expected);

    // From MSBuildGlob_Tests.GlobMatchingShouldWorkWithLiteralStrings, etc.
    [Test]
    [Arguments("abc", "abc", true)]
    [Arguments("ab?c*", "acd", false)]
    [Arguments("%42", "B", false)]      // %-escapes not unescaped at the matcher layer
    [Arguments("%42", "%42", true)]
    public void IsMatch_MSBuildGlob_PatternLevelRows(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.MSBuild).IsMatch(input).Should().Be(expected);

    // From MSBuildGlob_Tests.GlobShouldMatchEmptyArgWhenGlob...
    [Test]
    public void IsMatch_EmptyPattern_MatchesEmptyInput() =>
        GlobSpecification.Compile(string.Empty, GlobDialect.MSBuild).IsMatch(string.Empty).Should().BeTrue();

    [Test]
    public void IsMatch_StarPattern_MatchesEmptyInput() =>
        GlobSpecification.Compile("*", GlobDialect.MSBuild).IsMatch(string.Empty).Should().BeTrue();

    [Test]
    public void IsMatch_StarAStarPattern_DoesNotMatchEmptyInput() =>
        GlobSpecification.Compile("*a*", GlobDialect.MSBuild).IsMatch(string.Empty).Should().BeFalse();
}
