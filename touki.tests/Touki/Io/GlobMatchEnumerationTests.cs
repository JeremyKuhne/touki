// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Tests for the optimized glob file-system session and its exclusion composition.
/// </summary>
[TestClass]
public class GlobMatchEnumerationTests
{
    private static string Root => Path.Combine(Path.GetTempPath(), "glob-enum-root");

    private static IFileSystemMatcherSession Create(string includePattern, string? root = null) =>
        GlobSpecification.Compile(includePattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar)
            .CreateSession(root ?? Root);

    /// <summary>
    ///  Builds the same owned include/exclude session used by replay enumeration.
    /// </summary>
    private static IFileSystemMatcherSession CreateSet(string includePattern, params string[] excludePatterns) =>
        GlobEnumerator.BuildSession(
            includePattern,
            excludePatterns,
            Root,
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar);

    [TestMethod]
    public void MatchesFile_RootDirectory_TopLevel()
    {
        using IFileSystemMatcherSession boundary = Create("*.cs");

        boundary.MatchesFile(Root, "file.cs".AsSpan()).Should().BeTrue();
        boundary.MatchesFile(Root, "file.txt".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void MatchesFile_SubdirectoryPathIncluded()
    {
        using IFileSystemMatcherSession boundary = Create("**/*.cs");

        string subDir = Path.Combine(Root, "bin", "Debug");
        boundary.MatchesFile(subDir, "file.cs".AsSpan()).Should().BeTrue();
        boundary.MatchesFile(subDir, "file.txt".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void MatchesFile_ComposedSession_ExcludeBlocksInclude()
    {
        using IFileSystemMatcherSession boundary = CreateSet("**/*.cs", "**/obj/**");

        string objDir = Path.Combine(Root, "obj", "Debug");
        boundary.MatchesFile(objDir, "file.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished(objDir);

        string srcDir = Path.Combine(Root, "src");
        boundary.MatchesFile(srcDir, "file.cs".AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_ComposedSession_MultipleExcludes_AnyExcludeBlocks()
    {
        using IFileSystemMatcherSession boundary = CreateSet("**/*.cs", "**/obj/**", "**/bin/**");

        string objDirectory = Path.Combine(Root, "obj");
        string binDirectory = Path.Combine(Root, "bin");
        boundary.MatchesFile(objDirectory, "x.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished(objDirectory);
        boundary.MatchesFile(binDirectory, "x.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished(binDirectory);
        boundary.MatchesFile(Path.Combine(Root, "src"), "x.cs".AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesDirectory_NoLiteralPrefix_AlwaysRecursesOnInclusion()
    {
        using IFileSystemMatcherSession boundary = Create("**/*.cs");

        boundary.MatchesDirectory(Root, "obj".AsSpan())
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
        boundary.MatchesDirectory(Root, "src".AsSpan())
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
    }

    [TestMethod]
    public void MatchesDirectory_LiteralPrefix_PrunesDivergedSubtree()
    {
        using IFileSystemMatcherSession boundary = Create("bin/Debug/**/*.cs");

        boundary.MatchesDirectory(Root, "bin".AsSpan())
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
        boundary.DirectoryFinished(Root);
        boundary.MatchesDirectory(Root, "src".AsSpan())
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
        boundary.DirectoryFinished(Root);
        boundary.MatchesDirectory(Root, "lib".AsSpan())
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
    }

    [TestMethod]
    public void MatchesDirectory_LiteralPrefix_RecursesIntoAlignedSubtree()
    {
        using IFileSystemMatcherSession boundary = Create("bin/Debug/**/*.cs");

        string binDirectory = Path.Combine(Root, "bin");
        boundary.MatchesDirectory(Root, "bin".AsSpan())
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
        boundary.DirectoryFinished(Root);
        boundary.MatchesDirectory(binDirectory, "Debug".AsSpan())
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
        boundary.DirectoryFinished(binDirectory);
        boundary.MatchesDirectory(binDirectory, "Other".AsSpan())
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
    }

    [TestMethod]
    public void MatchesFile_LiteralPrefix_OnPrefix_DirRejectsFiles()
    {
        using IFileSystemMatcherSession boundary = Create("bin/Debug/**/*.cs");

        boundary.MatchesFile(Root, "stray.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished(Root);
        string binDirectory = Path.Combine(Root, "bin");
        boundary.MatchesFile(binDirectory, "stray.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished(binDirectory);
        boundary.MatchesFile(Path.Combine(Root, "bin", "Debug"), "ok.cs".AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_LiteralPrefix_DivergedDir_RejectsAllFiles()
    {
        using IFileSystemMatcherSession boundary = Create("bin/Debug/**/*.cs");

        string divergedDir = Path.Combine(Root, "src");
        boundary.MatchesFile(divergedDir, "file.cs".AsSpan()).Should().BeFalse();
        boundary.MatchesFile(divergedDir, "another.cs".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void MatchesFile_ComposedSession_ExcludeWithLiteralPrefix_IsSkippedInUnrelatedDir()
    {
        using IFileSystemMatcherSession boundary = CreateSet("**/*.cs", "obj/Debug/**");

        string sourceDirectory = Path.Combine(Root, "src");
        boundary.MatchesFile(sourceDirectory, "file.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished(sourceDirectory);
        boundary.MatchesFile(Path.Combine(Root, "obj", "Debug"), "blocked.cs".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void MatchesFile_NoLiteralPrefix_BehavesAsBefore()
    {
        using IFileSystemMatcherSession boundary = Create("**/*.cs");

        for (int depth = 0; depth < 5; depth++)
        {
            string dir = depth == 0
                ? Root
                : Path.Combine([Root, .. Enumerable.Range(0, depth).Select(i => $"d{i}")]);
            boundary.MatchesFile(dir, "x.cs".AsSpan()).Should().BeTrue();
            boundary.DirectoryFinished(dir);
        }
    }

    [TestMethod]
    public void MatchesDirectory_FilePattern_ReturnsMayContainMatchingFiles()
    {
        using IFileSystemMatcherSession boundary = Create("**/*.cs");

        boundary.MatchesDirectory(Root, "obj".AsSpan())
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
    }

    [TestMethod]
    public void MatchesDirectory_DirectoryOnly_ReturnsAllDescendantFilesMatch()
    {
        // gitignore `bin/` (trailing '/') sets DirectoryOnly; the factory also
        // prepends `**/` to the non-anchored slash-free remainder so it matches at
        // any depth. As an exclude, the matcher should claim the whole subtree.
        using GlobMatch matcher = GlobSpecification.Compile("bin/", GlobDialect.Git).CreateSession(Root);
        matcher.Specification.DirectoryOnly.Should().BeTrue();

        // Top-level `bin` directory is excluded.
        matcher.MatchesDirectory(Root, "bin".AsSpan())
            .Should().Be(DirectoryMatchType.AllDescendantFilesMatch);
        matcher.DirectoryFinished(Root);
        // Nested `bin` directory is also excluded (match-anywhere).
        string sourceDirectory = Path.Combine(Root, "src");
        matcher.MatchesDirectory(sourceDirectory, "bin".AsSpan())
            .Should().Be(DirectoryMatchType.AllDescendantFilesMatch);
        matcher.DirectoryFinished(sourceDirectory);
        // Unrelated directory is not excluded.
        matcher.MatchesDirectory(Root, "src".AsSpan())
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
    }

    [TestMethod]
    public void MatchesFile_DirectoryOnly_NeverMatchesFiles()
    {
        // `bin/` (DirectoryOnly) never matches files, even files named `bin`.
        using GlobMatch matcher = GlobSpecification.Compile("bin/", GlobDialect.Git).CreateSession(Root);

        matcher.MatchesFile(Root, "bin".AsSpan()).Should().BeFalse();
        matcher.MatchesFile(Root, "anything".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void MatchesDirectory_DirectoryOnly_DoesNotClaimUnmatchedDirs()
    {
        // `logs/` does not match `bin` etc.; only `logs` directories.
        using GlobMatch matcher = GlobSpecification.Compile("logs/", GlobDialect.Git).CreateSession(Root);

        matcher.MatchesDirectory(Root, "logs".AsSpan())
            .Should().Be(DirectoryMatchType.AllDescendantFilesMatch);
        matcher.DirectoryFinished(Root);
        matcher.MatchesDirectory(Root, "bin".AsSpan())
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
    }

    [TestMethod]
    public void MatchesDirectory_OrdinaryGitDirectory_ProofMatchesDescendantFiles()
    {
        using GlobMatch matcher = GlobSpecification.Compile(
            "/node_modules",
            GlobDialect.Git).CreateSession(Root);

        matcher.MatchesDirectory(Root, "node_modules")
            .Should().Be(DirectoryMatchType.AllDescendantFilesMatch);
        matcher.DirectoryFinished(Root);
        string packageDirectory = Path.Combine(Root, "node_modules", "package");
        matcher.MatchesFile(packageDirectory, "index.js").Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_OrdinaryGitMatchedAncestor_CachesUntilDirectoryFinished()
    {
        using GlobMatch matcher = GlobSpecification.Compile(
            "/node_modules",
            GlobDialect.Git).CreateSession(Root);

        string packageDirectory = Path.Combine(Root, "node_modules", "package");
        matcher.MatchesFile(packageDirectory, "index.js").Should().BeTrue();
        ((bool)matcher.TestAccessor.Dynamic._cacheValid).Should().BeTrue();
        ((bool)matcher.TestAccessor.Dynamic._directoryAncestorMatched).Should().BeTrue();

        matcher.MatchesFile(packageDirectory, "other.js").Should().BeTrue();
        matcher.DirectoryFinished(packageDirectory);
        ((bool)matcher.TestAccessor.Dynamic._cacheValid).Should().BeFalse();
        matcher.MatchesFile(Path.Combine(Root, "src"), "index.js").Should().BeFalse();
    }

    [TestMethod]
    public void MatchesDirectory_NegatedDirectoryOnly_ProofMatchesDescendantFiles()
    {
        using GlobMatch matcher = GlobSpecification.Compile(
            "!bin/",
            GlobDialect.Git).CreateSession(Root);

        matcher.MatchesDirectory(Root, "bin")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
        matcher.DirectoryFinished(Root);
        matcher.MatchesFile(Path.Combine(Root, "bin"), "file.txt").Should().BeFalse();
        matcher.DirectoryFinished(Path.Combine(Root, "bin"));
        matcher.MatchesFile(Path.Combine(Root, "src"), "file.txt").Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_ReusesCachedPrefix_AcrossSameDirectory()
    {
        using IFileSystemMatcherSession boundary = Create("**/*.cs");

        string subDir = Path.Combine(Root, "a", "b", "c");
        for (int i = 0; i < 100; i++)
        {
            boundary.MatchesFile(subDir, $"file{i}.cs".AsSpan()).Should().BeTrue();
            boundary.MatchesFile(subDir, $"file{i}.txt".AsSpan()).Should().BeFalse();
        }
    }

    [TestMethod]
    public void DirectoryFinished_InvalidatesCache_NewDirectoryRespected()
    {
        using IFileSystemMatcherSession boundary = Create("a/*.cs");

        string aDir = Path.Combine(Root, "a");
        string bDir = Path.Combine(Root, "b");

        boundary.MatchesFile(aDir, "x.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished(aDir);
        boundary.MatchesFile(bDir, "x.cs".AsSpan()).Should().BeFalse();
        boundary.DirectoryFinished(bDir);
        boundary.MatchesFile(aDir, "y.cs".AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_LongRelativeDirectory_GrowsCacheBuffer()
    {
        using IFileSystemMatcherSession boundary = Create("**/*.cs");

        string[] segments = new string[40];
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = $"segment{i:D2}";
        }

        string deepDir = Path.Combine([Root, .. segments]);
        boundary.MatchesFile(deepDir, "file.cs".AsSpan()).Should().BeTrue();
        boundary.DirectoryFinished(deepDir);

        boundary.MatchesFile(Root, "shallow.cs".AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_TranslatesNativeSeparator_ToMatcherSeparator()
    {
        using IFileSystemMatcherSession boundary = Create("a/b/*.cs");

        string subDir = Path.Combine(Root, "a", "b");
        boundary.MatchesFile(subDir, "file.cs".AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_Negated_InvertsResult()
    {
        using GlobMatch matcher = GlobSpecification.Compile("!*.cs", GlobDialect.Git).CreateSession(Root);
        matcher.Specification.Negated.Should().BeTrue();

        matcher.MatchesFile(Root, "file.cs".AsSpan()).Should().BeFalse();
        matcher.MatchesFile(Root, "file.txt".AsSpan()).Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_NoRootDirectory_FallsBackToFileNameMatch()
    {
        // When RootDirectory is not set the matcher cannot resolve a relative path; it
        // must behave as a flat-string matcher over the file name itself.
        using GlobMatch matcher = new(
            GlobSpecification.Compile("*.cs", GlobDialect.PosixPath),
            rootDirectory: null);

        matcher.MatchesFile(Root, "file.cs".AsSpan()).Should().BeTrue();
        matcher.MatchesFile(Root, "file.txt".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void Dispose_ReturnsRentedBuffer_NoThrow()
    {
        IFileSystemMatcherSession matcher = Create("**/*.cs");
        try
        {
            string[] segments = new string[40];
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = $"segment{i:D2}";
            }

            string deepDir = Path.Combine([Root, .. segments]);
            matcher.MatchesFile(deepDir, "file.cs".AsSpan());

            matcher.Dispose();
            matcher.Dispose();
        }
        finally
        {
            matcher.Dispose();
        }
    }
}
