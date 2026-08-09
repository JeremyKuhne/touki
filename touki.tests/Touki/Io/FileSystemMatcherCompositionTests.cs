// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

[TestClass]
public class FileSystemMatcherCompositionTests
{
    [TestMethod]
    public void CreateExclusionWins_FileMatches_ExcludesTakePrecedence()
    {
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins(
            [FileSystemMatcher.Create((_, fileName) => fileName.EndsWith(".cs"))],
            [FileSystemMatcher.Create((_, fileName) => fileName.StartsWith("Generated"))]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesFile("root", "Source.cs").Should().BeTrue();
        session.MatchesFile("root", "Generated.cs").Should().BeFalse();
        session.MatchesFile("root", "Source.txt").Should().BeFalse();
    }

    [TestMethod]
    public void CreateExclusionWins_DirectoryMatches_CombinesThreeStateResults()
    {
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins(
            [new ConstantMatcher(DirectoryMatchType.AllDescendantFilesMatch)],
            [new ConstantMatcher(DirectoryMatchType.MayContainMatchingFiles)]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesDirectory("root", "child").Should().Be(DirectoryMatchType.MayContainMatchingFiles);
    }

    public static IEnumerable<(DirectoryMatchType, DirectoryMatchType, DirectoryMatchType)>
        ExclusionWinsDirectoryRows()
    {
        DirectoryMatchType[] values =
        [
            DirectoryMatchType.NoDescendantFilesMatch,
            DirectoryMatchType.MayContainMatchingFiles,
            DirectoryMatchType.AllDescendantFilesMatch,
            (DirectoryMatchType)255
        ];

        foreach (DirectoryMatchType include in values)
        {
            foreach (DirectoryMatchType exclude in values)
            {
                DirectoryMatchType normalizedInclude = NormalizeForOracle(include);
                DirectoryMatchType normalizedExclude = NormalizeForOracle(exclude);
                DirectoryMatchType expected = normalizedInclude == DirectoryMatchType.NoDescendantFilesMatch
                    || normalizedExclude == DirectoryMatchType.AllDescendantFilesMatch
                        ? DirectoryMatchType.NoDescendantFilesMatch
                        : normalizedInclude == DirectoryMatchType.AllDescendantFilesMatch
                            && normalizedExclude == DirectoryMatchType.NoDescendantFilesMatch
                                ? DirectoryMatchType.AllDescendantFilesMatch
                                : DirectoryMatchType.MayContainMatchingFiles;
                yield return (include, exclude, expected);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(ExclusionWinsDirectoryRows))]
    public void CreateExclusionWins_DirectoryTruthTable_ReturnsExpected(
        DirectoryMatchType include,
        DirectoryMatchType exclude,
        DirectoryMatchType expected)
    {
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins(
            [new ConstantMatcher(include)],
            [new ConstantMatcher(exclude)]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesDirectory("root", "child").Should().Be(expected);
    }

    [TestMethod]
    public void CreateOrdered_LaterIncludeAfterExclude_RequiresRecursion()
    {
        IFileSystemMatcher matcher = FileSystemMatcher.CreateOrdered(
        [
            new(new ConstantMatcher(DirectoryMatchType.AllDescendantFilesMatch), FileSystemMatchAction.Exclude),
            new(new ConstantMatcher(DirectoryMatchType.MayContainMatchingFiles), FileSystemMatchAction.Include)
        ],
            includeUnmatched: true);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesDirectory("root", "child").Should().Be(DirectoryMatchType.MayContainMatchingFiles);
    }

    [TestMethod]
    public void CreateOrdered_LaterExcludeAfterPossibleInclude_Prunes()
    {
        IFileSystemMatcher matcher = FileSystemMatcher.CreateOrdered(
        [
            new(new ConstantMatcher(DirectoryMatchType.MayContainMatchingFiles), FileSystemMatchAction.Include),
            new(new ConstantMatcher(DirectoryMatchType.AllDescendantFilesMatch), FileSystemMatchAction.Exclude)
        ]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesDirectory("root", "child").Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
    }

    public static IEnumerable<(DirectoryMatchType, FileSystemMatchAction, bool, DirectoryMatchType)>
        OrderedDirectoryRows()
    {
        DirectoryMatchType[] values =
        [
            DirectoryMatchType.NoDescendantFilesMatch,
            DirectoryMatchType.MayContainMatchingFiles,
            DirectoryMatchType.AllDescendantFilesMatch,
            (DirectoryMatchType)255
        ];

        foreach (DirectoryMatchType value in values)
        {
            DirectoryMatchType normalized = NormalizeForOracle(value);
            yield return (value, FileSystemMatchAction.Include, false, normalized);
            yield return (value, FileSystemMatchAction.Include, true, DirectoryMatchType.AllDescendantFilesMatch);
            yield return (value, FileSystemMatchAction.Exclude, false, DirectoryMatchType.NoDescendantFilesMatch);
            yield return (
                value,
                FileSystemMatchAction.Exclude,
                true,
                normalized switch
                {
                    DirectoryMatchType.NoDescendantFilesMatch => DirectoryMatchType.AllDescendantFilesMatch,
                    DirectoryMatchType.AllDescendantFilesMatch => DirectoryMatchType.NoDescendantFilesMatch,
                    _ => DirectoryMatchType.MayContainMatchingFiles
                });
        }
    }

    [TestMethod]
    [DynamicData(nameof(OrderedDirectoryRows))]
    public void CreateOrdered_DirectoryTruthTable_ReturnsExpected(
        DirectoryMatchType matchType,
        FileSystemMatchAction action,
        bool includeUnmatched,
        DirectoryMatchType expected)
    {
        IFileSystemMatcher matcher = FileSystemMatcher.CreateOrdered(
            [new(new ConstantMatcher(matchType), action)],
            includeUnmatched);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesDirectory("root", "child").Should().Be(expected);
    }

    [TestMethod]
    public void CreateExclusionWins_PathChildren_UseCanonicalPathDispatch()
    {
        CanonicalTrackingMatcher first = new(matches: false);
        CanonicalTrackingMatcher second = new(matches: true);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([first, second]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesFile("root/sub", "file.cs").Should().BeTrue();

        first.CanonicalPathCalls.Should().Be(1);
        second.CanonicalPathCalls.Should().Be(1);
        first.SplitSpanCalls.Should().Be(0);
        second.SplitSpanCalls.Should().Be(0);
        first.LastPath.Should().Be("sub/file.cs");
        second.LastPath.Should().Be("sub/file.cs");
        first.PathAddress.Should().Be(second.PathAddress);
    }

    [TestMethod]
    public void CreateExclusionWins_MixedNativeAndPathChildren_HonorsEveryRuleKind()
    {
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins(
        [
            FileSystemMatcher.Create((_, fileName) => fileName.StartsWith("native")),
            FileSystemMatcher.CreatePath(path => path.StartsWith("path"))
        ],
        [
            FileSystemMatcher.Create((_, fileName) => fileName.SequenceEqual("native-blocked.cs")),
            FileSystemMatcher.CreatePath(path => path.SequenceEqual("path-blocked.cs"))
        ]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesFile("root", "native-blocked.cs").Should().BeFalse();
        session.MatchesFile("root", "native.cs").Should().BeTrue();
        session.MatchesFile("root", "path-blocked.cs").Should().BeFalse();
        session.MatchesFile("root", "path.cs").Should().BeTrue();
        session.MatchesFile("root", "other.cs").Should().BeFalse();
    }

    [TestMethod]
    public void CreateOrdered_MixedChildren_PreserveSplitAndCanonicalInputs()
    {
        string? actualDirectory = null;
        string? actualFileName = null;
        CanonicalTrackingMatcher pathMatcher = new(matches: true);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateOrdered(
        [
            new(
                FileSystemMatcher.Create((directory, fileName) =>
                {
                    actualDirectory = directory.ToString();
                    actualFileName = fileName.ToString();
                    return true;
                }),
                FileSystemMatchAction.Exclude),
            new(pathMatcher, FileSystemMatchAction.Include)
        ]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesFile("root/sub", "file.cs").Should().BeTrue();

        actualDirectory.Should().Be("root/sub");
        actualFileName.Should().Be("file.cs");
        pathMatcher.CanonicalPathCalls.Should().Be(1);
        pathMatcher.SplitSpanCalls.Should().Be(0);
        pathMatcher.LastPath.Should().Be("sub/file.cs");
    }

    [TestMethod]
    public void CreateOrdered_FileMatches_LastMatchingRuleWinsAndUsesDefault()
    {
        IFileSystemMatcher matcher = FileSystemMatcher.CreateOrdered(
        [
            new(
                FileSystemMatcher.Create((_, fileName) => fileName.EndsWith(".tmp")),
                FileSystemMatchAction.Exclude),
            new(
                FileSystemMatcher.Create((_, fileName) => fileName.SequenceEqual("keep.tmp")),
                FileSystemMatchAction.Include)
        ],
            includeUnmatched: true);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesFile("root", "keep.tmp").Should().BeTrue();
        session.MatchesFile("root", "other.tmp").Should().BeFalse();
        session.MatchesFile("root", "source.cs").Should().BeTrue();
    }

    [TestMethod]
    public void CreateOrdered_DirectoryOnlyExcludeThenFileInclude_ReopensOnlyMatchingFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "matcher-root");
        GlobSpecification directoryOnly = GlobSpecification.Compile("bin/", GlobDialect.Git);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateOrdered(
        [
            new(directoryOnly.CreateFileSystemMatcher(), FileSystemMatchAction.Exclude),
            new(
                FileSystemMatcher.CreatePath(path => path.SequenceEqual("bin/keep.txt")),
                FileSystemMatchAction.Include)
        ],
            includeUnmatched: true);
        using IFileSystemMatcherSession session = matcher.CreateSession(root);

        session.MatchesDirectory(root, "bin").Should().Be(DirectoryMatchType.MayContainMatchingFiles);
        session.MatchesFile(Path.Combine(root, "bin"), "keep.txt").Should().BeTrue();
        session.MatchesFile(Path.Combine(root, "bin"), "other.txt").Should().BeFalse();
    }

    [TestMethod]
    public void CreateSession_ChildCreationThrows_DisposesEarlierSessions()
    {
        TrackingMatcher first = new();
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins(
            [first, new ThrowingMatcher()]);

        Action action = () => matcher.CreateSession("root");

        action.Should().Throw<InvalidOperationException>();
        first.DisposeCount.Should().Be(1);
    }

    [TestMethod]
    public void CreateSession_ExcludeCreationAndIncludeDisposalThrow_PreservesCreationException()
    {
        TrackingMatcher include = new(throwOnDispose: true);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins(
            [include],
            [new ThrowingMatcher("create")]);

        Action action = () => matcher.CreateSession("root");

        action.Should().Throw<InvalidOperationException>().WithMessage("create");
        include.DisposeCount.Should().Be(1);
    }

    [TestMethod]
    public void Dispose_ChildThrows_AttemptsAllChildren()
    {
        TrackingMatcher first = new(throwOnDispose: true);
        TrackingMatcher second = new();
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([first, second]);
        IFileSystemMatcherSession session = matcher.CreateSession("root");
        try
        {
            Action action = session.Dispose;

            action.Should().Throw<InvalidOperationException>();
            first.DisposeCount.Should().Be(1);
            second.DisposeCount.Should().Be(1);
        }
        finally
        {
            session.Dispose();
        }
    }

    [TestMethod]
    public void Dispose_ExcludeThrows_DisposesIncludeAndPreservesException()
    {
        TrackingMatcher include = new();
        TrackingMatcher exclude = new(throwOnDispose: true);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([include], [exclude]);
        IFileSystemMatcherSession session = matcher.CreateSession("root");
        try
        {
            Action action = session.Dispose;

            InvalidOperationException exception = action.Should().Throw<InvalidOperationException>().Which;
            exception.Should().BeSameAs(exclude.DisposeException);
            include.DisposeCount.Should().Be(1);
            exclude.DisposeCount.Should().Be(1);
        }
        finally
        {
            session.Dispose();
        }
    }

    [TestMethod]
    public void DirectoryFinished_ExclusionWins_ForwardsToEveryChild()
    {
        TrackingMatcher include = new();
        TrackingMatcher exclude = new();
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([include], [exclude]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.DirectoryFinished("root");

        include.DirectoryFinishedCount.Should().Be(1);
        exclude.DirectoryFinishedCount.Should().Be(1);
    }

    [TestMethod]
    public void Dispose_CalledTwice_DisposesChildrenOnce()
    {
        TrackingMatcher child = new();
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([child]);
        IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.Dispose();
        session.Dispose();

        child.DisposeCount.Should().Be(1);
    }

    [TestMethod]
    public void CreateSession_TenThousandNestedFrameworkCompositions_EvaluatesIteratively()
    {
        IFileSystemMatcher never = FileSystemMatcher.Create((_, _) => false);
        IFileSystemMatcher matcher = FileSystemMatcher.Create((_, fileName) => fileName.EndsWith(".cs"));
        for (int depth = 0; depth < 10_000; depth++)
        {
            matcher = FileSystemMatcher.CreateOrdered(
            [
                new(never, FileSystemMatchAction.Exclude),
                new(matcher, FileSystemMatchAction.Include)
            ]);
        }

        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesFile("root/sub", "file.cs").Should().BeTrue();
        session.MatchesFile("root/sub", "file.txt").Should().BeFalse();
        session.MatchesDirectory("root", "sub")
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
        session.DirectoryFinished("root");
    }

    [TestMethod]
    public void CreateSession_NestedExclusionWins_EvaluatesFilesAndDirectoriesIteratively()
    {
        IFileSystemMatcher nested = FileSystemMatcher.CreateExclusionWins(
            [FileSystemMatcher.Create((_, fileName) => fileName.EndsWith(".cs"))],
            [FileSystemMatcher.Create((_, fileName) => fileName.StartsWith("Generated"))]);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateOrdered(
            [new(nested, FileSystemMatchAction.Include)]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesFile("root", "Source.cs").Should().BeTrue();
        session.MatchesFile("root", "Generated.cs").Should().BeFalse();
        session.MatchesFile("root", "Source.txt").Should().BeFalse();
        session.MatchesDirectory("root", "src")
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
        session.DirectoryFinished("root");

        IFileSystemMatcher allFiles = FileSystemMatcher.CreateExclusionWins(
            [new ConstantMatcher(DirectoryMatchType.AllDescendantFilesMatch)],
            [new ConstantMatcher(DirectoryMatchType.NoDescendantFilesMatch)]);
        IFileSystemMatcher allMatcher = FileSystemMatcher.CreateOrdered(
            [new(allFiles, FileSystemMatchAction.Include)]);
        using IFileSystemMatcherSession allSession = allMatcher.CreateSession("root");
        allSession.MatchesDirectory("root", "src")
            .Should().Be(DirectoryMatchType.AllDescendantFilesMatch);

        IFileSystemMatcher noFiles = FileSystemMatcher.CreateExclusionWins(
            [new ConstantMatcher(DirectoryMatchType.AllDescendantFilesMatch)],
            [new ConstantMatcher(DirectoryMatchType.AllDescendantFilesMatch)]);
        IFileSystemMatcher noMatcher = FileSystemMatcher.CreateOrdered(
            [new(noFiles, FileSystemMatchAction.Include)]);
        using IFileSystemMatcherSession noSession = noMatcher.CreateSession("root");
        noSession.MatchesDirectory("root", "src")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
    }

    [TestMethod]
    public void CreateSession_ChildrenReturnSameSession_RejectsDuplicateOwnership()
    {
        SingletonSessionMatcher child = new();
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([child, child]);

        Action action = () => matcher.CreateSession("root");

        action.Should().Throw<InvalidOperationException>();
        child.Session.DisposeCount.Should().Be(1);
    }

    [TestMethod]
    public void CreateSession_NestedPathChildren_ShareCanonicalBuffer()
    {
        CanonicalTrackingMatcher first = new(matches: false);
        CanonicalTrackingMatcher second = new(matches: true);
        IFileSystemMatcher nested = FileSystemMatcher.CreateOrdered(
        [
            new(first, FileSystemMatchAction.Include),
            new(second, FileSystemMatchAction.Include)
        ]);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([nested]);
        using IFileSystemMatcherSession session = matcher.CreateSession("root");

        session.MatchesFile("root/sub", "file.cs").Should().BeTrue();

        first.CanonicalPathCalls.Should().Be(1);
        second.CanonicalPathCalls.Should().Be(1);
        first.PathAddress.Should().Be(second.PathAddress);
    }

    [TestMethod]
    public void CreateSession_NestedCreationThrows_DisposesEarlierLeafAndPreservesException()
    {
        TrackingMatcher first = new(throwOnDispose: true);
        IFileSystemMatcher nested = FileSystemMatcher.CreateOrdered(
        [
            new(first, FileSystemMatchAction.Include),
            new(new ThrowingMatcher("nested-create"), FileSystemMatchAction.Include)
        ]);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([nested]);

        Action action = () => matcher.CreateSession("root");

        action.Should().Throw<InvalidOperationException>().WithMessage("nested-create");
        first.DisposeCount.Should().Be(1);
    }

    [TestMethod]
    public void Dispose_NestedChildrenThrow_AttemptsEveryLeaf()
    {
        TrackingMatcher first = new(throwOnDispose: true);
        TrackingMatcher second = new();
        IFileSystemMatcher nested = FileSystemMatcher.CreateOrdered(
        [
            new(first, FileSystemMatchAction.Include),
            new(second, FileSystemMatchAction.Include)
        ]);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([nested]);
        IFileSystemMatcherSession session = matcher.CreateSession("root");
        try
        {
            Action action = session.Dispose;

            action.Should().Throw<InvalidOperationException>();
            first.DisposeCount.Should().Be(1);
            second.DisposeCount.Should().Be(1);
        }
        finally
        {
            session.Dispose();
        }
    }

    [TestMethod]
    public void CreateSession_NestedChildrenReturnSameSession_RejectsDuplicateOwnership()
    {
        SingletonSessionMatcher child = new();
        IFileSystemMatcher nested = FileSystemMatcher.CreateOrdered(
        [
            new(child, FileSystemMatchAction.Include),
            new(child, FileSystemMatchAction.Exclude)
        ]);
        IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins([nested]);

        Action action = () => matcher.CreateSession("root");

        action.Should().Throw<InvalidOperationException>();
        child.Session.DisposeCount.Should().Be(1);
    }

    private sealed class ConstantMatcher(DirectoryMatchType matchType) : IFileSystemMatcher
    {
        public IFileSystemMatcherSession CreateSession(string rootDirectory) => new ConstantSession(matchType);
    }

    private sealed class ConstantSession(DirectoryMatchType matchType) : FileSystemMatcherSession
    {
        public override bool MatchesFile(
            ReadOnlySpan<char> currentDirectory,
            ReadOnlySpan<char> fileName) => matchType == DirectoryMatchType.AllDescendantFilesMatch;

        public override DirectoryMatchType MatchesDirectory(
            ReadOnlySpan<char> currentDirectory,
            ReadOnlySpan<char> directoryName) => matchType;
    }

    private sealed class TrackingMatcher(bool throwOnDispose = false) : IFileSystemMatcher
    {
        public int DisposeCount { get; private set; }

        public InvalidOperationException DisposeException { get; } = new();

        public int DirectoryFinishedCount { get; private set; }

        public IFileSystemMatcherSession CreateSession(string rootDirectory) =>
            new TrackingSession(this, throwOnDispose);

        private sealed class TrackingSession(
            TrackingMatcher owner,
            bool throwOnDispose) : FileSystemMatcherSession
        {
            public override bool MatchesFile(
                ReadOnlySpan<char> currentDirectory,
                ReadOnlySpan<char> fileName) => false;

            public override void DirectoryFinished(ReadOnlySpan<char> directory) =>
                owner.DirectoryFinishedCount++;

            public override void Dispose()
            {
                owner.DisposeCount++;
                if (throwOnDispose)
                {
                    throw owner.DisposeException;
                }
            }
        }
    }

    private sealed class ThrowingMatcher(string message = "failure") : IFileSystemMatcher
    {
        public IFileSystemMatcherSession CreateSession(string rootDirectory) =>
            throw new InvalidOperationException(message);
    }

    private sealed class CanonicalTrackingMatcher(bool matches) : IFileSystemMatcher
    {
        public int CanonicalPathCalls { get; private set; }

        public int SplitSpanCalls { get; private set; }

        public string? LastPath { get; private set; }

        public nint PathAddress { get; private set; }

        public IFileSystemMatcherSession CreateSession(string rootDirectory) => new Session(this, matches);

        private sealed class Session(
            CanonicalTrackingMatcher owner,
            bool matches) : FileSystemMatcherSession, ICanonicalPathMatcherSession
        {
            public override bool MatchesFile(
                ReadOnlySpan<char> currentDirectory,
                ReadOnlySpan<char> fileName)
            {
                owner.SplitSpanCalls++;
                return matches;
            }

            public unsafe bool MatchesPath(ReadOnlySpan<char> rootRelativePath)
            {
                owner.CanonicalPathCalls++;
                owner.LastPath = rootRelativePath.ToString();
                fixed (char* path = rootRelativePath)
                {
                    owner.PathAddress = (nint)path;
                }

                return matches;
            }
        }
    }

    private static DirectoryMatchType NormalizeForOracle(DirectoryMatchType value) => value switch
    {
        DirectoryMatchType.NoDescendantFilesMatch => value,
        DirectoryMatchType.AllDescendantFilesMatch => value,
        _ => DirectoryMatchType.MayContainMatchingFiles
    };

    private sealed class SingletonSessionMatcher : IFileSystemMatcher
    {
        public SingletonSession Session { get; } = new();

        public IFileSystemMatcherSession CreateSession(string rootDirectory) => Session;
    }

    private sealed class SingletonSession : FileSystemMatcherSession
    {
        public int DisposeCount { get; private set; }

        public override bool MatchesFile(
            ReadOnlySpan<char> currentDirectory,
            ReadOnlySpan<char> fileName) => false;

        public override void Dispose() => DisposeCount++;
    }
}