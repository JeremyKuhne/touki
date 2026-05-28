// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Linq;
using Touki.Io;
using Touki.Io.Globbing;

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

    // Single extglob alternation that covers the same two excluded subtrees.
    // `@(bin|obj)/**` matches the root-anchored `bin/` or `obj/` directories
    // and any descendants - equivalent to MSBuild's root-anchored
    // `bin/**` / `obj/**` pair, but as one compiled spec.
    private static readonly List<string> s_extGlobExclude =
    [
        "@(bin|obj)/**"
    ];

    // Single-pattern collapse: bash extglob negation at the first segment. A path
    // matches when its first directory segment is not `bin` or `obj` and the
    // remaining path ends in `*.cs`. Equivalent to the include + reduced
    // excludes pair under the assumption (true for this repo) that there are
    // no `.cs` files at the project root. Compiled with one GlobSpecification.
    private const string ExtGlobSingleInclude = "!(bin|obj)/**/*.cs";

    // Option-2 single-pattern collapse with root-level fallback: outer @(...)
    // alternation between the nested-file case and the root-file case. Matches
    // the same files as `**/*.cs` minus root-anchored `bin/**` and `obj/**`
    // including `.cs` files sitting at the project root, in one compiled spec.
    private const string ExtGlobSingleIncludeWithRoot = "@(!(bin|obj)/**/*.cs|*.cs)";

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

        // Sanity check: every scenario in this file must enumerate the same set of
        // files so the timing comparison is meaningful. Run each one and compare
        // counts; mismatch aborts the benchmark run with a descriptive message.
        HashSet<string> baseline = new(MSBuild(), StringComparer.OrdinalIgnoreCase);

        AssertSet(nameof(MSBuildReduced), MSBuildReduced(), baseline);
        AssertSet(nameof(MsBuildEnumerator), MsBuildEnumerator(), baseline);
        AssertSet(nameof(MsBuildEnumeratorResult), MsBuildEnumeratorResult(), baseline);
        AssertSet(nameof(GlobEnumerator), GlobEnumerator(), baseline);
        AssertSet(nameof(GlobEnumeratorReduced), GlobEnumeratorReduced(), baseline);
        AssertSet(nameof(GlobEnumeratorExtGlobExclude), GlobEnumeratorExtGlobExclude(), baseline);
        AssertSet(nameof(GlobEnumeratorExtGlobSingle), GlobEnumeratorExtGlobSingle(), baseline);
        AssertSet(nameof(GlobEnumeratorExtGlobSingleWithRoot), GlobEnumeratorExtGlobSingleWithRoot(), baseline);
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

    private static string Sample(IReadOnlyList<string> items) => string.Join(
        ", ",
        items.Take(5).Select(s => "'" + s.Replace(s_directoryRoot, "<root>", StringComparison.OrdinalIgnoreCase) + "'"));

    private static readonly string s_directoryRoot = string.Empty;

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> MSBuild()
    {
        var results = FileMatcherWrapper.GetFilesSimple(_directory, Filespec, s_excludes);
        return results;
    }

    /// <summary>
    ///  MSBuild's <c>FileMatcher</c> driven with the minimum semantically-equivalent
    ///  exclude set. Isolates the redundant-rule processing cost in the MSBuild
    ///  baseline from the actual per-file matching work.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<string> MSBuildReduced()
    {
        var results = FileMatcherWrapper.GetFilesSimple(_directory, Filespec, s_reducedExcludes);
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
            GlobDialect.MSBuild);
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
            GlobDialect.MSBuild);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }

    /// <summary>
    ///  Single extglob alternation as the exclude pattern. Compiles the two
    ///  subtrees into one specification; <see cref="MatchSet"/> still wraps it
    ///  for the include + 1-exclude shape.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<string> GlobEnumeratorExtGlobExclude()
    {
        using GlobEnumerator enumerator = Touki.Io.GlobEnumerator.Create(
            Filespec,
            s_extGlobExclude,
            _directory,
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }

    /// <summary>
    ///  Single extglob include that collapses the include and excludes into one
    ///  specification: <c>!(bin|obj)/**/*.cs</c>. No <see cref="MatchSet"/>
    ///  involvement; the include matcher answers per-file directly.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<string> GlobEnumeratorExtGlobSingle()
    {
        using GlobEnumerator enumerator = Touki.Io.GlobEnumerator.Create(
            ExtGlobSingleInclude,
            excludePattern: null,
            _directory,
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }

    /// <summary>
    ///  Single extglob include that also covers root-level <c>*.cs</c>:
    ///  <c>@(!(bin|obj)/**/*.cs|*.cs)</c>. Outer alternation, exactly-one of
    ///  the two arms. The true apples-to-apples shape vs
    ///  <c>GlobEnumeratorReduced</c>: both forms match the same result set on
    ///  any repo (root <c>.cs</c> files included), in one compiled
    ///  <c>GlobSpecification</c> instead of an include + 2 excludes.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<string> GlobEnumeratorExtGlobSingleWithRoot()
    {
        using GlobEnumerator enumerator = Touki.Io.GlobEnumerator.Create(
            ExtGlobSingleIncludeWithRoot,
            excludePattern: null,
            _directory,
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }
}
