// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Matches any file under an MSBuild root using the requested physical or logical filename semantics.
/// </summary>
internal sealed class MSBuildMatchAnyFile : DisposableBase,
    IFileSystemMatcherSession
{
    private readonly StringSegment _expression;
    private readonly StringSegment _rootPath;
    private readonly MatchType _matchType;
    private readonly MatchCasing _matchCasing;
    private readonly MatchCasing _rootMatchCasing;
    private readonly bool _useMSBuildFileNameSemantics;
    private readonly MSBuildFileNamePattern _msbuildFileNamePattern;
    private bool? _nestingMatched;

    /// <summary>
    ///  Initializes a filename matcher rooted at an MSBuild search path.
    /// </summary>
    /// <param name="expression">The filename expression.</param>
    /// <param name="rootPath">The root path beneath which files can match.</param>
    /// <param name="matchType">The pattern matching mode.</param>
    /// <param name="matchCasing">The filename comparison casing.</param>
    /// <param name="rootMatchCasing">The root path comparison casing.</param>
    /// <param name="useMSBuildFileNameSemantics">Whether to apply MSBuild filename policy.</param>
    public MSBuildMatchAnyFile(
        StringSegment expression,
        StringSegment rootPath,
        MatchType matchType,
        MatchCasing matchCasing,
        MatchCasing rootMatchCasing,
        bool useMSBuildFileNameSemantics)
    {
        _expression = expression;
        _rootPath = rootPath.TrimEnd(Path.DirectorySeparatorChar);
        _matchType = matchType;
        _matchCasing = Paths.GetFinalCasing(matchCasing);
        _rootMatchCasing = Paths.GetFinalCasing(rootMatchCasing);
        _useMSBuildFileNameSemantics = useMSBuildFileNameSemantics;
        if (useMSBuildFileNameSemantics)
        {
            _msbuildFileNamePattern = new(expression, matchType);
        }
    }

    /// <summary>
    ///  Invalidates the cached root-path classification for the completed directory.
    /// </summary>
    /// <param name="directory">The completed directory.</param>
    public void DirectoryFinished(ReadOnlySpan<char> directory) => _nestingMatched = null;

    /// <summary>
    ///  Classifies whether a candidate directory can contain files beneath the configured root.
    /// </summary>
    /// <param name="currentDirectory">The directory containing the candidate directory.</param>
    /// <param name="directoryName">The candidate directory name.</param>
    /// <returns>The match classification for the candidate directory and its descendants.</returns>
    public DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        if (MatchesRoot(currentDirectory))
        {
            return DirectoryMatchType.MayContainMatchingFiles;
        }

        return Paths.CandidateIsSameOrAncestorOf(
            _rootPath,
            currentDirectory,
            directoryName,
            ignoreCase: _rootMatchCasing == MatchCasing.CaseInsensitive)
                ? DirectoryMatchType.MayContainMatchingFiles
                : DirectoryMatchType.NoDescendantFilesMatch;
    }

    /// <summary>
    ///  Determines whether a file is beneath the configured root and matches the filename expression.
    /// </summary>
    /// <param name="currentDirectory">The directory containing the file.</param>
    /// <param name="fileName">The filename.</param>
    /// <returns><see langword="true"/> if the file matches; otherwise <see langword="false"/>.</returns>
    public bool MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName)
    {
        if (!MatchesRoot(currentDirectory))
        {
            return false;
        }

        if (!_useMSBuildFileNameSemantics)
        {
            return Paths.MatchesExpression(fileName, _expression, _matchCasing, _matchType);
        }

        return _msbuildFileNamePattern.Matches(fileName, _matchCasing);
    }

    private bool MatchesRoot(ReadOnlySpan<char> currentDirectory)
    {
        if (_rootPath.IsEmpty)
        {
            return true;
        }

        _nestingMatched ??= Paths.IsSameOrSubdirectory(
            _rootPath,
            currentDirectory,
            ignoreCase: _rootMatchCasing == MatchCasing.CaseInsensitive);

        return _nestingMatched.Value;
    }

    protected override void Dispose(bool disposing) { }
}