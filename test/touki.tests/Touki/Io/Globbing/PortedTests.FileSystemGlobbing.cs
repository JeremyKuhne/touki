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
[TestClass]
public class PortedTests_FileSystemGlobbing
{
    // From PatternMatchingTests.PatternMatchingWorks.
    [TestMethod]
    [DataRow("*.txt", "alpha.txt", true)]
    [DataRow("*.txt", "beta.txt", true)]
    [DataRow("*.txt", "gamma.dat", false)]
    [DataRow("alpha.*", "alpha.txt", true)]
    [DataRow("alpha.*", "beta.txt", false)]
    [DataRow("*.*", "alpha.txt", true)]
    [DataRow("*.*", "gamma.dat", true)]
    [DataRow("*", "alpha.txt", true)]
    [DataRow("*", "gamma.dat", true)]
    [DataRow("*et*", "alpha.txt", false)]
    [DataRow("*et*", "beta.txt", true)]
    [DataRow("*et*", "gamma.dat", false)]
    [DataRow("b*et*t", "alpha.txt", false)]
    [DataRow("b*et*t", "beta.txt", true)]
    [DataRow("b*et*x", "beta.txt", false)]
    public void IsMatch_PatternMatchingWorks(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.PatternBeginAndEndCantOverlap. Confirms that the prefix
    // and suffix portions of a `*`-bracketed pattern cannot share characters in the input.
    [TestMethod]
    [DataRow("1234*5678", "12345678", true)]
    [DataRow("12345*5678", "12345678", false)]
    [DataRow("12*3456*78", "12345678", true)]
    [DataRow("12*23*", "12345678", false)]
    [DataRow("*67*78", "12345678", false)]
    [DataRow("*45*56", "12345678", false)]
    public void IsMatch_PatternBeginAndEndCantOverlap(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.PatternMatchingWorksInFolders. Files under test:
    // alpha/hello.txt, beta/hello.txt, gamma/hello.txt.
    [TestMethod]
    [DataRow("*mm*/*", "gamma/hello.txt", true)]
    [DataRow("*mm*/*", "alpha/hello.txt", false)]
    [DataRow("*mm*/*", "beta/hello.txt", false)]
    [DataRow("/*mm*/*", "gamma/hello.txt", true)]
    [DataRow("/*mm*/*", "alpha/hello.txt", false)]
    [DataRow("*alpha*/*", "alpha/hello.txt", true)]
    [DataRow("*alpha*/*", "beta/hello.txt", false)]
    [DataRow("/*alpha*/*", "alpha/hello.txt", true)]
    [DataRow("*/*", "alpha/hello.txt", true)]
    [DataRow("*/*", "beta/hello.txt", true)]
    [DataRow("*/*", "gamma/hello.txt", true)]
    [DataRow("/*/*", "alpha/hello.txt", true)]
    [DataRow("/*/*", "beta/hello.txt", true)]
    [DataRow("*.*/*", "alpha/hello.txt", true)]
    [DataRow("*.*/*", "beta/hello.txt", true)]
    [DataRow("/*.*/*", "gamma/hello.txt", true)]
    public void IsMatch_PatternMatchingWorksInFolders(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.PatternMatchingCurrent. Patterns with leading `./` or
    // embedded `/./` should be equivalent to the same pattern with the dot-segments
    // removed. Files: alpha/hello.txt, beta/hello.txt, gamma/hello.txt.
    [TestMethod]
    [DataRow("./alpha/hello.txt", "alpha/hello.txt", true)]
    [DataRow("./alpha/hello.txt", "beta/hello.txt", false)]
    [DataRow("./**/hello.txt", "alpha/hello.txt", true)]
    [DataRow("./**/hello.txt", "gamma/hello.txt", true)]
    [DataRow("././**/hello.txt", "alpha/hello.txt", true)]
    [DataRow("././**/./hello.txt", "alpha/hello.txt", true)]
    [DataRow("././**/./**/hello.txt", "alpha/hello.txt", true)]
    [DataRow("./*mm*/hello.txt", "gamma/hello.txt", true)]
    [DataRow("./*mm*/hello.txt", "alpha/hello.txt", false)]
    [DataRow("./*mm*/*", "gamma/hello.txt", true)]
    [DataRow("./*mm*/*", "beta/hello.txt", false)]
    public void IsMatch_PatternMatchingCurrent(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.StarDotStarIsSameAsStar. Pattern `*.*` matches every
    // file regardless of whether it actually contains a dot.
    [TestMethod]
    [DataRow("*.*", "alpha.txt", true)]
    [DataRow("*.*", "alpha.", true)]
    [DataRow("*.*", ".txt", true)]
    [DataRow("*.*", ".", true)]
    [DataRow("*.*", "alpha", true)]
    [DataRow("*.*", "txt", true)]
    public void IsMatch_StarDotStarMatchesEverything(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.IncompletePatternsDoNotInclude. `*/*.txt` requires a
    // directory component; bare `x.txt` does not match.
    [TestMethod]
    [DataRow("*/*.txt", "one/x.txt", true)]
    [DataRow("*/*.txt", "two/x.txt", true)]
    [DataRow("*/*.txt", "x.txt", false)]
    public void IsMatch_IncompletePatternsRequireSegments(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.TrailingRecursiveWildcardMatchesAllFiles.
    [TestMethod]
    [DataRow("one/**", "one/x.txt", true)]
    [DataRow("one/**", "two/x.txt", false)]
    [DataRow("one/**", "one/x/y.txt", true)]
    public void IsMatch_TrailingRecursiveWildcardMatchesAllFiles(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.LeadingRecursiveWildcardMatchesAllLeadingPaths.
    [TestMethod]
    [DataRow("**/*.cs", "one/x.cs", true)]
    [DataRow("**/*.cs", "two/x.cs", true)]
    [DataRow("**/*.cs", "one/two/x.cs", true)]
    [DataRow("**/*.cs", "x.cs", true)]
    [DataRow("**/*.cs", "one/x.txt", false)]
    [DataRow("**/*.cs", "x.txt", false)]
    public void IsMatch_LeadingRecursiveWildcardMatchesAllLeadingPaths(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.InnerRecursiveWildcardMuseStartWithAndEndWith [sic].
    [TestMethod]
    [DataRow("one/**/*.cs", "one/x.cs", true)]
    [DataRow("one/**/*.cs", "two/x.cs", false)]
    [DataRow("one/**/*.cs", "one/two/x.cs", true)]
    [DataRow("one/**/*.cs", "x.cs", false)]
    [DataRow("one/**/*.cs", "one/x.txt", false)]
    public void IsMatch_InnerRecursiveWildcardMustStartAndEnd(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.RecursiveWildcardSurroundingContainsWith [sic].
    [TestMethod]
    [DataRow("**/x/**", "x/1", true)]
    [DataRow("**/x/**", "1/x/2", true)]
    [DataRow("**/x/**", "1/x", false)]
    [DataRow("**/x/**", "x", false)]
    [DataRow("**/x/**", "1", false)]
    [DataRow("**/x/**", "1/2", false)]
    public void IsMatch_RecursiveWildcardSurrounding(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.SequentialFoldersMayBeRequired. Long pattern with
    // multiple globstars and required literal segments.
    [TestMethod]
    [DataRow("a/b/**/1/2/**/2/3/**", "1/2/2/3/x", false)]
    [DataRow("a/b/**/1/2/**/2/3/**", "1/2/3/y", false)]
    [DataRow("a/b/**/1/2/**/2/3/**", "a/1/2/4/2/3/b", false)]
    [DataRow("a/b/**/1/2/**/2/3/**", "a/2/3/1/2/b", false)]
    [DataRow("a/b/**/1/2/**/2/3/**", "a/b/1/2/2/3/x", true)]
    [DataRow("a/b/**/1/2/**/2/3/**", "a/b/1/2/3/y", false)]
    [DataRow("a/b/**/1/2/**/2/3/**", "a/b/a/1/2/4/2/3/b", true)]
    [DataRow("a/b/**/1/2/**/2/3/**", "a/b/a/2/3/1/2/b", false)]
    public void IsMatch_SequentialFoldersMayBeRequired(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.RecursiveAloneIncludesEverything.
    [TestMethod]
    [DataRow("**", "1/2/2/3/x", true)]
    [DataRow("**", "1/2/3/y", true)]
    [DataRow("**", "x", true)]
    public void IsMatch_RecursiveAloneIncludesEverything(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    // From PatternMatchingTests.LeadingDotDotCanComeThroughPattern. The `..` segment
    // is treated as a literal path component at the matcher level (the enumerator is
    // what resolves it against the current sub-directory).
    [TestMethod]
    [DataRow("../2/*.cs", "../2/x.cs", true)]
    [DataRow("../2/*.cs", "../2/x.txt", false)]
    [DataRow("../2/**/*.cs", "../2/x.cs", true)]
    [DataRow("../2/**/*.cs", "../2/3/x.cs", true)]
    [DataRow("../2/**/*.cs", "../2/3/4/z.cs", true)]
    [DataRow("../2/**/*.cs", "../2/3/x.txt", false)]
    public void IsMatch_LeadingDotDotCanComeThroughPattern(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);
}
