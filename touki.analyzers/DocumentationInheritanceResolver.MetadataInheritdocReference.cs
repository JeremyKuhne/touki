// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

internal static partial class DocumentationInheritanceResolver
{
    /// <summary>
    ///  Describes one inheritdoc target read from metadata XML.
    /// </summary>
    private readonly struct MetadataInheritdocReference
    {
        public MetadataInheritdocReference(string target, bool hasPath)
        {
            Target = target;
            HasPath = hasPath;
        }

        public string Target { get; }

        public bool HasPath { get; }
    }
}