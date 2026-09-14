// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    /// <summary>
    ///  Composes expanded MSBuild trailing-dot alternatives as a union or its complement.
    /// </summary>
    private sealed class MSBuildTrailingDotAlternativesState : MSBuildTrailingDotState
    {
        private readonly GlobSpecification[] _alternatives;
        private readonly bool _negated;

        public MSBuildTrailingDotAlternativesState(
            GlobSpecification[] alternatives,
            bool negated)
        {
            _alternatives = alternatives;
            _negated = negated;
        }

        public override bool Match(
            GlobStrategy strategy,
            ReadOnlySpan<char> directoryPrefix,
            ReadOnlySpan<char> fileName)
        {
            if (_negated && !strategy.MatchCore(directoryPrefix, fileName))
            {
                return false;
            }

            foreach (GlobSpecification alternative in _alternatives)
            {
                if (alternative.MatchCore(directoryPrefix, fileName))
                {
                    return !_negated;
                }
            }

            return _negated;
        }
    }
}