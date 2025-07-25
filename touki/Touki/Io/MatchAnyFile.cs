﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Matcher that matches any file.
/// </summary>
public sealed class MatchAnyFile : MatchAnyBase
{
    /// <summary>
    ///  Constructs a new <see cref="MatchAnyFile"/> with a primary file name expression.
    /// </summary>
    /// <inheritdoc/>
    public MatchAnyFile(
        StringSegment expression,
        StringSegment rootPath,
        MatchType matchType,
        MatchCasing matchCasing) : base(expression, rootPath, matchType, matchCasing)
    {
    }

    /// <summary>
    ///  Constructs a new <see cref="MatchAnyFile"/> with a primary file name expression.
    /// </summary>
    /// <inheritdoc/>
    public MatchAnyFile(
        StringSegment expression,
        MatchType matchType,
        MatchCasing matchCasing) : base(expression, matchType, matchCasing)
    {
    }

    /// <inheritdoc/>
    protected override bool MatchesFile(ReadOnlySpan<char> fileName) => MatchesName(fileName);
}
