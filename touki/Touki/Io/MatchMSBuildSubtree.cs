// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Matches every file beneath a fixed root or beneath directory segments selected by an MSBuild pattern.
/// </summary>
internal sealed class MatchMSBuildSubtree : DisposableBase, IFileSystemMatcherSession
{
    private const byte CurrentDirectoryNotEvaluated = 0;
    private const byte CurrentDirectoryDoesNotMatch = 1;
    private const byte CurrentDirectoryMatches = 2;

    private readonly StringSegment _rootPath;
    private readonly StringSegment _matchStartPath;
    private readonly MatchCasing _matchCasing;
    private readonly StringSegment _directoryPattern;
    private readonly MatchType _matchType;
    private readonly bool _hasDirectoryPattern;
    private readonly bool _directoryPatternIsSimpleLiteral;
    private bool _cacheValid;
    private bool _currentDirectoryWithinRoot;
    private byte _currentDirectoryMatch;

    /// <summary>
    ///  Initializes a matcher that includes every file beneath a fixed root path.
    /// </summary>
    /// <param name="rootPath">The fixed root path.</param>
    /// <param name="matchCasing">The path comparison casing.</param>
    public MatchMSBuildSubtree(StringSegment rootPath, MatchCasing matchCasing)
    {
        _rootPath = rootPath.TrimEnd(Path.DirectorySeparatorChar);
        _matchCasing = Paths.GetFinalCasing(matchCasing);
    }

    /// <summary>
    ///  Initializes a matcher that includes subtrees selected by a directory pattern.
    /// </summary>
    /// <param name="rootPath">The fixed root path.</param>
    /// <param name="matchStartPath">The path from which relative directory matching begins.</param>
    /// <param name="directoryPattern">The directory segment pattern.</param>
    /// <param name="matchType">The pattern matching mode.</param>
    /// <param name="matchCasing">The path comparison casing.</param>
    public MatchMSBuildSubtree(
        StringSegment rootPath,
        StringSegment matchStartPath,
        StringSegment directoryPattern,
        MatchType matchType,
        MatchCasing matchCasing)
        : this(rootPath, matchCasing)
    {
        _matchStartPath = matchStartPath.TrimEnd(Path.DirectorySeparatorChar);
        _directoryPattern = directoryPattern;
        _matchType = matchType;
        _hasDirectoryPattern = true;
        _directoryPatternIsSimpleLiteral = matchType == MatchType.Simple
            && directoryPattern.IndexOfAny('*', '?') < 0;
    }

    /// <summary>
    ///  Invalidates the cached classification for the completed directory.
    /// </summary>
    /// <param name="directory">The completed directory.</param>
    public void DirectoryFinished(ReadOnlySpan<char> directory) => _cacheValid = false;

    /// <summary>
    ///  Classifies whether a candidate directory can contain matching files.
    /// </summary>
    /// <param name="currentDirectory">The directory containing the candidate directory.</param>
    /// <param name="directoryName">The candidate directory name.</param>
    /// <returns>The match classification for the candidate directory and its descendants.</returns>
    public DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        EnsureCurrentDirectory(currentDirectory);
        if (_currentDirectoryMatch == CurrentDirectoryMatches)
        {
            return DirectoryMatchType.AllDescendantFilesMatch;
        }

        if (!_hasDirectoryPattern && CandidateMatchesRoot(currentDirectory, directoryName))
        {
            return DirectoryMatchType.AllDescendantFilesMatch;
        }

        if (!_currentDirectoryWithinRoot)
        {
            return Paths.CandidateIsSameOrAncestorOf(
                _rootPath,
                currentDirectory,
                directoryName,
                ignoreCase: _matchCasing == MatchCasing.CaseInsensitive)
                    ? DirectoryMatchType.MayContainMatchingFiles
                    : DirectoryMatchType.NoDescendantFilesMatch;
        }

        return _hasDirectoryPattern && DirectoryMatchesPattern(directoryName)
                    ? DirectoryMatchType.AllDescendantFilesMatch
                    : DirectoryMatchType.MayContainMatchingFiles;
    }

    /// <summary>
    ///  Determines whether a file is within a selected subtree.
    /// </summary>
    /// <param name="currentDirectory">The directory containing the file.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns>
    ///  <see langword="true"/> if the file is within a selected subtree; otherwise <see langword="false"/>.
    /// </returns>
    public bool MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName)
    {
        EnsureCurrentDirectory(currentDirectory);
        if (_hasDirectoryPattern
            && _currentDirectoryMatch == CurrentDirectoryNotEvaluated
            && _currentDirectoryWithinRoot)
        {
            _currentDirectoryMatch = CurrentDirectoryMatchesPattern(currentDirectory)
                ? CurrentDirectoryMatches
                : CurrentDirectoryDoesNotMatch;
        }

        return _currentDirectoryMatch == CurrentDirectoryMatches;
    }

    private void EnsureCurrentDirectory(ReadOnlySpan<char> currentDirectory)
    {
        if (_cacheValid)
        {
            return;
        }

        bool ignoreCase = _matchCasing == MatchCasing.CaseInsensitive;
        _currentDirectoryWithinRoot = _hasDirectoryPattern
            ? _rootPath.IsEmpty || Paths.IsSameOrSubdirectory(_rootPath, currentDirectory, ignoreCase)
            : Paths.IsSameOrSubdirectory(_rootPath, currentDirectory, ignoreCase);
        _currentDirectoryMatch = _hasDirectoryPattern
            ? CurrentDirectoryNotEvaluated
            : _currentDirectoryWithinRoot
                ? CurrentDirectoryMatches
                : CurrentDirectoryDoesNotMatch;
        _cacheValid = true;
    }

    private bool CurrentDirectoryMatchesPattern(ReadOnlySpan<char> currentDirectory)
    {
        ReadOnlySpan<char> relativeDirectory = currentDirectory;
        if (!_matchStartPath.IsEmpty)
        {
            relativeDirectory = currentDirectory.Length <= _matchStartPath.Length
                ? default
                : currentDirectory[_matchStartPath.Length..];
        }

        PathSegmentEnumerator segments = new(relativeDirectory);
        while (segments.MoveNext())
        {
            if (DirectoryMatchesPattern(segments.Current))
            {
                return true;
            }
        }

        return false;
    }

    private bool DirectoryMatchesPattern(ReadOnlySpan<char> directoryName) =>
        _directoryPatternIsSimpleLiteral
            ? directoryName.Equals(_directoryPattern.AsSpan(), StringComparison.OrdinalIgnoreCase)
            : Paths.MatchesExpression(
                directoryName,
                _directoryPattern,
                MatchCasing.CaseInsensitive,
                _matchType);

    private bool CandidateMatchesRoot(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        bool needsSeparator = !currentDirectory.IsEmpty
            && currentDirectory[^1] != Path.DirectorySeparatorChar;
        int expectedLength = currentDirectory.Length + directoryName.Length + (needsSeparator ? 1 : 0);
        if (_rootPath.Length != expectedLength)
        {
            return false;
        }

        StringComparison comparison = _matchCasing == MatchCasing.CaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!_rootPath.StartsWith(currentDirectory, comparison))
        {
            return false;
        }

        int directoryNameOffset = currentDirectory.Length;
        if (needsSeparator)
        {
            if (_rootPath[directoryNameOffset] != Path.DirectorySeparatorChar)
            {
                return false;
            }

            directoryNameOffset++;
        }

        return _rootPath[directoryNameOffset..].AsSpan().Equals(directoryName, comparison);
    }

    protected override void Dispose(bool disposing)
    {
    }
}