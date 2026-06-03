// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Coverage and behavior tests for <see cref="GlobEnumerator"/>: every public
///  factory overload, null-argument validation, single-exclude and multi-exclude
///  composition (subtree subsumption + file-name disjointness dedupe), default
///  enumeration options, custom enumeration options, and the per-entry
///  <c>TransformEntry</c> output paths (root-relative for nested files,
///  bare name for top-level files).
/// </summary>
[TestClass]
public class GlobEnumeratorTests
{
    private static TempFolder CreateFixture()
    {
        TempFolder folder = new();
        string root = folder.TempPath;
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "src", "nested"));
        Directory.CreateDirectory(Path.Combine(root, "obj", "Debug"));
        Directory.CreateDirectory(Path.Combine(root, "bin", "Release"));

        File.WriteAllText(Path.Combine(root, "top.cs"), "");
        File.WriteAllText(Path.Combine(root, "top.txt"), "");
        File.WriteAllText(Path.Combine(root, "src", "a.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "b.user"), "");
        File.WriteAllText(Path.Combine(root, "src", "nested", "c.cs"), "");
        File.WriteAllText(Path.Combine(root, "obj", "Debug", "obj.cs"), "");
        File.WriteAllText(Path.Combine(root, "bin", "Release", "bin.cs"), "");
        return folder;
    }

    [TestMethod]
    public void Create_IncludeOnly_PosixPathDefault_FindsExpectedFiles()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            excludePattern: null,
            folder.TempPath,
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar);

        HashSet<string> results = Collect(enumerator);

        results.Should().Contain(JoinSep("top.cs"));
        results.Should().Contain(JoinSep("src", "a.cs"));
        results.Should().Contain(JoinSep("src", "nested", "c.cs"));
        results.Should().Contain(JoinSep("obj", "Debug", "obj.cs"));
        results.Should().NotContain(JoinSep("src", "b.user"));
    }

    [TestMethod]
    public void Create_IncludeWithEmptyExcludeString_BehavesAsIncludeOnly()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            "",
            folder.TempPath,
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar);

        IEnumerable<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_SingleExcludePattern_ExcludesMatchingFiles()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            "**/obj/**",
            folder.TempPath,
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar);

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("src", "a.cs"));
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_DialectOverload_HonorsDialect()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            excludePattern: null,
            folder.TempPath,
            GlobDialect.MSBuild);

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("top.cs"));
    }

    [TestMethod]
    public void Create_DialectAndOptionsOverload_HonorsOptions()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.CS",
            excludePattern: null,
            folder.TempPath,
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar | GlobOptions.IgnoreCase);

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("top.cs"));
    }

    [TestMethod]
    public void Create_MultipleExcludes_Default_AllExcludesApply()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            new[] { "**/obj/**", "**/bin/**" },
            folder.TempPath);

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
        results.Should().NotContain(JoinSep("bin", "Release", "bin.cs"));
        results.Should().Contain(JoinSep("src", "a.cs"));
    }

    [TestMethod]
    public void Create_MultipleExcludes_DialectOverload()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            new[] { "**/obj/**" },
            folder.TempPath,
            GlobDialect.MSBuild);

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_MultipleExcludes_DialectAndOptionsOverload()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            new[] { "**/obj/**" },
            folder.TempPath,
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar);

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_MultipleExcludes_SubtreeSubsumption_RedundantSubdirSkipped()
    {
        // `obj/**` subsumes `obj/Debug/**`; the dedupe pass marks the redundant
        // exclude as skipped before compilation.
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            new[] { "obj/**", "obj/Debug/**" },
            folder.TempPath);

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_MultipleExcludes_FileNameDisjointness_DropsUnreachableExclude()
    {
        // The include's trailing literal is `.cs`; the `.user` exclude can never
        // match a `.cs` file, so the dedupe pass drops it. The enumeration still
        // returns the expected files.
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            new[] { "**/*.user", "**/obj/**" },
            folder.TempPath);

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("src", "a.cs"));
        results.Should().NotContain(JoinSep("src", "b.user"));
    }

    [TestMethod]
    public void Create_MultipleExcludes_EmptyEntriesAreIgnored()
    {
        // Empty exclude strings are tolerated and skipped.
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            new[] { "", "**/obj/**", "" },
            folder.TempPath);

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
        results.Should().Contain(JoinSep("src", "a.cs"));
    }

    [TestMethod]
    public void Create_TrailingSlashSubtreeSubsumption()
    {
        // `obj/**/` (with trailing slash) is still recognized as a subtree pattern.
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            new[] { "obj/**/", "obj/Debug/**" },
            folder.TempPath);

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_BackslashSubtreePatternRecognized()
    {
        // The subsumption pass recognizes either separator.
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            new[] { @"obj\**", @"obj\Debug\**" },
            folder.TempPath);

        // The exclude compiles even with backslash; the matcher will use its own
        // separator. The point is the dedupe pass doesn't choke on `\`.
        _ = Collect(enumerator);
    }

    [TestMethod]
    public void Create_CustomEnumerationOptions_Respected()
    {
        using TempFolder folder = CreateFixture();
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = false,
            MatchCasing = MatchCasing.PlatformDefault,
            MatchType = MatchType.Simple,
            IgnoreInaccessible = true,
        };

        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "*.cs",
            excludePattern: null,
            folder.TempPath,
            GlobDialect.PosixPath,
            GlobOptions.None,
            options);

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain("top.cs");
        // RecurseSubdirectories=false means nested files aren't returned.
        results.Should().NotContain(JoinSep("src", "a.cs"));
    }

    [TestMethod]
    public void Create_NullIncludePattern_Throws()
    {
        using TempFolder folder = CreateFixture();

        FluentActions.Invoking(() =>
            GlobEnumerator.Create(null!, excludePattern: null, folder.TempPath))
            .Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Create_NullExcludeList_Throws()
    {
        using TempFolder folder = CreateFixture();

        FluentActions.Invoking(() =>
            GlobEnumerator.Create("**/*.cs", excludePatterns: null!, folder.TempPath))
            .Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Create_NullRootDirectory_Throws()
    {
        FluentActions.Invoking(() =>
            GlobEnumerator.Create("**/*.cs", excludePattern: null, rootDirectory: null!))
            .Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Create_NullRootDirectory_ExcludeListOverload_Throws()
    {
        FluentActions.Invoking(() =>
            GlobEnumerator.Create("**/*.cs", new[] { "**/obj/**" }, rootDirectory: null!))
            .Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Enumerate_RootWithTrailingSeparator_TopLevelFilesReturnedBareName()
    {
        // The root directory length calculation accounts for a trailing separator;
        // a top-level file in that case is yielded as its bare name.
        using TempFolder folder = CreateFixture();
        string rootWithSep = folder.TempPath + Path.DirectorySeparatorChar;
        using GlobEnumerator enumerator = GlobEnumerator.Create("*.cs", excludePattern: null, rootWithSep);

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain("top.cs");
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
