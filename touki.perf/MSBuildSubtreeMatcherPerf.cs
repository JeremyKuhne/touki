// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;

using Path = System.IO.Path;

namespace touki.perf;

/// <summary>
///  Measures classification of a terminal-globstar exclude on a matching and
///  nonmatching candidate directory.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class MSBuildSubtreeMatcherPerf
{
    private IFileSystemMatcherSession _matcher = null!;
    private string _root = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(Path.GetTempPath(), "touki-msbuild-subtree-perf");
        _matcher = EnumerationMatcherFactory.CreateMSBuild(
            "**/*",
            "**/bin/**",
            _root,
            out _);

        if (_matcher.MatchesDirectory(_root, "bin") != DirectoryMatchType.NoDescendantFilesMatch)
        {
            throw new InvalidOperationException("The subtree exclude benchmark hit fixture must be pruned.");
        }

        _matcher.DirectoryFinished(_root);
        if (_matcher.MatchesDirectory(_root, "src") == DirectoryMatchType.NoDescendantFilesMatch)
        {
            throw new InvalidOperationException("The subtree exclude benchmark miss fixture must recurse.");
        }

        _matcher.DirectoryFinished(_root);
    }

    [GlobalCleanup]
    public void GlobalCleanup() => _matcher.Dispose();

    [Benchmark(Baseline = true)]
    public bool Hit()
    {
        _matcher.DirectoryFinished(_root);
        return _matcher.MatchesDirectory(_root, "bin") == DirectoryMatchType.NoDescendantFilesMatch;
    }

    [Benchmark]
    public bool Miss()
    {
        _matcher.DirectoryFinished(_root);
        return _matcher.MatchesDirectory(_root, "src") != DirectoryMatchType.NoDescendantFilesMatch;
    }
}