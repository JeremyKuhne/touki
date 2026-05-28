// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Enumerates files under a root directory whose relative paths match a compiled
///  <see cref="GlobSpecification"/> include pattern (and optionally do not match an
///  exclude pattern). Built on top of <see cref="MatchEnumerator{TResult}"/> driving
///  a <see cref="Globbing.GlobMatch"/> (single pattern) or a <see cref="MatchSet"/>
///  of <see cref="Globbing.GlobMatch"/>es (include plus excludes); results are
///  returned as strings.
/// </summary>
/// <remarks>
///  <para>
///   This is a thin wrapper provided to make it easy to drive the
///   <see cref="Globbing"/> matcher across a real file system, primarily for
///   performance comparison against <see cref="MSBuildEnumerator"/>. The two enumerators
///   accept different pattern dialects-<see cref="GlobDialect.PosixPath"/> for this
///   one, MSBuild-style globs for <see cref="MSBuildEnumerator"/>-and have
///   different recursion-pruning trade-offs, so they are not drop-in replacements for
///   each other.
///  </para>
/// </remarks>
public sealed class GlobEnumerator : MatchEnumerator<string>
{
    private static EnumerationOptions DefaultOptions { get; } = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    };

    private readonly bool _stripRootDirectory;
    private readonly int _rootDirectoryLength;

    private GlobEnumerator(
        IEnumerationMatcher matcher,
        string rootDirectory,
        bool stripRootDirectory,
        EnumerationOptions options)
        : base(rootDirectory, matcher, options)
    {
        _stripRootDirectory = stripRootDirectory;
        _rootDirectoryLength = rootDirectory.Length
            + (Path.EndsInDirectorySeparator(rootDirectory) ? 0 : 1);
    }

    /// <summary>
    ///  Creates a new <see cref="GlobEnumerator"/> for the given include glob, with an
    ///  optional exclude glob. Uses <see cref="GlobDialect.PosixPath"/> with
    ///  <see cref="GlobOptions.None"/>.
    /// </summary>
    /// <param name="includePattern">The include pattern.</param>
    /// <param name="excludePattern">
    ///  Optional exclude pattern. When <see langword="null"/> or empty, no excludes are applied.
    /// </param>
    /// <param name="rootDirectory">
    ///  Directory to enumerate. Results are returned relative to this directory.
    /// </param>
    /// <param name="options">Optional enumeration options; sensible defaults are used otherwise.</param>
    public static GlobEnumerator Create(
        string includePattern,
        string? excludePattern,
        string rootDirectory,
        EnumerationOptions? options = null) =>
        Create(includePattern, excludePattern, rootDirectory, GlobDialect.PosixPath, GlobOptions.None, options);

    /// <summary>
    ///  Creates a new <see cref="GlobEnumerator"/> with the specified <paramref name="dialect"/>
    ///  and <see cref="GlobOptions.None"/>.
    /// </summary>
    public static GlobEnumerator Create(
        string includePattern,
        string? excludePattern,
        string rootDirectory,
        GlobDialect dialect,
        EnumerationOptions? options = null) =>
        Create(includePattern, excludePattern, rootDirectory, dialect, GlobOptions.None, options);

    /// <summary>
    ///  Creates a new <see cref="GlobEnumerator"/> with the specified <paramref name="dialect"/>
    ///  and <paramref name="globOptions"/>.
    /// </summary>
    /// <param name="includePattern">The include glob.</param>
    /// <param name="excludePattern">Optional exclude glob.</param>
    /// <param name="rootDirectory">Directory to enumerate.</param>
    /// <param name="dialect">The glob dialect that compiles <paramref name="includePattern"/> and <paramref name="excludePattern"/>.</param>
    /// <param name="globOptions">Glob compile options.</param>
    /// <param name="options">Optional enumeration options.</param>
    public static GlobEnumerator Create(
        string includePattern,
        string? excludePattern,
        string rootDirectory,
        GlobDialect dialect,
        GlobOptions globOptions,
        EnumerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(includePattern);
        ArgumentNullException.ThrowIfNull(rootDirectory);

        options ??= DefaultOptions;

        IEnumerationMatcher matcher = BuildMatcher(
            includePattern,
            string.IsNullOrEmpty(excludePattern) ? null : [excludePattern!],
            rootDirectory,
            dialect,
            globOptions);

        return new GlobEnumerator(matcher, rootDirectory, stripRootDirectory: true, options);
    }

    /// <summary>
    ///  Creates a new <see cref="GlobEnumerator"/> with multiple exclude patterns. Uses
    ///  <see cref="GlobDialect.PosixPath"/> with <see cref="GlobOptions.None"/>.
    /// </summary>
    /// <param name="includePattern">The include glob.</param>
    /// <param name="excludePatterns">
    ///  Exclude globs; a file is yielded only when the include matches and none of the
    ///  excludes match.
    /// </param>
    /// <param name="rootDirectory">Directory to enumerate.</param>
    /// <param name="options">Optional enumeration options.</param>
    public static GlobEnumerator Create(
        string includePattern,
        IReadOnlyList<string> excludePatterns,
        string rootDirectory,
        EnumerationOptions? options = null) =>
        Create(includePattern, excludePatterns, rootDirectory, GlobDialect.PosixPath, GlobOptions.None, options);

    /// <inheritdoc cref="Create(string, IReadOnlyList{string}, string, GlobDialect, GlobOptions, EnumerationOptions?)"/>
    public static GlobEnumerator Create(
        string includePattern,
        IReadOnlyList<string> excludePatterns,
        string rootDirectory,
        GlobDialect dialect,
        EnumerationOptions? options = null) =>
        Create(includePattern, excludePatterns, rootDirectory, dialect, GlobOptions.None, options);

    /// <summary>
    ///  Creates a new <see cref="GlobEnumerator"/> with multiple exclude patterns and the
    ///  specified <paramref name="dialect"/> / <paramref name="globOptions"/>.
    /// </summary>
    public static GlobEnumerator Create(
        string includePattern,
        IReadOnlyList<string> excludePatterns,
        string rootDirectory,
        GlobDialect dialect,
        GlobOptions globOptions,
        EnumerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(includePattern);
        ArgumentNullException.ThrowIfNull(excludePatterns);
        ArgumentNullException.ThrowIfNull(rootDirectory);

        options ??= DefaultOptions;

        IEnumerationMatcher matcher = BuildMatcher(
            includePattern,
            excludePatterns,
            rootDirectory,
            dialect,
            globOptions);

        return new GlobEnumerator(matcher, rootDirectory, stripRootDirectory: true, options);
    }

    /// <summary>
    ///  Builds the composed <see cref="IEnumerationMatcher"/> for an include pattern
    ///  plus zero or more exclude patterns. When there are no excludes, returns the
    ///  include <see cref="Globbing.GlobMatch"/> directly (it implements
    ///  <see cref="IEnumerationMatcher"/>); otherwise wraps the include matcher and
    ///  one exclude matcher per pattern in a <see cref="MatchSet"/>.
    /// </summary>
    private static IEnumerationMatcher BuildMatcher(
        string includePattern,
        IReadOnlyList<string>? excludePatterns,
        string rootDirectory,
        GlobDialect dialect,
        GlobOptions globOptions)
    {
        GlobMatch include = GlobSpecification.Compile(includePattern, dialect, globOptions).CreateMatcher(rootDirectory);

        if (excludePatterns is null || excludePatterns.Count == 0)
        {
            return include;
        }

        MatchSet matchSet = new(include);

        // Dedupe before compiling each exclude. The two rules mirror
        // MSBuildSpecification's normalize/dedupe pass - together they remove
        // ~15-20% of the per-file matcher work on net481 (and ~5% on net10) for
        // realistic exclude lists that include redundant subtree rules and
        // file-name patterns that can't match the include's file shape.
        //
        //  1. Subtree subsumption: drop any exclude whose pattern is a strict
        //     subdirectory of another `<dir>/**`-shaped exclude in the same list.
        //  2. File-name disjointness: drop any exclude whose trailing literal
        //     (e.g., the `.user` in `**/*.user`) is disjoint from the include's
        //     trailing literal under suffix-comparison. A file matching the
        //     include must end with the include's literal, and a file matching
        //     the exclude must end with the exclude's literal; when neither
        //     literal ends with the other, no file can satisfy both.
        bool[]? skip = null;
        if (excludePatterns.Count > 1)
        {
            skip = new bool[excludePatterns.Count];
            ApplySubtreeSubsumption(excludePatterns, skip);
        }

        bool hasIncludeLiteral = TryExtractTrailingLiteral(includePattern, out StringSegment includeLiteral);

        for (int i = 0; i < excludePatterns.Count; i++)
        {
            string pattern = excludePatterns[i];
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            if (skip is not null && skip[i])
            {
                continue;
            }

            if (hasIncludeLiteral && IsFileNameDisjoint(includeLiteral, pattern))
            {
                continue;
            }

            GlobMatch exclude = GlobSpecification.Compile(pattern, dialect, globOptions).CreateMatcher(rootDirectory);
            matchSet.AddExclude(exclude);
        }

        return matchSet;
    }

    /// <summary>
    ///  Marks exclude patterns that are strict subdirectories of another
    ///  <c>&lt;dir&gt;/**</c>-shaped exclude in the same list. Operates on both
    ///  forward-slash and backslash separators so callers don't have to normalize
    ///  before invoking.
    /// </summary>
    private static void ApplySubtreeSubsumption(IReadOnlyList<string> excludes, bool[] skip)
    {
        for (int i = 0; i < excludes.Count; i++)
        {
            if (skip[i])
            {
                continue;
            }

            string a = excludes[i];
            if (string.IsNullOrEmpty(a) || !EndsWithSlashStarStar(a, out int aBodyLength))
            {
                continue;
            }

            // aBody is the pattern up to and including the trailing separator
            // that precedes "**". A path subsumed by `a` starts with aBody and has
            // more characters after it (a strict subdirectory).
            ReadOnlySpan<char> aBody = a.AsSpan(0, aBodyLength);

            for (int j = 0; j < excludes.Count; j++)
            {
                if (i == j || skip[j])
                {
                    continue;
                }

                string b = excludes[j];
                if (string.IsNullOrEmpty(b) || b.Length <= aBodyLength)
                {
                    continue;
                }

                if (b.AsSpan(0, aBodyLength).Equals(aBody, StringComparison.Ordinal))
                {
                    skip[j] = true;
                }
            }
        }
    }

    /// <summary>
    ///  Returns <see langword="true"/> when <paramref name="pattern"/> ends with
    ///  <c>/**</c> or <c>\**</c> (optionally with a trailing path separator),
    ///  reporting the length of the body that precedes the <c>**</c> -
    ///  inclusive of the separator that splits the body from <c>**</c>.
    /// </summary>
    private static bool EndsWithSlashStarStar(string pattern, out int bodyLength)
    {
        // Tolerate a single trailing separator (e.g., "obj/**/").
        int end = pattern.Length;
        if (end > 0 && (pattern[end - 1] == '/' || pattern[end - 1] == '\\'))
        {
            end--;
        }

        if (end < 3 || pattern[end - 1] != '*' || pattern[end - 2] != '*')
        {
            bodyLength = 0;
            return false;
        }

        char beforeStars = pattern[end - 3];
        if (beforeStars is not ('/' or '\\'))
        {
            bodyLength = 0;
            return false;
        }

        bodyLength = end - 2; // include the slash
        return true;
    }

    /// <summary>
    ///  Extracts the trailing literal of <paramref name="pattern"/>'s last segment
    ///  - the characters after the last <c>*</c> in the last separator-bounded
    ///  segment. Returns <see langword="true"/> when the segment is suitable for the
    ///  file-name disjointness check; the resulting <paramref name="literal"/> is a
    ///  <see cref="StringSegment"/> view over <paramref name="pattern"/> (no
    ///  allocation). Returns <see langword="false"/> when the last segment ends with
    ///  <c>*</c> (no trailing literal), is empty, or contains class metacharacters
    ///  that would defeat the simple suffix comparison.
    /// </summary>
    private static bool TryExtractTrailingLiteral(string pattern, out StringSegment literal)
    {
        literal = default;
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        int lastSeparator = pattern.LastIndexOfAny(['/', '\\']);
        int segmentStart = lastSeparator + 1;
        if (segmentStart >= pattern.Length)
        {
            return false;
        }

        // Find the last '*' inside the last segment only.
        int lastStar = -1;
        for (int i = pattern.Length - 1; i >= segmentStart; i--)
        {
            if (pattern[i] == '*')
            {
                lastStar = i;
                break;
            }
        }

        int literalStart = lastStar < 0 ? segmentStart : lastStar + 1;
        int literalLength = pattern.Length - literalStart;
        if (literalLength == 0)
        {
            return false;
        }

        // Reject literals containing metacharacters; we'd misclassify those.
        for (int i = literalStart; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c is '?' or '[' or ']' or '*')
            {
                return false;
            }
        }

        literal = new StringSegment(pattern, literalStart, literalLength);
        return true;
    }

    /// <summary>
    ///  Returns <see langword="true"/> when the exclude <paramref name="pattern"/>'s
    ///  trailing literal is disjoint from <paramref name="includeLiteral"/> -
    ///  neither is a suffix of the other - meaning no file can satisfy both.
    /// </summary>
    private static bool IsFileNameDisjoint(StringSegment includeLiteral, string pattern)
    {
        if (!TryExtractTrailingLiteral(pattern, out StringSegment excludeLiteral))
        {
            return false;
        }

        if (includeLiteral.Equals(excludeLiteral))
        {
            return false;
        }

        if (includeLiteral.EndsWith(excludeLiteral) || excludeLiteral.EndsWith(includeLiteral))
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        !entry.IsDirectory && base.ShouldIncludeEntry(ref entry);

    /// <inheritdoc/>
    protected override string TransformEntry(ref FileSystemEntry entry)
    {
        if (!_stripRootDirectory)
        {
            return entry.ToFullPath();
        }

        if (entry.Directory.Length <= _rootDirectoryLength)
        {
            return entry.FileName.ToString();
        }

        return $"{entry.Directory[_rootDirectoryLength..]}{Path.DirectorySeparatorChar}{entry.FileName}";
    }
}
