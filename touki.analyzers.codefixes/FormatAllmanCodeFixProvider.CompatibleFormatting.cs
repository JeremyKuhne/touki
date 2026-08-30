// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public sealed partial class FormatAllmanCodeFixProvider
{
    /// <summary>
    ///  Carries one compatible formatted text result and the linked documents that share it.
    /// </summary>
    private readonly struct CompatibleFormatting
    {
        public CompatibleFormatting(SourceText text, ImmutableArray<DocumentId> documentIds)
        {
            Text = text;
            DocumentIds = documentIds;
        }

        public SourceText Text { get; }

        public ImmutableArray<DocumentId> DocumentIds { get; }
    }
}