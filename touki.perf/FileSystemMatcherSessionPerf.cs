// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;

using Path = System.IO.Path;

namespace touki.perf;

/// <summary>
///  Measures the callback overhead introduced by reusable matcher definitions and per-enumeration sessions.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class FileSystemMatcherSessionPerf
{
    private readonly FileSystemMatchPredicate _predicate = MatchCSharpFile;
    private readonly PathMatchPredicate _pathPredicate = MatchCSharpPath;

    private IFileSystemMatcherSession _predicateSession = null!;
    private IFileSystemMatcherSession _compositeSession = null!;
    private IFileSystemMatcherSession _nativeCompositeEight = null!;
    private IFileSystemMatcherSession _nativeCompositeThirtyTwo = null!;
    private IFileSystemMatcherSession _pathSession = null!;
    private IFileSystemMatcherSession _pathCompositeSession = null!;
    private IFileSystemMatcherSession _pathCompositeEight = null!;
    private IFileSystemMatcherSession _pathCompositeThirtyTwo = null!;
    private string _root = string.Empty;
    private string _directory = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(Path.GetTempPath(), "touki-session-perf");
        _directory = Path.Combine(_root, "src");
        _predicateSession = FileSystemMatcher.Create(_predicate).CreateSession(_root);
        _compositeSession = FileSystemMatcher.CreateExclusionWins(
            [FileSystemMatcher.Create(_predicate)]).CreateSession(_root);
        _nativeCompositeEight = CreateNativeComposite(8);
        _nativeCompositeThirtyTwo = CreateNativeComposite(32);
        _pathSession = FileSystemMatcher.CreatePath(_pathPredicate).CreateSession(_root);
        _pathCompositeSession = CreatePathComposite(1);
        _pathCompositeEight = CreatePathComposite(8);
        _pathCompositeThirtyTwo = CreatePathComposite(32);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _predicateSession.Dispose();
        _compositeSession.Dispose();
        _nativeCompositeEight.Dispose();
        _nativeCompositeThirtyTwo.Dispose();
        _pathSession.Dispose();
        _pathCompositeSession.Dispose();
        _pathCompositeEight.Dispose();
        _pathCompositeThirtyTwo.Dispose();
    }

    [Benchmark(Baseline = true)]
    public bool DirectPredicate() => _predicate(_directory, "file.cs");

    [Benchmark]
    public bool PredicateSession() => _predicateSession.MatchesFile(_directory, "file.cs");

    [Benchmark]
    public bool SingleIncludeComposition() => _compositeSession.MatchesFile(_directory, "file.cs");

    [Benchmark]
    public bool NativeCompositionEight() => _nativeCompositeEight.MatchesFile(_directory, "file.cs");

    [Benchmark]
    public bool NativeCompositionThirtyTwo() => _nativeCompositeThirtyTwo.MatchesFile(_directory, "file.cs");

    [Benchmark]
    public DirectoryMatchType ConservativeDirectoryMatch() =>
        _predicateSession.MatchesDirectory(_root, "src");

    [Benchmark]
    public bool ShortPathAdapter() => _pathSession.MatchesFile(_directory, "file.cs");

    [Benchmark]
    public bool PathCompositionOne() => _pathCompositeSession.MatchesFile(_directory, "file.cs");

    [Benchmark]
    public bool PathCompositionEight() => _pathCompositeEight.MatchesFile(_directory, "file.cs");

    [Benchmark]
    public bool PathCompositionThirtyTwo() => _pathCompositeThirtyTwo.MatchesFile(_directory, "file.cs");

    private IFileSystemMatcherSession CreateNativeComposite(int count)
    {
        IFileSystemMatcher matcher = FileSystemMatcher.Create(MatchTextFile);
        IFileSystemMatcher[] matchers = new IFileSystemMatcher[count];
        for (int index = 0; index < matchers.Length; index++)
        {
            matchers[index] = matcher;
        }

        return FileSystemMatcher.CreateExclusionWins(matchers).CreateSession(_root);
    }

    private IFileSystemMatcherSession CreatePathComposite(int count)
    {
        IFileSystemMatcher matcher = FileSystemMatcher.CreatePath(MatchTextPath);
        IFileSystemMatcher[] matchers = new IFileSystemMatcher[count];
        for (int index = 0; index < matchers.Length; index++)
        {
            matchers[index] = matcher;
        }

        return FileSystemMatcher.CreateExclusionWins(matchers).CreateSession(_root);
    }

    private static bool MatchCSharpFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName) => fileName.EndsWith(".cs", StringComparison.Ordinal);

    private static bool MatchCSharpPath(ReadOnlySpan<char> path) =>
        path.EndsWith(".cs", StringComparison.Ordinal);

    private static bool MatchTextFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName) => fileName.EndsWith(".txt", StringComparison.Ordinal);

    private static bool MatchTextPath(ReadOnlySpan<char> path) =>
        path.EndsWith(".txt", StringComparison.Ordinal);
}