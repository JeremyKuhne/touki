// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    private static partial class Normalization
    {
        /// <summary>
        ///  Classification used by FileSystemGlobbing segment normalization.
        /// </summary>
        private enum FileSystemGlobbingSegmentKind : byte
        {
            Literal,
            Empty,
            Current,
            Parent,
            StarDotStar,
            DoubleStar,
            RecursiveSuffix,
        }
    }
}