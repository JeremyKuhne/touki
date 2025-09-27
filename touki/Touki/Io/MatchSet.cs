// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;

namespace Touki.Io;

/// <summary>
///  A set of matchers that can include and exclude matches for enumeration.
/// </summary>
/// <remarks>
///  <para>
///   Excludes are processed before includes, meaning that if an exclude matcher matches, the include matchers will
///   not be considered. This optimizes the matching process by allowing for early exits when a subdirectory does
///   not match.
///  </para>
/// </remarks>
public sealed class MatchSet : DisposableBase, IEnumerationMatcher
{
    private readonly SingleOptimizedList<IEnumerationMatcher, ArrayPoolList<IEnumerationMatcher>> _includes = [];
    private SingleOptimizedList<IEnumerationMatcher, ArrayPoolList<IEnumerationMatcher>>? _excludes;

    /// <summary>
    ///  Constructs a new <see cref="MatchSet"/> with the specified primary match.
    /// </summary>
    public MatchSet(IEnumerationMatcher includeMatcher)
    {
        ArgumentNullException.ThrowIfNull(includeMatcher);
        _includes.Add(includeMatcher);
    }

    /// <summary>
    ///  Adds an include matcher to the set.
    /// </summary>
    /// <param name="includeMatcher">If no excludes have been processed, this matcher will be considered for matches.</param>
    public void AddInclude(IEnumerationMatcher includeMatcher)
    {
        ArgumentNullException.ThrowIfNull(includeMatcher);
        _includes.Add(includeMatcher);
    }

    /// <summary>
    ///  Adds an exclude matcher to the set.
    /// </summary>
    /// <param name="excludeMatcher">This matcher will be considered to block consideration for matching.</param>
    public void AddExclude(IEnumerationMatcher excludeMatcher)
    {
        ArgumentNullException.ThrowIfNull(excludeMatcher);
        _excludes ??= [];
        _excludes.Add(excludeMatcher);
    }

    void IEnumerationMatcher.DirectoryFinished()
    {
        foreach (IEnumerationMatcher matcher in _includes)
        {
            matcher.DirectoryFinished();
        }

        if (_excludes is { } excludes)
        {
            foreach (IEnumerationMatcher matcher in excludes)
            {
                matcher.DirectoryFinished();
            }
        }
    }

    bool IEnumerationMatcher.MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName,
        bool matchForExclusion)
    {
        if (_excludes is { } excludes)
        {
            foreach (IEnumerationMatcher matcher in excludes)
            {
                if (matcher.MatchesDirectory(currentDirectory, directoryName, matchForExclusion: true))
                {
                    // Excluded
                    return false;
                }
            }
        }

        foreach (IEnumerationMatcher matcher in _includes)
        {
            if (matcher.MatchesDirectory(currentDirectory, directoryName, matchForExclusion: false))
            {
                // Matched
                return true;
            }
        }

        return false;
    }

    bool IEnumerationMatcher.MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName)
    {
        if (_excludes is { } excludes)
        {
            foreach (IEnumerationMatcher matcher in excludes)
            {
                if (matcher.MatchesFile(currentDirectory, fileName))
                {
                    // Excluded
                    return false;
                }
            }
        }

        foreach (IEnumerationMatcher matcher in _includes)
        {
            if (matcher.MatchesFile(currentDirectory, fileName))
            {
                // Matched
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _includes.Dispose();
            _excludes?.Dispose();
        }
    }
}
