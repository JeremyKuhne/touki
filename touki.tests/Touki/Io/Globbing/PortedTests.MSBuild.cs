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
    [Theory]
    // No wildcards
    [InlineData("a", "a", true)]
    [InlineData("a", "", false)]
    [InlineData("", "a", false)]

    // Non-ASCII characters
    [InlineData("šđčćž", "šđčćž", true)]

    // '*' wildcard
    [InlineData("abc", "*bc", true)]
    [InlineData("abc", "a*c", true)]
    [InlineData("abc", "ab*", true)]
    [InlineData("ab", "*ab", true)]
    [InlineData("ab", "a*b", true)]
    [InlineData("ab", "ab*", true)]
    [InlineData("aba", "ab*ba", false)]
    [InlineData("", "*", true)]

    // '?' wildcard
    [InlineData("abc", "?bc", true)]
    [InlineData("abc", "a?c", true)]
    [InlineData("abc", "ab?", true)]
    [InlineData("ab", "?ab", false)]
    [InlineData("ab", "a?b", false)]
    [InlineData("ab", "ab?", false)]
    [InlineData("", "?", false)]

    // Mixed '*' / '?'
    [InlineData("a", "*?", true)]
    [InlineData("a", "?*", true)]
    [InlineData("ab", "*?", true)]
    [InlineData("ab", "?*", true)]
    [InlineData("abc", "*?", true)]
    [InlineData("abc", "?*", true)]

    // Multiple mixed wildcards
    [InlineData("a", "??", false)]
    [InlineData("ab", "?*?", true)]
    [InlineData("ab", "*?*?*", true)]
    [InlineData("abc", "?**?*?", true)]
    [InlineData("abc", "?**?*c?", false)]
    [InlineData("abcd", "?b*??", true)]
    [InlineData("abcd", "?a*??", false)]
    [InlineData("abcd", "?**?c?", true)]
    [InlineData("abcd", "?**?d?", false)]
    [InlineData("abcde", "?*b*?*d*?", true)]

    // '?' literal in the input string
    [InlineData("?", "?", true)]
    [InlineData("?a", "?a", true)]
    [InlineData("a?", "a?", true)]
    [InlineData("a?b", "a?", false)]
    [InlineData("a?ab", "a?aab", false)]
    [InlineData("aa?bbbc?d", "aa?bbc?dd", false)]

    // '*' literal in the input string
    [InlineData("*", "*", true)]
    [InlineData("*a", "*a", true)]
    [InlineData("a*", "a*", true)]
    [InlineData("a*b", "a*", true)]
    [InlineData("a*ab", "a*aab", false)]
    [InlineData("a*abab", "a*b", true)]
    [InlineData("aa*bbbc*d", "aa*bbc*dd", false)]
    [InlineData("aa*bbbc*d", "a*bbc*d", true)]
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
    [Theory]
    // *.txt - 5 matches
    [InlineData("*.txt", "file.txt", true)]
    [InlineData("*.txt", "file1.txt", true)]
    [InlineData("*.txt", "fie1.txt", true)]
    [InlineData("*.txt", "fire1.txt", true)]
    [InlineData("*.txt", "file.bak.txt", true)]
    [InlineData("*.txt", "Foo.cs", false)]
    [InlineData("*.txt", "Foo2.cs", false)]
    [InlineData("*.txt", "file1.txtother", false)]

    // ???.cs - 1 match (exactly 3 chars before the .cs)
    [InlineData("???.cs", "Foo.cs", true)]
    [InlineData("???.cs", "Foo2.cs", false)]

    // ????.cs - 1 match
    [InlineData("????.cs", "Foo2.cs", true)]
    [InlineData("????.cs", "Foo.cs", false)]

    // file?.txt - 1 match
    [InlineData("file?.txt", "file1.txt", true)]
    [InlineData("file?.txt", "file.txt", false)]
    [InlineData("file?.txt", "file1.txtother", false)]

    // fi?e?.txt - 2 matches
    [InlineData("fi?e?.txt", "file1.txt", true)]
    [InlineData("fi?e?.txt", "fire1.txt", true)]
    [InlineData("fi?e?.txt", "file.txt", false)]

    // ???.* - 1 match
    [InlineData("???.*", "Foo.cs", true)]
    [InlineData("???.*", "Foo2.cs", false)]

    // ????.* - 4 matches
    [InlineData("????.*", "Foo2.cs", true)]
    [InlineData("????.*", "fie1.txt", true)]
    [InlineData("????.*", "file.txt", true)]
    [InlineData("????.*", "Foo.cs", false)]

    // *.??? - 5 matches
    [InlineData("*.???", "Foo.cs", false)]  // .cs is two chars, not three
    [InlineData("*.???", "file.txt", true)]
    [InlineData("*.???", "file.bak.txt", true)]

    // f??e1.txt - 2 matches
    [InlineData("f??e1.txt", "file1.txt", true)]
    [InlineData("f??e1.txt", "fire1.txt", true)]
    [InlineData("f??e1.txt", "fie1.txt", false)]

    // file.*.txt - 1 match
    [InlineData("file.*.txt", "file.bak.txt", true)]
    [InlineData("file.*.txt", "file.txt", false)]
    public void IsMatch_GetFilesPatternMatching(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.MSBuild).IsMatch(input).Should().Be(expected);

    // From MSBuildGlob_Tests.GlobMatchingShouldWorkWithLiteralStrings, etc.
    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("ab?c*", "acd", false)]
    [InlineData("%42", "B", false)]      // %-escapes not unescaped at the matcher layer
    [InlineData("%42", "%42", true)]
    public void IsMatch_MSBuildGlob_PatternLevelRows(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.MSBuild).IsMatch(input).Should().Be(expected);

    // From MSBuildGlob_Tests.GlobShouldMatchEmptyArgWhenGlob...
    [Fact]
    public void IsMatch_EmptyPattern_MatchesEmptyInput() =>
        GlobSpecification.Compile(string.Empty, GlobDialect.MSBuild).IsMatch(string.Empty).Should().BeTrue();

    [Fact]
    public void IsMatch_StarPattern_MatchesEmptyInput() =>
        GlobSpecification.Compile("*", GlobDialect.MSBuild).IsMatch(string.Empty).Should().BeTrue();

    [Fact]
    public void IsMatch_StarAStarPattern_DoesNotMatchEmptyInput() =>
        GlobSpecification.Compile("*a*", GlobDialect.MSBuild).IsMatch(string.Empty).Should().BeFalse();
}
