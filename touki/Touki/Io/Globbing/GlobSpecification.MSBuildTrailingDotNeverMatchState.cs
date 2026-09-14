// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    /// <summary>
    ///  Represents an MSBuild trailing-dot pattern whose alternatives can never match.
    /// </summary>
    private sealed class MSBuildTrailingDotNeverMatchState : MSBuildTrailingDotState
    {
        public static MSBuildTrailingDotNeverMatchState Instance { get; } = new();

        private MSBuildTrailingDotNeverMatchState()
        {
        }

        public override bool Match(
            GlobStrategy strategy,
            ReadOnlySpan<char> directoryPrefix,
            ReadOnlySpan<char> fileName) => false;
    }
}