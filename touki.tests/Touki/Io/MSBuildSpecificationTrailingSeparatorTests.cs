// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Pins down trailing-separator behavior in <see cref="MSBuildSpecification"/> /
///  <see cref="MSBuildEnumerator"/> and contrasts it with MSBuild's <c>FileMatcher</c>.
/// </summary>
/// <remarks>
///  <para>
///   Two layers matter when comparing against MSBuild:
///  </para>
///  <para>
///   1. <b>Parsing / enumeration</b>: MSBuild's <c>FileMatcher.SplitFileSpec</c> leaves an empty
///   <c>filenamePart</c> for trailing-separator specs (e.g. <c>Foo/</c>, <c>Foo/**/</c>),
///   which causes its regex/enumeration layer to match nothing. Touki agrees at this layer.
///  </para>
///  <para>
///   2. <b><c>FileMatcher.GetFiles</c> no-wildcard shortcut</b>: when the spec contains no
///   <c>*</c> or <c>?</c>, MSBuild's <c>GetFiles</c> returns the spec verbatim
///   (via <c>CreateArrayWithSingleItemIfNotExcluded</c>) without ever consulting the filesystem.
///   So <c>GetFiles("Foo/")</c> returns <c>["Foo/"]</c> regardless of whether <c>Foo/</c> exists,
///   while touki's <c>MSBuildEnumerator</c> actually walks the filesystem and returns an empty
///   result. The asserting tests below only cover wildcard specs, where both layers agree;
///   the no-wildcard divergence is documented in
///   <see cref="MSBuildSpecification.Normalize"/>'s remarks.
///  </para>
/// </remarks>
public class MSBuildSpecificationTrailingSeparatorTests
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

    private static string[] EnumerateTouki(string root, string spec)
    {
        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(
            fileSpec: spec,
            projectDirectory: root);

        if (result.Action != MSBuildSearchAction.RunSearch || result.Enumerator is null)
        {
            return [];
        }

        using MSBuildEnumerator enumerator = result.Enumerator;
        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        return [.. files.Select(f => f.Replace('\\', '/'))];
    }

    [Theory]
    // Wildcard trailing-separator specs: touki and MSBuild's FileMatcher.GetFiles agree on these
    // because they all bypass the no-wildcard shortcut and go through the regex/enumeration path.
    [InlineData("Foo/**", new[] { "Foo/b.txt", "Foo/Bar/c.txt" })]
    [InlineData("Foo/**/", new[] { "Foo/b.txt", "Foo/Bar/c.txt" })]
    [InlineData("Foo/**/*.txt", new[] { "Foo/b.txt", "Foo/Bar/c.txt" })]
    [InlineData("**", new[] { "a.txt", "Foo/b.txt", "Foo/Bar/c.txt" })]
    [InlineData("**/", new[] { "a.txt", "Foo/b.txt", "Foo/Bar/c.txt" })]
    [InlineData("**/*.txt", new[] { "a.txt", "Foo/b.txt", "Foo/Bar/c.txt" })]
    public void MSBuildEnumerator_TrailingSeparatorWildcardSpec_MatchesExpected(string spec, string[] expected)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        string[] actual = EnumerateTouki(tempFolder.TempPath, spec);

        actual.Should().BeEquivalentTo(expected);
    }

    [Theory]
    // No-wildcard specs whose resolved fixed path is not an existing directory. Previously these
    // threw an IOException from FileSystemEnumerator (e.g. "Foo/b.txt/" tries to enumerate a file as
    // a directory). The MatchEnumerator pre-checks Directory.Exists on its start directory now and
    // returns an empty result instead, matching MSBuild's "ReturnEmptyList" branch in
    // GetFileSearchData when the fixed directory does not exist as a directory.
    [InlineData("Foo")]
    [InlineData("Foo/")]
    [InlineData("Foo/b.txt/")]
    [InlineData("Missing/")]
    [InlineData("Missing/file.txt")]
    public void MSBuildEnumerator_NoWildcardSpecResolvingToNonDirectory_ReturnsEmpty(string spec)
    {
        using TempFolder tempFolder = new();
        CreateFixture(tempFolder.TempPath);

        string[] actual = EnumerateTouki(tempFolder.TempPath, spec);

        actual.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Foo/", "Foo", "", "", false)]
    [InlineData("Foo/Bar/", "Foo/Bar", "", "", false)]
    [InlineData("Foo/b.txt/", "Foo/b.txt", "", "", false)]
    // Trailing separator on a recursive spec: touki keeps WildPath="**" and synthesizes
    // FileName="*" because the wildcard path is the simple recursive match (the
    // IsSimpleRecursiveMatch && FileName.IsEmpty branch in the constructor). End-to-end this
    // matches MSBuild's FileMatcher.GetFiles behavior for the same spec.
    [InlineData("Foo/**/", "Foo", "**", "*", true)]
    [InlineData("**/", "", "**", "*", true)]
    public void MSBuildSpecification_TrailingSeparator_Parses(
        string spec,
        string expectedFixed,
        string expectedWild,
        string expectedFile,
        bool expectedHasWildcards)
    {
        MSBuildSpecification parsed = new(spec);

        expectedFixed = expectedFixed.Replace('/', Path.DirectorySeparatorChar);
        expectedWild = expectedWild.Replace('/', Path.DirectorySeparatorChar);
        expectedFile = expectedFile.Replace('/', Path.DirectorySeparatorChar);

        parsed.FixedPath.ToString().Should().Be(expectedFixed);
        parsed.WildPath.ToString().Should().Be(expectedWild);
        parsed.FileName.ToString().Should().Be(expectedFile);
        parsed.HasAnyWildCards.Should().Be(expectedHasWildcards);
    }
}
