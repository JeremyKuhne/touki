// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Characterizes the filename-policy boundary between MSBuild's physical filesystem search
///  and its in-memory matcher.
/// </summary>
[TestClass]
public class FileMatcherFileNameOracleTests
{
    private static void CreateFixture(string root)
    {
        string[] fileNames =
        [
            "README",
            "LICENSE",
            "A",
            "AB",
            "ABCD",
            "ab",
            "aX.Yb",
            "LICENSE.txt",
            "notes.txt",
            "source.cs",
            "page.htm",
            "page.html",
            "file.tx",
            "file.txt",
            "f.txt",
            "fo.txt",
            "foo.txt"
        ];

        foreach (string fileName in fileNames)
        {
            File.WriteAllText(Path.Combine(root, fileName), string.Empty);
        }

        string nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "README"), string.Empty);
        File.WriteAllText(Path.Combine(nested, "LICENSE"), string.Empty);
        File.WriteAllText(Path.Combine(nested, "LICENSE.txt"), string.Empty);
    }

    [TestMethod]
    [DataRow("*.*", new[] { "README", "LICENSE" })]
    [DataRow("LICENSE.*", new[] { "LICENSE", "LICENSE.txt" })]
    [DataRow("*.", new[] { "README", "LICENSE" })]
    public void GetFiles_WindowsDosWildcardPatterns_IncludeExtensionlessFiles(
        string pattern,
        string[] expectedExtensionless)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Assert.Inconclusive("DOS wildcard filesystem behavior is Windows-only.");
        }

        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        string[] files = FileMatcherWrapper.GetFilesSimple(tempFolder.TempPath, pattern);

        files.Select(Path.GetFileName).Should().Contain(expectedExtensionless);
    }

    [TestMethod]
    [DataRow("*.*", "README", false)]
    [DataRow("LICENSE.*", "LICENSE", false)]
    [DataRow("*.", "README", false)]
    [DataRow("*.htm", "page.html", false)]
    [DataRow("*.htm", "page.htm", true)]
    public void IsMatch_InMemoryMatcher_UsesLogicalWildcardSemantics(
        string pattern,
        string input,
        bool expected) =>
        FileMatcherWrapper.IsMatch(input, pattern).Should().Be(expected);

    [TestMethod]
    [DataRow("*")]
    [DataRow("*.*")]
    [DataRow("LICENSE.*")]
    [DataRow("*.")]
    [DataRow("*.txt")]
    [DataRow("*.htm")]
    [DataRow("file.tx?")]
    [DataRow("???.txt")]
    [DataRow("?.")]
    [DataRow("??.")]
    [DataRow("*..")]
    [DataRow("*.?")]
    [DataRow("file.??")]
    [DataRow("a*.*b")]
    [DataRow("license.*")]
    [DataRow("**/*.*")]
    [DataRow("**/LICENSE.*")]
    [DataRow("**/*.")]
    [DataRow("**/license.*")]
    public void EnumerateFiles_FileNamePattern_MatchesFileMatcher(string pattern)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        List<string> actual = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(new(pattern, tempFolder.TempPath));
        while (enumerator.MoveNext())
        {
            actual.Add(enumerator.Current);
        }

        string[] expected = FileMatcherWrapper.GetFilesSimple(tempFolder.TempPath, pattern);
        actual.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    [DataRow("*.htm")]
    [DataRow("file.tx?")]
    [DataRow("???.txt")]
    public void EnumerateFiles_Win32MatchType_StillAppliesMSBuildPostFilter(string pattern)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);
        EnumerationOptions options = new()
        {
            MatchType = MatchType.Win32,
            MatchCasing = MatchCasing.PlatformDefault,
            RecurseSubdirectories = true
        };

        List<string> actual = [];
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(
            new(pattern, tempFolder.TempPath, enumerationOptions: options));
        while (enumerator.MoveNext())
        {
            actual.Add(enumerator.Current);
        }

        string[] expected = FileMatcherWrapper.GetFilesSimple(tempFolder.TempPath, pattern);
        actual.Should().BeEquivalentTo(expected);
    }
}