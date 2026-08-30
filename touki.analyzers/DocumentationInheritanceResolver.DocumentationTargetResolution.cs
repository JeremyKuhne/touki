// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

internal static partial class DocumentationInheritanceResolver
{
    /// <summary>
    ///  Identifies the result of resolving an inherited-documentation target.
    /// </summary>
    private enum DocumentationTargetResolution
    {
        Unresolved,
        Resolved,
        Ambiguous
    }
}