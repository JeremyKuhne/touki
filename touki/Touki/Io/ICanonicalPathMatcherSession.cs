// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Allows composed matcher sessions to consume a canonical root-relative path without rebuilding it per session.
/// </summary>
internal interface ICanonicalPathMatcherSession
{
    /// <summary>
    ///  Determines whether a canonical root-relative path matches.
    /// </summary>
    /// <param name="rootRelativePath">The canonical root-relative path.</param>
    /// <returns><see langword="true"/> if the path matches; otherwise <see langword="false"/>.</returns>
    bool MatchesPath(ReadOnlySpan<char> rootRelativePath);
}
