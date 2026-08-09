// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Coverage and behavior tests for <see cref="GlobEnumerator"/>: options validation,
///  exclude composition, default and custom enumeration options, and the per-entry
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

    private static GlobEnumerationOptions CreateOptions(
        IReadOnlyList<string>? excludePatterns = null,
        GlobDialect dialect = GlobDialect.PosixPath,
        GlobOptions globOptions = GlobOptions.None,
        EnumerationOptions? enumerationOptions = null) => new()
    {
        ExcludePatterns = excludePatterns ?? Array.Empty<string>(),
        Dialect = dialect,
        GlobOptions = globOptions,
        EnumerationOptions = enumerationOptions ?? new EnumerationOptions
        {
            MatchType = MatchType.Simple,
            MatchCasing = MatchCasing.PlatformDefault,
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        }
    };

    [TestMethod]
    public void Create_IncludeOnly_PosixPathDefault_FindsExpectedFiles()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(globOptions: GlobOptions.AllowGlobStar));

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
            folder.TempPath,
            CreateOptions([""], globOptions: GlobOptions.AllowGlobStar));

        IEnumerable<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_SingleExcludePattern_ExcludesMatchingFiles()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(["**/obj/**"], globOptions: GlobOptions.AllowGlobStar));

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("src", "a.cs"));
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    [DataRow(GlobDialect.MSBuild, GlobOptions.None)]
    [DataRow(GlobDialect.FileSystemGlobbing, GlobOptions.None)]
    [DataRow(GlobDialect.PosixPath, GlobOptions.AllowGlobStar)]
    [DataRow(GlobDialect.Bash, GlobOptions.AllowGlobStar)]
    public void Create_FileOnlyExclude_DoesNotPruneDirectory(
        GlobDialect dialect,
        GlobOptions globOptions)
    {
        using TempFolder folder = new();
        string objDirectory = Path.Combine(folder.TempPath, "obj");
        Directory.CreateDirectory(objDirectory);
        File.WriteAllText(Path.Combine(objDirectory, "excluded.txt"), string.Empty);
        File.WriteAllText(Path.Combine(objDirectory, "included.cs"), string.Empty);

        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*",
            folder.TempPath,
            CreateOptions(["**/obj/*.txt"], dialect, globOptions));

        HashSet<string> results = Collect(enumerator);
        results.Should().ContainSingle().Which.Should().Be(JoinSep("obj", "included.cs"));
    }

    [TestMethod]
    public void Create_DialectOption_HonorsDialect()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(dialect: GlobDialect.MSBuild));

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("top.cs"));
    }

    [TestMethod]
    public void Create_GlobOptions_HonorsOptions()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.CS",
            folder.TempPath,
            CreateOptions(
                globOptions: GlobOptions.AllowGlobStar | GlobOptions.IgnoreCase));

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("top.cs"));
    }

    [TestMethod]
    public void Create_MultipleExcludes_Default_AllExcludesApply()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(["**/obj/**", "**/bin/**"]));

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
        results.Should().NotContain(JoinSep("bin", "Release", "bin.cs"));
        results.Should().Contain(JoinSep("src", "a.cs"));
    }

    [TestMethod]
    public void Create_MultipleExcludes_DialectOption()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(["**/obj/**"], dialect: GlobDialect.MSBuild));

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_MultipleExcludes_DialectAndGlobOptions()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(["**/obj/**"], globOptions: GlobOptions.AllowGlobStar));

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_MultipleOverlappingSubtreeExcludes_ApplyTogether()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(["obj/**", "obj/Debug/**"]));

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_MultipleFileExcludes_ApplyTogether()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(["**/*.user", "**/obj/**"]));

        HashSet<string> results = Collect(enumerator);
        results.Should().Contain(JoinSep("src", "a.cs"));
        results.Should().NotContain(JoinSep("src", "b.user"));
    }

    [TestMethod]
    public void Create_MSBuildCaseInsensitiveExclude_IsNotDropped()
    {
        using TempFolder folder = new();
        File.WriteAllText(Path.Combine(folder.TempPath, "source.cs"), string.Empty);

        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.CS",
            folder.TempPath,
            CreateOptions(["**/*.cs"], dialect: GlobDialect.MSBuild));

        Collect(enumerator).Should().BeEmpty();
    }

    [TestMethod]
    public void Create_PosixPathWithoutGlobstar_AppliesBothDoubleStarExcludes()
    {
        using TempFolder folder = new();
        string directory = Path.Combine(folder.TempPath, "foo", "bar");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);

        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "foo/bar/*",
            folder.TempPath,
            CreateOptions(["foo/**", "foo/bar/**"]));

        Collect(enumerator).Should().BeEmpty();
    }

    [TestMethod]
    public void Create_MultipleExcludes_EmptyEntriesAreIgnored()
    {
        // Empty exclude strings are tolerated and skipped.
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(["", "**/obj/**", ""]));

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
        results.Should().Contain(JoinSep("src", "a.cs"));
    }

    [TestMethod]
    public void Create_TrailingSlashSubtreePatterns_ApplyTogether()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions(["obj/**/", "obj/Debug/**"]));

        HashSet<string> results = Collect(enumerator);
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Create_BackslashSubtreePatterns_CompileTogether()
    {
        using TempFolder folder = CreateFixture();
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            folder.TempPath,
            CreateOptions([@"obj\**", @"obj\Debug\**"]));

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
            folder.TempPath,
            CreateOptions(enumerationOptions: options));

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
            GlobEnumerator.Create(null!, folder.TempPath))
            .Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Create_NullExcludeList_Throws()
    {
        using TempFolder folder = CreateFixture();

        GlobEnumerationOptions options = new() { ExcludePatterns = null! };

        FluentActions.Invoking(() => GlobEnumerator.Create("**/*.cs", folder.TempPath, options))
            .Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Create_NullExcludeElement_Throws()
    {
        using TempFolder folder = CreateFixture();
        GlobEnumerationOptions options = new() { ExcludePatterns = [null!] };

        FluentActions.Invoking(() => GlobEnumerator.Create("**/*.cs", folder.TempPath, options))
            .Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Create_NullRootDirectory_Throws()
    {
        FluentActions.Invoking(() => GlobEnumerator.Create("**/*.cs", rootDirectory: null!))
            .Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Create_NullEnumerationOptionsProperty_UsesDefaults()
    {
        using TempFolder folder = CreateFixture();
        GlobEnumerationOptions options = new()
        {
            GlobOptions = GlobOptions.AllowGlobStar,
            EnumerationOptions = null
        };
        using GlobEnumerator enumerator = GlobEnumerator.Create("**/*.cs", folder.TempPath, options);

        Collect(enumerator).Should().Contain(JoinSep("src", "nested", "c.cs"));
    }

    [TestMethod]
    public void Create_MutableOptions_AreSnapshotted()
    {
        using TempFolder folder = CreateFixture();
        List<string> excludes = ["**/obj/**"];
        EnumerationOptions enumerationOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };
        GlobEnumerationOptions options = new()
        {
            ExcludePatterns = excludes,
            GlobOptions = GlobOptions.AllowGlobStar,
            EnumerationOptions = enumerationOptions
        };
        using GlobEnumerator enumerator = GlobEnumerator.Create("**/*.cs", folder.TempPath, options);

        excludes.Clear();
        enumerationOptions.RecurseSubdirectories = false;
        HashSet<string> results = Collect(enumerator);

        results.Should().Contain(JoinSep("src", "nested", "c.cs"));
        results.Should().NotContain(JoinSep("obj", "Debug", "obj.cs"));
    }

    [TestMethod]
    public void Enumerate_RootWithTrailingSeparator_TopLevelFilesReturnedBareName()
    {
        // The root directory length calculation accounts for a trailing separator;
        // a top-level file in that case is yielded as its bare name.
        using TempFolder folder = CreateFixture();
        string rootWithSep = folder.TempPath + Path.DirectorySeparatorChar;
        using GlobEnumerator enumerator = GlobEnumerator.Create("*.cs", rootWithSep);

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
