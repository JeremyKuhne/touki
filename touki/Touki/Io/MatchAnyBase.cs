// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;
using Touki.Text;

namespace Touki.Io;

/// <summary>
///  Base class for simple matches that match any file or directory name.
/// </summary>
public abstract class MatchAnyBase : DisposableBase, IEnumerationMatcher
{
    private readonly MatchType _matchType;
    private protected readonly MatchCasing _matchCasing;
    private protected readonly StringSegment _rootPath;

    private readonly SingleOptimizedList<StringSegment, ArrayPoolList<StringSegment>> _expressions = [];

    private bool? _nestingMatched;

    /// <summary>
    ///  Constructs a new <see cref="MatchAnyBase"/> with a single name expression.
    /// </summary>
    /// <param name="expression">The primary expression to match.</param>
    /// <param name="rootPath">The root path that must match, can be empty for all paths.</param>
    /// <param name="matchType">The type of match to perform.</param>
    /// <param name="matchCasing">How to match casing.</param>
    public MatchAnyBase(
        StringSegment expression,
        StringSegment rootPath,
        MatchType matchType,
        MatchCasing matchCasing)
    {
        _matchType = matchType;
        _matchCasing = Paths.GetFinalCasing(matchCasing);

        // The directories passed back don't have a directory separator on the end.
        _rootPath = rootPath.TrimEnd(Path.DirectorySeparatorChar);
        AddSpec(expression);
    }

    /// <inheritdoc cref="MatchAnyBase(StringSegment, StringSegment, MatchType, MatchCasing)"/>
    public MatchAnyBase(
        StringSegment expression,
        MatchType matchType,
        MatchCasing matchCasing) : this(expression, default, matchType, matchCasing)
    {
    }

    /// <summary>
    ///  Adds another name expression to match.
    /// </summary>
    /// <param name="expression">The name matching expression to add.</param>
    /// <returns><see langword="true"/> if the expression was added, <see langword="false"/> if it was a duplicate.</returns>
    public bool AddSpec(StringSegment expression)
    {
        Debug.Assert(!expression.IsEmpty);
        Debug.Assert(expression.IndexOf(Path.DirectorySeparatorChar) == -1);

        if (_expressions.Contains(
            expression,
            _matchCasing == MatchCasing.CaseInsensitive
                ? StringSegmentComparer.OrdinalIgnoreCase
                : StringSegmentComparer.Ordinal))
        {
            // Already contains this expression, no need to add it again.
            return false;
        }

        _expressions.Add(expression);
        return true;
    }

    void IEnumerationMatcher.DirectoryFinished()
    {
        _nestingMatched = null;
        DirectoryFinished();
    }

    /// <inheritdoc cref="IEnumerationMatcher.DirectoryFinished"/>
    public virtual void DirectoryFinished() { }

    bool IEnumerationMatcher.MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName) =>
        MatchesRoot(currentDirectory) && MatchesFile(fileName);

    /// <inheritdoc cref="IEnumerationMatcher.MatchesFile(ReadOnlySpan{char}, ReadOnlySpan{char})"/>
    protected virtual bool MatchesFile(ReadOnlySpan<char> fileName) => true;

    bool IEnumerationMatcher.MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName,
        bool matchForExclusion) =>
        MatchesRoot(currentDirectory) && MatchesDirectory(directoryName, matchForExclusion);

    /// <inheritdoc cref="IEnumerationMatcher.MatchesDirectory(ReadOnlySpan{char}, ReadOnlySpan{char}, bool)"/>
    protected virtual bool MatchesDirectory(ReadOnlySpan<char> directoryName, bool matchForExclusion) => true;

    /// <summary>
    ///  Returns true if the given <paramref name="name"/> matches any of the expressions in <see cref="_expressions"/>
    ///  using the specified <see cref="MatchCasing"/> and <see cref="MatchType"/>.
    /// </summary>
    protected bool MatchesName(ReadOnlySpan<char> name)
    {
        for (int i = 0; i < _expressions.Count; i++)
        {
            if (Paths.MatchesExpression(name, _expressions[i], _matchCasing, _matchType))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesRoot(ReadOnlySpan<char> currentDirectory)
    {
        if (_rootPath.IsEmpty)
        {
            // No root path means we match everything.
            return true;
        }

        if (_nestingMatched.HasValue)
        {
            return _nestingMatched.Value;
        }

        _nestingMatched = Paths.IsSameOrSubdirectory(
            _rootPath,
            currentDirectory,
            ignoreCase: _matchCasing == MatchCasing.CaseInsensitive);

        // It would be nice to short-circuit if start an enumeration with a path that matches the root
        // (as all subsequent directories will also match). We'd need to express enumeration start and end
        // in the interface for that and other similar optimizations.

        return _nestingMatched.Value;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _expressions.Dispose();
        }
    }
}
