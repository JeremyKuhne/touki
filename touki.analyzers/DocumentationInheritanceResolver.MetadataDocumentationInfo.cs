// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;

namespace Touki.Analyzers;

internal static partial class DocumentationInheritanceResolver
{
    /// <summary>
    ///  Captures documentation elements read from one metadata XML member.
    /// </summary>
    private struct MetadataDocumentationInfo
    {
        public bool HasSummary;
        public bool HasInheritdoc;
        public bool HasImplicitInheritdoc;
        public List<MetadataInheritdocReference>? InheritdocReferences;
    }
}