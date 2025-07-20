// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information


namespace Touki.Io;

/// <summary>
///  File system enumerator that matches files and directories based on a specified matcher.
/// </summary>
public sealed class MatchEnumerator : FileSystemEnumerator<string>
{
    private readonly IEnumerationMatcher _matcher;
    private readonly FindTransform? _findTransform;

    /// <summary>
    ///  Default options for the enumerator.
    /// </summary>
    private static EnumerationOptions DefaultOptions { get; } = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    };

    /// <summary>
    ///  Initializes a new instance of the <see cref="MatchEnumerator"/> class.
    /// </summary>
    public MatchEnumerator(
        string directory,
        IEnumerationMatcher matcher,
        FindTransform? transform = null)
        : this(directory, matcher, transform, DefaultOptions)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="MatchEnumerator"/> class.
    /// </summary>
    public MatchEnumerator(
        string directory,
        IEnumerationMatcher matcher,
        FindTransform? transform,
        EnumerationOptions options)
        : base(directory, options)
    {
        ArgumentNull.ThrowIfNull(directory);
        ArgumentNull.ThrowIfNull(matcher);
        _matcher = matcher;
        _findTransform = transform;
    }

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        _matcher.MatchesFile(entry.Directory, entry.FileName);

    /// <inheritdoc/>
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
        _matcher.MatchesDirectory(entry.Directory, entry.FileName);

    /// <inheritdoc/>
    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory) => _matcher.DirectoryFinished();

    /// <inheritdoc/>
    protected override string TransformEntry(ref FileSystemEntry entry) =>
        _findTransform?.Invoke(ref entry) ?? entry.ToFullPath();

    /// <summary>
    /// Delegate for transforming raw find data into a result.
    /// </summary>
    public delegate string FindTransform(ref FileSystemEntry entry);
}
