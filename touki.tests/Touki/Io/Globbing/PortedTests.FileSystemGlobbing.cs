// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Pattern-level scenarios ported from the upstream
///  <c>Microsoft.Extensions.FileSystemGlobbing</c> test suite.
/// </summary>
/// <remarks>
///  <para>
///   Source: <see href="https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.FileSystemGlobbing/tests/PatternMatchingTests.cs"><c>dotnet/runtime/src/libraries/Microsoft.Extensions.FileSystemGlobbing/tests/PatternMatchingTests.cs</c></see>,
///   licensed under the MIT license to the .NET Foundation.
///  </para>
///  <para>
///   Upstream tests exercise the full include/exclude/sub-directory enumerator
///   (<c>Matcher.AddInclude / AddExclude / Execute</c>). These rows are
///   distilled to pattern-level matches against <see cref="GlobSpecification.IsMatch"/>
///   on <see cref="GlobDialect.FileSystemGlobbing"/>. Scenarios that depend on
///   include/exclude composition, sub-directory traversal state, or stem
///   extraction are excluded - those belong with the enumerator-level tests
///   for <see cref="GlobEnumerator"/>.
///  </para>
/// </remarks>
public class PortedTests_FileSystemGlobbing
{
    // From PatternMatchingTests.PatternMatchingWorks.
    [Theory]
    [InlineData("*.txt", "alpha.txt", true)]
    [InlineData("*.txt", "beta.txt", true)]
    [InlineData("*.txt", "gamma.dat", false)]
    [InlineData("alpha.*", "alpha.txt", true)]
    [InlineData("alpha.*", "beta.txt", false)]
    [InlineData("*.*", "alpha.txt", true)]
    [InlineData("*.*", "gamma.dat", true)]
    [InlineData("*", "alpha.txt", true)]
    [InlineData("*", "gamma.dat", true)]
    [InlineData("*et*", "alpha.txt", false)]
    [InlineData("*et*", "beta.txt", true)]
    [InlineData("*et*", "gamma.dat", false)]
    [InlineData("b*et*t", "alpha.txt", false)]
    [InlineData("b*et*t", "beta.txt", true)]
    [InlineData("b*et*x", "beta.txt", false)]
    public void IsMatch_PatternMatchingWorks(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.PatternBeginAndEndCantOverlap. Confirms that the prefix
    // and suffix portions of a `*`-bracketed pattern cannot share characters in the input.
    [Theory]
    [InlineData("1234*5678", "12345678", true)]
    [InlineData("12345*5678", "12345678", false)]
    [InlineData("12*3456*78", "12345678", true)]
    [InlineData("12*23*", "12345678", false)]
    [InlineData("*67*78", "12345678", false)]
    [InlineData("*45*56", "12345678", false)]
    public void IsMatch_PatternBeginAndEndCantOverlap(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.PatternMatchingWorksInFolders. Files under test:
    // alpha/hello.txt, beta/hello.txt, gamma/hello.txt.
    [Theory]
    [InlineData("*mm*/*", "gamma/hello.txt", true)]
    [InlineData("*mm*/*", "alpha/hello.txt", false)]
    [InlineData("*mm*/*", "beta/hello.txt", false)]
    [InlineData("/*mm*/*", "gamma/hello.txt", true)]
    [InlineData("/*mm*/*", "alpha/hello.txt", false)]
    [InlineData("*alpha*/*", "alpha/hello.txt", true)]
    [InlineData("*alpha*/*", "beta/hello.txt", false)]
    [InlineData("/*alpha*/*", "alpha/hello.txt", true)]
    [InlineData("*/*", "alpha/hello.txt", true)]
    [InlineData("*/*", "beta/hello.txt", true)]
    [InlineData("*/*", "gamma/hello.txt", true)]
    [InlineData("/*/*", "alpha/hello.txt", true)]
    [InlineData("/*/*", "beta/hello.txt", true)]
    [InlineData("*.*/*", "alpha/hello.txt", true)]
    [InlineData("*.*/*", "beta/hello.txt", true)]
    [InlineData("/*.*/*", "gamma/hello.txt", true)]
    public void IsMatch_PatternMatchingWorksInFolders(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.PatternMatchingCurrent. Patterns with leading `./` or
    // embedded `/./` should be equivalent to the same pattern with the dot-segments
    // removed. Files: alpha/hello.txt, beta/hello.txt, gamma/hello.txt.
    [Theory]
    [InlineData("./alpha/hello.txt", "alpha/hello.txt", true)]
    [InlineData("./alpha/hello.txt", "beta/hello.txt", false)]
    [InlineData("./**/hello.txt", "alpha/hello.txt", true)]
    [InlineData("./**/hello.txt", "gamma/hello.txt", true)]
    [InlineData("././**/hello.txt", "alpha/hello.txt", true)]
    [InlineData("././**/./hello.txt", "alpha/hello.txt", true)]
    [InlineData("././**/./**/hello.txt", "alpha/hello.txt", true)]
    [InlineData("./*mm*/hello.txt", "gamma/hello.txt", true)]
    [InlineData("./*mm*/hello.txt", "alpha/hello.txt", false)]
    [InlineData("./*mm*/*", "gamma/hello.txt", true)]
    [InlineData("./*mm*/*", "beta/hello.txt", false)]
    public void IsMatch_PatternMatchingCurrent(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.StarDotStarIsSameAsStar. Pattern `*.*` matches every
    // file regardless of whether it actually contains a dot.
    [Theory]
    [InlineData("*.*", "alpha.txt", true)]
    [InlineData("*.*", "alpha.", true)]
    [InlineData("*.*", ".txt", true)]
    [InlineData("*.*", ".", true)]
    [InlineData("*.*", "alpha", true)]
    [InlineData("*.*", "txt", true)]
    public void IsMatch_StarDotStarMatchesEverything(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.IncompletePatternsDoNotInclude. `*/*.txt` requires a
    // directory component; bare `x.txt` does not match.
    [Theory]
    [InlineData("*/*.txt", "one/x.txt", true)]
    [InlineData("*/*.txt", "two/x.txt", true)]
    [InlineData("*/*.txt", "x.txt", false)]
    public void IsMatch_IncompletePatternsRequireSegments(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.TrailingRecursiveWildcardMatchesAllFiles.
    [Theory]
    [InlineData("one/**", "one/x.txt", true)]
    [InlineData("one/**", "two/x.txt", false)]
    [InlineData("one/**", "one/x/y.txt", true)]
    public void IsMatch_TrailingRecursiveWildcardMatchesAllFiles(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.LeadingRecursiveWildcardMatchesAllLeadingPaths.
    [Theory]
    [InlineData("**/*.cs", "one/x.cs", true)]
    [InlineData("**/*.cs", "two/x.cs", true)]
    [InlineData("**/*.cs", "one/two/x.cs", true)]
    [InlineData("**/*.cs", "x.cs", true)]
    [InlineData("**/*.cs", "one/x.txt", false)]
    [InlineData("**/*.cs", "x.txt", false)]
    public void IsMatch_LeadingRecursiveWildcardMatchesAllLeadingPaths(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.InnerRecursiveWildcardMuseStartWithAndEndWith [sic].
    [Theory]
    [InlineData("one/**/*.cs", "one/x.cs", true)]
    [InlineData("one/**/*.cs", "two/x.cs", false)]
    [InlineData("one/**/*.cs", "one/two/x.cs", true)]
    [InlineData("one/**/*.cs", "x.cs", false)]
    [InlineData("one/**/*.cs", "one/x.txt", false)]
    public void IsMatch_InnerRecursiveWildcardMustStartAndEnd(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.RecursiveWildcardSurroundingContainsWith [sic].
    [Theory]
    [InlineData("**/x/**", "x/1", true)]
    [InlineData("**/x/**", "1/x/2", true)]
    [InlineData("**/x/**", "1/x", false)]
    [InlineData("**/x/**", "x", false)]
    [InlineData("**/x/**", "1", false)]
    [InlineData("**/x/**", "1/2", false)]
    public void IsMatch_RecursiveWildcardSurrounding(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.SequentialFoldersMayBeRequired. Long pattern with
    // multiple globstars and required literal segments.
    [Theory]
    [InlineData("a/b/**/1/2/**/2/3/**", "1/2/2/3/x", false)]
    [InlineData("a/b/**/1/2/**/2/3/**", "1/2/3/y", false)]
    [InlineData("a/b/**/1/2/**/2/3/**", "a/1/2/4/2/3/b", false)]
    [InlineData("a/b/**/1/2/**/2/3/**", "a/2/3/1/2/b", false)]
    [InlineData("a/b/**/1/2/**/2/3/**", "a/b/1/2/2/3/x", true)]
    [InlineData("a/b/**/1/2/**/2/3/**", "a/b/1/2/3/y", false)]
    [InlineData("a/b/**/1/2/**/2/3/**", "a/b/a/1/2/4/2/3/b", true)]
    [InlineData("a/b/**/1/2/**/2/3/**", "a/b/a/2/3/1/2/b", false)]
    public void IsMatch_SequentialFoldersMayBeRequired(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.RecursiveAloneIncludesEverything.
    [Theory]
    [InlineData("**", "1/2/2/3/x", true)]
    [InlineData("**", "1/2/3/y", true)]
    [InlineData("**", "x", true)]
    public void IsMatch_RecursiveAloneIncludesEverything(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.LeadingDotDotCanComeThroughPattern. The `..` segment
    // is treated as a literal path component at the matcher level (the enumerator is
    // what resolves it against the current sub-directory).
    [Theory]
    [InlineData("../2/*.cs", "../2/x.cs", true)]
    [InlineData("../2/*.cs", "../2/x.txt", false)]
    [InlineData("../2/**/*.cs", "../2/x.cs", true)]
    [InlineData("../2/**/*.cs", "../2/3/x.cs", true)]
    [InlineData("../2/**/*.cs", "../2/3/4/z.cs", true)]
    [InlineData("../2/**/*.cs", "../2/3/x.txt", false)]
    public void IsMatch_LeadingDotDotCanComeThroughPattern(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);
}
