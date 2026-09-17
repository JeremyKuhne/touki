// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    /// <summary>
    ///  Encapsulates one complete MSBuild trailing-dot matching mode.
    /// </summary>
    private abstract class MSBuildTrailingDotState
    {
        public abstract bool Match(
            GlobStrategy strategy,
            ReadOnlySpan<char> directoryPrefix,
            ReadOnlySpan<char> fileName);
    }
}