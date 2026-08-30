// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Combines directory classifications and manages owned child sessions for ordered-rule implementations.
/// </summary>
/// <param name="rules">The ordered match rules.</param>
/// <param name="sessions">The owned matcher sessions corresponding to the rules.</param>
/// <param name="includeUnmatched">Whether paths that match no rule are included.</param>
internal abstract class OrderedFileSystemMatcherSessionBase(
    FileSystemMatchRule[] rules,
    IFileSystemMatcherSession[] sessions,
    bool includeUnmatched) : FileSystemMatcherSession
{
    private int _disposed;

    /// <summary>
    ///  Gets the ordered match rules.
    /// </summary>
    protected FileSystemMatchRule[] Rules { get; } = rules;

    /// <summary>
    ///  Gets the owned matcher sessions corresponding to the rules.
    /// </summary>
    protected IFileSystemMatcherSession[] Sessions { get; } = sessions;

    /// <summary>
    ///  Gets whether paths that match no rule are included.
    /// </summary>
    protected bool IncludeUnmatched { get; } = includeUnmatched;

    public override DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        bool canInclude = IncludeUnmatched;
        bool canExclude = !IncludeUnmatched;
        for (int index = 0; index < Sessions.Length; index++)
        {
            DirectoryMatchType matchType = DirectoryMatchTypeOperations.Normalize(
                Sessions[index].MatchesDirectory(currentDirectory, directoryName));

            bool include = Rules[index].Action == FileSystemMatchAction.Include;
            if (matchType == DirectoryMatchType.AllDescendantFilesMatch)
            {
                canInclude = include;
                canExclude = !include;
            }
            else if (matchType == DirectoryMatchType.MayContainMatchingFiles)
            {
                canInclude |= include;
                canExclude |= !include;
            }
        }

        if (canInclude && canExclude)
        {
            return DirectoryMatchType.MayContainMatchingFiles;
        }

        return canInclude
            ? DirectoryMatchType.AllDescendantFilesMatch
            : DirectoryMatchType.NoDescendantFilesMatch;
    }

    public override void DirectoryFinished(ReadOnlySpan<char> directory)
    {
        for (int index = 0; index < Sessions.Length; index++)
        {
            Sessions[index].DirectoryFinished(directory);
        }
    }

    public override void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            FileSystemMatcherSessionFactory.DisposeSessions(Sessions);
        }
    }
}
