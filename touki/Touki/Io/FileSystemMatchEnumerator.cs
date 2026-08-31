// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.ExceptionServices;
using System.Threading;

namespace Touki.Io;

/// <summary>
///  File-system enumerator that delegates file and directory decisions to a reusable matcher definition.
/// </summary>
public abstract class FileSystemMatchEnumerator<TResult> : FileSystemEnumerator<TResult>
{
    private readonly IFileSystemMatcher _matcher;
    private IFileSystemMatcherSession? _session;
    private int _additionalResourcesDisposed;
    private int _sessionDisposed;

    /// <summary>
    ///  Constructs an enumerator rooted at <paramref name="rootDirectory"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The matcher definition is borrowed. The enumerator lazily creates and owns one matcher session.
    ///  </para>
    /// </remarks>
    /// <param name="rootDirectory">The directory to enumerate.</param>
    /// <param name="matcher">The matcher used to select files and directories.</param>
    /// <param name="options">The enumeration options, or <see langword="null"/> to use the defaults.</param>
    protected FileSystemMatchEnumerator(
        string rootDirectory,
        IFileSystemMatcher matcher,
        EnumerationOptions? options = null)
        : this(new(rootDirectory, matcher, options))
    {
    }

    private FileSystemMatchEnumerator(FileSystemMatchEnumeratorArguments arguments)
        : base(arguments.RootDirectory, arguments.Options)
    {
        EnumerationRootDirectory = arguments.RootDirectory;
        _matcher = arguments.Matcher;
    }

    /// <summary>
    ///  Gets the normalized enumeration root passed to matcher sessions.
    /// </summary>
    protected string EnumerationRootDirectory { get; }

    /// <inheritdoc/>
    protected sealed override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        !entry.IsDirectory
            && GetSession().MatchesFile(entry.Directory, entry.FileName)
            && ShouldIncludeMatchedFile(ref entry);

    /// <inheritdoc/>
    protected sealed override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
        GetSession().MatchesDirectory(entry.Directory, entry.FileName)
            != DirectoryMatchType.NoDescendantFilesMatch;

    /// <inheritdoc/>
    protected sealed override void OnDirectoryFinished(ReadOnlySpan<char> directory) =>
        _session?.DirectoryFinished(directory);

    /// <summary>
    ///  Applies an additional derived-type filter after the matcher has included a file.
    /// </summary>
    /// <param name="entry">The matched file entry.</param>
    /// <returns><see langword="true"/> to include the file; otherwise <see langword="false"/>.</returns>
    protected virtual bool ShouldIncludeMatchedFile(ref FileSystemEntry entry) => true;

    /// <summary>
    ///  Releases resources owned by a derived enumerator.
    /// </summary>
    /// <param name="disposing">
    ///  <see langword="true"/> to release managed resources; otherwise <see langword="false"/>.
    /// </param>
    protected virtual void DisposeAdditionalResources(bool disposing) { }

    /// <inheritdoc/>
    protected sealed override void Dispose(bool disposing)
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

        if (disposing)
        {
            if (Interlocked.Exchange(ref _additionalResourcesDisposed, 1) == 0)
            {
                try
                {
                    DisposeAdditionalResources(disposing);
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }

            if (Interlocked.Exchange(ref _sessionDisposed, 1) == 0)
            {
                try
                {
                    _session?.Dispose();
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }
        }

        if (firstException is not null)
        {
            ExceptionDispatchInfo.Capture(firstException).Throw();
        }
    }

    private IFileSystemMatcherSession GetSession() =>
        _session ??= _matcher.CreateSession(EnumerationRootDirectory)
            ?? throw new InvalidOperationException("The matcher returned a null session.");
}