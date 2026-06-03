// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Tests for <see cref="OrderedMatchSet"/>'s "last matching rule wins" evaluation.
///  These cover the abstraction in isolation; full <c>.gitignore</c> integration
///  tests live in the gitignore loader's own test class.
/// </summary>
[TestClass]
public class OrderedMatchSetTests
{
    private static string Root => Path.Combine(Path.GetTempPath(), "ordered-match-set-root");

    private static GlobMatch CompileGit(string pattern) =>
        GlobSpecification.Compile(pattern, GlobDialect.Git).CreateMatcher(Root);

    [TestMethod]
    public void Empty_MatchesFile_ReturnsFalse()
    {
        using OrderedMatchSet set = new();
        IEnumerationMatcher boundary = set;

        boundary.MatchesFile(Root, "anything".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void SingleInclude_Matched_FileIncluded()
    {
        using OrderedMatchSet set = new();
        set.AddInclude(CompileGit("*.cs"));

        IEnumerationMatcher boundary = set;
        boundary.MatchesFile(Root, "Foo.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Root, "Foo.txt".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void SingleExclude_Matched_FileExcluded()
    {
        using OrderedMatchSet set = new();
        set.AddExclude(CompileGit("*.log"));

        IEnumerationMatcher boundary = set;
        boundary.MatchesFile(Root, "Foo.log".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();
        // No rule matches `Foo.cs` so the result is the default "not included" (false).
        // OrderedMatchSet treats an empty match set as "no opinion → exclude".
        boundary.MatchesFile(Root, "Foo.cs".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void IncludeThenExclude_LaterExcludeWins()
    {
        using OrderedMatchSet set = new();
        set.AddInclude(CompileGit("*.txt"));
        set.AddExclude(CompileGit("secret*"));

        IEnumerationMatcher boundary = set;
        boundary.MatchesFile(Root, "notes.txt".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        // `secret.txt` matches both rules; the later exclude wins.
        boundary.MatchesFile(Root, "secret.txt".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void ExcludeThenInclude_LaterIncludeWins()
    {
        // Models a `.gitignore` `bin/` exclude followed by `!bin/keep.txt` re-include.
        // OrderedMatchSet's contract is that the LAST matching rule decides, so the
        // include "rescues" the file.
        using OrderedMatchSet set = new();
        set.AddExclude(CompileGit("*.log"));
        set.AddInclude(CompileGit("keep*"));

        IEnumerationMatcher boundary = set;
        boundary.MatchesFile(Root, "keep.log".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Root, "other.log".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void ThreeRules_LastMatchingWins()
    {
        using OrderedMatchSet set = new();
        set.AddInclude(CompileGit("*.txt"));        // 1: include all .txt
        set.AddExclude(CompileGit("*"));            // 2: exclude everything
        set.AddInclude(CompileGit("README*"));      // 3: re-include README*

        IEnumerationMatcher boundary = set;

        // `README.txt` matches 1, 2, 3 → last is include → included.
        boundary.MatchesFile(Root, "README.txt".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        // `foo.txt` matches 1, 2 → last is exclude → excluded.
        boundary.MatchesFile(Root, "foo.txt".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();
        // `foo.bin` matches only 2 → excluded.
        boundary.MatchesFile(Root, "foo.bin".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void DirectoryFinished_FansOutToAllRules()
    {
        // Verifies that the set propagates DirectoryFinished to every rule's matcher;
        // multiple GlobMatcher instances each invalidate their own per-directory cache.
        using OrderedMatchSet set = new();
        set.AddInclude(CompileGit("a/*.cs"));
        set.AddExclude(CompileGit("a/secret*"));

        IEnumerationMatcher boundary = set;

        string aDir = Path.Combine(Root, "a");
        string bDir = Path.Combine(Root, "b");

        boundary.MatchesFile(aDir, "Foo.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        // After DirectoryFinished, cache invalidates; the new dir is matched fresh.
        boundary.MatchesFile(bDir, "Foo.cs".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void MatchesDirectory_DirectoryOnlyExclude_ClaimsSubtree()
    {
        // `bin/` (DirectoryOnly exclude) should claim the whole `bin` subtree.
        using OrderedMatchSet set = new();
        set.AddExclude(CompileGit("bin/"));

        IEnumerationMatcher boundary = set;

        // `matchForExclusion` from the outer caller: the bin directory should be
        // excluded → boundary returns true (skip).
        boundary.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: true).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesDirectory(Root, "src".AsSpan(), matchForExclusion: true).Should().BeFalse();
    }

    [TestMethod]
    public void Count_ReturnsAddedRules()
    {
        using OrderedMatchSet set = new();
        set.Count.Should().Be(0);

        set.AddInclude(CompileGit("a"));
        set.Count.Should().Be(1);

        set.AddExclude(CompileGit("b"));
        set.Count.Should().Be(2);
    }

    [TestMethod]
    public void AddInclude_NullMatcher_Throws()
    {
        using OrderedMatchSet set = new();
        Action act = () => set.AddInclude(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void AddExclude_NullMatcher_Throws()
    {
        using OrderedMatchSet set = new();
        Action act = () => set.AddExclude(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Ctor_IncludeByDefaultFalse_IsDefault()
    {
        using OrderedMatchSet set = new();
        set.IncludeByDefault.Should().BeFalse();
    }

    [TestMethod]
    public void Ctor_IncludeByDefaultTrue_FlagRoundTrips()
    {
        using OrderedMatchSet set = new(includeByDefault: true);
        set.IncludeByDefault.Should().BeTrue();
    }

    [TestMethod]
    public void IncludeByDefault_Empty_MatchesFile_ReturnsTrue()
    {
        // Gitignore mode: with no rules, every file is included.
        using OrderedMatchSet set = new(includeByDefault: true);
        IEnumerationMatcher boundary = set;

        boundary.MatchesFile(Root, "anything".AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    public void IncludeByDefault_OnlyExclude_NonMatchingFileStillIncluded()
    {
        // Gitignore-style: `*.log` excludes log files; everything else stays included.
        using OrderedMatchSet set = new(includeByDefault: true);
        set.AddExclude(CompileGit("*.log"));

        IEnumerationMatcher boundary = set;
        boundary.MatchesFile(Root, "trace.txt".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Root, "trace.log".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void IncludeByDefault_ExcludeThenInclude_RescuesFile()
    {
        // Gitignore "exclude all logs, but keep important.log".
        using OrderedMatchSet set = new(includeByDefault: true);
        set.AddExclude(CompileGit("*.log"));
        set.AddInclude(CompileGit("important.log"));

        IEnumerationMatcher boundary = set;
        boundary.MatchesFile(Root, "trace.log".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Root, "important.log".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Root, "readme.md".AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    public void IncludeByDefault_DirectoryOnlyExclude_ClaimsSubtree()
    {
        // Strict gitignore semantics: `bin/` claims the whole subtree at the
        // walker's recurse decision. Per gitignore(5) you can't re-include a
        // file whose parent directory is excluded, so the per-file evaluation
        // never has to deal with descendants - the walker just doesn't
        // recurse.
        using OrderedMatchSet set = new(includeByDefault: true);
        set.AddExclude(CompileGit("bin/"));

        IEnumerationMatcher boundary = set;

        // ShouldRecurseIntoEntry side: "bin" is excluded → don't recurse.
        boundary.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: false).Should().BeFalse();
        boundary.DirectoryFinished();
        // Composite-side query: claim the subtree.
        boundary.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: true).Should().BeTrue();
        boundary.DirectoryFinished();
        // Sibling tree is unaffected.
        boundary.MatchesDirectory(Root, "src".AsSpan(), matchForExclusion: false).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesDirectory(Root, "src".AsSpan(), matchForExclusion: true).Should().BeFalse();
    }

    [TestMethod]
    public void IncludeByDefault_DirectoryExcludeThenFileInclude_SubtreeStillSkipped()
    {
        // Strict gitignore semantics: a later file-level include cannot rescue
        // files under an excluded directory (gitignore(5)). The walker should
        // skip the subtree entirely; `!keep.log` has no effect on `bin/`.
        using OrderedMatchSet set = new(includeByDefault: true);
        set.AddExclude(CompileGit("bin/"));
        set.AddInclude(CompileGit("keep.log"));

        IEnumerationMatcher boundary = set;

        // Walker is told not to enter `bin/`, so `bin/keep.log` is never
        // queried via MatchesFile in practice.
        boundary.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: false).Should().BeFalse();
    }
}
