// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

[TestClass]
public class FileSystemMatchEnumeratorTests
{
#if !DEBUG
    [TestMethod]
    public void NormalizeRootDirectory_NormalizedRoot_DoesNotAllocate()
    {
        string rootDirectory = Path.GetFullPath(Environment.CurrentDirectory);
        FileSystemMatchEnumeratorArguments.NormalizeRootDirectory(rootDirectory);

        string normalizedRoot;
        using (MemoryWatch.Create)
        {
            normalizedRoot = FileSystemMatchEnumeratorArguments.NormalizeRootDirectory(rootDirectory);
        }

        normalizedRoot.Should().BeSameAs(rootDirectory);
    }
#endif

    [TestMethod]
    public void MoveNext_MatchingFiles_ReturnsCanonicalRelativePaths()
    {
        using TempFolder folder = new();
        string source = Path.Combine(folder.TempPath, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.cs"), string.Empty);
        File.WriteAllText(Path.Combine(source, "a.txt"), string.Empty);
        IFileSystemMatcher matcher = FileSystemMatcher.Create(
            (_, fileName) => fileName.EndsWith(".cs"));
        using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
            folder.TempPath,
            matcher);

        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        results.Should().Equal("src/a.cs");
    }

    [TestMethod]
    public void Constructor_DoesNotCreateSessionUntilTraversal()
    {
        using TempFolder folder = new();
        TrackingMatcher matcher = new();
        using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
            folder.TempPath,
            matcher);

        matcher.CreateCount.Should().Be(0);
        enumerator.MoveNext().Should().BeFalse();
        matcher.CreateCount.Should().Be(0);
    }

    [TestMethod]
    public void MoveNext_RelativeRootWithTrailingSeparator_PassesNormalizedRootToSession()
    {
        using TempFolder folder = new();
        File.WriteAllText(Path.Combine(folder.TempPath, "a.cs"), string.Empty);
        string relativeRoot = Path.GetRelativePath(Environment.CurrentDirectory, folder.TempPath)
            + Path.DirectorySeparatorChar;
        TrackingMatcher matcher = new();
        using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
            relativeRoot,
            matcher);

        enumerator.MoveNext().Should().BeTrue();

        matcher.LastRootDirectory.Should().Be(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(relativeRoot)));
    }

    [TestMethod]
    public void MoveNext_DirectoryExcluded_DoesNotVisitDescendant()
    {
        using TempFolder folder = new();
        string excluded = Path.Combine(folder.TempPath, "obj");
        Directory.CreateDirectory(excluded);
        File.WriteAllText(Path.Combine(excluded, "a.cs"), string.Empty);
        IFileSystemMatcher matcher = new ExcludeDirectoryMatcher("obj");
        using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
            folder.TempPath,
            matcher);

        enumerator.MoveNext().Should().BeFalse();
    }

    [TestMethod]
    public void Dispose_AfterTraversal_DisposesSessionOnce()
    {
        using TempFolder folder = new();
        File.WriteAllText(Path.Combine(folder.TempPath, "a.cs"), string.Empty);
        TrackingMatcher matcher = new();
        FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
            folder.TempPath,
            matcher);
        try
        {
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Dispose();
            enumerator.Dispose();
        }
        finally
        {
            enumerator.Dispose();
        }

        matcher.CreateCount.Should().Be(1);
        matcher.DisposeCount.Should().Be(1);
    }

    [TestMethod]
    public void MoveNext_CompiledGlobDefinition_MatchesExpectedFiles()
    {
        using TempFolder folder = new();
        string source = Path.Combine(folder.TempPath, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.cs"), string.Empty);
        File.WriteAllText(Path.Combine(source, "a.txt"), string.Empty);
        GlobSpecification specification = GlobSpecification.Compile(
            "**/*.cs",
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar);
        using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
            folder.TempPath,
            specification.CreateFileSystemMatcher());

        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        results.Should().Equal("src/a.cs");
    }

    [TestMethod]
    public void Create_CallerMutatesEnumerationOptions_TraversalUsesSnapshot()
    {
        using TempFolder folder = new();
        string source = Path.Combine(folder.TempPath, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.cs"), string.Empty);
        EnumerationOptions options = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        };
        IFileSystemMatcher matcher = FileSystemMatcher.Create((_, _) => true);
        using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
            folder.TempPath,
            matcher,
            options);

        options.RecurseSubdirectories = false;
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        results.Should().Contain("src/a.cs");
    }

    [TestMethod]
    public void MoveNext_UnixLiteralBackslash_PreservesFilenameCharacter()
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            Assert.Inconclusive("A backslash is a directory separator on Windows.");
        }

        using TempFolder folder = new();
        string directory = Path.Combine(folder.TempPath, "literal\\name");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "file.cs"), string.Empty);
        string? observedPath = null;
        IFileSystemMatcher matcher = FileSystemMatcher.CreatePath(path =>
        {
            observedPath = path.ToString();
            return true;
        });
        using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
            folder.TempPath,
            matcher);

        enumerator.MoveNext().Should().BeTrue();

        observedPath.Should().Be("literal\\name/file.cs");
        enumerator.Current.Should().Be("literal\\name/file.cs");
    }

    [TestMethod]
    public void Dispose_DerivedAndSessionThrow_AttemptsDerivedThenSessionAndPreservesFirstException()
    {
        using TempFolder folder = new();
        File.WriteAllText(Path.Combine(folder.TempPath, "file.cs"), string.Empty);
        List<string> order = [];
        DisposalOrderEnumerator enumerator = new(folder.TempPath, new DisposalOrderMatcher(order), order);
        try
        {
            enumerator.MoveNext().Should().BeTrue();

            Action action = enumerator.Dispose;

            action.Should().Throw<InvalidOperationException>().WithMessage("derived");
            order.Should().Equal("derived", "session");
        }
        finally
        {
            enumerator.Dispose();
        }
    }

    private sealed class TrackingMatcher : IFileSystemMatcher
    {
        public int CreateCount { get; private set; }

        public int DisposeCount { get; private set; }

        public string? LastRootDirectory { get; private set; }

        public IFileSystemMatcherSession CreateSession(string rootDirectory)
        {
            CreateCount++;
            LastRootDirectory = rootDirectory;
            return new TrackingSession(this);
        }

        private sealed class TrackingSession(TrackingMatcher owner) : FileSystemMatcherSession
        {
            public override bool MatchesFile(
                ReadOnlySpan<char> currentDirectory,
                ReadOnlySpan<char> fileName) => true;

            public override void Dispose() => owner.DisposeCount++;
        }
    }

    private sealed class ExcludeDirectoryMatcher(string excludedName) : IFileSystemMatcher
    {
        public IFileSystemMatcherSession CreateSession(string rootDirectory) =>
            new ExcludeDirectorySession(excludedName);
    }

    private sealed class ExcludeDirectorySession(string excludedName) : FileSystemMatcherSession
    {
        public override bool MatchesFile(
            ReadOnlySpan<char> currentDirectory,
            ReadOnlySpan<char> fileName) => true;

        public override DirectoryMatchType MatchesDirectory(
            ReadOnlySpan<char> currentDirectory,
            ReadOnlySpan<char> directoryName) => directoryName.Equals(excludedName, StringComparison.Ordinal)
                ? DirectoryMatchType.NoDescendantFilesMatch
                : DirectoryMatchType.MayContainMatchingFiles;
    }

    private sealed class DisposalOrderMatcher(List<string> order) : IFileSystemMatcher
    {
        public IFileSystemMatcherSession CreateSession(string rootDirectory) => new DisposalOrderSession(order);
    }

    private sealed class DisposalOrderSession(List<string> order) : FileSystemMatcherSession
    {
        public override bool MatchesFile(
            ReadOnlySpan<char> currentDirectory,
            ReadOnlySpan<char> fileName) => true;

        public override void Dispose()
        {
            order.Add("session");
            throw new InvalidOperationException("session");
        }
    }

    private sealed class DisposalOrderEnumerator(
        string rootDirectory,
        IFileSystemMatcher matcher,
        List<string> order) : FileSystemMatchEnumerator<string>(rootDirectory, matcher)
    {
        protected override string TransformEntry(ref FileSystemEntry entry) => entry.FileName.ToString();

        protected override void DisposeAdditionalResources(bool disposing)
        {
            order.Add("derived");
            throw new InvalidOperationException("derived");
        }
    }
}