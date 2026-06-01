// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Linq;
using Touki.Io;
using Touki.Io.Globbing;

using Directory = System.IO.Directory;
using File = System.IO.File;
using Path = System.IO.Path;

namespace touki.perf;

/// <summary>
///  Replay-based variant of <see cref="MsBuildEnumeratePerf2"/>. Instead of walking the real
///  file system, every scenario drives its matcher over recorded snapshots committed as
///  compressed archives (<c>touki.perf/RecordedData/*.zip</c>), extracted to the build output
///  by <c>CompressedContent.targets</c> and reloaded from <see cref="AppContext.BaseDirectory"/>
///  during <see cref="GlobalSetup"/>. This isolates the matcher engine cost from file-system
///  I/O and makes the benchmark deterministic across runs.
/// </summary>
/// <remarks>
///  <para>
///   Two snapshots back the run: <c>enumeration.csv</c> (a <see cref="RecordedFileSystem"/>
///   capture of the directory tree, driving the Touki matchers) and <c>msbuild-filesystem.csv</c>
///   (a <see cref="RecordedMSBuildFileSystem"/> capture of the queries MSBuild's internal
///   <c>FileMatcher</c> issues, driving the MSBuild baseline through
///   <see cref="MSBuildFileSystemPlayback"/>). Both are committed as <c>.zip</c> archives and
///   extracted to the output directory at build time; the bootstrap in <see cref="GlobalSetup"/>
///   re-records them only if the extracted files are missing.
///  </para>
/// </remarks>
[MemoryDiagnoser]
public class MsBuildEnumeratePerf3
{
    private const string Filespec = "**/*.cs";

    private const string UnsplitExcludes = "bin/Debug/**;obj/Debug/**;bin/**;obj/**/;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store";

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

    private static readonly List<string> s_reducedExcludes =
    [
        "bin/**",
        "obj/**"
    ];

    private static readonly List<string> s_extGlobExclude =
    [
        "@(bin|obj)/**"
    ];

    private const string ExtGlobSingleInclude = "!(bin|obj)/**/*.cs";

    private const string ExtGlobSingleIncludeWithRoot = "@(!(bin|obj)/**/*.cs|*.cs)";

    private string _directory = string.Empty;
    private RecordedFileSystem _fileSystem = null!;
    private MSBuildFileSystemPlayback _msbuildPlayback = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Walk up from the perf assembly's location until we find the repo root (touki.slnx anchor).
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "touki.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        _directory = dir ?? throw new InvalidOperationException(
            "Could not locate touki.slnx walking up from " + AppContext.BaseDirectory);

        // The snapshots are committed as compressed archives and extracted to the build
        // output by CompressedContent.targets, so they are read from next to the assembly.
        string recordedDataDirectory = Path.Combine(AppContext.BaseDirectory, "RecordedData");
        Directory.CreateDirectory(recordedDataDirectory);
        string enumerationCsv = Path.Combine(recordedDataDirectory, "enumeration.csv");
        string msbuildCsv = Path.Combine(recordedDataDirectory, "msbuild-filesystem.csv");

        // Fallback bootstrap: if the extracted snapshot is missing (e.g. a toolchain that
        // does not flow the content), record the directory tree from the repo root.
        if (!File.Exists(enumerationCsv))
        {
            DirectoryEnumerationRecorder.Record(_directory, enumerationCsv);
        }

        // Fallback bootstrap for MSBuild's IFileSystem queries.
        if (!File.Exists(msbuildCsv))
        {
            RecordedMSBuildFileSystem recordingData = new();
            MSBuildFileSystemRecorder recorder = new(recordingData);
            FileMatcherWrapper.GetFilesSimple(_directory, Filespec, s_excludes, recorder);
            FileMatcherWrapper.GetFilesSimple(_directory, Filespec, s_reducedExcludes, recorder);
            recordingData.Save(msbuildCsv);
        }

        // Reload everything from the extracted snapshot.
        _fileSystem = RecordedFileSystem.Load(enumerationCsv);
        _msbuildPlayback = new MSBuildFileSystemPlayback(RecordedMSBuildFileSystem.Load(msbuildCsv));

        // Sanity check: every replayed scenario must enumerate the same set of files the real
        // MSBuildEnumerator produces over the live file system, so the timing comparison is valid.
        HashSet<string> baseline = new(RealMsBuildBaseline(), StringComparer.OrdinalIgnoreCase);

        AssertSet(nameof(MSBuild), MSBuild(), baseline);
        AssertSet(nameof(MSBuildReduced), MSBuildReduced(), baseline);
        AssertSet(nameof(MsBuildEnumerator), MsBuildEnumerator(), baseline);
        AssertSet(nameof(GlobEnumerator), GlobEnumerator(), baseline);
        AssertSet(nameof(GlobEnumeratorReduced), GlobEnumeratorReduced(), baseline);
        AssertSet(nameof(GlobEnumeratorExtGlobExclude), GlobEnumeratorExtGlobExclude(), baseline);
        AssertSet(nameof(GlobEnumeratorExtGlobSingle), GlobEnumeratorExtGlobSingle(), baseline);
        AssertSet(nameof(GlobEnumeratorExtGlobSingleWithRoot), GlobEnumeratorExtGlobSingleWithRoot(), baseline);
    }

    private List<string> RealMsBuildBaseline()
    {
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(Filespec, UnsplitExcludes, _directory);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }

    private static void AssertSet(string name, IReadOnlyList<string> actual, HashSet<string> expected)
    {
        HashSet<string> actualSet = new(actual, StringComparer.OrdinalIgnoreCase);
        if (actualSet.SetEquals(expected))
        {
            return;
        }

        string[] missingFromActual = [.. expected.Except(actualSet, StringComparer.OrdinalIgnoreCase)];
        string[] extraInActual = [.. actualSet.Except(expected, StringComparer.OrdinalIgnoreCase)];

        throw new InvalidOperationException(
            $"Benchmark '{name}' result set differs from baseline: "
            + $"{missingFromActual.Length} missing (e.g. {Sample(missingFromActual)}), "
            + $"{extraInActual.Length} extra (e.g. {Sample(extraInActual)}).");
    }

    private static string Sample(IReadOnlyList<string> items) =>
        string.Join(", ", items.Take(5).Select(s => "'" + s + "'"));

    private List<string> Replay(
        IEnumerationMatcher matcher,
        string rootDirectory,
        bool excludeDirectories)
    {
        using RecordedDirectoryEnumerator enumerator = new(
            _fileSystem,
            matcher,
            rootDirectory,
            excludeDirectories: excludeDirectories);

        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }

    private List<string> ReplayGlob(
        string includePattern,
        IReadOnlyList<string>? excludePatterns,
        GlobOptions globOptions)
    {
        IEnumerationMatcher matcher = EnumerationMatcherFactory.CreateGlob(
            includePattern,
            excludePatterns,
            _directory,
            GlobDialect.MSBuild,
            globOptions);

        return Replay(matcher, _directory, excludeDirectories: false);
    }

    // The MSBuild FileMatcher baseline replays MSBuild's internal IFileSystem queries captured
    // into msbuild-filesystem.csv, so it runs without touching disk like every other scenario.

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> MSBuild() =>
        FileMatcherWrapper.GetFilesSimple(_directory, Filespec, s_excludes, _msbuildPlayback);

    [Benchmark]
    public IReadOnlyList<string> MSBuildReduced() =>
        FileMatcherWrapper.GetFilesSimple(_directory, Filespec, s_reducedExcludes, _msbuildPlayback);

    [Benchmark]
    public IReadOnlyList<string> MsBuildEnumerator()
    {
        IEnumerationMatcher matcher = EnumerationMatcherFactory.CreateMSBuild(
            Filespec,
            UnsplitExcludes,
            _directory,
            out string startDirectory);

        return Replay(matcher, startDirectory, excludeDirectories: true);
    }

    [Benchmark]
    public IReadOnlyList<string> GlobEnumerator() =>
        ReplayGlob(Filespec, s_excludes, GlobOptions.None);

    [Benchmark]
    public IReadOnlyList<string> GlobEnumeratorReduced() =>
        ReplayGlob(Filespec, s_reducedExcludes, GlobOptions.None);

    [Benchmark]
    public IReadOnlyList<string> GlobEnumeratorExtGlobExclude() =>
        ReplayGlob(Filespec, s_extGlobExclude, GlobOptions.AllowExtGlob);

    [Benchmark]
    public IReadOnlyList<string> GlobEnumeratorExtGlobSingle() =>
        ReplayGlob(ExtGlobSingleInclude, null, GlobOptions.AllowExtGlob);

    [Benchmark]
    public IReadOnlyList<string> GlobEnumeratorExtGlobSingleWithRoot() =>
        ReplayGlob(ExtGlobSingleIncludeWithRoot, null, GlobOptions.AllowExtGlob);
}
