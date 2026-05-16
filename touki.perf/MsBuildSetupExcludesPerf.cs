// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;

using Directory = System.IO.Directory;
using File = System.IO.File;
using Path = System.IO.Path;

namespace touki.perf;

/// <summary>
///  Setup-dominated MSBuild enumeration with the default item-excludes string. Mirrors
///  <see cref="MsBuildSetupPerf"/> but exercises the exclude pipeline (which splits and qualifies the
///  exclude specs at construction time).
/// </summary>
[MemoryDiagnoser]
public class MsBuildSetupExcludesPerf
{
    private const string Filespec = "**/*.cs";

    private const string UnsplitExcludes =
        "bin/Debug/**;obj/Debug/**;bin/**;obj/**/;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store";

    // FileMatcher takes already-split excludes; mirror the contents of UnsplitExcludes.
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
        _directory = Path.Combine(Path.GetTempPath(), $"touki-perf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(Path.Combine(_directory, "src"));
        Directory.CreateDirectory(Path.Combine(_directory, "src", "nested"));
        Directory.CreateDirectory(Path.Combine(_directory, "bin"));
        Directory.CreateDirectory(Path.Combine(_directory, "obj"));

        // Matching files
        File.WriteAllText(Path.Combine(_directory, "a.cs"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "src", "b.cs"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "src", "nested", "c.cs"), string.Empty);

        // Files that should be filtered out by the default excludes
        File.WriteAllText(Path.Combine(_directory, "bin", "ignored.cs"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "obj", "ignored.cs"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "skip.user"), string.Empty);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> MSBuild()
    {
        return FileMatcherWrapper.GetFilesSimple(_directory, Filespec, s_excludes);
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
