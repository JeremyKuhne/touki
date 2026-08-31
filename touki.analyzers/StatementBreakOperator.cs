// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Describes a governed operator and the syntax tokens surrounding it.
/// </summary>
internal readonly struct StatementBreakOperator(
    SyntaxNode node,
    SyntaxToken previousToken,
    TextSpan operatorSpan,
    string operatorText,
    SyntaxToken nextToken,
    bool spaceAfter,
    bool trailingPlacement)
{
    public SyntaxNode Node { get; } = node;

    public SyntaxToken PreviousToken { get; } = previousToken;

    public TextSpan OperatorSpan { get; } = operatorSpan;

    public string OperatorText { get; } = operatorText;

    public SyntaxToken NextToken { get; } = nextToken;

    public bool SpaceAfter { get; } = spaceAfter;

    public bool TrailingPlacement { get; } = trailingPlacement;
}