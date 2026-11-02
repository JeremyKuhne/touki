// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

internal static class ParameterListFormatting
{
    public const string MaxLineLengthOption = "dotnet_code_quality.TOUKI0043.max_line_length";
    public const int DefaultMaxLineLength = 140;

    public static bool IsList(SyntaxNode node) => node.Kind() is
        SyntaxKind.ParameterList
        or SyntaxKind.BracketedParameterList
        or SyntaxKind.ArgumentList
        or SyntaxKind.BracketedArgumentList
        or SyntaxKind.AttributeArgumentList;

    public static int GetMaxLineLength(AnalyzerConfigOptions options) =>
        options.TryGetPositiveInteger(MaxLineLengthOption, out int limit)
            || options.TryGetPositiveInteger("max_line_length", out limit)
                ? limit
                : DefaultMaxLineLength;

    public static bool IsNameofCandidate(SyntaxNode list) => list.Parent is InvocationExpressionSyntax
    {
        Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
    };

    public static bool IsNameofOperator(SyntaxNode list, SemanticModel model, CancellationToken cancellationToken) =>
        list.Parent is InvocationExpressionSyntax invocation
            && model.GetOperation(invocation, cancellationToken) is INameOfOperation;

    public static bool IsViolation(
        SyntaxNode list,
        SourceText source,
        int maxLineLength,
        string indentationUnit,
        CancellationToken cancellationToken)
    {
        ChildSyntaxList children = list.ChildNodesAndTokens();
        if (children.Count < 3 || list.ContainsDiagnostics)
        {
            return false;
        }

        SyntaxToken open = children[0].AsToken();
        SyntaxToken close = children[children.Count - 1].AsToken();
        if (open.IsMissing || close.IsMissing)
        {
            return false;
        }

        TextLine openLine = source.Lines.GetLineFromPosition(open.SpanStart);
        bool wrapped = false;
        int previousEnd = open.Span.End;
        foreach (SyntaxNodeOrToken child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!child.IsNode)
            {
                continue;
            }

            wrapped |= DifferentLines(source, previousEnd, child.SpanStart);
            previousEnd = child.Span.End;
        }

        wrapped |= DifferentLines(source, previousEnd, close.SpanStart);
        if (!wrapped)
        {
            return !DifferentLines(source, open.SpanStart, close.SpanStart)
                && openLine.Span.Length > maxLineLength;
        }

        int indentationEnd = openLine.Start;
        while (indentationEnd < open.SpanStart && source[indentationEnd] is ' ' or '\t')
        {
            indentationEnd++;
        }

        int baseLength = indentationEnd - openLine.Start;
        previousEnd = open.Span.End;
        foreach (SyntaxNodeOrToken child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!child.IsNode)
            {
                continue;
            }

            TextLine itemLine = source.Lines.GetLineFromPosition(child.SpanStart);
            if (!DifferentLines(source, previousEnd, child.SpanStart)
                || child.SpanStart - itemLine.Start != baseLength + indentationUnit.Length)
            {
                return true;
            }

            for (int index = 0; index < baseLength; index++)
            {
                if (source[itemLine.Start + index] != source[openLine.Start + index])
                {
                    return true;
                }
            }

            for (int index = 0; index < indentationUnit.Length; index++)
            {
                if (source[itemLine.Start + baseLength + index] != indentationUnit[index])
                {
                    return true;
                }
            }

            previousEnd = child.Span.End;
        }

        return DifferentLines(source, previousEnd, close.SpanStart);
    }

    private static bool DifferentLines(SourceText source, int first, int second) =>
        source.Lines.GetLineFromPosition(first).LineNumber != source.Lines.GetLineFromPosition(second).LineNumber;
}
