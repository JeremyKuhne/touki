// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

internal static partial class AllmanFormatter
{
    private sealed partial class PreprocessorLineMap
    {
        /// <summary>
        ///  Describes an inclusive range of source line numbers.
        /// </summary>
        private readonly struct LineRange
        {
            public LineRange(int firstLine, int finalLine)
            {
                FirstLine = firstLine;
                FinalLine = finalLine;
            }

            public int FirstLine { get; }

            public int FinalLine { get; }
        }
    }
}