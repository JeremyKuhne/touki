// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public class MatchAnyTests
{
    [Theory]
    [InlineData("temp", "temp", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("temp", "TEMP", MatchType.Simple, MatchCasing.CaseSensitive, false)]
    [InlineData("temp", "TEMP", MatchType.Simple, MatchCasing.CaseInsensitive, true)]
    [InlineData("temp*", "tempdir", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("temp*", "other", MatchType.Simple, MatchCasing.CaseSensitive, false)]
    public void MatchAnyDirectory_MatchesDirectory_ReturnsExpectedResult(
        string pattern,
        string directoryName,
        MatchType matchType,
        MatchCasing matchCasing,
        bool expectedResult)
    {
        IEnumerationMatcher matcher = new MatchAnyDirectory(
            pattern,
            matchType,
            matchCasing);

        matcher.MatchesDirectory("".AsSpan(), directoryName.AsSpan()).Should().Be(expectedResult);
    }

    [Fact]
    public void MatchAnyDirectory_WithMultipleSpecs_MatchesAny()
    {
        MatchAnyDirectory matcher = new(
            "docs",
            MatchType.Simple,
            MatchCasing.CaseSensitive);

        matcher.AddSpec("src");
        matcher.AddSpec("build*");

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesDirectory("".AsSpan(), "docs".AsSpan()).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "src".AsSpan()).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "build".AsSpan()).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "build-output".AsSpan()).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "tests".AsSpan()).Should().BeFalse();
        iMatcher.MatchesDirectory("".AsSpan(), "SRC".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void MatchAnyDirectory_AddSpec_HandlesDuplicates()
    {
        MatchAnyDirectory matcher = new(
            "src",
            MatchType.Simple,
            MatchCasing.CaseInsensitive);

        matcher.AddSpec("docs").Should().BeTrue();
        matcher.AddSpec("src").Should().BeFalse();
        matcher.AddSpec("SRC").Should().BeFalse();
        matcher.AddSpec("docs").Should().BeFalse();

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesDirectory("".AsSpan(), "src".AsSpan()).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "SRC".AsSpan()).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "docs".AsSpan()).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "build".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void MatchAnyDirectory_MatchesFile_AlwaysReturnsFalse()
    {
        IEnumerationMatcher matcher = new MatchAnyDirectory(
            "any",
            MatchType.Simple,
            MatchCasing.CaseSensitive);

        bool result = matcher.MatchesFile("".AsSpan(), "file.txt".AsSpan());

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("file.txt", "file.txt", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("file.txt", "FILE.TXT", MatchType.Simple, MatchCasing.CaseSensitive, false)]
    [InlineData("file.txt", "FILE.TXT", MatchType.Simple, MatchCasing.CaseInsensitive, true)]
    [InlineData("*.txt", "file.txt", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("*.txt", "file.doc", MatchType.Simple, MatchCasing.CaseSensitive, false)]
    public void MatchAnyFile_MatchesFile_ReturnsExpectedResult(
        string pattern,
        string fileName,
        MatchType matchType,
        MatchCasing matchCasing,
        bool expectedResult)
    {
        // Create the matcher
        IEnumerationMatcher matcher = new MatchAnyFile(
            pattern,
            matchType,
            matchCasing);

        matcher.MatchesFile("".AsSpan(), fileName.AsSpan()).Should().Be(expectedResult);
    }

    [Fact]
    public void MatchAnyFile_WithMultipleSpecs_MatchesAny()
    {
        MatchAnyFile matcher = new(
            "*.txt",
            MatchType.Simple,
            MatchCasing.CaseSensitive);

        matcher.AddSpec("*.log");
        matcher.AddSpec("data.csv");

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesFile("".AsSpan(), "file1.txt".AsSpan()).Should().BeTrue();
        iMatcher.MatchesFile("".AsSpan(), "file2.log".AsSpan()).Should().BeTrue();
        iMatcher.MatchesFile("".AsSpan(), "data.csv".AsSpan()).Should().BeTrue();
        iMatcher.MatchesFile("".AsSpan(), "file3.doc".AsSpan()).Should().BeFalse();
        iMatcher.MatchesFile("".AsSpan(), "other.dat".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void MatchAnyFile_AddSpec_HandlesDuplicates()
    {
        MatchAnyFile matcher = new(
            "*.txt",
            MatchType.Simple,
            MatchCasing.CaseInsensitive);

        matcher.AddSpec("*.log").Should().BeTrue();
        matcher.AddSpec("*.txt").Should().BeFalse();
        matcher.AddSpec("*.TXT").Should().BeFalse();
        matcher.AddSpec("*.log").Should().BeFalse();

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesFile("".AsSpan(), "file.txt".AsSpan()).Should().BeTrue();
        iMatcher.MatchesFile("".AsSpan(), "FILE.TXT".AsSpan()).Should().BeTrue();
        iMatcher.MatchesFile("".AsSpan(), "file.log".AsSpan()).Should().BeTrue();
        iMatcher.MatchesFile("".AsSpan(), "file.csv".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void MatchAnyFile_MatchesDirectory_AlwaysReturnsTrue()
    {
        IEnumerationMatcher matcher = new MatchAnyFile(
            "any",
            MatchType.Simple,
            MatchCasing.CaseSensitive);

        matcher.MatchesDirectory("".AsSpan(), "dir".AsSpan()).Should().BeTrue();
    }

    [Theory]
    [InlineData("/root", "file.txt", "/root", "file.txt", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("/root", "file.txt", "/root/subdir", "file.txt", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("/root", "file.txt", "/other", "file.txt", MatchType.Simple, MatchCasing.CaseSensitive, false)]
    [InlineData("/root", "*.txt", "/root", "file.txt", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("/root", "*.txt", "/root", "file.doc", MatchType.Simple, MatchCasing.CaseSensitive, false)]
    public void MatchAnyFileAfter_MatchesFile_ReturnsExpectedResult(
        string rootPath,
        string pattern,
        string currentDir,
        string fileName,
        MatchType matchType,
        MatchCasing matchCasing,
        bool expectedResult)
    {
        IEnumerationMatcher matcher = new MatchAnyFile(
            pattern,
            rootPath,
            matchType,
            matchCasing);

        matcher.MatchesFile(currentDir.AsSpan(), fileName.AsSpan()).Should().Be(expectedResult);
    }

    [Fact]
    public void MatchAnyFile_MatchesDirectory_ReturnsExpectedResult()
    {
        IEnumerationMatcher matcher = new MatchAnyFile(
            "*.txt",
            "/root",
            MatchType.Simple,
            MatchCasing.CaseSensitive);

        matcher.MatchesDirectory("/root".AsSpan(), "subdir".AsSpan()).Should().BeTrue();
        matcher.DirectoryFinished();
        matcher.MatchesDirectory("/foo".AsSpan(), "subdir".AsSpan()).Should().BeFalse();
    }


    [Theory]
    [InlineData("/root", "subdir", "/root", "subdir", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("/root", "subdir", "/root/parent", "subdir", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("/root", "subdir", "/other", "subdir", MatchType.Simple, MatchCasing.CaseSensitive, false)]
    [InlineData("/root", "sub*", "/root", "subdir", MatchType.Simple, MatchCasing.CaseSensitive, true)]
    [InlineData("/root", "sub*", "/root", "other", MatchType.Simple, MatchCasing.CaseSensitive, false)]
    public void MatchAnyDirectory_MatchesDirectory_WithRoot_ReturnsExpectedResult(
        string rootPath,
        string pattern,
        string currentDir,
        string dirName,
        MatchType matchType,
        MatchCasing matchCasing,
        bool expectedResult)
    {
        IEnumerationMatcher matcher = new MatchAnyDirectory(
            pattern,
            rootPath,
            matchType,
            matchCasing);

        matcher.MatchesDirectory(currentDir.AsSpan(), dirName.AsSpan()).Should().Be(expectedResult);
    }

    [Fact]
    public void MatchAnyDirectoryAfter_MatchesFile_AlwaysReturnsFalse()
    {
        IEnumerationMatcher matcher = new MatchAnyDirectory(
            "/root",
            "subdir",
            MatchType.Simple,
            MatchCasing.CaseSensitive);

        matcher.MatchesFile("/root".AsSpan(), "file.txt".AsSpan()).Should().BeFalse();
    }
}
