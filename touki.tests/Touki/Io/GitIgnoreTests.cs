// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Integration tests for the <see cref="GitIgnore"/> loader: parsing
///  <c>.gitignore</c> content into an <see cref="OrderedMatchSet"/> and verifying
///  the composed evaluation matches gitignore semantics.
/// </summary>
public class GitIgnoreTests
{
    private static string Root => Path.Combine(Path.GetTempPath(), "gitignore-root");

    [Fact]
    public void Parse_EmptyContent_ProducesEmptySet()
    {
        using OrderedMatchSet set = GitIgnore.Parse("", Root);
        set.Count.Should().Be(0);
    }

    [Fact]
    public void Parse_BlankAndCommentLines_AreSkipped()
    {
        const string content = """

            # this is a comment
            # another comment
            
            *.log

            """;
        using OrderedMatchSet set = GitIgnore.Parse(content, Root);
        set.Count.Should().Be(1);
    }

    [Fact]
    public void Parse_SingleExcludeRule_ExcludesMatchingFiles()
    {
        using OrderedMatchSet set = GitIgnore.Parse("*.log", Root);
        IEnumerationMatcher matcher = set;

        // Matched by the exclude → not included.
        matcher.MatchesFile(Root, "trace.log".AsSpan()).Should().BeFalse();
        matcher.DirectoryFinished();
        // Not matched by any rule → default-include applies → included.
        matcher.MatchesFile(Root, "trace.txt".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void Parse_IncludeAfterExclude_RescuesMatchingFile()
    {
        // Models the canonical .gitignore "exclude all, but rescue one":
        //   *.log
        //   !keep.log
        const string content = """
            *.log
            !keep.log
            """;
        using OrderedMatchSet set = GitIgnore.Parse(content, Root);
        IEnumerationMatcher matcher = set;

        matcher.MatchesFile(Root, "keep.log".AsSpan()).Should().BeTrue();
        matcher.DirectoryFinished();
        matcher.MatchesFile(Root, "trace.log".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void Parse_DirectoryOnlyRule_ClaimsSubtree()
    {
        using OrderedMatchSet set = GitIgnore.Parse("bin/", Root);
        IEnumerationMatcher matcher = set;

        matcher.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: true).Should().BeTrue();
        matcher.DirectoryFinished();
        matcher.MatchesDirectory(Root, "src".AsSpan(), matchForExclusion: true).Should().BeFalse();
    }

    [Fact]
    public void Parse_LeadingBackslashEscapesHash()
    {
        // `\#literal` matches a file literally named `#literal`.
        using OrderedMatchSet set = GitIgnore.Parse(@"\#literal", Root);
        IEnumerationMatcher matcher = set;

        matcher.MatchesFile(Root, "#literal".AsSpan()).Should().BeFalse();  // excluded
    }

    [Fact]
    public void Parse_LeadingBackslashEscapesBang()
    {
        // `\!important.txt` matches a file literally named `!important.txt`.
        using OrderedMatchSet set = GitIgnore.Parse(@"\!important.txt", Root);
        IEnumerationMatcher matcher = set;

        matcher.MatchesFile(Root, "!important.txt".AsSpan()).Should().BeFalse();  // excluded
    }

    [Fact]
    public void Parse_RootAnchoredRule_OnlyMatchesAtRoot()
    {
        // `/build` (leading `/`) is root-anchored.
        using OrderedMatchSet set = GitIgnore.Parse("/build", Root);
        IEnumerationMatcher matcher = set;

        // At root the rule matches → excluded.
        matcher.MatchesFile(Root, "build".AsSpan()).Should().BeFalse();
        matcher.DirectoryFinished();
        // Nested `build` is NOT matched by the root-anchored rule, so the
        // default-include semantics keep it included.
        matcher.MatchesFile(Path.Combine(Root, "src"), "build".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void Parse_NonAnchoredRule_MatchesAtAnyDepth()
    {
        // `*.log` (no `/`) matches at any depth per gitignore.
        using OrderedMatchSet set = GitIgnore.Parse("*.log", Root);
        IEnumerationMatcher matcher = set;

        matcher.MatchesFile(Root, "a.log".AsSpan()).Should().BeFalse();
        matcher.DirectoryFinished();
        matcher.MatchesFile(Path.Combine(Root, "deep", "nested", "dir"), "a.log".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void Parse_TrailingWhitespace_IsStripped()
    {
        // Lines with trailing whitespace should be parsed as if the whitespace wasn't
        // there.
        const string content = "*.log   \n*.tmp\t\t";
        using OrderedMatchSet set = GitIgnore.Parse(content, Root);
        set.Count.Should().Be(2);

        IEnumerationMatcher matcher = set;
        // Both exclude rules match → not included.
        matcher.MatchesFile(Root, "a.log".AsSpan()).Should().BeFalse();
        matcher.DirectoryFinished();
        matcher.MatchesFile(Root, "a.tmp".AsSpan()).Should().BeFalse();
        matcher.DirectoryFinished();
        // A file matching neither rule remains included.
        matcher.MatchesFile(Root, "a.cs".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void Parse_CrlfLineEndings_AreHandled()
    {
        const string content = "*.log\r\n*.tmp\r\n";
        using OrderedMatchSet set = GitIgnore.Parse(content, Root);
        set.Count.Should().Be(2);
    }

    [Fact]
    public void Parse_RealisticGitignore_EvaluatesCorrectly()
    {
        // A realistic .gitignore subset combining excludes, re-includes, root anchors,
        // and directory-only markers. Verifies the end-to-end evaluation.
        const string content = """
            # build outputs
            bin/
            obj/

            # logs
            *.log
            !keep.log

            # explicit root anchor
            /node_modules
            """;
        using OrderedMatchSet set = GitIgnore.Parse(content, Root);
        IEnumerationMatcher matcher = set;

        // bin/ and obj/ are DirectoryOnly excludes; strict gitignore semantics
        // claim the whole subtree. Even though `!keep.log` follows, it cannot
        // rescue files under an excluded directory (gitignore(5)).
        matcher.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: true).Should().BeTrue();
        matcher.DirectoryFinished();
        matcher.MatchesDirectory(Root, "obj".AsSpan(), matchForExclusion: true).Should().BeTrue();
        matcher.DirectoryFinished();
        // src/ is not excluded.
        matcher.MatchesDirectory(Root, "src".AsSpan(), matchForExclusion: true).Should().BeFalse();
        matcher.DirectoryFinished();

        // *.log excluded, but keep.log re-included.
        matcher.MatchesFile(Root, "trace.log".AsSpan()).Should().BeFalse();
        matcher.DirectoryFinished();
        matcher.MatchesFile(Root, "keep.log".AsSpan()).Should().BeTrue();
        matcher.DirectoryFinished();

        // node_modules is excluded at root only (leading /); the rule is not
        // DirectoryOnly so it doesn't claim the subtree at directory time.
        matcher.MatchesDirectory(Root, "node_modules".AsSpan(), matchForExclusion: true).Should().BeFalse();
        matcher.DirectoryFinished();
        matcher.MatchesFile(Root, "node_modules".AsSpan()).Should().BeFalse();  // matched → excluded
    }

    [Fact]
    public void Parse_OnlyExcludes_ClaimsSubtrees()
    {
        // When there are no include rules after a DirectoryOnly exclude, the set
        // can safely claim the whole subtree for exclusion.
        const string content = """
            bin/
            obj/
            """;
        using OrderedMatchSet set = GitIgnore.Parse(content, Root);
        IEnumerationMatcher matcher = set;

        matcher.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: true).Should().BeTrue();
        matcher.DirectoryFinished();
        matcher.MatchesDirectory(Root, "obj".AsSpan(), matchForExclusion: true).Should().BeTrue();
        matcher.DirectoryFinished();
        matcher.MatchesDirectory(Root, "src".AsSpan(), matchForExclusion: true).Should().BeFalse();
    }

    [Fact]
    public void AddRules_AppendsToExistingSet()
    {
        // Stacking parent and child .gitignore: child rules go after parent so they
        // can override.
        using OrderedMatchSet set = GitIgnore.Parse("*.log", Root);
        GitIgnore.AddRules(set, "!keep.log", Root);

        set.Count.Should().Be(2);

        IEnumerationMatcher matcher = set;
        matcher.MatchesFile(Root, "keep.log".AsSpan()).Should().BeTrue();
        matcher.DirectoryFinished();
        matcher.MatchesFile(Root, "trace.log".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void Parse_NullContent_Throws()
    {
        Action act = () => GitIgnore.Parse(null!, Root);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Parse_NullRoot_Throws()
    {
        Action act = () => GitIgnore.Parse("*.log", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Parse_ReturnsIncludeByDefaultSet()
    {
        // Gitignore semantics: by default files are included; ignore rules
        // subtract. The returned set must be configured with IncludeByDefault.
        using OrderedMatchSet set = GitIgnore.Parse("*.log", Root);
        set.IncludeByDefault.Should().BeTrue();
    }

    [Fact]
    public void Parse_NonMatchingFile_IsIncluded()
    {
        // `*.log` excludes log files; `trace.txt` matches no rule so it must
        // remain included (the gitignore default-include semantics).
        using OrderedMatchSet set = GitIgnore.Parse("*.log", Root);
        IEnumerationMatcher matcher = set;

        matcher.MatchesFile(Root, "trace.txt".AsSpan()).Should().BeTrue();
        matcher.DirectoryFinished();
        matcher.MatchesFile(Root, "trace.log".AsSpan()).Should().BeFalse();
    }
}
