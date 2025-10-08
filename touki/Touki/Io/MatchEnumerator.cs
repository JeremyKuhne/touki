// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  File system enumerator that matches files and directories based on a specified matcher.
/// </summary>
public abstract class MatchEnumerator<TResult> : FileSystemEnumerator<TResult>
{
    private readonly IEnumerationMatcher _matcher;

    /// <summary>
    ///  Initializes a new instance of the <see cref="MatchEnumerator{TResult}"/> class.
    /// </summary>
    public MatchEnumerator(
        string directory,
        IEnumerationMatcher matcher,
        EnumerationOptions? options)
        : base(directory, options)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(matcher);
        _matcher = matcher;
    }

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        _matcher.MatchesFile(entry.Directory, entry.FileName);

    /// <inheritdoc/>
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
        _matcher.MatchesDirectory(entry.Directory, entry.FileName, matchForExclusion: false);

    /// <inheritdoc/>
    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory) => _matcher.DirectoryFinished();
}
