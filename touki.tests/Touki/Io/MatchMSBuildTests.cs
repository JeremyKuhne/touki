// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

[TestClass]
public class MatchMSBuildTests
{
    private static MatchMSBuild CreateSpec(string pattern, string root)
    {
        string rootDirectory = Path.GetFullPath(root);
        string fullPathSpec = Path.GetFullPath(pattern, rootDirectory);

        MSBuildSpecification specification = new(fullPathSpec);

        MatchCasing casing = Paths.OSDefaultMatchCasing;
        MatchType matchType = MatchType.Simple;

        return new MatchMSBuild(specification, matchType, casing);
    }

    private static IEnumerable<string> Enumerate(string pattern, string root, params string[] files)
    {
        MatchMSBuild spec = CreateSpec(pattern, root);
        EnumeratorMock enumerator = new(root, files, spec);
        return enumerator.Enumerate().Select(result => result.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    public static IEnumerable<(string, string[], string[])> EnumerationData()
    {
        bool insensitive = Paths.OSDefaultMatchCasing == MatchCasing.CaseInsensitive;

        yield return ("*.txt", ["file1.txt", "file2.txt", "file3.md"], ["file1.txt", "file2.txt"]);
        yield return ("*.cs", ["a.cs", "b.cs", "c.txt", "d.cs"], ["a.cs", "b.cs", "d.cs"]);
        yield return ("**/*.cs", ["root.cs", "sub/a.cs", "sub/b.txt", "sub/sub2/c.cs"], ["root.cs", "sub/a.cs", "sub/sub2/c.cs"]);
        yield return ("?.txt", ["a.txt", "b.txt", "ab.txt", "abc.txt"], ["a.txt", "b.txt"]);
        yield return ("???.txt", ["ab.txt", "abc.txt", "abcd.txt", "a.txt"], ["abc.txt"]);
        yield return ("test*.cs", ["test1.cs", "test22.cs", "test333.cs", "other.cs", "test.txt"], ["test1.cs", "test22.cs", "test333.cs"]);
        yield return (
            "**/*.cs",
            ["root.cs", "level1/level1.cs", "level1/level2/level2.cs", "level1/level2/level3/level3.cs", "level1/level2/other.txt"],
            ["root.cs", "level1/level1.cs", "level1/level2/level2.cs", "level1/level2/level3/level3.cs"]);
        yield return ("*.cs", ["file1.txt", "file2.md"], []);
        yield return ("Program.cs", ["Program.cs", "Program.txt", "Other.cs"], ["Program.cs"]);
        yield return ("**/deep.txt", ["root.txt", "a/b/c/d/deep.txt", "a/intermediate.txt"], ["a/b/c/d/deep.txt"]);
        yield return ("file.?", ["file.c", "file.h", "file.cpp", "file.txt"], ["file.c", "file.h"]);
        yield return ("Test*/*.cs", ["Test/file.cs", "Tests/file.cs", "Other/file.cs"], ["Test/file.cs", "Tests/file.cs"]);
        yield return ("*.*", [], []);
        yield return ("**", ["root.txt", "sub/nested.txt"], ["root.txt", "sub/nested.txt"]);
        yield return ("file.txt", ["file.txt", "FILE.TXT"], insensitive ? ["file.txt", "FILE.TXT"] : ["file.txt"]);
        yield return (
            "**/target.cs",
            ["target.cs", "level1/target.cs", "level1/level2/target.cs", "level1/level2/other.txt"],
            ["target.cs", "level1/target.cs", "level1/level2/target.cs"]);
        yield return ("**/target.cs", ["target.cs", "a/b/c/d/e/target.cs"], ["target.cs", "a/b/c/d/e/target.cs"]);
        yield return (
            "**/bin/*.exe",
            ["src/bin/app.exe", "tests/bin/test.exe", "docs/bin/doc.exe", "project/nested/bin/nested.exe", "bin.exe"],
            ["src/bin/app.exe", "tests/bin/test.exe", "docs/bin/doc.exe", "project/nested/bin/nested.exe"]);
        yield return (
            "???/v1/**/?*.cs",
            ["src/v1/a.cs", "src/v1/b.cs", "src/v2/a.cs", "lib/v1/a.cs", "test/v1/core/a.cs"],
            ["src/v1/a.cs", "src/v1/b.cs", "lib/v1/a.cs"]);
        yield return ("???/v1/**/?*.cs", ["src/v1/a.cs"], ["src/v1/a.cs"]);
        yield return ("?*.cs", ["a.cs"], ["a.cs"]);
        yield return ("???/*.cs", ["src/a.cs"], ["src/a.cs"]);
        yield return ("???/v1/*.cs", ["src/v1/a.cs"], ["src/v1/a.cs"]);
        yield return ("**/bin/*.exe", ["bin.exe"], []);
        yield return (
            "**/src/**/*.cs",
            ["src/tests/tracing/runtimeeventsource/NativeRuntimeEventSourceTest.cs"],
            ["src/tests/tracing/runtimeeventsource/NativeRuntimeEventSourceTest.cs"]);
    }

    [TestMethod]
    [DynamicData(nameof(EnumerationData))]
    public void SpecEnumeration(string pattern, string[] files, string[] expected)
    {
        string root = Path.Join(Path.GetTempPath(), "SpecEnumerationTests");
        IEnumerable<string> results = Enumerate(pattern, root, files);
        results.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    [DataRow("C:/temp/*.txt", "C:/temp", MatchType.Simple, MatchCasing.CaseInsensitive)]
    [DataRow("C:/projects/**/*.cs", "C:/projects", MatchType.Simple, MatchCasing.CaseSensitive)]
    [DataRow("C:/src/test/**/bin/*.dll", "C:/src/test", MatchType.Win32, MatchCasing.CaseInsensitive)]
    public void Constructor_InitializesCorrectFields(string fullPathSpec, string startDirectory, MatchType matchType, MatchCasing matchCasing)
    {
        // Create the spec with the provided parameters
        MSBuildSpecification specification = new(fullPathSpec.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        MatchMSBuild match = new(specification, matchType, matchCasing);

        // Access internal state through TestAccessor
        dynamic accessor = match.TestAccessor.Dynamic;

        // Verify the internal state
        ((MatchType)accessor._matchType).Should().Be(matchType);
        ((MatchCasing)accessor._matchCasing).Should().Be(matchCasing);
        ((int)accessor._startDirectoryLength).Should().Be(startDirectory.Length);
    }

    [TestMethod]
    [DataRow("C:/temp/*.txt", false, false)]
    [DataRow("C:/temp/**/*.txt", true, true)]
    [DataRow("C:/temp/**", true, true)]
    [DataRow("C:/temp/**/bin/*.dll", false, false)]
    public void Constructor_SetsRecursionFlags(string fullPathSpec, bool expectedAlwaysRecurse, bool expectedEndsInAnyDirectory)
    {
        // Use a common directory and matching options
        MSBuildSpecification specification = new(fullPathSpec.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));

        MatchMSBuild match = new(specification, MatchType.Simple, MatchCasing.CaseInsensitive);

        // Verify public properties
        match.AlwaysRecurse.Should().Be(expectedAlwaysRecurse);
        match.EndsInAnyDirectory.Should().Be(expectedEndsInAnyDirectory);
    }

    [TestMethod]
    public void CacheInvalidation_WorksCorrectly()
    {
        MSBuildSpecification specification = new("C:/temp/*.txt".Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        MatchMSBuild match = new(specification, MatchType.Simple, MatchCasing.CaseInsensitive);

        dynamic accessor = match.TestAccessor.Dynamic;

        // Initially the cache should not be valid
        ((bool)accessor._cacheValid).Should().BeFalse();

        // We can set up cache state manually through the accessor
        accessor._cacheValid = true;
        accessor._cachedFullyMatches = true;

        // Invalidate the cache
        match.DirectoryFinished();

        // Verify cache is invalidated
        ((bool)accessor._cacheValid).Should().BeFalse();
    }
}
