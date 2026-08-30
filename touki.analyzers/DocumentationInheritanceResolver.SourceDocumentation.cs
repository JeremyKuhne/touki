// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

internal static partial class DocumentationInheritanceResolver
{
    /// <summary>
    ///  Carries parsed source documentation and whether its declaration is inspectable C#.
    /// </summary>
    private readonly struct SourceDocumentation
    {
        public SourceDocumentation(
            XmlDocumentationInfo documentation,
            bool hasCSharpDeclaration)
        {
            Documentation = documentation;
            HasCSharpDeclaration = hasCSharpDeclaration;
        }

        public XmlDocumentationInfo Documentation { get; }

        public bool HasCSharpDeclaration { get; }
    }
}