// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Tests for <see cref="GlobMatch"/>'s direct <see cref="IEnumerationMatcher"/>
///  implementation. Multi-pattern composition uses <see cref="MatchSet"/>; this
///  mirrors how <see cref="GlobEnumerator"/> wires real enumerations.
/// </summary>
public class GlobMatchEnumerationTests
{
    private static string Root => Path.Combine(Path.GetTempPath(), "glob-enum-root");

    private static GlobMatch Create(string includePattern, string? root = null) =>
        GlobSpecification.Compile(includePattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar)
            .CreateMatcher(root ?? Root);

    /// <summary>
    ///  Builds a <see cref="MatchSet"/> wrapping one include matcher plus an exclude
    ///  matcher per supplied pattern. Mirrors how <see cref="GlobEnumerator"/> composes
    ///  multi-pattern enumeration.
    /// </summary>
    private static MatchSet CreateSet(string includePattern, params string[] excludePatterns)
    {
        GlobMatch include = Create(includePattern);
        MatchSet set = new(include);
        foreach (string excludePattern in excludePatterns)
        {
            set.AddExclude(Create(excludePattern));
        }

        return set;
    }

    [Fact]
    public void MatchesFile_RootDirectory_TopLevel()
    {
        using GlobMatch matcher = Create("*.cs");
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesFile(Root, "file.cs".AsSpan()).Should().BeTrue();
        boundary.MatchesFile(Root, "file.txt".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_SubdirectoryPathIncluded()
    {
        using GlobMatch matcher = Create("**/*.cs");
        IEnumerationMatcher boundary = matcher;

        string subDir = Path.Combine(Root, "bin", "Debug");
        boundary.MatchesFile(subDir, "file.cs".AsSpan()).Should().BeTrue();
        boundary.MatchesFile(subDir, "file.txt".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void MatchSet_ExcludeBlocksInclude()
    {
        using MatchSet set = CreateSet("**/*.cs", "**/obj/**");
        IEnumerationMatcher boundary = set;

        string objDir = Path.Combine(Root, "obj", "Debug");
        boundary.MatchesFile(objDir, "file.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();

        string srcDir = Path.Combine(Root, "src");
        boundary.MatchesFile(srcDir, "file.cs".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void MatchSet_MultipleExcludes_AnyExcludeBlocks()
    {
        using MatchSet set = CreateSet("**/*.cs", "**/obj/**", "**/bin/**");
        IEnumerationMatcher boundary = set;

        boundary.MatchesFile(Path.Combine(Root, "obj"), "x.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Path.Combine(Root, "bin"), "x.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Path.Combine(Root, "src"), "x.cs".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void MatchesDirectory_NoLiteralPrefix_AlwaysRecursesOnInclusion()
    {
        using GlobMatch matcher = Create("**/*.cs");
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesDirectory(Root, "obj".AsSpan(), matchForExclusion: false).Should().BeTrue();
        boundary.MatchesDirectory(Root, "src".AsSpan(), matchForExclusion: false).Should().BeTrue();
    }

    [Fact]
    public void MatchesDirectory_LiteralPrefix_PrunesDivergedSubtree()
    {
        using GlobMatch matcher = Create("bin/Debug/**/*.cs");
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: false).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesDirectory(Root, "src".AsSpan(), matchForExclusion: false).Should().BeFalse();
        boundary.DirectoryFinished();
        boundary.MatchesDirectory(Root, "lib".AsSpan(), matchForExclusion: false).Should().BeFalse();
    }

    [Fact]
    public void MatchesDirectory_LiteralPrefix_RecursesIntoAlignedSubtree()
    {
        using GlobMatch matcher = Create("bin/Debug/**/*.cs");
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: false).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesDirectory(Path.Combine(Root, "bin"), "Debug".AsSpan(), matchForExclusion: false).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesDirectory(Path.Combine(Root, "bin"), "Other".AsSpan(), matchForExclusion: false).Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_LiteralPrefix_OnPrefix_DirRejectsFiles()
    {
        using GlobMatch matcher = Create("bin/Debug/**/*.cs");
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesFile(Root, "stray.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Path.Combine(Root, "bin"), "stray.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Path.Combine(Root, "bin", "Debug"), "ok.cs".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void MatchesFile_LiteralPrefix_DivergedDir_RejectsAllFiles()
    {
        using GlobMatch matcher = Create("bin/Debug/**/*.cs");
        IEnumerationMatcher boundary = matcher;

        string divergedDir = Path.Combine(Root, "src");
        boundary.MatchesFile(divergedDir, "file.cs".AsSpan()).Should().BeFalse();
        boundary.MatchesFile(divergedDir, "another.cs".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void MatchSet_ExcludeWithLiteralPrefix_IsSkippedInUnrelatedDir()
    {
        using MatchSet set = CreateSet("**/*.cs", "obj/Debug/**");
        IEnumerationMatcher boundary = set;

        boundary.MatchesFile(Path.Combine(Root, "src"), "file.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(Path.Combine(Root, "obj", "Debug"), "blocked.cs".AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void MatchesFile_NoLiteralPrefix_BehavesAsBefore()
    {
        using GlobMatch matcher = Create("**/*.cs");
        IEnumerationMatcher boundary = matcher;

        for (int depth = 0; depth < 5; depth++)
        {
            string dir = depth == 0
                ? Root
                : Path.Combine([Root, .. Enumerable.Range(0, depth).Select(i => $"d{i}")]);
            boundary.MatchesFile(dir, "x.cs".AsSpan()).Should().BeTrue();
            boundary.DirectoryFinished();
        }
    }

    [Fact]
    public void MatchesDirectory_NeverClaimsForExclusion()
    {
        using GlobMatch matcher = Create("**/*.cs");
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesDirectory(Root, "obj".AsSpan(), matchForExclusion: true).Should().BeFalse();
    }

    [Fact]
    public void MatchesDirectory_DirectoryOnly_ClaimsForExclusion()
    {
        // gitignore `bin/` (trailing '/') sets DirectoryOnly; the factory also
        // prepends `**/` to the non-anchored slash-free remainder so it matches at
        // any depth. As an exclude, the matcher should claim the whole subtree.
        GlobMatch matcher = GlobSpecification.Compile("bin/", GlobDialect.Git).CreateMatcher(Root);
        matcher.Specification.DirectoryOnly.Should().BeTrue();
        IEnumerationMatcher boundary = matcher;

        // Top-level `bin` directory is excluded.
        boundary.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: true).Should().BeTrue();
        boundary.DirectoryFinished();
        // Nested `bin` directory is also excluded (match-anywhere).
        boundary.MatchesDirectory(Path.Combine(Root, "src"), "bin".AsSpan(), matchForExclusion: true).Should().BeTrue();
        boundary.DirectoryFinished();
        // Unrelated directory is not excluded.
        boundary.MatchesDirectory(Root, "src".AsSpan(), matchForExclusion: true).Should().BeFalse();
        matcher.Dispose();
    }

    [Fact]
    public void MatchesFile_DirectoryOnly_NeverMatchesFiles()
    {
        // `bin/` (DirectoryOnly) never matches files, even files named `bin`.
        GlobMatch matcher = GlobSpecification.Compile("bin/", GlobDialect.Git).CreateMatcher(Root);
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesFile(Root, "bin".AsSpan()).Should().BeFalse();
        boundary.MatchesFile(Root, "anything".AsSpan()).Should().BeFalse();
        matcher.Dispose();
    }

    [Fact]
    public void MatchesDirectory_DirectoryOnly_DoesNotClaimUnmatchedDirs()
    {
        // `logs/` does not match `bin` etc.; only `logs` directories.
        GlobMatch matcher = GlobSpecification.Compile("logs/", GlobDialect.Git).CreateMatcher(Root);
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesDirectory(Root, "logs".AsSpan(), matchForExclusion: true).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesDirectory(Root, "bin".AsSpan(), matchForExclusion: true).Should().BeFalse();
        matcher.Dispose();
    }

    [Fact]
    public void MatchesFile_ReusesCachedPrefix_AcrossSameDirectory()
    {
        using GlobMatch matcher = Create("**/*.cs");
        IEnumerationMatcher boundary = matcher;

        string subDir = Path.Combine(Root, "a", "b", "c");
        for (int i = 0; i < 100; i++)
        {
            boundary.MatchesFile(subDir, $"file{i}.cs".AsSpan()).Should().BeTrue();
            boundary.MatchesFile(subDir, $"file{i}.txt".AsSpan()).Should().BeFalse();
        }
    }

    [Fact]
    public void DirectoryFinished_InvalidatesCache_NewDirectoryRespected()
    {
        using GlobMatch matcher = Create("a/*.cs");
        IEnumerationMatcher boundary = matcher;

        string aDir = Path.Combine(Root, "a");
        string bDir = Path.Combine(Root, "b");

        boundary.MatchesFile(aDir, "x.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();
        boundary.MatchesFile(bDir, "x.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished();
        boundary.MatchesFile(aDir, "y.cs".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void MatchesFile_LongRelativeDirectory_GrowsCacheBuffer()
    {
        using GlobMatch matcher = Create("**/*.cs");
        IEnumerationMatcher boundary = matcher;

        string[] segments = new string[40];
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = $"segment{i:D2}";
        }

        string deepDir = Path.Combine([Root, .. segments]);
        boundary.MatchesFile(deepDir, "file.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished();

        boundary.MatchesFile(Root, "shallow.cs".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void MatchesFile_TranslatesNativeSeparator_ToMatcherSeparator()
    {
        using GlobMatch matcher = Create("a/b/*.cs");
        IEnumerationMatcher boundary = matcher;

        string subDir = Path.Combine(Root, "a", "b");
        boundary.MatchesFile(subDir, "file.cs".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void MatchesFile_Negated_InvertsResult()
    {
        GlobMatch matcher = GlobSpecification.Compile("!*.cs", GlobDialect.Git).CreateMatcher(Root);
        matcher.Specification.Negated.Should().BeTrue();
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesFile(Root, "file.cs".AsSpan()).Should().BeFalse();
        boundary.MatchesFile(Root, "file.txt".AsSpan()).Should().BeTrue();
        matcher.Dispose();
    }

    [Fact]
    public void MatchesFile_NoRootDirectory_FallsBackToFileNameMatch()
    {
        // When RootDirectory is not set the matcher cannot resolve a relative path; it
        // must behave as a flat-string matcher over the file name itself.
        GlobMatch matcher = GlobSpecification.Compile("*.cs", GlobDialect.PosixPath).CreateMatcher();
        IEnumerationMatcher boundary = matcher;

        boundary.MatchesFile(Root, "file.cs".AsSpan()).Should().BeTrue();
        boundary.MatchesFile(Root, "file.txt".AsSpan()).Should().BeFalse();
        matcher.Dispose();
    }

    [Fact]
    public void Dispose_ReturnsRentedBuffer_NoThrow()
    {
        GlobMatch matcher = Create("**/*.cs");
        IEnumerationMatcher boundary = matcher;

        string[] segments = new string[40];
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = $"segment{i:D2}";
        }

        string deepDir = Path.Combine([Root, .. segments]);
        boundary.MatchesFile(deepDir, "file.cs".AsSpan());

        matcher.Dispose();
        matcher.Dispose();
    }
}
