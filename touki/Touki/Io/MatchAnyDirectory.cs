// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Touki.Io;

/// <summary>
///  Matcher that matches any directory.
/// </summary>
/// <remarks>
///  <para>
///   This is intended to accept or reject directories only. It will never match files.
///   To match any directory name of a given pattern (no matter where it occurs), use
///   <see cref="MatchAnyDirectory(StringSegment, MatchType, MatchCasing)"/>. To match
///   directories only under a specific root path, use
///   <see cref="MatchAnyDirectory(StringSegment, StringSegment, MatchType, MatchCasing)"/>.
///  </para>
///  <para>
///   When used as an include pattern, a match will allow recursion into the match.
///   When used as an exclude pattern, a match will prevent recursion into the match.
///  </para>
/// </remarks>
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
    protected override bool MatchesDirectory(ReadOnlySpan<char> directoryName, bool matchForExclusion) => MatchesName(directoryName);
}
