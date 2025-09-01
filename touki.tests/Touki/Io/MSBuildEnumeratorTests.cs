// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public class MSBuildEnumeratorTests
{
    private static string s_projectRoot = Path.GetFullPath(Path.Join(Environment.CurrentDirectory, "../../../../.."));

    [Fact]
    public void EnumerateFiles_WithGlobPattern_ReturnsMatchingFiles()
    {
        using TempFolder tempFolder = new();

        File.WriteAllText(Path.Join(tempFolder, "file1.txt"), "Content 1");
        File.WriteAllText(Path.Join(tempFolder, "file2.txt"), "Content 2");
        File.WriteAllText(Path.Join(tempFolder, "file3.md"), "Content 3");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("*.txt", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("*.cs", directory);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("**/*.cs", directory);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("?.txt", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("???.txt", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("test*.cs", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("subdir/*.txt", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("**/*.cs", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("src/**/*.cs", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("components/*.test.cs", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("*.cs", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("Program.cs", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("**/deep.txt", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("file.?", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("Test*/*.cs", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("*.*", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("**", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("file.txt", tempFolder);
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
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("**/target.cs", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().HaveCount(3);
        files.Should().Contain(f => f.EndsWith("target.cs"));

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "**/target.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithDeepSpecificDirectoryPattern_MatchesMultipleLevels()
    {
        using TempFolder tempFolder = new();

        string src = Path.Join(tempFolder, "src");
        string srcMain = Path.Join(src, "main");
        string srcUtils = Path.Join(src, "utils");
        string srcMainComponents = Path.Join(srcMain, "components");
        string tests = Path.Join(tempFolder, "tests");
        string testsUnit = Path.Join(tests, "unit");

        Directory.CreateDirectory(srcMainComponents);
        Directory.CreateDirectory(srcUtils);
        Directory.CreateDirectory(testsUnit);

        File.WriteAllText(Path.Join(tempFolder, "root.cs"), "Root");
        File.WriteAllText(Path.Join(src, "app.cs"), "App");
        File.WriteAllText(Path.Join(srcMain, "program.cs"), "Program");
        File.WriteAllText(Path.Join(srcMainComponents, "button.cs"), "Button");
        File.WriteAllText(Path.Join(srcUtils, "helper.cs"), "Helper");
        File.WriteAllText(Path.Join(tests, "base.cs"), "Base test");
        File.WriteAllText(Path.Join(testsUnit, "unit.cs"), "Unit test");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("src/main/**/*.cs", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "src/main/**/*.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithMiddleRecursivePattern_MatchesInterveningDirectories()
    {
        using TempFolder tempFolder = new();

        string libCore = Path.Join(tempFolder, "lib", "core");
        string libCoreV1 = Path.Join(libCore, "v1");
        string libCoreV2 = Path.Join(libCore, "v2");
        string libUtilsV1 = Path.Join(tempFolder, "lib", "utils", "v1");
        string appCore = Path.Join(tempFolder, "app", "core");

        Directory.CreateDirectory(libCoreV1);
        Directory.CreateDirectory(libCoreV2);
        Directory.CreateDirectory(libUtilsV1);
        Directory.CreateDirectory(appCore);

        File.WriteAllText(Path.Join(libCoreV1, "api.cs"), "API v1");
        File.WriteAllText(Path.Join(libCoreV2, "api.cs"), "API v2");
        File.WriteAllText(Path.Join(libUtilsV1, "api.cs"), "Utils API v1");
        File.WriteAllText(Path.Join(appCore, "api.cs"), "App API");
        File.WriteAllText(Path.Join(tempFolder, "api.cs"), "Root API");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("lib/**/v*/api.cs", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "lib/**/v*/api.cs");
        files.Should().BeEquivalentTo(expected);

        files.Should().BeEquivalentTo(
        [
            Path.Join("lib", "core", "v1", "api.cs"),
            Path.Join("lib", "core", "v2", "api.cs"),
            Path.Join("lib", "utils", "v1", "api.cs")
        ]);

        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithMultipleRecursiveSegments_MatchesComplexPatterns()
    {
        using TempFolder tempFolder = new();

        string srcTestsUnit = Path.Join(tempFolder, "src", "tests", "unit");
        string srcTestsIntegration = Path.Join(tempFolder, "src", "tests", "integration");
        string docsTestsUnit = Path.Join(tempFolder, "docs", "tests", "unit");
        string srcMainTests = Path.Join(tempFolder, "src", "main", "tests");

        Directory.CreateDirectory(srcTestsUnit);
        Directory.CreateDirectory(srcTestsIntegration);
        Directory.CreateDirectory(docsTestsUnit);
        Directory.CreateDirectory(srcMainTests);

        File.WriteAllText(Path.Join(srcTestsUnit, "test1.cs"), "Unit test 1");
        File.WriteAllText(Path.Join(srcTestsIntegration, "test2.cs"), "Integration test");
        File.WriteAllText(Path.Join(docsTestsUnit, "test3.cs"), "Docs test");
        File.WriteAllText(Path.Join(srcMainTests, "test4.cs"), "Main test");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("src/**/tests/**/*.cs", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().BeEquivalentTo(
        [
            Path.Join("src", "tests", "unit", "test1.cs"),
            Path.Join("src", "tests", "integration", "test2.cs"),
            Path.Join("src", "main", "tests", "test4.cs")
        ]);

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "src/**/tests/**/*.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithConsecutiveWildcardSegments_DeduplicatesCorrectly()
    {
        using TempFolder tempFolder = new();

        string level1 = Path.Join(tempFolder, "level1");
        string level2 = Path.Join(level1, "level2");
        string level3 = Path.Join(level2, "level3");

        Directory.CreateDirectory(level3);

        File.WriteAllText(Path.Join(tempFolder, "file.txt"), "Root");
        File.WriteAllText(Path.Join(level1, "file.txt"), "Level 1");
        File.WriteAllText(Path.Join(level2, "file.txt"), "Level 2");
        File.WriteAllText(Path.Join(level3, "file.txt"), "Level 3");

        // Test that consecutive ** are treated as a single **
        List<string> files1 = [];
        using MSBuildEnumerator enumerator1 = MSBuildEnumerator.Create("**/**/file.txt", tempFolder);
        while (enumerator1.MoveNext())
        {
            files1.Add(enumerator1.Current);
        }

        List<string> files2 = [];
        using MSBuildEnumerator enumerator2 = MSBuildEnumerator.Create("**/file.txt", tempFolder);
        while (enumerator2.MoveNext())
        {
            files2.Add(enumerator2.Current);
        }

        IReadOnlyList<string> expected1 = FileMatcherWrapper.GetFilesSimple(tempFolder, "**/**/file.txt");
        IReadOnlyList<string> expected2 = FileMatcherWrapper.GetFilesSimple(tempFolder, "**/file.txt");
        expected1.Should().BeEquivalentTo(expected2);

        files1.Should().BeEquivalentTo(expected1);

        // Both patterns should match the same files since consecutive ** are deduplicated
        files1.Should().HaveCount(4);
        files2.Should().HaveCount(4);
        files1.Should().BeEquivalentTo(files2);
    }

    [Fact]
    public void EnumerateFiles_WithTripleConsecutiveWildcards_HandlesDeduplication()
    {
        using TempFolder tempFolder = new();

        string deep = Path.Join(tempFolder, "a", "b", "c", "d", "e");
        Directory.CreateDirectory(deep);

        File.WriteAllText(Path.Join(tempFolder, "target.cs"), "Root");
        File.WriteAllText(Path.Join(deep, "target.cs"), "Deep");

        List<string> files1 = [];
        using MSBuildEnumerator enumerator1 = MSBuildEnumerator.Create("**/**/**/target.cs", tempFolder);
        while (enumerator1.MoveNext())
        {
            files1.Add(enumerator1.Current);
        }

        List<string> files2 = [];
        using MSBuildEnumerator enumerator2 = MSBuildEnumerator.Create("**/target.cs", tempFolder);
        while (enumerator2.MoveNext())
        {
            files2.Add(enumerator2.Current);
        }

        // Should be equivalent due to ** deduplication
        files1.Should().BeEquivalentTo(files2);
        files1.Should().HaveCount(2);

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "**/target.cs");
        files1.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithRecursiveBeforeSpecificDirectory_MatchesCorrectPaths()
    {
        using TempFolder tempFolder = new();

        string srcBin = Path.Join(tempFolder, "src", "bin");
        string testsBin = Path.Join(tempFolder, "tests", "bin");
        string docsBin = Path.Join(tempFolder, "docs", "bin");
        string nestedBin = Path.Join(tempFolder, "project", "nested", "bin");

        Directory.CreateDirectory(srcBin);
        Directory.CreateDirectory(testsBin);
        Directory.CreateDirectory(docsBin);
        Directory.CreateDirectory(nestedBin);

        File.WriteAllText(Path.Join(srcBin, "app.exe"), "Source binary");
        File.WriteAllText(Path.Join(testsBin, "test.exe"), "Test binary");
        File.WriteAllText(Path.Join(docsBin, "doc.exe"), "Doc binary");
        File.WriteAllText(Path.Join(nestedBin, "nested.exe"), "Nested binary");
        File.WriteAllText(Path.Join(tempFolder, "bin.exe"), "Root binary");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("**/bin/*.exe", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "**/bin/*.exe");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithSpecificDirectoryThenRecursive_MatchesAfterSpecificPath()
    {
        using TempFolder tempFolder = new();

        string buildDebug = Path.Join(tempFolder, "build", "debug");
        string buildRelease = Path.Join(tempFolder, "build", "release");
        string buildDebugSub = Path.Join(buildDebug, "sub");
        string buildReleaseDeep = Path.Join(buildRelease, "deep", "nested");
        string otherDebug = Path.Join(tempFolder, "other", "debug");

        Directory.CreateDirectory(buildDebugSub);
        Directory.CreateDirectory(buildReleaseDeep);
        Directory.CreateDirectory(otherDebug);

        File.WriteAllText(Path.Join(buildDebug, "debug.dll"), "Debug DLL");
        File.WriteAllText(Path.Join(buildDebugSub, "sub.dll"), "Sub DLL");
        File.WriteAllText(Path.Join(buildRelease, "release.dll"), "Release DLL");
        File.WriteAllText(Path.Join(buildReleaseDeep, "nested.dll"), "Nested DLL");
        File.WriteAllText(Path.Join(otherDebug, "other.dll"), "Other DLL");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("build/**/*.dll", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "build/**/*.dll");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_WithComplexMixedPattern_HandlesAllWildcardTypes()
    {
        using TempFolder tempFolder = new();

        string srcV1 = Path.Join(tempFolder, "src", "v1");
        string srcV2 = Path.Join(tempFolder, "src", "v2");
        string libV1 = Path.Join(tempFolder, "lib", "v1");
        string testV1Core = Path.Join(tempFolder, "test", "v1", "core");

        Directory.CreateDirectory(srcV1);
        Directory.CreateDirectory(srcV2);
        Directory.CreateDirectory(libV1);
        Directory.CreateDirectory(testV1Core);

        File.WriteAllText(Path.Join(srcV1, "a.cs"), "Source A v1");
        File.WriteAllText(Path.Join(srcV1, "b.cs"), "Source B v1");
        File.WriteAllText(Path.Join(srcV2, "a.cs"), "Source A v2");
        File.WriteAllText(Path.Join(libV1, "a.cs"), "Lib A v1");
        File.WriteAllText(Path.Join(testV1Core, "a.cs"), "Test A v1");

        // Pattern: any 3-char directory, then v1, then ** for any subdirs, then single-char filename + .cs
        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("???/v1/**/?*.cs", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(tempFolder, "???/v1/**/?*.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_ComplexMixedPattern_Issue()
    {
        using TempFolder tempFolder = new();

        string srcV1 = Path.Join(tempFolder, "src", "v1");
        Directory.CreateDirectory(srcV1);
        File.WriteAllText(Path.Join(srcV1, "a.cs"), "Source A v1");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("???/v1/**/?*.cs", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        // Should find src/v1/a.cs
        files.Should().BeEquivalentTo([Path.Join("src", "v1", "a.cs")]);
    }

    [Fact]
    public void EnumerateFiles_MatchSpec_QuestionMarkStar()
    {
        using TempFolder tempFolder = new();
        File.WriteAllText(Path.Join(tempFolder, "a.cs"), "A");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("?*.cs", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        // Should find a.cs since it matches ?*.cs
        files.Should().HaveCount(1);
        files.Should().Contain("a.cs");
    }

    [Fact]
    public void EnumerateFiles_ThreeCharDirectory()
    {
        using TempFolder tempFolder = new();

        string srcDir = Path.Join(tempFolder, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Join(srcDir, "a.cs"), "A");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("???/*.cs", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        // Should find src/a.cs since src matches ???
        files.Should().BeEquivalentTo([Path.Join("src", "a.cs")]);
    }

    [Fact]
    public void EnumerateFiles_TwoLevelDirectory()
    {
        using TempFolder tempFolder = new();

        string srcV1Dir = Path.Join(tempFolder, "src", "v1");
        Directory.CreateDirectory(srcV1Dir);
        File.WriteAllText(Path.Join(srcV1Dir, "a.cs"), "A");

        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("???/v1/*.cs", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        // Should find src/v1/a.cs
        files.Should().BeEquivalentTo([Path.Join("src", "v1", "a.cs")]);
    }

    [Fact]
    public void EnumerateFiles_SpecificIssue()
    {
        using TempFolder tempFolder = new();
        File.WriteAllText(Path.Join(tempFolder, "bin.exe"), "Root binary");

        // Test if root file bin.exe matches pattern **/bin/*.exe
        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("**/bin/*.exe", tempFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        // This should be empty - bin.exe is not inside a bin directory
        files.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateFiles_ToukiProject_SimpleCSharp()
    {
        string toukiFolder = Path.Join(s_projectRoot, "touki");
        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("**/*.cs", toukiFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(toukiFolder, "**/*.cs");
        files.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void EnumerateFiles_ToukiProject_CSharpOneExclude()
    {
        string toukiFolder = Path.Join(s_projectRoot, "touki");
        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create("**/*.cs", "bin/Debug/**;", toukiFolder);
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        List<string> excludes = ["bin/Debug/**;"];
        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(toukiFolder, "**/*.cs", excludes);
        files.Should().BeEquivalentTo(expected);
    }

    [Fact(Skip = "Local testing")]
    public void EnumerateFiles_ToukiProject_CSharpDefaultExclude()
    {
        string toukiFolder = Path.Join(s_projectRoot, "touki");
        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(
            "**/*.cs",
            "bin/Debug/**;obj/Debug/**;bin/**;obj/**/;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store",
            toukiFolder);

        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        List<string> excludes =
        [
            "bin/Debug/**;",
            "obj/Debug/**;",
            "bin/**;",
            "obj/**/;",
            "**/*.user;",
            "**/*.*proj;",
            "**/*.sln;",
            "**/*.slnx;",
            "**/*.vssscc;",
            "**/.DS_Store"
        ];

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(toukiFolder, "**/*.cs", excludes);
        files.Should().BeEquivalentTo(expected);
    }

    [Fact(Skip = "Local testing")]
    public void EnumerateFiles_RuntimeFolder_CSharpDefaultExclude()
    {
        string toukiFolder = @"n:\repos\runtime\";
        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(
            "**/*.cs",
            "bin/Debug/**;obj/Debug/**;bin/**;obj/**/;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store",
            toukiFolder);

        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        List<string> excludes =
        [
            "bin/Debug/**;",
            "obj/Debug/**;",
            "bin/**;",
            "obj/**/;",
            "**/*.user;",
            "**/*.*proj;",
            "**/*.sln;",
            "**/*.slnx;",
            "**/*.vssscc;",
            "**/.DS_Store"
        ];

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(toukiFolder, "**/*.cs", excludes);
        files.Count.Should().Be(expected.Count);
    }

    [Fact()]
    public void EnumerateFiles_RuntimeFolder_ComplexPath()
    {
        string rootFolder = @"n:\repos\runtime\";
        List<string> files = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(
            "**/src/**/*.cs",
            //            "bin/Debug/**;obj/Debug/**;bin/**;obj/**/;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store",
            rootFolder);

        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        List<string> excludes =
        [
            "bin/Debug/**;",
            "obj/Debug/**;",
            "bin/**;",
            "obj/**/;",
            "**/*.user;",
            "**/*.*proj;",
            "**/*.sln;",
            "**/*.slnx;",
            "**/*.vssscc;",
            "**/.DS_Store"
        ];

        IReadOnlyList<string> expected = FileMatcherWrapper.GetFilesSimple(rootFolder, "**/src/**/*.cs");//, excludes);
        files.Count.Should().Be(expected.Count);
    }
}
