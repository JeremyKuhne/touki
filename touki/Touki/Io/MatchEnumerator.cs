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
    private readonly bool _skipEnumeration;

    /// <summary>
    ///  Initializes a new instance of the <see cref="MatchEnumerator{TResult}"/> class.
    /// </summary>
    public MatchEnumerator(
        string directory,
        IEnumerationMatcher matcher,
        EnumerationOptions? options)
        : base(GetExistingDirectory(directory, out bool skip), options)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        _matcher = matcher;
        _skipEnumeration = skip;
    }

    private static string GetExistingDirectory(string directory, out bool skipEnumeration)
    {
        ArgumentNullException.ThrowIfNull(directory);

        // The underlying <see cref="FileSystemEnumerator{T}"/> opens the start directory eagerly in
        // its constructor (Init -> CreateDirectoryHandle) and throws when the path is missing or is a
        // file rather than a directory. <see cref="EnumerationOptions.IgnoreInaccessible"/> only covers
        // access-denied errors, not invalid-parameter or directory-not-found errors. To handle specs
        // whose resolved fixed path is not an existing directory (e.g. trailing-separator specs like
        // "Foo/b.txt/" or specs naming a missing subdirectory), we redirect the base enumerator to a
        // sentinel directory that always exists and skip enumeration in <see cref="MoveNext"/>. This
        // mirrors MSBuild's behavior in <c>FileMatcher.GetFileSearchData</c>, which returns
        // <c>SearchAction.ReturnEmptyList</c> when the fixed directory does not exist.
        if (Directory.Exists(directory))
        {
            skipEnumeration = false;
            return directory;
        }

        skipEnumeration = true;
        return Path.GetTempPath();
    }

    /// <summary>
    ///  Advances the enumerator to the next match.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Shadowed (not overridden) because <see cref="FileSystemEnumerator{T}.MoveNext"/> is not
    ///   virtual on either target framework. Callers that hold a <see cref="MatchEnumerator{TResult}"/>
    ///   (or a derived type such as <see cref="MSBuildEnumerator"/>) statically - including the
    ///   <c>foreach</c> loops emitted against those types - bind to this shadow and get the
    ///   no-existing-directory guard. Code that upcasts to <see cref="FileSystemEnumerator{T}"/> or
    ///   <see cref="System.Collections.Generic.IEnumerator{T}"/> would bypass it; we don't expose those
    ///   shapes in the public API.
    ///  </para>
    /// </remarks>
    public new bool MoveNext() => !_skipEnumeration && base.MoveNext();

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        _matcher.MatchesFile(entry.Directory, entry.FileName);

    /// <inheritdoc/>
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
        _matcher.MatchesDirectory(entry.Directory, entry.FileName, matchForExclusion: false);

    /// <inheritdoc/>
    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory) => _matcher.DirectoryFinished();
}
