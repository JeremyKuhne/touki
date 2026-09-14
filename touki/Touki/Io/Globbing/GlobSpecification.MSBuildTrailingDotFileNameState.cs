// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    /// <summary>
    ///  Matches an MSBuild trailing-dot file-name pattern before applying the primary strategy.
    /// </summary>
    private sealed class MSBuildTrailingDotFileNameState : MSBuildTrailingDotState
    {
        private readonly StringSegment _pattern;

        public MSBuildTrailingDotFileNameState(StringSegment pattern)
        {
            _pattern = pattern;
        }

        public override bool Match(
            GlobStrategy strategy,
            ReadOnlySpan<char> directoryPrefix,
            ReadOnlySpan<char> fileName)
        {
            return MSBuildTrailingDotFileNameMatcher.Matches(fileName, _pattern, strategy.IgnoreCaseKind)
                && strategy.MatchCore(directoryPrefix, fileName);
        }
    }
}