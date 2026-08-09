// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;

using Directory = System.IO.Directory;
using File = System.IO.File;
using Path = System.IO.Path;

namespace touki.perf;

/// <summary>
///  Measures replayed MSBuild include/exclude composition for proven subtree excludes and
///  file-only excludes that must continue traversing the matched directory.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class MSBuildExcludeCompositionPerf
{
    private const int ModuleCount = 64;

    private string _root = string.Empty;
    private RecordedFileSystem _fileSystem = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"touki-msbuild-exclude-perf-{Guid.NewGuid():N}");
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
            File.WriteAllText(Path.Combine(obj, "excluded.txt"), string.Empty);
            File.WriteAllText(Path.Combine(obj, "included.cs"), string.Empty);
            File.WriteAllText(Path.Combine(bin, "generated.cs"), string.Empty);
        }

        System.IO.StringWriter writer = new();
        DirectoryEnumerationRecorder.Record(_root, writer);
        _fileSystem = RecordedFileSystem.Load(new System.IO.StringReader(writer.ToString()));

        ValidateCount(
            "**/*.cs",
            "**/bin/**;**/obj/**",
            ModuleCount,
            "common C# subtree excludes");
        ValidateCount(
            "**/*",
            "**/obj/**",
            ModuleCount * 2,
            "all-files subtree exclude");
        ValidateCount(
            "**/*",
            "**/obj/*.txt",
            ModuleCount * 3,
            "all-files file-only exclude");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Benchmark]
    public int CommonCSharpSubtreeExcludes() =>
        Replay("**/*.cs", "**/bin/**;**/obj/**");

    [Benchmark]
    public int AllFilesSubtreeExclude() =>
        Replay("**/*", "**/obj/**");

    [Benchmark]
    public int AllFilesFileOnlyExclude() =>
        Replay("**/*", "**/obj/*.txt");

    private int Replay(string include, string excludes)
    {
        IFileSystemMatcherSession matcher = EnumerationMatcherFactory.CreateMSBuild(
            include,
            excludes,
            _root,
            out string startDirectory);
        using RecordedDirectoryEnumerator enumerator = new(
            _fileSystem,
            matcher,
            startDirectory);

        int count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private void ValidateCount(string include, string excludes, int expected, string scenario)
    {
        int actual = Replay(include, excludes);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"The {scenario} benchmark fixture returned {actual} files instead of {expected}.");
        }
    }
}