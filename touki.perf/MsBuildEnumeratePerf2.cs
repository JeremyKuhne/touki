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

    // Reduced exclude set: MSBuildSpecification's normalize/dedupe step drops
    // `bin/Debug/**` (subsumed by `bin/**`) and `obj/Debug/**` (subsumed by `obj/**`),
    // and the file-name patterns (**/*.user, **/*.*proj, **/*.sln, **/*.slnx,
    // **/*.vssscc, **/.DS_Store) can never fire against the `**/*.cs` include
    // because a `.cs` file name doesn't end in any of those suffixes. The set below
    // is the minimum semantically-equivalent exclude list for this filespec and
    // measures GlobEnumerator without the redundant-rule processing cost.
    private static readonly List<string> s_reducedExcludes =
    [
        "bin/**",
        "obj/**"
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

    [Benchmark]
    public IReadOnlyList<string> GlobEnumerator()
    {
        using GlobEnumerator enumerator = Touki.Io.GlobEnumerator.Create(
            Filespec,
            s_excludes,
            _directory,
            Touki.Io.Globbing.GlobDialect.MSBuild);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }

    [Benchmark]
    public IReadOnlyList<string> GlobEnumeratorReduced()
    {
        // Same enumerator with the minimum semantically-equivalent exclude set. The
        // expected result count must match the other benchmarks (4850 .cs files on
        // the touki repo); this measures how much of the GlobEnumerator gap to
        // MsBuildEnumerator is attributable to processing redundant exclude rules
        // versus the matcher engine itself.
        using GlobEnumerator enumerator = Touki.Io.GlobEnumerator.Create(
            Filespec,
            s_reducedExcludes,
            _directory,
            Touki.Io.Globbing.GlobDialect.MSBuild);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }
}
