// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Companion to <see cref="MSBuildSpecificationTrailingSeparatorTests"/> that pins down the
///  behavior of MSBuild's internal <c>FileMatcher</c> (exposed via
///  <see cref="FileMatcherWrapper"/>) for the same trailing-separator scenarios. If MSBuild
///  changes any of these behaviors, these tests will break and force us to revisit the
///  comparative notes in <see cref="MSBuildSpecification.Normalize"/> /
///  <see cref="MSBuildSpecificationTrailingSeparatorTests"/>.
/// </summary>
public class FileMatcherTrailingSeparatorOracleTests
{
    private static void CreateFixture(string root)
    {
        File.WriteAllText(Path.Combine(root, "a.txt"), string.Empty);
        string foo = Path.Combine(root, "Foo");
        Directory.CreateDirectory(foo);
        File.WriteAllText(Path.Combine(foo, "b.txt"), string.Empty);
        string bar = Path.Combine(foo, "Bar");
        Directory.CreateDirectory(bar);
        File.WriteAllText(Path.Combine(bar, "c.txt"), string.Empty);
    }

    private static string[] EnumerateOracle(string root, string spec)
    {
        FileMatcherWrapper.GetFilesResult result = FileMatcherWrapper.GetFiles(root, spec);
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return [.. result.FileList
            .Select(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? f[prefix.Length..] : f)
            .Select(f => f.Replace('\\', '/'))];
    }

    // 1. The "no-wildcard shortcut" in FileMatcher.GetFiles: for any spec without '*' or '?',
    //    the spec is returned verbatim via CreateArrayWithSingleItemIfNotExcluded, without
    //    consulting the filesystem. The fixture is created but ignored: the returned string is
    //    always the input spec (in MSBuild's normalized form, which echoes the input separators).
    //
    //    This is the layer where touki and MSBuild visibly disagree: touki's MSBuildEnumerator
    //    actually walks the filesystem and returns an empty result for these specs.

    [Test]
    [Arguments("a.txt")]                  // file exists
    [Arguments("Foo")]                    // directory exists (NOT a file)
    [Arguments("Foo/")]                   // trailing slash on existing directory
    [Arguments("Foo/b.txt")]              // file exists
    [Arguments("Foo/b.txt/")]             // trailing slash on existing FILE (nonsensical, still verbatim)
    [Arguments("Missing/file.txt")]       // path that does not exist
    [Arguments("Missing/")]               // trailing slash on missing directory
    public void GetFiles_NoWildcardSpec_ReturnsSpecVerbatimRegardlessOfDisk(string spec)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        FileMatcherWrapper.GetFilesResult result = FileMatcherWrapper.GetFiles(tempFolder.TempPath, spec);

        // No-wildcard specs are short-circuited in FileMatcher.GetFiles: the spec is returned verbatim
        // via CreateArrayWithSingleItemIfNotExcluded and the action is SearchAction.None (the early-
        // return path before any actual search is initiated).
        result.Action.Should().Be(FileMatcherWrapper.SearchAction.None);

        // Single-element list containing exactly the input spec.
        result.FileList.Should().ContainSingle().Which.Should().Be(spec);
    }

    // 2. Wildcard trailing-separator specs: FileMatcher routes through SplitFileSpec /
    //    GetFileSpecInfo and actually enumerates. For these cases touki and MSBuild agree.
    //    SplitFileSpec's special case for filenamePart == "**" rewrites it to "*.*" and appends
    //    "**" to the wildcardDirectoryPart, which is why "Foo/**" matches every file under Foo
    //    (including Foo/Bar/c.txt) and not just "*.*" files in Foo itself.

    [Test]
    [Arguments("Foo/**", new[] { "Foo/b.txt", "Foo/Bar/c.txt" })]
    [Arguments("Foo/**/", new[] { "Foo/b.txt", "Foo/Bar/c.txt" })]
    [Arguments("Foo/**/*.txt", new[] { "Foo/b.txt", "Foo/Bar/c.txt" })]
    [Arguments("**", new[] { "a.txt", "Foo/b.txt", "Foo/Bar/c.txt" })]
    [Arguments("**/", new[] { "a.txt", "Foo/b.txt", "Foo/Bar/c.txt" })]
    [Arguments("**/*.txt", new[] { "a.txt", "Foo/b.txt", "Foo/Bar/c.txt" })]
    public void GetFiles_WildcardTrailingSeparator_EnumeratesMatchingFiles(string spec, string[] expected)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        string[] actual = EnumerateOracle(tempFolder.TempPath, spec);

        actual.Should().BeEquivalentTo(expected);
    }

    // 3. Wildcard specs whose fixed directory does not exist on disk: FileMatcher returns an
    //    empty list (the SearchAction.ReturnEmptyList branch in GetFileSearchData). This is the
    //    behavior MatchEnumerator now mirrors for both wildcard and non-wildcard cases.

    [Test]
    [Arguments("Missing/**")]
    [Arguments("Missing/*.txt")]
    [Arguments("Missing/**/*.cs")]
    public void GetFiles_WildcardSpecWithMissingFixedDirectory_ReturnsEmpty(string spec)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        string[] actual = EnumerateOracle(tempFolder.TempPath, spec);

        actual.Should().BeEmpty();
    }
}
