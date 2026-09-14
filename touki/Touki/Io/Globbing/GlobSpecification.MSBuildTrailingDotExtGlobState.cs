// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    /// <summary>
    ///  Owns the normalized and raw strategies required for MSBuild trailing-dot extglob matching.
    /// </summary>
    private sealed class MSBuildTrailingDotExtGlobState : MSBuildTrailingDotState
    {
        private readonly CompiledGlobStrategy _strategy;
        private readonly CompiledGlobStrategy _rawStrategy;

        public MSBuildTrailingDotExtGlobState(
            CompiledGlobStrategy strategy,
            CompiledGlobStrategy rawStrategy)
        {
            _strategy = strategy;
            _rawStrategy = rawStrategy;
        }

        public override bool Match(
            GlobStrategy strategy,
            ReadOnlySpan<char> directoryPrefix,
            ReadOnlySpan<char> fileName)
        {
            ReadOnlySpan<char> normalized = MSBuildTrailingDotFileNameMatcher.NormalizeExtGlobInput(
                fileName,
                out bool isAllDotInput);

            bool trailingDotMatch = isAllDotInput
                ? _strategy.MatchesMSBuildTrailingDotAllDotInput(directoryPrefix)
                : _strategy.MatchCore(directoryPrefix, normalized);

            return trailingDotMatch || _rawStrategy.MatchCore(directoryPrefix, fileName);
        }
    }
}