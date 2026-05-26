// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Matches any input (pattern is a single <c>*</c> or a run of <c>*</c>s), subject to
///  the POSIX leading-dot rule.
/// </summary>
internal sealed class AnyGlobStrategy : GlobStrategy
{
    public AnyGlobStrategy(GlobDialect dialect, GlobOptions options)
        : base(dialect, options)
    {
    }

    /// <inheritdoc/>
    internal override bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName)
    {
        // AnyGlobStrategy is path-unaware (pattern is `*` / `**`); the directory prefix
        // is always empty by construction. The leading-dot rule applies to the bare
        // file name in path-unaware semantics.
        Debug.Assert(directoryPrefix.IsEmpty);
        return MatchLeadingDot || fileName.Length <= 0 || fileName[0] != '.';
    }
}
