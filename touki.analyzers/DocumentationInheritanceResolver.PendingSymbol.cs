// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis;

namespace Touki.Analyzers;

internal static partial class DocumentationInheritanceResolver
{
    /// <summary>
    ///  Describes a symbol awaiting inherited-documentation resolution.
    /// </summary>
    private readonly struct PendingSymbol
    {
        public PendingSymbol(
            ISymbol symbol,
            Compilation compilation,
            bool followHierarchy,
            bool includeImplicitInterfaceTargets)
        {
            Symbol = symbol;
            Compilation = compilation;
            FollowHierarchy = followHierarchy;
            IncludeImplicitInterfaceTargets = includeImplicitInterfaceTargets;
        }

        public ISymbol Symbol { get; }

        public Compilation Compilation { get; }

        public bool FollowHierarchy { get; }

        public bool IncludeImplicitInterfaceTargets { get; }
    }
}