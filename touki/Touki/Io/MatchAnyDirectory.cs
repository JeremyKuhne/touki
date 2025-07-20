// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Matcher that matches any directory.
/// </summary>
public sealed class MatchAnyDirectory : MatchAnyBase
{
    /// <summary>
    ///  Constructs a new <see cref="MatchAnyDirectory"/> with a primary directory name expression.
    /// </summary>
    /// <inheritdoc/>
    public MatchAnyDirectory(
        StringSegment expression,
        StringSegment rootPath,
        MatchType matchType,
        MatchCasing matchCasing) : base(expression, rootPath, matchType, matchCasing)
    {
    }

    /// <summary>
    ///  Constructs a new <see cref="MatchAnyDirectory"/> with a single directory name expression.
    /// </summary>
    public MatchAnyDirectory(StringSegment expression, MatchType matchType, MatchCasing matchCasing)
        : base(expression, matchType, matchCasing)
    {
    }

    /// <inheritdoc/>
    protected override bool MatchesFile(ReadOnlySpan<char> fileName) => false;

    /// <inheritdoc/>
    protected override bool MatchesDirectory(ReadOnlySpan<char> directoryName) => MatchesName(directoryName);
}
