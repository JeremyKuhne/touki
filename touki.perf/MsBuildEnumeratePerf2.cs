// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;

using File = System.IO.File;
using Path = System.IO.Path;

namespace touki.perf;

[MemoryDiagnoser]
public class MsBuildEnumeratePerf2
{
    private const string Filespec = "**/*.cs";

    private const string UnsplitExcludes = "bin/Debug/**;obj/Debug/**;bin/**;obj/**/;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store";

    // FileMatcher takes already-split excludes; mirror the contents of UnsplitExcludes (no trailing ';' -
    // each entry must be a single literal pattern, not a semicolon-suffixed one).
    private static readonly List<string> s_excludes =
    [
        "bin/Debug/**",
        "obj/Debug/**",
        "bin/**",
        "obj/**",
        "**/*.user",
        "**/*.*proj",
        "**/*.sln",
        "**/*.slnx",
        "**/*.vssscc",
        "**/.DS_Store"
    ];

    private string _directory = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Walk up from the perf assembly's location until we find the repo root (touki.slnx anchor).
        // Keeps the benchmark portable across machines without hardcoding an absolute path.
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "touki.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        _directory = dir ?? throw new InvalidOperationException(
            "Could not locate touki.slnx walking up from " + AppContext.BaseDirectory);
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> MSBuild()
    {
        var results = FileMatcherWrapper.GetFilesSimple(_directory, Filespec, s_excludes);
        return results;
    }

    [Benchmark]
    public IReadOnlyList<string> MsBuildEnumerator()
    {
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(Filespec, UnsplitExcludes, _directory);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }

    [Benchmark]
    public IReadOnlyList<string> MsBuildEnumeratorResult()
    {
        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(
            Filespec,
            excludeSpecs: UnsplitExcludes,
            projectDirectory: _directory);
        using MSBuildEnumerator enumerator = result.Enumerator!;
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }
}
