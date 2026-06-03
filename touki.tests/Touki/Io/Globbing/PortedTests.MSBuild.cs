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
[TestClass]
public class PortedTests_MSBuild
{
    // From FileMatcher_Tests.WildcardMatching.
    // Tuple order in the upstream source is (input, pattern, expected).
    [TestMethod]
    // No wildcards
    [DataRow("a", "a", true)]
    [DataRow("a", "", false)]
    [DataRow("", "a", false)]

    // Non-ASCII characters
    [DataRow("šđčćž", "šđčćž", true)]

    // '*' wildcard
    [DataRow("abc", "*bc", true)]
    [DataRow("abc", "a*c", true)]
    [DataRow("abc", "ab*", true)]
    [DataRow("ab", "*ab", true)]
    [DataRow("ab", "a*b", true)]
    [DataRow("ab", "ab*", true)]
    [DataRow("aba", "ab*ba", false)]
    [DataRow("", "*", true)]

    // '?' wildcard
    [DataRow("abc", "?bc", true)]
    [DataRow("abc", "a?c", true)]
    [DataRow("abc", "ab?", true)]
    [DataRow("ab", "?ab", false)]
    [DataRow("ab", "a?b", false)]
    [DataRow("ab", "ab?", false)]
    [DataRow("", "?", false)]

    // Mixed '*' / '?'
    [DataRow("a", "*?", true)]
    [DataRow("a", "?*", true)]
    [DataRow("ab", "*?", true)]
    [DataRow("ab", "?*", true)]
    [DataRow("abc", "*?", true)]
    [DataRow("abc", "?*", true)]

    // Multiple mixed wildcards
    [DataRow("a", "??", false)]
    [DataRow("ab", "?*?", true)]
    [DataRow("ab", "*?*?*", true)]
    [DataRow("abc", "?**?*?", true)]
    [DataRow("abc", "?**?*c?", false)]
    [DataRow("abcd", "?b*??", true)]
    [DataRow("abcd", "?a*??", false)]
    [DataRow("abcd", "?**?c?", true)]
    [DataRow("abcd", "?**?d?", false)]
    [DataRow("abcde", "?*b*?*d*?", true)]

    // '?' literal in the input string
    [DataRow("?", "?", true)]
    [DataRow("?a", "?a", true)]
    [DataRow("a?", "a?", true)]
    [DataRow("a?b", "a?", false)]
    [DataRow("a?ab", "a?aab", false)]
    [DataRow("aa?bbbc?d", "aa?bbc?dd", false)]

    // '*' literal in the input string
    [DataRow("*", "*", true)]
    [DataRow("*a", "*a", true)]
    [DataRow("a*", "a*", true)]
    [DataRow("a*b", "a*", true)]
    [DataRow("a*ab", "a*aab", false)]
    [DataRow("a*abab", "a*b", true)]
    [DataRow("aa*bbbc*d", "aa*bbc*dd", false)]
    [DataRow("aa*bbbc*d", "a*bbc*d", true)]
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
    [TestMethod]
    // *.txt - 5 matches
    [DataRow("*.txt", "file.txt", true)]
    [DataRow("*.txt", "file1.txt", true)]
    [DataRow("*.txt", "fie1.txt", true)]
    [DataRow("*.txt", "fire1.txt", true)]
    [DataRow("*.txt", "file.bak.txt", true)]
    [DataRow("*.txt", "Foo.cs", false)]
    [DataRow("*.txt", "Foo2.cs", false)]
    [DataRow("*.txt", "file1.txtother", false)]

    // ???.cs - 1 match (exactly 3 chars before the .cs)
    [DataRow("???.cs", "Foo.cs", true)]
    [DataRow("???.cs", "Foo2.cs", false)]

    // ????.cs - 1 match
    [DataRow("????.cs", "Foo2.cs", true)]
    [DataRow("????.cs", "Foo.cs", false)]

    // file?.txt - 1 match
    [DataRow("file?.txt", "file1.txt", true)]
    [DataRow("file?.txt", "file.txt", false)]
    [DataRow("file?.txt", "file1.txtother", false)]

    // fi?e?.txt - 2 matches
    [DataRow("fi?e?.txt", "file1.txt", true)]
    [DataRow("fi?e?.txt", "fire1.txt", true)]
    [DataRow("fi?e?.txt", "file.txt", false)]

    // ???.* - 1 match
    [DataRow("???.*", "Foo.cs", true)]
    [DataRow("???.*", "Foo2.cs", false)]

    // ????.* - 4 matches
    [DataRow("????.*", "Foo2.cs", true)]
    [DataRow("????.*", "fie1.txt", true)]
    [DataRow("????.*", "file.txt", true)]
    [DataRow("????.*", "Foo.cs", false)]

    // *.??? - 5 matches
    [DataRow("*.???", "Foo.cs", false)]  // .cs is two chars, not three
    [DataRow("*.???", "file.txt", true)]
    [DataRow("*.???", "file.bak.txt", true)]

    // f??e1.txt - 2 matches
    [DataRow("f??e1.txt", "file1.txt", true)]
    [DataRow("f??e1.txt", "fire1.txt", true)]
    [DataRow("f??e1.txt", "fie1.txt", false)]

    // file.*.txt - 1 match
    [DataRow("file.*.txt", "file.bak.txt", true)]
    [DataRow("file.*.txt", "file.txt", false)]
    public void IsMatch_GetFilesPatternMatching(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.MSBuild).IsMatch(input).Should().Be(expected);

    // From MSBuildGlob_Tests.GlobMatchingShouldWorkWithLiteralStrings, etc.
    [TestMethod]
    [DataRow("abc", "abc", true)]
    [DataRow("ab?c*", "acd", false)]
    [DataRow("%42", "B", false)]      // %-escapes not unescaped at the matcher layer
    [DataRow("%42", "%42", true)]
    public void IsMatch_MSBuildGlob_PatternLevelRows(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.MSBuild).IsMatch(input).Should().Be(expected);

    // From MSBuildGlob_Tests.GlobShouldMatchEmptyArgWhenGlob...
    [TestMethod]
    public void IsMatch_EmptyPattern_MatchesEmptyInput() =>
        GlobSpecification.Compile(string.Empty, GlobDialect.MSBuild).IsMatch(string.Empty).Should().BeTrue();

    [TestMethod]
    public void IsMatch_StarPattern_MatchesEmptyInput() =>
        GlobSpecification.Compile("*", GlobDialect.MSBuild).IsMatch(string.Empty).Should().BeTrue();

    [TestMethod]
    public void IsMatch_StarAStarPattern_DoesNotMatchEmptyInput() =>
        GlobSpecification.Compile("*a*", GlobDialect.MSBuild).IsMatch(string.Empty).Should().BeFalse();
}
