// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;
using Touki.Io.Globbing;

using Directory = System.IO.Directory;
using File = System.IO.File;
using Path = System.IO.Path;

namespace touki.perf;

/// <summary>
///  Measures the public glob enumerator over a fixed tree so API changes can be compared independently
///  of source-tree growth.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class GlobEnumeratorApiPerf
{
    private const int ModuleCount = 64;

    private static readonly string[] s_excludes = ["**/bin/**", "**/obj/**"];

    private static readonly GlobEnumerationOptions s_includeOnlyOptions = new()
    {
        GlobOptions = GlobOptions.AllowGlobStar
    };

    private static readonly GlobEnumerationOptions s_excludeOptions = new()
    {
        ExcludePatterns = s_excludes,
        GlobOptions = GlobOptions.AllowGlobStar
    };

    private string _root = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"touki-glob-api-perf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        for (int moduleIndex = 0; moduleIndex < ModuleCount; moduleIndex++)
        {
            string module = Path.Combine(_root, $"module-{moduleIndex:D2}");
            string source = Path.Combine(module, "src");
            string obj = Path.Combine(module, "obj");
            string bin = Path.Combine(module, "bin");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(obj);
            Directory.CreateDirectory(bin);
            File.WriteAllText(Path.Combine(source, "code.cs"), string.Empty);
            File.WriteAllText(Path.Combine(source, "notes.txt"), string.Empty);
            File.WriteAllText(Path.Combine(obj, "generated.cs"), string.Empty);
            File.WriteAllText(Path.Combine(bin, "generated.cs"), string.Empty);
        }

        if (Enumerate(excludes: null) != ModuleCount * 3)
        {
            throw new InvalidOperationException("The include-only fixture returned an unexpected count.");
        }

        if (Enumerate(s_excludes) != ModuleCount)
        {
            throw new InvalidOperationException("The exclude fixture returned an unexpected count.");
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Benchmark(Baseline = true)]
    public int IncludeOnly() => Enumerate(excludes: null);

    [Benchmark]
    public int IncludeWithExcludes() => Enumerate(s_excludes);

    private int Enumerate(IReadOnlyList<string>? excludes)
    {
        using GlobEnumerator enumerator = GlobEnumerator.Create(
            "**/*.cs",
            _root,
            excludes is null ? s_includeOnlyOptions : s_excludeOptions);

        int count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }
}