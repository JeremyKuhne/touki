// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;

namespace touki.perf;

[MemoryDiagnoser]
public class MsBuildEnumeratePerf2
{
    private const string Directory = @"n:\test\";
                                    // @"n:\repos\runtime\";
    private const string Filespec = "**/*.cs";
    // private const string Filespec = "**/src/**// /*.cs";

    private static readonly string s_unsplitExcludes = "bin/Debug/**;obj/Debug/**;bin/**;obj/**/;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store";

    private static readonly List<string> s_excludes =
    [
        "bin/Debug/**;",
        "obj/Debug/**;",
        "bin/**;",
        "obj/**/;",
        "**/*.user;",
        "**/*.*proj;",
        "**/*.sln;",
        "**/*.slnx;",
        "**/*.vssscc;",
        "**/.DS_Store"
    ];

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> MSBuild()
    {
        var results = FileMatcherWrapper.GetFilesSimple(Directory, Filespec, s_excludes);
        return results;
    }

    [Benchmark]
    public IReadOnlyList<string> MsBuildEnumerator()
    {
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(Filespec, s_unsplitExcludes, Directory);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }
}
