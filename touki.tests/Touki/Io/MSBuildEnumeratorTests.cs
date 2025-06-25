// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public class MSBuildEnumeratorTests
{
    [Fact]
    public void EnumerateFiles_WithGlobPattern_ReturnsMatchingFiles()
    {
        using TempFolder tempFolder = new();

        File.WriteAllText(Path.Join(tempFolder, "file1.txt"), "Content 1");
        File.WriteAllText(Path.Join(tempFolder, "file2.txt"), "Content 2");
        File.WriteAllText(Path.Join(tempFolder, "file3.md"), "Content 3");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "*.txt");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(2);
        files.Should().Contain(f => f.EndsWith("file1.txt"));
        files.Should().Contain(f => f.EndsWith("file2.txt"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "*.txt");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_MatchesDirectoryEnumerate()
    {
        // N:\repos\touki\artifacts\x64\Release\touki.tests\net9.0
        string currentDirectory = Directory.GetCurrentDirectory();
        string directory = Path.GetFullPath(Path.Join(currentDirectory, "../../../../../Touki"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(directory, "*.cs");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(directory, "*.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_MatchesDirectoryEnumerate_Recursive()
    {
        // N:\repos\touki\artifacts\x64\Release\touki.tests\net9.0
        string currentDirectory = Directory.GetCurrentDirectory();
        string directory = Path.GetFullPath(Path.Join(currentDirectory, "../../../../.."));
        string[] expected = FileMatcherWrapper.GetFilesSimple(directory, "**/*.cs");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(directory, "**/*.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Count.Should().Be(expected.Length);
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithQuestionMarkPattern_MatchesSingleCharacter()
    {
        using TempFolder tempFolder = new();

        File.WriteAllText(Path.Join(tempFolder, "a.txt"), "Content 1");
        File.WriteAllText(Path.Join(tempFolder, "b.txt"), "Content 2");
        File.WriteAllText(Path.Join(tempFolder, "ab.txt"), "Content 3");
        File.WriteAllText(Path.Join(tempFolder, "abc.txt"), "Content 4");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "?.txt");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(2);
        files.Should().Contain(f => f.EndsWith("a.txt"));
        files.Should().Contain(f => f.EndsWith("b.txt"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "?.txt");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithMultipleQuestionMarks_MatchesExactLength()
    {
        using TempFolder tempFolder = new();

        File.WriteAllText(Path.Join(tempFolder, "ab.txt"), "Content 1");
        File.WriteAllText(Path.Join(tempFolder, "abc.txt"), "Content 2");
        File.WriteAllText(Path.Join(tempFolder, "abcd.txt"), "Content 3");
        File.WriteAllText(Path.Join(tempFolder, "a.txt"), "Content 4");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "???.txt");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(1);
        files.Should().Contain(f => f.EndsWith("abc.txt"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "???.txt");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithMixedWildcards_MatchesPattern()
    {
        using TempFolder tempFolder = new();

        File.WriteAllText(Path.Join(tempFolder, "test1.cs"), "Content 1");
        File.WriteAllText(Path.Join(tempFolder, "test22.cs"), "Content 2");
        File.WriteAllText(Path.Join(tempFolder, "test333.cs"), "Content 3");
        File.WriteAllText(Path.Join(tempFolder, "other.cs"), "Content 4");
        File.WriteAllText(Path.Join(tempFolder, "test.txt"), "Content 5");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "test*.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(3);
        files.Should().Contain(f => f.EndsWith("test1.cs"));
        files.Should().Contain(f => f.EndsWith("test22.cs"));
        files.Should().Contain(f => f.EndsWith("test333.cs"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "test*.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithSubdirectoryPattern_MatchesInSpecificDirectory()
    {
        using TempFolder tempFolder = new();

        string subDir = Path.Join(tempFolder, "subdir");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Join(tempFolder, "file1.txt"), "Content 1");
        File.WriteAllText(Path.Join(subDir, "file2.txt"), "Content 2");
        File.WriteAllText(Path.Join(subDir, "file3.txt"), "Content 3");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "subdir/*.txt");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(2);
        files.Should().Contain(f => f.EndsWith("file2.txt"));
        files.Should().Contain(f => f.EndsWith("file3.txt"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "subdir/*.txt");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithDeepRecursivePattern_MatchesAllSubdirectories()
    {
        using TempFolder tempFolder = new();

        string level1 = Path.Join(tempFolder, "level1");
        string level2 = Path.Join(level1, "level2");
        string level3 = Path.Join(level2, "level3");

        Directory.CreateDirectory(level3);

        File.WriteAllText(Path.Join(tempFolder, "root.cs"), "Root");
        File.WriteAllText(Path.Join(level1, "level1.cs"), "Level1");
        File.WriteAllText(Path.Join(level2, "level2.cs"), "Level2");
        File.WriteAllText(Path.Join(level3, "level3.cs"), "Level3");
        File.WriteAllText(Path.Join(level2, "other.txt"), "Other");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "**/*.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(4);
        files.Should().Contain(f => f.EndsWith("root.cs"));
        files.Should().Contain(f => f.Contains("level1.cs"));
        files.Should().Contain(f => f.Contains("level2.cs"));
        files.Should().Contain(f => f.Contains("level3.cs"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "**/*.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithSpecificDirectoryRecursivePattern_MatchesFromSpecificLevel()
    {
        using TempFolder tempFolder = new();

        string src = Path.Join(tempFolder, "src");
        string tests = Path.Join(tempFolder, "tests");
        string srcSub = Path.Join(src, "sub");

        Directory.CreateDirectory(srcSub);
        Directory.CreateDirectory(tests);

        File.WriteAllText(Path.Join(tempFolder, "root.cs"), "Root");
        File.WriteAllText(Path.Join(src, "main.cs"), "Main");
        File.WriteAllText(Path.Join(srcSub, "helper.cs"), "Helper");
        File.WriteAllText(Path.Join(tests, "test.cs"), "Test");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "src/**/*.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(2);
        files.Should().Contain(f => f.Contains("main.cs"));
        files.Should().Contain(f => f.Contains("helper.cs"));
        files.Should().NotContain(f => f.Contains("root.cs"));
        files.Should().NotContain(f => f.Contains("test.cs"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "src/**/*.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithComplexPattern_MatchesCombinedWildcards()
    {
        using TempFolder tempFolder = new();

        string subDir = Path.Join(tempFolder, "components");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Join(subDir, "Button.cs"), "Component");
        File.WriteAllText(Path.Join(subDir, "Button.test.cs"), "Test");
        File.WriteAllText(Path.Join(subDir, "Input.cs"), "Component");
        File.WriteAllText(Path.Join(subDir, "Input.test.cs"), "Test");
        File.WriteAllText(Path.Join(subDir, "Dialog.tsx"), "React");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "components/*.test.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(2);
        files.Should().Contain(f => f.Contains("Button.test.cs"));
        files.Should().Contain(f => f.Contains("Input.test.cs"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "components/*.test.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithNoMatches_ReturnsEmpty()
    {
        using TempFolder tempFolder = new();

        File.WriteAllText(Path.Join(tempFolder, "file1.txt"), "Content 1");
        File.WriteAllText(Path.Join(tempFolder, "file2.md"), "Content 2");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "*.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().BeEmpty();

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "*.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithExactFileName_MatchesSpecificFile()
    {
        using TempFolder tempFolder = new();

        File.WriteAllText(Path.Join(tempFolder, "Program.cs"), "Content 1");
        File.WriteAllText(Path.Join(tempFolder, "Program.txt"), "Content 2");
        File.WriteAllText(Path.Join(tempFolder, "Other.cs"), "Content 3");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "Program.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(1);
        files.Should().Contain(f => f.EndsWith("Program.cs"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "Program.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithNestedRecursivePattern_MatchesDeepPaths()
    {
        using TempFolder tempFolder = new();

        string nested = Path.Join(tempFolder, "a", "b", "c", "d");
        Directory.CreateDirectory(nested);

        File.WriteAllText(Path.Join(tempFolder, "root.txt"), "Root");
        File.WriteAllText(Path.Join(nested, "deep.txt"), "Deep");
        File.WriteAllText(Path.Join(Path.Join(tempFolder, "a"), "intermediate.txt"), "Intermediate");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "**/deep.txt");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(1);
        files.Should().Contain(f => f.EndsWith("deep.txt"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "**/deep.txt");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithMultipleExtensionPattern_MatchesVariousExtensions()
    {
        using TempFolder tempFolder = new();

        File.WriteAllText(Path.Join(tempFolder, "file.c"), "C Code");
        File.WriteAllText(Path.Join(tempFolder, "file.h"), "Header");
        File.WriteAllText(Path.Join(tempFolder, "file.cpp"), "C++ Code");
        File.WriteAllText(Path.Join(tempFolder, "file.txt"), "Text");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "file.?");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(2);
        files.Should().Contain(f => f.EndsWith("file.c"));
        files.Should().Contain(f => f.EndsWith("file.h"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "file.?");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithDirectoryNamePattern_MatchesDirectoryNames()
    {
        using TempFolder tempFolder = new();

        string testDir = Path.Join(tempFolder, "Test");
        string testsDir = Path.Join(tempFolder, "Tests");
        string otherDir = Path.Join(tempFolder, "Other");

        Directory.CreateDirectory(testDir);
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(otherDir);

        File.WriteAllText(Path.Join(testDir, "file.cs"), "Test File");
        File.WriteAllText(Path.Join(testsDir, "file.cs"), "Tests File");
        File.WriteAllText(Path.Join(otherDir, "file.cs"), "Other File");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "Test*/*.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "Test*/*.cs");

        files.Should().HaveCount(2);
        files.Should().Contain(f => f.Contains("Test") && f.EndsWith("file.cs"));
        files.Should().Contain(f => f.Contains("Tests") && f.EndsWith("file.cs"));

        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithEmptyDirectory_ReturnsEmpty()
    {
        using TempFolder tempFolder = new();

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "*.*");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().BeEmpty();

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "*.*");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithStarStarAtEnd_MatchesAllFilesRecursively()
    {
        using TempFolder tempFolder = new();

        string subDir = Path.Join(tempFolder, "sub");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Join(tempFolder, "root.txt"), "Root");
        File.WriteAllText(Path.Join(subDir, "nested.txt"), "Nested");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "**");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "**");

        files.Should().HaveCount(2);
        files.Should().Contain(f => f.EndsWith("root.txt"));
        files.Should().Contain(f => f.Contains("sub\\nested.txt"));

        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithCaseVariations_MatchesBasedOnPlatform()
    {
        using TempFolder tempFolder = new();

        File.WriteAllText(Path.Join(tempFolder, "file.txt"), "Lower");
        File.WriteAllText(Path.Join(tempFolder, "FILE.TXT"), "Upper");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "file.txt");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        // On Windows, should match both due to case insensitivity
        // On Linux, should only match the exact case
        bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        if (isWindows)
        {
            files.Should().HaveCountGreaterOrEqualTo(1);
        }
        else
        {
            files.Should().HaveCount(1);
            files.Should().Contain(f => f.EndsWith("file.txt"));
        }

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "file.txt");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithDoubleStarAtStart_MatchesFromAnyLevel()
    {
        using TempFolder tempFolder = new();

        string level1 = Path.Join(tempFolder, "level1");
        string level2 = Path.Join(level1, "level2");
        Directory.CreateDirectory(level2);

        File.WriteAllText(Path.Join(tempFolder, "target.cs"), "Root target");
        File.WriteAllText(Path.Join(level1, "target.cs"), "Level1 target");
        File.WriteAllText(Path.Join(level2, "target.cs"), "Level2 target");
        File.WriteAllText(Path.Join(level2, "other.txt"), "Other file");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(tempFolder, "**/target.cs");
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(3);
        files.Should().Contain(f => f.EndsWith("target.cs"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "**/target.cs");
        files.Should().BeEquivalentTo(expected);
    }
}
