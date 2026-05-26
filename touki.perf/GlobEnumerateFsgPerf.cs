// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Touki.Io;

using File = System.IO.File;
using Path = System.IO.Path;

namespace touki.perf;

/// <summary>
///  Real-world enumeration comparison between
///  <see cref="Touki.Io.GlobEnumerator"/>(<see cref="Touki.Io.Globbing.GlobDialect.FileSystemGlobbing"/>)
///  and <see cref="Matcher"/>. Mirrors <see cref="MsBuildEnumeratePerf2"/>: walks
///  the touki repository tree for <c>**/*.cs</c> while excluding the standard
///  <c>bin/**</c> + <c>obj/**</c> trees and a typical fan of file-name patterns
///  (<c>**/*.user</c>, <c>**/*.*proj</c>, etc.).
/// </summary>
[MemoryDiagnoser]
public class GlobEnumerateFsgPerf
{
    private const string Filespec = "**/*.cs";

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

    // Reduced exclude set: redundant rules removed so the benchmark measures the
    // GlobEnumerator engine alone, without the constant cost of compiling exclude
    // patterns whose results can never overlap the include.
    private static readonly List<string> s_reducedExcludes =
    [
        "bin/**",
        "obj/**"
    ];

    private string _directory = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Walk up from the perf assembly's location until we find the repo root.
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "touki.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        _directory = dir ?? throw new InvalidOperationException(
            "Could not locate touki.slnx walking up from " + AppContext.BaseDirectory);
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> FileSystemGlobbing()
    {
        Matcher matcher = new(StringComparison.Ordinal);
        matcher.AddInclude(Filespec);
        foreach (string exclude in s_excludes)
        {
            matcher.AddExclude(exclude);
        }

        // Matcher.Execute walks the directory wrapper and returns matching files as
        // a PatternMatchingResult; the Files collection holds the per-file matches.
        PatternMatchingResult result = matcher.Execute(new DirectoryInfoWrapper(new(_directory)));
        List<string> results = [];
        foreach (FilePatternMatch match in result.Files)
        {
            results.Add(match.Path);
        }

        return results;
    }

    [Benchmark]
    public IReadOnlyList<string> FileSystemGlobbing_GetResultsInFullPath()
    {
        // The convenience helper that calls Execute internally and returns full paths.
        Matcher matcher = new(StringComparison.Ordinal);
        matcher.AddInclude(Filespec);
        foreach (string exclude in s_excludes)
        {
            matcher.AddExclude(exclude);
        }

        return [.. matcher.GetResultsInFullPath(_directory)];
    }

    [Benchmark]
    public IReadOnlyList<string> GlobEnumerator()
    {
        using GlobEnumerator enumerator = Touki.Io.GlobEnumerator.Create(
            Filespec,
            s_excludes,
            _directory,
            Touki.Io.Globbing.GlobDialect.FileSystemGlobbing);
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
        // Minimum semantically-equivalent exclude set: drops redundant subdirectory
        // rules and file-name patterns disjoint from the `.cs` include suffix.
        // Measures how much of the gap to FileSystemGlobbing is attributable to
        // exclude-list size vs the underlying matcher engine.
        using GlobEnumerator enumerator = Touki.Io.GlobEnumerator.Create(
            Filespec,
            s_reducedExcludes,
            _directory,
            Touki.Io.Globbing.GlobDialect.FileSystemGlobbing);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }
}
