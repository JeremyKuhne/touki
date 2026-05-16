// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;

using Directory = System.IO.Directory;
using File = System.IO.File;
using Path = System.IO.Path;

namespace touki.perf;

/// <summary>
///  Measures setup-dominated cost of an MSBuild-style enumeration on a small, controlled tree.
///  The directory is built in <see cref="GlobalSetup"/> with a small handful of files so the
///  enumeration itself completes in microseconds and per-call setup overhead is visible.
/// </summary>
[MemoryDiagnoser]
public class MsBuildSetupPerf
{
    private const string Filespec = "**/*.cs";

    private string _directory = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"touki-perf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(Path.Combine(_directory, "src"));
        Directory.CreateDirectory(Path.Combine(_directory, "src", "nested"));

        // Five matching files spread shallow + deep so the recursive walk visits >1 directory.
        File.WriteAllText(Path.Combine(_directory, "a.cs"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "b.cs"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "src", "c.cs"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "src", "d.cs"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "src", "nested", "e.cs"), string.Empty);
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
        return FileMatcherWrapper.GetFilesSimple(_directory, Filespec);
    }

    [Benchmark]
    public IReadOnlyList<string> MsBuildEnumerator()
    {
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(Filespec, _directory);
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
        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(Filespec, projectDirectory: _directory);
        using MSBuildEnumerator enumerator = result.Enumerator!;
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }
}
