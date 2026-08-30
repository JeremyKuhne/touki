// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

internal static partial class AllmanFormatter
{
    /// <summary>
    ///  Identifies the preprocessor role of a source line.
    /// </summary>
    private enum PreprocessorLineKind
    {
        None,
        Directive,
        AlternateBranch,
        DisabledText
    }
}