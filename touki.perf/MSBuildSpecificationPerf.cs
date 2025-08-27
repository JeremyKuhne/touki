// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if (NETFRAMEWORK)
using Microsoft.IO;
using Microsoft.IO.Enumeration;
#elif (NET)
using System.IO.Enumeration;
#endif
using Touki.Io;

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class MsBuildSpecificationPerf
{
    private const string Directory = @"d:\code\dotnet-sdk\";
    // private const string Filespec = "**/*.cs";
    private const string Filespec = "**/src/**/*.cs";

    private static readonly List<string> s_excludeSpecs = new() { "**/obj/**", "**/bin/**", "**/TestData/**", "**/Generated/**" };

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> MSBuild()
    {
        var results = FileMatcherWrapper.GetFiles(Directory, Filespec, s_excludeSpecs);
        return results.FileList;
    }

    [Benchmark]
    public IReadOnlyList<string> MatchSetWithMSBuildMatcher()
    {
        var includeSpec = new MSBuildSpecification(Filespec);
        var matchSet = new MatchSet(new MatchMSBuild(includeSpec, Directory, MatchType.Simple, MatchCasing.PlatformDefault));
        foreach (var e in s_excludeSpecs)
        {
            matchSet.AddExclude(new MatchMSBuild(new MSBuildSpecification(e), Directory, MatchType.Simple, MatchCasing.PlatformDefault, MatchMSBuild.SpecMode.Exclude));
        }

        using MatchEnumerator enumerator = new MatchEnumerator(Directory, matchSet, static (ref FileSystemEntry fse) => fse.FileName.ToString());
        List<string> results = new();
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }
}
