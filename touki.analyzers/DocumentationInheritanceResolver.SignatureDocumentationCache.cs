// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Touki.Analyzers;

internal static partial class DocumentationInheritanceResolver
{
    /// <summary>
    ///  Reuses parsed inheritance targets and traversal collections for one member's signature.
    /// </summary>
    internal sealed class SignatureDocumentationCache
    {
        private Dictionary<ISymbol, SourceDocumentation>? _source;
        private Dictionary<ISymbol, MetadataDocumentationResult>? _metadata;

        internal HashSet<ISymbol> Inspected { get; } = [with(SymbolEqualityComparer.Default)];

        internal HashSet<ISymbol> ExpandedHierarchies { get; } = [with(SymbolEqualityComparer.Default)];

        internal Dictionary<Compilation, Compilation> AliasNormalizedCompilations { get; } = [];

        internal void ResetTraversal()
        {
            Inspected.Clear();
            ExpandedHierarchies.Clear();
            AliasNormalizedCompilations.Clear();
        }

        internal SourceDocumentation GetSource(ISymbol symbol, Compilation compilation, CancellationToken cancellationToken)
        {
            if (_source is not null && _source.TryGetValue(symbol, out SourceDocumentation documentation))
            {
                return documentation;
            }

            SourceDocumentation result = GetSourceDocumentation(
                symbol,
                compilation,
                includeSourceDeclaration: null,
                cancellationToken);
            (_source ??= [with(SymbolEqualityComparer.Default)]).Add(symbol, result);
            return result;
        }

        internal MetadataDocumentationResult GetMetadata(ISymbol symbol, CancellationToken cancellationToken)
        {
            if (_metadata is not null && _metadata.TryGetValue(symbol, out MetadataDocumentationResult documentation))
            {
                return documentation;
            }

            MetadataDocumentationResult result = ReadMetadataDocumentation(
                symbol,
                includeSignatureTags: true,
                cancellationToken);
            (_metadata ??= [with(SymbolEqualityComparer.Default)]).Add(symbol, result);
            return result;
        }
    }
}
