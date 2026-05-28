// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public sealed partial class GlobSpecification
{
    private static partial class Factory
    {
        /// <summary>
        ///  Coarse classification of a pattern's metacharacter usage.
        /// </summary>
        private struct PatternShape
        {
            public int StarCount;
            public bool HasQuestionMarks;
            public bool HasClasses;
            public bool HasEscapes;
            public bool HasExtGlob;
            public bool LeadsWithStar;
            public bool EndsWithStar;
            public bool IsAllStars;
            public bool HasNoMetacharacters;
            public int SingleStarSourceIndex;
        }
    }
}
