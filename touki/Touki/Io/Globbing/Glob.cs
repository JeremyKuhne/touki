// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  One-shot glob matching helpers for callers that don't need to cache a compiled
///  <see cref="GlobSpecification"/>.
/// </summary>
public static class Glob
{
    /// <summary>
    ///  Compiles <paramref name="pattern"/> and tests whether <paramref name="input"/>
    ///  matches it. Equivalent to
    ///  <see cref="GlobSpecification.Compile(string, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>
    ///  followed by <see cref="GlobSpecification.IsMatch(ReadOnlySpan{char})"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This helper allocates a specification on every call. For repeated use of the
    ///   same pattern, compile once and cache the resulting <see cref="GlobSpecification"/>.
    ///  </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="pattern"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="GlobFormatException">The pattern is invalid.</exception>
    public static bool IsMatch(
        string pattern,
        ReadOnlySpan<char> input,
        GlobDialect dialect,
        GlobOptions options = GlobOptions.None)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return IsMatch(new StringSegment(pattern), input, dialect, options);
    }

    /// <inheritdoc cref="IsMatch(string, ReadOnlySpan{char}, GlobDialect, GlobOptions)"/>
    public static bool IsMatch(
        StringSegment pattern,
        ReadOnlySpan<char> input,
        GlobDialect dialect,
        GlobOptions options = GlobOptions.None)
    {
        GlobSpecification specification = GlobSpecification.Compile(pattern, dialect, options);
        return specification.IsMatch(input);
    }

    /// <summary>
    ///  Compiles <paramref name="pattern"/> and lazily enumerates matching files below
    ///  <paramref name="rootDirectory"/> as canonical root-relative paths.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Pattern compilation, root normalization, and enumeration-option snapshotting occur
    ///   before this method returns. File-system traversal begins when the returned sequence is
    ///   enumerated. Relative roots are resolved against the current directory at call time.
    ///   Each enumeration owns an independent matcher session. Returned paths are relative to
    ///   the normalized root and use <c>/</c> separators.
    ///  </para>
    ///  <para>
    ///   A <see langword="null"/> <paramref name="enumerationOptions"/> selects recursive
    ///   traversal that ignores inaccessible entries. This method does not impose a pattern-length
    ///   bound and is intended for trusted or prevalidated patterns. Compile untrusted patterns
    ///   separately with a finite <c>maxPatternLength</c>.
    ///  </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="rootDirectory"/> or <paramref name="pattern"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="GlobFormatException">The pattern is invalid.</exception>
    public static IEnumerable<string> EnumerateFiles(
        string rootDirectory,
        string pattern,
        GlobDialect dialect,
        GlobOptions options = GlobOptions.None,
        EnumerationOptions? enumerationOptions = null)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        ArgumentNullException.ThrowIfNull(pattern);

        GlobSpecification specification = GlobSpecification.Compile(pattern, dialect, options);
        FileSystemMatchEnumeratorArguments arguments = new(
            rootDirectory,
            specification.CreateFileSystemMatcher(),
            enumerationOptions);

        return EnumerateFilesIterator(
            arguments.RootDirectory,
            arguments.Matcher,
            arguments.Options);

        static IEnumerable<string> EnumerateFilesIterator(
            string normalizedRoot,
            IFileSystemMatcher matcher,
            EnumerationOptions snapshottedOptions)
        {
            using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(
                normalizedRoot,
                matcher,
                snapshottedOptions);
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }
    }
}
