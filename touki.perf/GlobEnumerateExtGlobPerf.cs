// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io;
using Touki.Io.Globbing;
using Touki.Text;

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
///   <b>Baseline:</b> immutable matcher composition with one compiled glob
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
///   <c>{ 1, 2, 4, 8 }</c>. The composed path scales linearly in
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
        using ValueStringBuilder builder = new(stackalloc char[256]);
        builder.Append("**/@(");
        for (int i = 0; i < PatternCount; i++)
        {
            _patterns[i] = "**/*." + s_extensions[i];
            if (i > 0)
            {
                builder.Append('|');
            }

            builder.Append("*.");
            builder.Append(s_extensions[i]);
        }

        builder.Append(')');
        _extGlobPattern = builder.ToString();
    }

    /// <summary>
    ///  Immutable composition with one compiled include per extension.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int ImmutableComposition_NIncludes()
    {
        IFileSystemMatcher[] includes = new IFileSystemMatcher[_patterns.Length];
        try
        {
            for (int index = 0; index < _patterns.Length; index++)
            {
                includes[index] = GlobSpecification.Compile(
                    _patterns[index],
                    GlobDialect.Bash,
                    GlobOptions.AllowGlobStar);
            }

            IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins(includes);
            using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
                _directory,
                matcher,
                s_options);
            return Count(enumerator);
        }
        finally
        {
            for (int index = 0; index < includes.Length; index++)
            {
                (includes[index] as IDisposable)?.Dispose();
            }
        }
    }

    /// <summary>
    ///  Single extglob include combining all extensions.
    /// </summary>
    [Benchmark]
    public int ExtGlob_SingleInclude()
    {
        GlobSpecification include = GlobSpecification.Compile(
            _extGlobPattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);
        using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
            _directory,
            include.CreateFileSystemMatcher(),
            s_options);
        return Count(enumerator);
    }

    private static int Count(FileSystemPathEnumerator enumerator)
    {
        int count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }
}
