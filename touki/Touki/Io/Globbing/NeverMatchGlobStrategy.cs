// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Matcher that returns <see langword="false"/> for every input. Used when a dialect
///  considers the source pattern invalid in a non-throwing way - e.g., MSBuild's
///  <c>MSBuildGlob.Parse</c> accepts patterns containing three or more consecutive
///  <c>*</c> characters but returns a glob that never matches anything.
/// </summary>
internal sealed class NeverMatchGlobStrategy : GlobStrategy
{
    public NeverMatchGlobStrategy(GlobDialect dialect, GlobOptions options)
        : base(dialect, options)
    {
    }

    /// <inheritdoc/>
    internal override bool MatchCore(
        ReadOnlySpan<char> directoryPrefix,
        ReadOnlySpan<char> fileName) => false;
}
