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
    [Test]
    [Arguments("*.txt", "alpha.txt", true)]
    [Arguments("*.txt", "beta.txt", true)]
    [Arguments("*.txt", "gamma.dat", false)]
    [Arguments("alpha.*", "alpha.txt", true)]
    [Arguments("alpha.*", "beta.txt", false)]
    [Arguments("*.*", "alpha.txt", true)]
    [Arguments("*.*", "gamma.dat", true)]
    [Arguments("*", "alpha.txt", true)]
    [Arguments("*", "gamma.dat", true)]
    [Arguments("*et*", "alpha.txt", false)]
    [Arguments("*et*", "beta.txt", true)]
    [Arguments("*et*", "gamma.dat", false)]
    [Arguments("b*et*t", "alpha.txt", false)]
    [Arguments("b*et*t", "beta.txt", true)]
    [Arguments("b*et*x", "beta.txt", false)]
    public void IsMatch_PatternMatchingWorks(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.PatternBeginAndEndCantOverlap. Confirms that the prefix
    // and suffix portions of a `*`-bracketed pattern cannot share characters in the input.
    [Test]
    [Arguments("1234*5678", "12345678", true)]
    [Arguments("12345*5678", "12345678", false)]
    [Arguments("12*3456*78", "12345678", true)]
    [Arguments("12*23*", "12345678", false)]
    [Arguments("*67*78", "12345678", false)]
    [Arguments("*45*56", "12345678", false)]
    public void IsMatch_PatternBeginAndEndCantOverlap(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.PatternMatchingWorksInFolders. Files under test:
    // alpha/hello.txt, beta/hello.txt, gamma/hello.txt.
    [Test]
    [Arguments("*mm*/*", "gamma/hello.txt", true)]
    [Arguments("*mm*/*", "alpha/hello.txt", false)]
    [Arguments("*mm*/*", "beta/hello.txt", false)]
    [Arguments("/*mm*/*", "gamma/hello.txt", true)]
    [Arguments("/*mm*/*", "alpha/hello.txt", false)]
    [Arguments("*alpha*/*", "alpha/hello.txt", true)]
    [Arguments("*alpha*/*", "beta/hello.txt", false)]
    [Arguments("/*alpha*/*", "alpha/hello.txt", true)]
    [Arguments("*/*", "alpha/hello.txt", true)]
    [Arguments("*/*", "beta/hello.txt", true)]
    [Arguments("*/*", "gamma/hello.txt", true)]
    [Arguments("/*/*", "alpha/hello.txt", true)]
    [Arguments("/*/*", "beta/hello.txt", true)]
    [Arguments("*.*/*", "alpha/hello.txt", true)]
    [Arguments("*.*/*", "beta/hello.txt", true)]
    [Arguments("/*.*/*", "gamma/hello.txt", true)]
    public void IsMatch_PatternMatchingWorksInFolders(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.PatternMatchingCurrent. Patterns with leading `./` or
    // embedded `/./` should be equivalent to the same pattern with the dot-segments
    // removed. Files: alpha/hello.txt, beta/hello.txt, gamma/hello.txt.
    [Test]
    [Arguments("./alpha/hello.txt", "alpha/hello.txt", true)]
    [Arguments("./alpha/hello.txt", "beta/hello.txt", false)]
    [Arguments("./**/hello.txt", "alpha/hello.txt", true)]
    [Arguments("./**/hello.txt", "gamma/hello.txt", true)]
    [Arguments("././**/hello.txt", "alpha/hello.txt", true)]
    [Arguments("././**/./hello.txt", "alpha/hello.txt", true)]
    [Arguments("././**/./**/hello.txt", "alpha/hello.txt", true)]
    [Arguments("./*mm*/hello.txt", "gamma/hello.txt", true)]
    [Arguments("./*mm*/hello.txt", "alpha/hello.txt", false)]
    [Arguments("./*mm*/*", "gamma/hello.txt", true)]
    [Arguments("./*mm*/*", "beta/hello.txt", false)]
    public void IsMatch_PatternMatchingCurrent(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.StarDotStarIsSameAsStar. Pattern `*.*` matches every
    // file regardless of whether it actually contains a dot.
    [Test]
    [Arguments("*.*", "alpha.txt", true)]
    [Arguments("*.*", "alpha.", true)]
    [Arguments("*.*", ".txt", true)]
    [Arguments("*.*", ".", true)]
    [Arguments("*.*", "alpha", true)]
    [Arguments("*.*", "txt", true)]
    public void IsMatch_StarDotStarMatchesEverything(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.IncompletePatternsDoNotInclude. `*/*.txt` requires a
    // directory component; bare `x.txt` does not match.
    [Test]
    [Arguments("*/*.txt", "one/x.txt", true)]
    [Arguments("*/*.txt", "two/x.txt", true)]
    [Arguments("*/*.txt", "x.txt", false)]
    public void IsMatch_IncompletePatternsRequireSegments(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.TrailingRecursiveWildcardMatchesAllFiles.
    [Test]
    [Arguments("one/**", "one/x.txt", true)]
    [Arguments("one/**", "two/x.txt", false)]
    [Arguments("one/**", "one/x/y.txt", true)]
    public void IsMatch_TrailingRecursiveWildcardMatchesAllFiles(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.LeadingRecursiveWildcardMatchesAllLeadingPaths.
    [Test]
    [Arguments("**/*.cs", "one/x.cs", true)]
    [Arguments("**/*.cs", "two/x.cs", true)]
    [Arguments("**/*.cs", "one/two/x.cs", true)]
    [Arguments("**/*.cs", "x.cs", true)]
    [Arguments("**/*.cs", "one/x.txt", false)]
    [Arguments("**/*.cs", "x.txt", false)]
    public void IsMatch_LeadingRecursiveWildcardMatchesAllLeadingPaths(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.InnerRecursiveWildcardMuseStartWithAndEndWith [sic].
    [Test]
    [Arguments("one/**/*.cs", "one/x.cs", true)]
    [Arguments("one/**/*.cs", "two/x.cs", false)]
    [Arguments("one/**/*.cs", "one/two/x.cs", true)]
    [Arguments("one/**/*.cs", "x.cs", false)]
    [Arguments("one/**/*.cs", "one/x.txt", false)]
    public void IsMatch_InnerRecursiveWildcardMustStartAndEnd(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.RecursiveWildcardSurroundingContainsWith [sic].
    [Test]
    [Arguments("**/x/**", "x/1", true)]
    [Arguments("**/x/**", "1/x/2", true)]
    [Arguments("**/x/**", "1/x", false)]
    [Arguments("**/x/**", "x", false)]
    [Arguments("**/x/**", "1", false)]
    [Arguments("**/x/**", "1/2", false)]
    public void IsMatch_RecursiveWildcardSurrounding(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.SequentialFoldersMayBeRequired. Long pattern with
    // multiple globstars and required literal segments.
    [Test]
    [Arguments("a/b/**/1/2/**/2/3/**", "1/2/2/3/x", false)]
    [Arguments("a/b/**/1/2/**/2/3/**", "1/2/3/y", false)]
    [Arguments("a/b/**/1/2/**/2/3/**", "a/1/2/4/2/3/b", false)]
    [Arguments("a/b/**/1/2/**/2/3/**", "a/2/3/1/2/b", false)]
    [Arguments("a/b/**/1/2/**/2/3/**", "a/b/1/2/2/3/x", true)]
    [Arguments("a/b/**/1/2/**/2/3/**", "a/b/1/2/3/y", false)]
    [Arguments("a/b/**/1/2/**/2/3/**", "a/b/a/1/2/4/2/3/b", true)]
    [Arguments("a/b/**/1/2/**/2/3/**", "a/b/a/2/3/1/2/b", false)]
    public void IsMatch_SequentialFoldersMayBeRequired(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.RecursiveAloneIncludesEverything.
    [Test]
    [Arguments("**", "1/2/2/3/x", true)]
    [Arguments("**", "1/2/3/y", true)]
    [Arguments("**", "x", true)]
    public void IsMatch_RecursiveAloneIncludesEverything(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.LeadingDotDotCanComeThroughPattern. The `..` segment
    // is treated as a literal path component at the matcher level (the enumerator is
    // what resolves it against the current sub-directory).
    [Test]
    [Arguments("../2/*.cs", "../2/x.cs", true)]
    [Arguments("../2/*.cs", "../2/x.txt", false)]
    [Arguments("../2/**/*.cs", "../2/x.cs", true)]
    [Arguments("../2/**/*.cs", "../2/3/x.cs", true)]
    [Arguments("../2/**/*.cs", "../2/3/4/z.cs", true)]
    [Arguments("../2/**/*.cs", "../2/3/x.txt", false)]
    public void IsMatch_LeadingDotDotCanComeThroughPattern(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);
}
