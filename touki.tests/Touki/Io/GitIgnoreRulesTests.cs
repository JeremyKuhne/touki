// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

[DoNotParallelize]
[TestClass]
public sealed class GitIgnoreRulesTests
{
    private static SequentialSeparatorGitOracleTests.RepoFixture s_fixture = null!;

    private static string Root => Path.Combine(Path.GetTempPath(), "gitignore-rules-root");

    [ClassInitialize]
    public static void ClassInitialize(TestContext context) =>
        s_fixture = new SequentialSeparatorGitOracleTests.RepoFixture();

    [ClassCleanup]
    public static void ClassCleanup() => s_fixture?.Dispose();

    [TestMethod]
    public void Parse_BlankAndCommentLines_SnapshotsOnlyRules()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("\n# comment\n*.log\n\n");

        rules.Count.Should().Be(1);
    }

    [TestMethod]
    public void Parse_NullContent_Throws()
    {
        Action action = () => GitIgnoreRules.Parse(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Parse_EscapedLeadingMarkers_AreLiteral()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("\\#literal\n\\!important.txt");

        rules.IsIgnoredFile("#literal").Should().BeTrue();
        rules.IsIgnoredFile("!important.txt").Should().BeTrue();
        rules.IsIgnoredFile("other.txt").Should().BeFalse();
        rules.IsIgnoredFile("#other").Should().BeFalse();
    }

    [TestMethod]
    public void Parse_TrailingWhitespaceAndCrlf_AreHandled()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("*.log   \r\n*.tmp\t\t\r\n");

        rules.Count.Should().Be(2);
        rules.IsIgnoredFile("a.log").Should().BeTrue();
        rules.IsIgnoredFile("a.tmp").Should().BeTrue();
    }

    [TestMethod]
    public void Compile_DefaultSource_Throws()
    {
        Action action = () => GitIgnoreRules.Compile([default]);

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Constructor_NonCanonicalBasePath_Throws()
    {
        Action action = () => new GitIgnoreRuleSource("*.log", "/src");

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow("src/./generated")]
    [DataRow("src/../generated")]
    [DataRow("src//generated")]
    public void Constructor_NonCanonicalSegment_Throws(string basePath)
    {
        Action action = () => new GitIgnoreRuleSource("*.log", basePath);

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void IsIgnoredFile_ExcludeThenInclude_UsesLastMatchingRule()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("*.log\n!keep.log");

        rules.IsIgnoredFile("trace.log").Should().BeTrue();
        rules.IsIgnoredFile("keep.log").Should().BeFalse();
        rules.IsIgnoredFile("trace.txt").Should().BeFalse();
    }

    [TestMethod]
    public void IsIgnoredFile_RepeatedLeadingBang_ReincludesLiteralBangOnly()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("*\n!!keep");

        rules.IsIgnoredFile("!keep").Should().BeFalse();
        rules.IsIgnoredFile("other").Should().BeTrue();
    }

    [TestMethod]
    public void IsIgnoredFile_RootAnchorAndUnanchoredRule_UseGitScope()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("/build\n*.log");

        rules.IsIgnoredFile("build").Should().BeTrue();
        rules.IsIgnoredFile("src/build").Should().BeFalse();
        rules.IsIgnoredFile("trace.log").Should().BeTrue();
        rules.IsIgnoredFile("deep/nested/trace.log").Should().BeTrue();
    }

    [TestMethod]
    public void IsIgnoredFile_DescendantIncludeWithoutParentInclude_RemainsIgnored()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("bin/\n!bin/keep.txt");

        rules.IsIgnoredFile("bin/keep.txt").Should().BeTrue();
    }

    [TestMethod]
    public void IsIgnoredFile_ParentThenDescendantIncludes_RescueFile()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("bin/\n!bin/\n!bin/keep.txt");

        rules.IsIgnoredFile("bin/keep.txt").Should().BeFalse();
    }

    [TestMethod]
    public void IsIgnoredFile_EmptyPath_Throws()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("*.log");

        Action action = () => rules.IsIgnoredFile(string.Empty);

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Compile_NestedSource_AppliesOnlyUnderBasePath()
    {
        GitIgnoreRules rules = GitIgnoreRules.Compile(
        [
            new("*.log"),
            new("!keep.log", "src")
        ]);

        rules.IsIgnoredFile("keep.log").Should().BeTrue();
        rules.IsIgnoredFile("src/keep.log").Should().BeFalse();
        rules.IsIgnoredFile("tests/keep.log").Should().BeTrue();
    }

    [TestMethod]
    public void Parse_RealisticRules_EvaluatesFilesAndDirectories()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse(
            "bin/\nobj/\n*.log\n!keep.log\n/node_modules");
        using IFileSystemMatcherSession included = rules.CreateIncludedMatcher().CreateSession(Root);

        rules.IsIgnoredFile("trace.log").Should().BeTrue();
        rules.IsIgnoredFile("keep.log").Should().BeFalse();
        rules.IsIgnoredFile("bin/keep.log").Should().BeTrue();
        rules.IsIgnoredFile("node_modules/package/index.js").Should().BeTrue();
        included.MatchesDirectory(Root, "obj")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
        included.MatchesDirectory(Root, "src")
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
    }

    [TestMethod]
    public void CreateIncludedMatcher_FilePolarity_IsOppositeOfIgnoredMatcher()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("*.log");
        using IFileSystemMatcherSession included = rules.CreateIncludedMatcher().CreateSession(Root);
        using IFileSystemMatcherSession ignored = rules.CreateIgnoredMatcher().CreateSession(Root);

        included.MatchesFile(Root, "trace.log").Should().BeFalse();
        ignored.MatchesFile(Root, "trace.log").Should().BeTrue();
        included.MatchesFile(Root, "trace.txt").Should().BeTrue();
        ignored.MatchesFile(Root, "trace.txt").Should().BeFalse();
    }

    [TestMethod]
    public void CreateIncludedMatcher_IgnoredDirectory_ClassifiesWholeSubtree()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("/node_modules");
        using IFileSystemMatcherSession included = rules.CreateIncludedMatcher().CreateSession(Root);
        using IFileSystemMatcherSession ignored = rules.CreateIgnoredMatcher().CreateSession(Root);

        included.MatchesDirectory(Root, "node_modules")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
        ignored.MatchesDirectory(Root, "node_modules")
            .Should().Be(DirectoryMatchType.AllDescendantFilesMatch);
        included.MatchesDirectory(Root, "src")
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
    }

    [TestMethod]
    public void CreateIncludedMatcher_NestedFiles_UsesCanonicalRelativePath()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse("src/generated/\n*.log");
        using IFileSystemMatcherSession included = rules.CreateIncludedMatcher().CreateSession(Root);
        using IFileSystemMatcherSession ignored = rules.CreateIgnoredMatcher().CreateSession(Root);
        string sourceDirectory = Path.Combine(Root, "src");
        string generatedDirectory = Path.Combine(sourceDirectory, "generated");

        included.MatchesFile(sourceDirectory, "trace.log").Should().BeFalse();
        ignored.MatchesFile(sourceDirectory, "trace.log").Should().BeTrue();
        included.MatchesFile(sourceDirectory, "source.cs").Should().BeTrue();
        included.MatchesDirectory(sourceDirectory, "generated")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
        ignored.MatchesDirectory(sourceDirectory, "generated")
            .Should().Be(DirectoryMatchType.AllDescendantFilesMatch);
        included.MatchesDirectory(generatedDirectory, "nested")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
    }

    [TestMethod]
    public void CreateIncludedMatcher_EmptyRules_ClassifiesWholeTree()
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse(string.Empty);
        using IFileSystemMatcherSession included = rules.CreateIncludedMatcher().CreateSession(Root);
        using IFileSystemMatcherSession ignored = rules.CreateIgnoredMatcher().CreateSession(Root);

        included.MatchesDirectory(Root, "src")
            .Should().Be(DirectoryMatchType.AllDescendantFilesMatch);
        ignored.MatchesDirectory(Root, "src")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
    }

    public static IEnumerable<(string[], string)> OracleRows()
    {
        yield return (["bin/"], "bin/output/file.dll");
        yield return (["/node_modules"], "node_modules/package/index.js");
        yield return (["/node_modules"], "src/node_modules/package/index.js");
        yield return (["*.log"], "src/trace.log");
        yield return (["bin/", "!bin/keep.txt"], "bin/keep.txt");
        yield return (["bin/", "!bin/", "!bin/keep.txt"], "bin/keep.txt");
        yield return (["*", "!!keep"], "!keep");
    }

    [TestMethod]
    [DynamicData(nameof(OracleRows))]
    public void IsIgnoredFile_AncestorScenarios_AgreeWithLibGit2Sharp(
        string[] patterns,
        string path)
    {
        GitIgnoreRules rules = GitIgnoreRules.Parse(string.Join("\n", patterns));
        bool expected = s_fixture.IsIgnored(patterns, path);

        rules.IsIgnoredFile(path).Should().Be(
            expected,
            because: $"GitIgnoreRules and LibGit2Sharp must agree for '{string.Join(" | ", patterns)}' vs '{path}'");
    }
}