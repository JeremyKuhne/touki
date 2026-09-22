// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Linq;
using Touki.Io;
namespace touki.perf;

/// <summary>
///  Measures MSBuild directory-pattern matching for a direct anchor and for a repeated
///  anchor that requires keeping more than one globstar state active.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class MSBuildPathMatcherPerf
{
    [AllowNull]
    private MatchMSBuild _directMatcher;

    [AllowNull]
    private MatchMSBuild _repeatedMatcher;

    private string _directDirectory = string.Empty;
    private string _repeatedDirectory = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        string root = Path.Join(Path.GetTempPath(), "touki-msbuild-path-perf");
        _directDirectory = Path.Join(
            root,
            string.Join(Path.DirectorySeparatorChar.ToString(), Enumerable.Repeat("x", 62)),
            "a",
            "y",
            "a",
            "b");

        _repeatedDirectory = Path.Join(
            root,
            string.Join(Path.DirectorySeparatorChar.ToString(), Enumerable.Repeat("a", 64)),
            "a",
            "b");

        _directMatcher = CreateMatcher(root);
        _repeatedMatcher = CreateMatcher(root);

        if (!_directMatcher.MatchesFile(_directDirectory, "source.cs"))
        {
            throw new InvalidOperationException("The direct-anchor benchmark fixture must match.");
        }

        _directMatcher.DirectoryFinished(_directDirectory);
        if (!_repeatedMatcher.MatchesFile(_repeatedDirectory, "source.cs"))
        {
            throw new InvalidOperationException("The repeated-anchor benchmark fixture must match.");
        }

        _repeatedMatcher.DirectoryFinished(_repeatedDirectory);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _directMatcher.Dispose();
        _repeatedMatcher.Dispose();
    }

    [Benchmark(Baseline = true)]
    public bool NonRepeatingAnchorHit()
    {
        _directMatcher.DirectoryFinished(_directDirectory);
        return _directMatcher.MatchesFile(_directDirectory, "source.cs");
    }

    [Benchmark]
    public bool RepeatedAnchorHit()
    {
        _repeatedMatcher.DirectoryFinished(_repeatedDirectory);
        return _repeatedMatcher.MatchesFile(_repeatedDirectory, "source.cs");
    }

    private static MatchMSBuild CreateMatcher(string root)
    {
        string specificationPath = Path.Join(root, "**/a/**/a/b/*.cs");
        MSBuildSpecification specification = new(specificationPath);
        return new MatchMSBuild(
            specification,
            MatchType.Simple,
            MatchCasing.PlatformDefault);
    }
}