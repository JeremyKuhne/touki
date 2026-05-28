// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;
using Touki.Io.Globbing;

#if NETFRAMEWORK
using Microsoft.IO.Enumeration;
#else
using System.IO.Enumeration;
#endif

using File = System.IO.File;
using Path = System.IO.Path;

namespace touki.perf;

/// <summary>
///  Enumeration parity benchmark for <see cref="GlobOptions.AllowExtGlob"/>.
///  Walks the touki repository selecting every file whose extension belongs
///  to one of <see cref="PatternCount"/> common extensions, comparing two
///  equivalent expressions of the same selection.
/// </summary>
/// <remarks>
///  <para>
///   <b>Baseline:</b> a <see cref="MatchSet"/> with one <see cref="GlobMatch"/>
///   include per extension. Each include compiles to the
///   <see cref="GlobStarFileNameStrategy"/> specialization (cheap per-file
///   suffix match). At enumeration time the walker queries every file against
///   every include.
///  </para>
///  <para>
///   <b>Extglob:</b> a single <see cref="GlobSpecification"/> for
///   <c>**/@(*.ext1|*.ext2|...)</c>, compiled with
///   <see cref="GlobOptions.AllowExtGlob"/> on the
///   <see cref="GlobDialect.Bash"/> dialect. The factory recognizes this
///   suffix-set shape and lowers it to a <c>MultiSuffixGlobStrategy</c> wrapped
///   in <c>GlobStarFileNameStrategy</c>, so the per-file hot path is a tight
///   <c>EndsWith</c> sweep - not the recursive bytecode interpreter.
///   Other extglob shapes (e.g. <c>+(...)</c>, alternatives that are not pure
///   <c>*literal</c>) skip this specialization and flow through the recursive
///   walker in <c>CompiledGlobStrategy</c>.
///  </para>
///  <para>
///   <b>Excludes:</b> none. Both walkers descend the same tree, so the
///   difference is the per-file matching cost - not directory pruning,
///   not I/O, not exclude-list compile cost.
///  </para>
///  <para>
///   <b>Sweep:</b> <see cref="PatternCount"/> runs through
///   <c>{ 1, 2, 4, 8 }</c>. The MatchSet path scales linearly in
///   <see cref="PatternCount"/> on per-file matching and allocation; the
///   extglob path stays at one compiled specification regardless of N.
///  </para>
/// </remarks>
[MemoryDiagnoser]
public class GlobEnumerateExtGlobPerf
{
    // Common extensions, chosen so every entry is present in the touki tree.
    private static readonly string[] s_extensions =
    [
        "cs",
        "md",
        "json",
        "txt",
        "xml",
        "yml",
        "props",
        "targets",
    ];

    private static readonly EnumerationOptions s_options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };

    private string _directory = string.Empty;
    private string[] _patterns = [];
    private string _extGlobPattern = string.Empty;

    [Params(1, 2, 4, 8)]
    public int PatternCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "touki.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        _directory = dir ?? throw new InvalidOperationException(
            "Could not locate touki.slnx walking up from " + AppContext.BaseDirectory);

        _patterns = new string[PatternCount];
        System.Text.StringBuilder sb = new();
        sb.Append("**/@(");
        for (int i = 0; i < PatternCount; i++)
        {
            _patterns[i] = "**/*." + s_extensions[i];
            if (i > 0)
            {
                sb.Append('|');
            }

            sb.Append("*.");
            sb.Append(s_extensions[i]);
        }

        sb.Append(')');
        _extGlobPattern = sb.ToString();
    }

    /// <summary>
    ///  N-include <see cref="MatchSet"/>, one <see cref="GlobMatch"/> per
    ///  extension.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int MatchSet_NIncludes()
    {
        GlobMatch first = GlobSpecification
            .Compile(_patterns[0], GlobDialect.Bash, GlobOptions.AllowGlobStar)
            .CreateMatcher(_directory);
        using MatchSet matchSet = new(first);
        for (int i = 1; i < _patterns.Length; i++)
        {
            GlobMatch extra = GlobSpecification
                .Compile(_patterns[i], GlobDialect.Bash, GlobOptions.AllowGlobStar)
                .CreateMatcher(_directory);
            matchSet.AddInclude(extra);
        }

        using PerfMatchEnumerator enumerator = new(_directory, matchSet, s_options);
        int count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    /// <summary>
    ///  Single extglob include combining all extensions.
    /// </summary>
    [Benchmark]
    public int ExtGlob_SingleInclude()
    {
        using GlobMatch include = GlobSpecification
            .Compile(_extGlobPattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob)
            .CreateMatcher(_directory);

        using PerfMatchEnumerator enumerator = new(_directory, include, s_options);
        int count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    /// <summary>
    ///  Test-internal concrete <see cref="MatchEnumerator{TResult}"/> that
    ///  yields full paths.
    /// </summary>
    private sealed class PerfMatchEnumerator : MatchEnumerator<string>
    {
        public PerfMatchEnumerator(string directory, IEnumerationMatcher matcher, EnumerationOptions options)
            : base(directory, matcher, options)
        {
        }

        protected override string TransformEntry(ref FileSystemEntry entry) =>
            entry.ToFullPath();
    }
}
