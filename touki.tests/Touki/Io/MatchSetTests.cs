// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public class MatchSetTests
{
    [Fact]
    public void Constructor_NullMatcher_ThrowsArgumentNullException()
    {
        Action action = () => new MatchSet(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddInclude_NullMatcher_ThrowsArgumentNullException()
    {
        using MatchSet matchSet = new(new MockMatcher());
        Action action = () => matchSet.AddInclude(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddExclude_NullMatcher_ThrowsArgumentNullException()
    {
        using MatchSet matchSet = new(new MockMatcher());
        Action action = () => matchSet.AddExclude(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MatchesDirectory_SingleInclude_Matches()
    {
        using IEnumerationMatcher matcher = new MatchSet(
            new MatchAnyDirectory("src", MatchType.Simple, MatchCasing.CaseSensitive));

        matcher.MatchesDirectory(string.Empty, "src").Should().BeTrue();
        matcher.MatchesDirectory(string.Empty, "docs").Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_SingleInclude_Matches()
    {
        using IEnumerationMatcher matcher = new MatchSet(
            new MatchAnyFile("*.txt", MatchType.Simple, MatchCasing.CaseSensitive));

        matcher.MatchesFile(string.Empty, "file.txt").Should().BeTrue();
        matcher.MatchesFile(string.Empty, "file.doc").Should().BeFalse();
    }

    [Fact]
    public void MatchesDirectory_MultipleIncludes_MatchesAny()
    {
        using MatchSet matcher = new(new MatchAnyDirectory("src", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddInclude(new MatchAnyDirectory("docs", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesDirectory(string.Empty, "src").Should().BeTrue();
        iMatcher.MatchesDirectory(string.Empty, "docs").Should().BeTrue();
        iMatcher.MatchesDirectory(string.Empty, "build").Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_MultipleIncludes_MatchesAny()
    {
        using MatchSet matcher = new(new MatchAnyFile("*.txt", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddInclude(new MatchAnyFile("*.log", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesFile(string.Empty, "file.txt").Should().BeTrue();
        iMatcher.MatchesFile(string.Empty, "file.log").Should().BeTrue();
        iMatcher.MatchesFile(string.Empty, "file.doc").Should().BeFalse();
    }

    [Fact]
    public void MatchesDirectory_ExcludeOverridesInclude()
    {
        using MatchSet matcher = new(new MatchAnyDirectory("src", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddExclude(new MatchAnyDirectory("src", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesDirectory(string.Empty, "src").Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_ExcludeOverridesInclude()
    {
        using MatchSet matcher = new(new MatchAnyFile("*.txt", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddExclude(new MatchAnyFile("important.txt", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesFile(string.Empty, "file.txt").Should().BeTrue();
        iMatcher.MatchesFile(string.Empty, "important.txt").Should().BeFalse();
    }

    [Fact]
    public void MatchesDirectory_ExcludeWithoutInclude_NoMatch()
    {
        using MatchSet matcher = new(new MatchAnyDirectory("src", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddExclude(new MatchAnyDirectory("docs", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesDirectory(string.Empty, "src").Should().BeTrue();
        iMatcher.MatchesDirectory(string.Empty, "docs").Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_ExcludeWithoutInclude_NoMatch()
    {
        using MatchSet matcher = new(new MatchAnyFile("*.txt", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddExclude(new MatchAnyFile("*.log", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesFile(string.Empty, "file.txt").Should().BeTrue();
        iMatcher.MatchesFile(string.Empty, "file.log").Should().BeFalse();
    }

    [Fact]
    public void MatchesDirectory_NoIncludesMatch_ReturnsFalse()
    {
        using MatchSet matcher = new(new MatchAnyDirectory("src", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddInclude(new MatchAnyDirectory("docs", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesDirectory(string.Empty, "build").Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_NoIncludesMatch_ReturnsFalse()
    {
        using MatchSet matcher = new(new MatchAnyFile("*.txt", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddInclude(new MatchAnyFile("*.log", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesFile(string.Empty, "file.doc").Should().BeFalse();
    }

    [Fact]
    public void DirectoryFinished_CallsDirectoryFinishedOnAllMatchers()
    {
        MockMatcher include1 = new();
        MockMatcher include2 = new();
        MockMatcher exclude1 = new();
        MockMatcher exclude2 = new();

        using MatchSet matcher = new(include1);
        matcher.AddInclude(include2);
        matcher.AddExclude(exclude1);
        matcher.AddExclude(exclude2);

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.DirectoryFinished();

        include1.DirectoryFinishedCount.Should().Be(1);
        include2.DirectoryFinishedCount.Should().Be(1);
        exclude1.DirectoryFinishedCount.Should().Be(1);
        exclude2.DirectoryFinishedCount.Should().Be(1);
    }

    [Fact]
    public void DirectoryFinished_NoExcludes_CallsDirectoryFinishedOnIncludes()
    {
        MockMatcher include1 = new();
        MockMatcher include2 = new();

        using MatchSet matcher = new(include1);
        matcher.AddInclude(include2);

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.DirectoryFinished();

        include1.DirectoryFinishedCount.Should().Be(1);
        include2.DirectoryFinishedCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_DoesNotDisposeMatchers()
    {
        MockMatcher include1 = new();
        MockMatcher include2 = new();
        MockMatcher exclude1 = new();
        MockMatcher exclude2 = new();

        MatchSet matcher = new(include1);
        matcher.AddInclude(include2);
        matcher.AddExclude(exclude1);
        matcher.AddExclude(exclude2);

        matcher.Dispose();

        include1.IsDisposed.Should().BeFalse();
        include2.IsDisposed.Should().BeFalse();
        exclude1.IsDisposed.Should().BeFalse();
        exclude2.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void MatchesDirectory_ComplexScenario_WorksCorrectly()
    {
        using MatchSet matcher = new(new MatchAnyDirectory("src", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddInclude(new MatchAnyDirectory("test*", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddExclude(new MatchAnyDirectory("test-utils", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesDirectory(string.Empty, "src").Should().BeTrue();
        iMatcher.MatchesDirectory(string.Empty, "tests").Should().BeTrue();
        iMatcher.MatchesDirectory(string.Empty, "test-utils").Should().BeFalse();
        iMatcher.MatchesDirectory(string.Empty, "docs").Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_ComplexScenario_WorksCorrectly()
    {
        using MatchSet matcher = new(new MatchAnyFile("*.cs", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddInclude(new MatchAnyFile("*.txt", MatchType.Simple, MatchCasing.CaseSensitive));
        matcher.AddExclude(new MatchAnyFile("*Test*", MatchType.Simple, MatchCasing.CaseSensitive));

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesFile(string.Empty, "Program.cs").Should().BeTrue();
        iMatcher.MatchesFile(string.Empty, "readme.txt").Should().BeTrue();
        iMatcher.MatchesFile(string.Empty, "TestFile.cs").Should().BeFalse();
        iMatcher.MatchesFile(string.Empty, "Test.txt").Should().BeFalse();
        iMatcher.MatchesFile(string.Empty, "file.doc").Should().BeFalse();
    }

    [Fact]
    public void MatchesDirectory_WithMockMatchers_EvaluatesCorrectly()
    {
        MockMatcher include1 = new() { OnMatchesDirectory = (_, name) => name == "include1" };
        MockMatcher include2 = new() { OnMatchesDirectory = (_, name) => name == "include2" };
        MockMatcher exclude1 = new() { OnMatchesDirectory = (_, name) => name == "exclude1" };
        MockMatcher exclude2 = new() { OnMatchesDirectory = (_, name) => name == "include1" }; // Excludes include1

        using MatchSet matcher = new(include1);
        matcher.AddInclude(include2);
        matcher.AddExclude(exclude1);
        matcher.AddExclude(exclude2);

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesDirectory(string.Empty, "include1").Should().BeFalse(); // Excluded by exclude2
        iMatcher.MatchesDirectory(string.Empty, "include2").Should().BeTrue();
        iMatcher.MatchesDirectory(string.Empty, "exclude1").Should().BeFalse();
        iMatcher.MatchesDirectory(string.Empty, "other").Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_WithMockMatchers_EvaluatesCorrectly()
    {
        MockMatcher include1 = new() { OnMatchesFile = (_, name) => name == "file1.txt" };
        MockMatcher include2 = new() { OnMatchesFile = (_, name) => name == "file2.txt" };
        MockMatcher exclude1 = new() { OnMatchesFile = (_, name) => name == "excluded.txt" };
        MockMatcher exclude2 = new() { OnMatchesFile = (_, name) => name == "file1.txt" }; // Excludes file1.txt

        using MatchSet matcher = new(include1);
        matcher.AddInclude(include2);
        matcher.AddExclude(exclude1);
        matcher.AddExclude(exclude2);

        IEnumerationMatcher iMatcher = matcher;
        iMatcher.MatchesFile(string.Empty, "file1.txt").Should().BeFalse(); // Excluded by exclude2
        iMatcher.MatchesFile(string.Empty, "file2.txt").Should().BeTrue();
        iMatcher.MatchesFile(string.Empty, "excluded.txt").Should().BeFalse();
        iMatcher.MatchesFile(string.Empty, "other.txt").Should().BeFalse();
    }

    [Fact]
    public void MatchesDirectory_EmptyStrings_HandledCorrectly()
    {
        // Should never see an empty string, just make sure it doesn't throw.
        using NoAssertContext context = new();
        using IEnumerationMatcher matcher = new MatchSet(
            new MatchAnyDirectory("", MatchType.Simple, MatchCasing.CaseSensitive));

        matcher.MatchesDirectory(string.Empty, string.Empty).Should().BeFalse();
        matcher.MatchesDirectory(string.Empty, "anydir").Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_EmptyStrings_HandledCorrectly()
    {
        // Should never see an empty string, just make sure it doesn't throw.
        using NoAssertContext context = new();
        using IEnumerationMatcher matcher = new MatchSet(
            new MatchAnyFile("", MatchType.Simple, MatchCasing.CaseSensitive));

        matcher.MatchesFile(string.Empty, string.Empty).Should().BeFalse();
        matcher.MatchesFile(string.Empty, "anyfile.txt").Should().BeFalse();
    }

    private sealed class MockMatcher : IEnumerationMatcher
    {
        public int DirectoryFinishedCount { get; private set; }
        public bool IsDisposed { get; private set; }
        public Func<string, string, bool> OnMatchesDirectory { get; set; } = (_, _) => false;
        public Func<string, string, bool> OnMatchesFile { get; set; } = (_, _) => false;

        public void DirectoryFinished() => DirectoryFinishedCount++;
        public void Dispose() => IsDisposed = true;
        public bool MatchesDirectory(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> directoryName) =>
            OnMatchesDirectory(currentDirectory.ToString(), directoryName.ToString());
        public bool MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName) =>
            OnMatchesFile(currentDirectory.ToString(), fileName.ToString());
    }
}
