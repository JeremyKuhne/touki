// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;

namespace Touki.Analyzers;

public sealed partial class MemberXmlDocumentationAnalyzer
{
    /// <summary>
    ///  Identifies member visibility groups selected for documentation analysis.
    /// </summary>
    [Flags]
    private enum ApiSurface
    {
        Public = 1,
        Internal = 2,
        Private = 4,
        Default = Public | Internal,
        All = Default | Private
    }
}