// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Regression tests for the extglob negation directory pruning. A directory
///  whose subtree an anchored negation provably excludes (the root-level
///  <c>!(bin|obj)/...</c> shape, or a nested <c>src/!(bin)/**/*.cs</c>) is pruned
///  by <see cref="GlobMatch.MatchesDirectory"/> instead of being descended and
///  rejected file by file. The pruning must stay conservative: non-exact names,
///  directories with matching descendants, and non-anchored shapes
///  (<c>**/!(bin)/*.cs</c>) must keep enumerating normally.
/// </summary>
[TestClass]
public class GlobMatchFirstSegmentNegationPruningTests
{
    private static TempFolder CreateFixture()
    {
        TempFolder folder = new();
        string root = folder.TempPath;
        Directory.CreateDirectory(Path.Combine(root, "src", "nested"));
        Directory.CreateDirectory(Path.Combine(root, "src", "bin"));
        Directory.CreateDirectory(Path.Combine(root, "src", "lib"));
        Directory.CreateDirectory(Path.Combine(root, "obj", "Debug"));
        Directory.CreateDirectory(Path.Combine(root, "bin", "Release"));
        Directory.CreateDirectory(Path.Combine(root, "binx"));

        File.WriteAllText(Path.Combine(root, "top.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "a.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "nested", "c.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "bin", "d.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "lib", "f.cs"), "");
        File.WriteAllText(Path.Combine(root, "obj", "Debug", "obj.cs"), "");
        File.WriteAllText(Path.Combine(root, "bin", "Release", "bin.cs"), "");
        File.WriteAllText(Path.Combine(root, "binx", "e.cs"), "");
        return folder;
    }

    private static GlobMatch CreateMSBuildExtGlob(string pattern, string root) =>
        GlobSpecification.Compile(pattern, GlobDialect.MSBuild, GlobOptions.AllowExtGlob)
            .CreateMatcher(root);

    [TestMethod]
    public void MatchesDirectory_FirstSegmentNegation_PrunesNegatedRootDirectories()
    {
        using TempFolder folder = CreateFixture();
        string root = folder.TempPath;
        using GlobMatch matcher = CreateMSBuildExtGlob("!(bin|obj)/**/*.cs", root);
        IEnumerationMatcher boundary = matcher;

        // Root-level directories whose name is exactly one of the negated literals
        // are pruned: no file beneath them can match.
        boundary.MatchesDirectory(root, "bin".AsSpan(), matchForExclusion: false).Should().BeFalse();
        boundary.MatchesDirectory(root, "obj".AsSpan(), matchForExclusion: false).Should().BeFalse();

        // Non-negated root directories are descended.
        boundary.MatchesDirectory(root, "src".AsSpan(), matchForExclusion: false).Should().BeTrue();

        // Pruning is exact: a name that merely shares a prefix is not negated.
        boundary.MatchesDirectory(root, "binx".AsSpan(), matchForExclusion: false).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesDirectory_FirstSegmentNegation_DoesNotPruneNestedDirectories()
    {
        using TempFolder folder = CreateFixture();
        string root = folder.TempPath;
        using GlobMatch matcher = CreateMSBuildExtGlob("!(bin|obj)/**/*.cs", root);
        IEnumerationMatcher boundary = matcher;

        // A `bin` directory nested under another segment can still contribute
        // matches (e.g. src/bin/d.cs matches !(bin|obj)/**/*.cs), so it must not
        // be pruned.
        string srcDir = Path.Combine(root, "src");
        boundary.MatchesDirectory(srcDir, "bin".AsSpan(), matchForExclusion: false).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesDirectory_NonFirstSegmentNegation_DoesNotPrune()
    {
        using TempFolder folder = CreateFixture();
        string root = folder.TempPath;

        // The negation is behind a globstar, so it is not anchored to the first
        // path segment - `bin` at the root must still be descended.
        using GlobMatch matcher = CreateMSBuildExtGlob("**/!(bin)/*.cs", root);
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesDirectory(root, "bin".AsSpan(), matchForExclusion: false).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesDirectory_NestedAnchoredNegation_PrunesNegatedSubdirectory()
    {
        using TempFolder folder = CreateFixture();
        string root = folder.TempPath;

        // The negation is anchored to the second segment under a literal prefix:
        // src/!(bin)/**/*.cs excludes src/bin entirely but descends src/lib.
        using GlobMatch matcher = CreateMSBuildExtGlob("src/!(bin)/**/*.cs", root);
        IEnumerationMatcher boundary = matcher;

        string srcDir = Path.Combine(root, "src");
        boundary.MatchesDirectory(srcDir, "bin".AsSpan(), matchForExclusion: false).Should().BeFalse();
        boundary.MatchesDirectory(srcDir, "lib".AsSpan(), matchForExclusion: false).Should().BeTrue();

        // The `src` root itself stays viable.
        boundary.MatchesDirectory(root, "src".AsSpan(), matchForExclusion: false).Should().BeTrue();
    }

    [TestMethod]
    public void Enumerate_NestedAnchoredNegation_ExcludesNegatedSubdirectory()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "src/!(bin)/**/*.cs",
            excludePattern: null,
            folder.TempPath,
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        HashSet<string> results = Collect(enumerator);

        results.Should().Contain(JoinSep("src", "nested", "c.cs"));
        results.Should().Contain(JoinSep("src", "lib", "f.cs"));
        results.Should().NotContain(JoinSep("src", "bin", "d.cs"));
    }

    [TestMethod]
    public void Enumerate_FirstSegmentNegation_ExcludesNegatedSubtrees()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "!(bin|obj)/**/*.cs",
            excludePattern: null,
            folder.TempPath,
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        HashSet<string> results = Collect(enumerator);

        results.Should().Contain(JoinSep("src", "a.cs"));
        results.Should().Contain(JoinSep("src", "nested", "c.cs"));
        results.Should().Contain(JoinSep("src", "bin", "d.cs"));
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
        results.Should().NotContain(JoinSep("bin", "Release", "bin.cs"));
    }

    [TestMethod]
    public void Enumerate_NonFirstSegmentNegation_StillDescendsBin()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/!(bin)/*.cs",
            excludePattern: null,
            folder.TempPath,
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        HashSet<string> results = Collect(enumerator);

        // The negation targets the file's parent directory, not the root segment,
        // so bin/Release/bin.cs (parent "Release" != "bin") must be enumerated.
        // If the root `bin` were wrongly pruned this file would disappear.
        results.Should().Contain(JoinSep("bin", "Release", "bin.cs"));
    }

    private static string JoinSep(params string[] parts) =>
        string.Join(Path.DirectorySeparatorChar.ToString(), parts);

    private static HashSet<string> Collect(GlobEnumerator enumerator)
    {
        HashSet<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }
}
