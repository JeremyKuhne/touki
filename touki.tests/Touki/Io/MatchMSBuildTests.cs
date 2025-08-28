// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Xunit.Abstractions;

namespace Touki.Io;

public class MatchMSBuildTests(ITestOutputHelper _log)
{
    private static MatchMSBuild CreateSpec(string pattern, string root)
    {
        var spec = new MSBuildSpecification(pattern);
        string rootDirectory = Path.GetFullPath(root);

        MatchCasing casing = Paths.OSDefaultMatchCasing;
        MatchType matchType = MatchType.Simple;

        return new MatchMSBuild(spec, rootDirectory, matchType, casing);
    }

    private IEnumerable<string> Enumerate(string pattern, string root, params string[] files)
    {
        MatchMSBuild spec = CreateSpec(pattern, root);
        EnumeratorMock enumerator = new(root, files, spec, _log);
        return enumerator.Enumerate().Select(result => result.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    public static TheoryData<string, string[], string[]> EnumerationData()
    {
        bool insensitive = Paths.OSDefaultMatchCasing == MatchCasing.CaseInsensitive;

        return new TheoryData<string, string[], string[]>()
        {
            { "*.txt", ["file1.txt", "file2.txt", "file3.md"], ["file1.txt", "file2.txt"] },
            { "*.cs", ["a.cs", "b.cs", "c.txt", "d.cs"], ["a.cs", "b.cs", "d.cs"] },
            { "**/*.cs", ["root.cs", "sub/a.cs", "sub/b.txt", "sub/sub2/c.cs"], ["root.cs", "sub/a.cs", "sub/sub2/c.cs"] },
            { "?.txt", ["a.txt", "b.txt", "ab.txt", "abc.txt"], ["a.txt", "b.txt"] },
            { "???.txt", ["ab.txt", "abc.txt", "abcd.txt", "a.txt"], ["abc.txt"] },
            { "test*.cs", ["test1.cs", "test22.cs", "test333.cs", "other.cs", "test.txt"], ["test1.cs", "test22.cs", "test333.cs"] },
            {
                "**/*.cs",
                ["root.cs", "level1/level1.cs", "level1/level2/level2.cs", "level1/level2/level3/level3.cs", "level1/level2/other.txt"],
                ["root.cs", "level1/level1.cs", "level1/level2/level2.cs", "level1/level2/level3/level3.cs"]
            },
            { "*.cs", ["file1.txt", "file2.md"], [] },
            { "Program.cs", ["Program.cs", "Program.txt", "Other.cs"], ["Program.cs"] },
            { "**/deep.txt", ["root.txt", "a/b/c/d/deep.txt", "a/intermediate.txt"], ["a/b/c/d/deep.txt"] },
            { "file.?", ["file.c", "file.h", "file.cpp", "file.txt"], ["file.c", "file.h"] },
            { "Test*/*.cs", ["Test/file.cs", "Tests/file.cs", "Other/file.cs"], ["Test/file.cs", "Tests/file.cs"] },
            { "*.*", [], [] },
            { "**", ["root.txt", "sub/nested.txt"], ["root.txt", "sub/nested.txt"] },
            { "file.txt", ["file.txt", "FILE.TXT"], insensitive ? ["file.txt", "FILE.TXT"] : ["file.txt"] },
            {
                "**/target.cs",
                ["target.cs", "level1/target.cs", "level1/level2/target.cs", "level1/level2/other.txt"],
                ["target.cs", "level1/target.cs", "level1/level2/target.cs"]
            },
            { "**/target.cs", ["target.cs", "a/b/c/d/e/target.cs"], ["target.cs", "a/b/c/d/e/target.cs"] },
            {
                "**/bin/*.exe",
                ["src/bin/app.exe", "tests/bin/test.exe", "docs/bin/doc.exe", "project/nested/bin/nested.exe", "bin.exe"],
                ["src/bin/app.exe", "tests/bin/test.exe", "docs/bin/doc.exe", "project/nested/bin/nested.exe"]
            },
            {
                "???/v1/**/?*.cs",
                ["src/v1/a.cs", "src/v1/b.cs", "src/v2/a.cs", "lib/v1/a.cs", "test/v1/core/a.cs"],
                ["src/v1/a.cs", "src/v1/b.cs", "lib/v1/a.cs"]
            },
            { "???/v1/**/?*.cs", ["src/v1/a.cs"], ["src/v1/a.cs"] },
            { "?*.cs", ["a.cs"], ["a.cs"] },
            { "???/*.cs", ["src/a.cs"], ["src/a.cs"] },
            { "???/v1/*.cs", ["src/v1/a.cs"], ["src/v1/a.cs"] },
            { "**/bin/*.exe", ["bin.exe"], [] },
        };
    }

    [Theory]
    [MemberData(nameof(EnumerationData))]
    public void SpecEnumeration(string pattern, string[] files, string[] expected)
    {
        string root = Path.Join(Path.GetTempPath(), "SpecEnumerationTests");
        IEnumerable<string> results = Enumerate(pattern, root, files);
        results.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("C:/temp/*.txt", "C:/temp", MatchType.Simple, MatchCasing.CaseInsensitive)]
    [InlineData("C:/projects/**/*.cs", "C:/projects", MatchType.Simple, MatchCasing.CaseSensitive)]
    [InlineData("C:/src/test/**/bin/*.dll", "C:/src/test", MatchType.Win32, MatchCasing.CaseInsensitive)]
    public void Constructor_InitializesCorrectFields(string fullPathSpec, string startDirectory, MatchType matchType, MatchCasing matchCasing)
    {
        // Create the spec with the provided parameters
        MatchMSBuild spec = new(fullPathSpec, startDirectory, matchType, matchCasing);

        // Access internal state through TestAccessor
        dynamic accessor = spec.TestAccessor().Dynamic;

        // Verify the internal state
        Assert.Equal(matchType, accessor._matchType);
        Assert.Equal(matchCasing, accessor._matchCasing);
        Assert.Equal(startDirectory.Length, accessor._startDirectoryLength);
    }

    [Theory]
    [InlineData("*.txt", false, false)]
    [InlineData("**/*.txt", true, true)]
    [InlineData("**", true, true)]
    [InlineData("**/bin/*.dll", false, false)]
    public void Constructor_SetsRecursionFlags(string msbuildIncludeSpec, bool expectedAlwaysRecurse, bool expectedEndsInAnyDirectory)
    {
        // Use a common directory and matching options
        string startDirectory = "C:/temp";
        MatchMSBuild spec = new(
            msbuildIncludeSpec.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
            startDirectory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
            MatchType.Simple,
            MatchCasing.CaseInsensitive);

        // Verify public properties
        spec.AlwaysRecurse.Should().Be(expectedAlwaysRecurse);
        spec.EndsInAnyDirectory.Should().Be(expectedEndsInAnyDirectory);
    }

    [Fact]
    public void CacheInvalidation_WorksCorrectly()
    {
        MatchMSBuild spec = new("C:/temp/*.txt", "C:/temp", MatchType.Simple, MatchCasing.CaseInsensitive);

        dynamic accessor = spec.TestAccessor().Dynamic;

        // Initially the cache should not be valid
        Assert.False(accessor._cacheValid);

        // We can set up cache state manually through the accessor
        accessor._cacheValid = true;
        accessor._cachedFullyMatches = true;

        // Invalidate the cache
        spec.DirectoryFinished();

        // Verify cache is invalidated
        Assert.False(accessor._cacheValid);
    }

    [Fact]
    public void Blah()
    {
        var Directory = @"d:\code\dotnet-sdk\";
        var includeSpec = new MSBuildSpecification("**/src/**/*.cs");
        string[] s_excludeSpecs = ["**/obj/**", "**/bin/**", "**/TestData/**", "**/Generated/**" ];
        var matchSet = new MatchSet(new MatchMSBuild(includeSpec, Directory, MatchType.Simple, MatchCasing.PlatformDefault));
        foreach (var e in s_excludeSpecs)
        {
            matchSet.AddExclude(new MatchMSBuild(new MSBuildSpecification(e), Directory, MatchType.Simple, MatchCasing.PlatformDefault, MatchMSBuild.SpecMode.Exclude));
        }

        using MatchEnumerator enumerator = new MatchEnumerator(Directory, matchSet, static (ref FileSystemEntry fse) => fse.FileName.ToString());
        List<string> results = new();
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }
    }
}
