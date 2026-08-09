// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Touki.Io;

/// <summary>
///  Enumerates files under a root directory whose relative paths match a compiled
///  <see cref="GlobSpecification"/> include pattern and optionally do not match one or more
///  exclude patterns. Each enumeration owns an optimized root-bound matcher session; results are
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
public sealed class GlobEnumerator : FileSystemEnumerator<string>
{
    private static EnumerationOptions DefaultEnumerationOptions { get; } = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    };

    private readonly IFileSystemMatcherSession _session;
    private readonly int _rootDirectoryLength;
    private int _sessionDisposed;

    private GlobEnumerator(
        IFileSystemMatcherSession session,
        string rootDirectory,
        EnumerationOptions options)
        : base(rootDirectory, options)
    {
        _session = session;
        _rootDirectoryLength = rootDirectory.Length
            + (Path.EndsInDirectorySeparator(rootDirectory) ? 0 : 1);
    }

    /// <summary>
    ///  Creates an enumerator for files matching <paramref name="includePattern"/> beneath
    ///  <paramref name="rootDirectory"/>.
    /// </summary>
    public static GlobEnumerator Create(
        string includePattern,
        string rootDirectory,
        GlobEnumerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(includePattern);
        ArgumentNullException.ThrowIfNull(rootDirectory);

        IReadOnlyList<string> excludePatterns = options?.ExcludePatterns ?? Array.Empty<string>();
        if (options is not null && options.ExcludePatterns is null)
        {
            throw new ArgumentException("Exclude patterns cannot be null.", nameof(options));
        }

        for (int index = 0; index < excludePatterns.Count; index++)
        {
            _ = excludePatterns[index]
                ?? throw new ArgumentException("Exclude patterns cannot contain null.", nameof(options));
        }

        EnumerationOptions enumerationOptions = options?.EnumerationOptions is { } suppliedEnumerationOptions
            ? SnapshotEnumerationOptions(suppliedEnumerationOptions)
            : DefaultEnumerationOptions;
        string normalizedRoot = FileSystemMatchEnumeratorArguments.NormalizeRootDirectory(rootDirectory);
        IFileSystemMatcherSession session = BuildSession(
            includePattern,
            excludePatterns,
            normalizedRoot,
            options?.Dialect ?? GlobDialect.PosixPath,
            options?.GlobOptions ?? GlobOptions.None);
        try
        {
            return new GlobEnumerator(session, normalizedRoot, enumerationOptions);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private static EnumerationOptions SnapshotEnumerationOptions(EnumerationOptions options) => new()
    {
        AttributesToSkip = options.AttributesToSkip,
        BufferSize = options.BufferSize,
        IgnoreInaccessible = options.IgnoreInaccessible,
        MatchCasing = options.MatchCasing,
        MatchType = options.MatchType,
        MaxRecursionDepth = options.MaxRecursionDepth,
        RecurseSubdirectories = options.RecurseSubdirectories,
        ReturnSpecialDirectories = options.ReturnSpecialDirectories
    };

    /// <summary>
    ///  Builds an owned matcher session for an include pattern plus zero or more exclude patterns.
    /// </summary>
    internal static IFileSystemMatcherSession BuildSession(
        string includePattern,
        IReadOnlyList<string>? excludePatterns,
        string rootDirectory,
        GlobDialect dialect,
        GlobOptions globOptions)
    {
        GlobMatch include = GlobSpecification
            .Compile(includePattern, dialect, globOptions)
            .CreateSession(rootDirectory);

        if (excludePatterns is null || excludePatterns.Count == 0)
        {
            return include;
        }

        GlobMatch[] excludes = new GlobMatch[excludePatterns.Count];
        int excludeCount = 0;

        try
        {
            for (int index = 0; index < excludePatterns.Count; index++)
            {
                string pattern = excludePatterns[index];
                if (string.IsNullOrEmpty(pattern))
                {
                    continue;
                }

                excludes[excludeCount++] = GlobSpecification
                    .Compile(pattern, dialect, globOptions)
                    .CreateSession(rootDirectory);
            }

            if (excludeCount != excludes.Length)
            {
                Array.Resize(ref excludes, excludeCount);
            }

            return new GlobEnumeratorFileSystemMatcherSession(include, excludes);
        }
        catch
        {
            include.Dispose();
            for (int index = 0; index < excludeCount; index++)
            {
                excludes[index].Dispose();
            }

            throw;
        }
    }

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        !entry.IsDirectory && _session.MatchesFile(entry.Directory, entry.FileName);

    /// <inheritdoc/>
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
        _session.MatchesDirectory(entry.Directory, entry.FileName)
            != DirectoryMatchType.NoDescendantFilesMatch;

    /// <inheritdoc/>
    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory) =>
        _session.DirectoryFinished(directory);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        Exception? firstException = null;
        try
        {
            base.Dispose(disposing);
        }
        catch (Exception exception)
        {
            firstException = exception;
        }

        if (disposing && Interlocked.Exchange(ref _sessionDisposed, 1) == 0)
        {
            try
            {
                _session.Dispose();
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }

        if (firstException is not null)
        {
            ExceptionDispatchInfo.Capture(firstException).Throw();
        }
    }

    /// <inheritdoc/>
    protected override string TransformEntry(ref FileSystemEntry entry)
    {
        if (entry.Directory.Length <= _rootDirectoryLength)
        {
            return entry.FileName.ToString();
        }

        return $"{entry.Directory[_rootDirectoryLength..]}{Path.DirectorySeparatorChar}{entry.FileName}";
    }
}
