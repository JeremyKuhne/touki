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

        // Include (matchForExclusion = false)
        matcher.MatchesDirectory("".AsSpan(), directoryName.AsSpan(), false).Should().Be(expectedResult);
        // Exclude (matchForExclusion = true) — directory matcher result should be the same
        matcher.MatchesDirectory("".AsSpan(), directoryName.AsSpan(), true).Should().Be(expectedResult);
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
        // Include
        iMatcher.MatchesDirectory("".AsSpan(), "docs".AsSpan(), false).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "src".AsSpan(), false).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "build".AsSpan(), false).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "build-output".AsSpan(), false).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "tests".AsSpan(), false).Should().BeFalse();
        iMatcher.MatchesDirectory("".AsSpan(), "SRC".AsSpan(), false).Should().BeFalse();
        // Exclude — same results for directory matcher
        iMatcher.MatchesDirectory("".AsSpan(), "docs".AsSpan(), true).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "src".AsSpan(), true).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "build".AsSpan(), true).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "build-output".AsSpan(), true).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "tests".AsSpan(), true).Should().BeFalse();
        iMatcher.MatchesDirectory("".AsSpan(), "SRC".AsSpan(), true).Should().BeFalse();
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
        // Include
        iMatcher.MatchesDirectory("".AsSpan(), "src".AsSpan(), false).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "SRC".AsSpan(), false).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "docs".AsSpan(), false).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "build".AsSpan(), false).Should().BeFalse();
        // Exclude — same results for directory matcher
        iMatcher.MatchesDirectory("".AsSpan(), "src".AsSpan(), true).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "SRC".AsSpan(), true).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "docs".AsSpan(), true).Should().BeTrue();
        iMatcher.MatchesDirectory("".AsSpan(), "build".AsSpan(), true).Should().BeFalse();
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
    public void MatchAnyFile_MatchesDirectory_ReturnsTrueWhenIncludingAndFalseWhenExcluding()
    {
        IEnumerationMatcher matcher = new MatchAnyFile(
            "any",
            MatchType.Simple,
            MatchCasing.CaseSensitive);

        // Include allows recursion into directories
        matcher.MatchesDirectory("".AsSpan(), "dir".AsSpan(), false).Should().BeTrue();
        // Exclude does not block recursion into directories for file matchers
        matcher.MatchesDirectory("".AsSpan(), "dir".AsSpan(), true).Should().BeFalse();
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
        rootPath = Paths.ChangeAlternateDirectorySeparators(rootPath);
        currentDir = Paths.ChangeAlternateDirectorySeparators(currentDir);

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
        string root = Paths.ChangeAlternateDirectorySeparators("/root");
        string foo = Paths.ChangeAlternateDirectorySeparators("/foo");

        IEnumerationMatcher matcher = new MatchAnyFile(
            "*.txt",
            root,
            MatchType.Simple,
            MatchCasing.CaseSensitive);

        // Include within root
        matcher.MatchesDirectory(root.AsSpan(), "subdir".AsSpan(), matchForExclusion: false).Should().BeTrue();

        // Exclude within root — file matcher should return false when excluding directories
        matcher.MatchesDirectory(root.AsSpan(), "subdir".AsSpan(), matchForExclusion: true).Should().BeFalse();
        matcher.DirectoryFinished();

        // Outside root, include returns false and exclude also returns false
        matcher.MatchesDirectory(
            Paths.ChangeAlternateDirectorySeparators(foo).AsSpan(),
            "subdir".AsSpan(),
            matchForExclusion: false).Should().BeFalse();
        matcher.MatchesDirectory(
            Paths.ChangeAlternateDirectorySeparators(foo).AsSpan(),
            "subdir".AsSpan(),
            matchForExclusion: true).Should().BeFalse();
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
        rootPath = Paths.ChangeAlternateDirectorySeparators(rootPath);
        currentDir = Paths.ChangeAlternateDirectorySeparators(currentDir);

        IEnumerationMatcher matcher = new MatchAnyDirectory(
            pattern,
            rootPath,
            matchType,
            matchCasing);

        // Include (matchForExclusion = false)
        matcher.MatchesDirectory(currentDir.AsSpan(), dirName.AsSpan(), false).Should().Be(expectedResult);
        // Exclude (matchForExclusion = true) — directory matcher result should be the same
        matcher.MatchesDirectory(currentDir.AsSpan(), dirName.AsSpan(), true).Should().Be(expectedResult);
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
