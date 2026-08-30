// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Touki.Analyzers;

/// <summary>
///  Describes the target and filtering state of a source <c>&lt;inheritdoc&gt;</c> element.
/// </summary>
internal readonly struct InheritdocReference
{
    public InheritdocReference(CrefSyntax? target, bool hasPath)
    {
        Target = target;
        HasPath = hasPath;
    }

    public CrefSyntax? Target { get; }

    public bool HasPath { get; }
}