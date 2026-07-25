// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public partial class MatchMSBuild
{
    private enum PathMatchState
    {
        /// <summary>
        ///  The path does not match the specification, and there is no possibility of a match in a subdirectory.
        /// </summary>
        NoMatch,

        /// <summary>
        ///  The path partially matches the specification, meaning it could match in a subdirectory, but files within
        ///  the specified path do not match.
        /// </summary>
        PartialMatch,

        /// <summary>
        ///  The path fully matches the specification, meaning it matches all segments and files within the
        ///  specified path.
        /// </summary>
        FullMatch
    }
}
