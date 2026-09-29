// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

internal static partial class DocumentationInheritanceResolver
{
    /// <summary>
    ///  Retains the distinction between absent, malformed, and parsed XML for each target.
    /// </summary>
    internal readonly struct MetadataDocumentationResult
    {
        internal MetadataDocumentationResult(bool parsed, MetadataDocumentationInfo documentation)
        {
            HasXml = true;
            Parsed = parsed;
            Documentation = documentation;
        }

        internal bool HasXml { get; }

        internal bool Parsed { get; }

        internal MetadataDocumentationInfo Documentation { get; }
    }
}
