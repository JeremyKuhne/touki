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
public class OrderedMatchSetTests
{
    private static string Root => Path.Combine(Path.GetTempPath(), "ordered-match-set-root");

    private static GlobMatch CompileGit(string pattern) =>
        GlobSpecification.Compile(pattern, GlobDialect.Git).CreateMatcher(Root);

    [Fact]
    public void Empty_MatchesFile_ReturnsFalse()
    {
        using OrderedMatchSet set = new();
        IEnumerationMatcher boundary = set;

        boundary.MatchesFile(Root, "anything".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void SingleInclude_Matched_FileIncluded()
    {
        using OrderedMatchSet set = new();
        set.AddInclude(CompileGit("*.cs"));

        IEnumerationMatcher boundary = set;
        boundary.MatchesFile(Root, "Foo.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Root, "Foo.txt".AsSpan()).Should().BeFalse();
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Count_ReturnsAddedRules()
    {
        using OrderedMatchSet set = new();
        set.Count.Should().Be(0);

        set.AddInclude(CompileGit("a"));
        set.Count.Should().Be(1);

        set.AddExclude(CompileGit("b"));
        set.Count.Should().Be(2);
    }

    [Fact]
    public void AddInclude_NullMatcher_Throws()
    {
        using OrderedMatchSet set = new();
        Action act = () => set.AddInclude(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddExclude_NullMatcher_Throws()
    {
        using OrderedMatchSet set = new();
        Action act = () => set.AddExclude(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
