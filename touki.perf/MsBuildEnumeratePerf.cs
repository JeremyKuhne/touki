// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;

namespace touki.perf;


[MemoryDiagnoser]
public class MsBuildEnumeratePerf
{
    private const string Directory = @"n:\repos\runtime\";
    // private const string Filespec = "**/*.cs";
    private const string Filespec = "**/src/**/*.cs";

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> MSBuild()
    {
        var results = FileMatcherWrapper.GetFilesSimple(Directory, Filespec);
        return results;
    }

    [Benchmark]
    public IReadOnlyList<string> MsBuildEnumerator()
    {
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(Filespec, Directory);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }
}
