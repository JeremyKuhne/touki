// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis;

namespace Touki.Analyzers;

public sealed partial class UseTextWriterWriteFormattedCodeFixProvider
{
    /// <summary>
    ///  Associates an annotated invocation with the symbol identity validated before rewriting.
    /// </summary>
    private sealed class BindingSnapshot(SyntaxAnnotation annotation, string symbolIdentity)
    {
        public SyntaxAnnotation Annotation { get; } = annotation;

        public string SymbolIdentity { get; } = symbolIdentity;
    }
}