// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public sealed partial class AddLiteralArgumentNameCodeFixProvider
{
    private readonly struct ArgumentNameCandidate(
        SyntaxNode argument,
        TextChange change,
        string escapedParameterName)
    {
        public SyntaxNode Argument { get; } = argument;

        public TextChange Change { get; } = change;

        public string EscapedParameterName { get; } = escapedParameterName;
    }
}