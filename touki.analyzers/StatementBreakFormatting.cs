// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

internal static partial class StatementBreakFormatting
{
    private const int MaximumAncestorDepth = 256;

    public static bool TryGetOperator(SyntaxNode node, int index, out StatementBreakOperator result)
    {
        result = default;
        if (index > 1)
        {
            return false;
        }

        switch (node)
        {
            case BinaryExpressionSyntax expression when index == 0 && expression.IsKind(SyntaxKind.IsExpression):
                result = Trailing(
                    expression,
                    expression.Left.GetLastToken(),
                    expression.OperatorToken,
                    expression.Right.GetFirstToken());
                return true;
            case BinaryExpressionSyntax expression when index == 0:
                result = Leading(
                    expression,
                    expression.Left.GetLastToken(),
                    expression.OperatorToken,
                    expression.Right.GetFirstToken(),
                    spaceAfter: true);
                return true;
            case AssignmentExpressionSyntax expression when index == 0:
                result = Trailing(
                    expression,
                    expression.Left.GetLastToken(),
                    expression.OperatorToken,
                    expression.Right.GetFirstToken());
                return true;
            case ConditionalExpressionSyntax expression:
                result = index == 0
                    ? Leading(
                        expression,
                        expression.Condition.GetLastToken(),
                        expression.QuestionToken,
                        expression.WhenTrue.GetFirstToken(),
                        spaceAfter: true)
                    : Leading(
                        expression,
                        expression.WhenTrue.GetLastToken(),
                        expression.ColonToken,
                        expression.WhenFalse.GetFirstToken(),
                        spaceAfter: true);
                return true;
            case MemberAccessExpressionSyntax expression when index == 0:
                result = Leading(
                    expression,
                    expression.Expression.GetLastToken(),
                    expression.OperatorToken,
                    expression.Name.GetFirstToken(),
                    spaceAfter: false);
                return true;
            case ConditionalAccessExpressionSyntax expression when index == 0:
                return TryGetConditionalAccessOperator(expression, out result);
            case RangeExpressionSyntax { LeftOperand: not null, RightOperand: not null } expression when index == 0:
                result = Leading(
                    expression,
                    expression.LeftOperand.GetLastToken(),
                    expression.OperatorToken,
                    expression.RightOperand.GetFirstToken(),
                    spaceAfter: false);
                return true;
            case IsPatternExpressionSyntax expression when index == 0:
                result = Trailing(
                    expression,
                    expression.Expression.GetLastToken(),
                    expression.IsKeyword,
                    expression.Pattern.GetFirstToken());
                return true;
            case BinaryPatternSyntax pattern when index == 0:
                result = Leading(
                    pattern,
                    pattern.Left.GetLastToken(),
                    pattern.OperatorToken,
                    pattern.Right.GetFirstToken(),
                    spaceAfter: true);
                return true;
            case RelationalPatternSyntax pattern when index == 0:
                result = Leading(
                    pattern,
                    pattern.OperatorToken.GetPreviousToken(),
                    pattern.OperatorToken,
                    pattern.Expression.GetFirstToken(),
                    spaceAfter: true);
                return true;
            case EqualsValueClauseSyntax clause when index == 0:
                result = Trailing(
                    clause,
                    clause.EqualsToken.GetPreviousToken(),
                    clause.EqualsToken,
                    clause.Value.GetFirstToken());
                return true;
            case LetClauseSyntax clause when index == 0:
                result = Trailing(
                    clause,
                    clause.Identifier,
                    clause.EqualsToken,
                    clause.Expression.GetFirstToken());
                return true;
            case NameEqualsSyntax clause when index == 0:
                SyntaxToken nextToken = clause.EqualsToken.GetNextToken();
                if (clause.Parent is null || !clause.Parent.Span.Contains(nextToken.Span))
                {
                    return false;
                }

                result = Trailing(
                    clause,
                    clause.Name.GetLastToken(),
                    clause.EqualsToken,
                    nextToken);
                return true;
            case ArrowExpressionClauseSyntax clause when index == 0:
                result = Trailing(
                    clause,
                    clause.ArrowToken.GetPreviousToken(),
                    clause.ArrowToken,
                    clause.Expression.GetFirstToken());
                return true;
            case LambdaExpressionSyntax expression when index == 0:
                result = Trailing(
                    expression,
                    expression.ArrowToken.GetPreviousToken(),
                    expression.ArrowToken,
                    expression.Body.GetFirstToken());
                return true;
            case SwitchExpressionArmSyntax arm when index == 0:
                result = Trailing(
                    arm,
                    arm.EqualsGreaterThanToken.GetPreviousToken(),
                    arm.EqualsGreaterThanToken,
                    arm.Expression.GetFirstToken());
                return true;
            default:
                return false;
        }
    }

    public static bool TryFindOperator(
        SyntaxNode root,
        TextSpan diagnosticSpan,
        out StatementBreakOperator result)
    {
        result = default;
        if (diagnosticSpan.IsEmpty || diagnosticSpan.End > root.FullSpan.End)
        {
            return false;
        }

        SyntaxToken token = root.FindToken(diagnosticSpan.Start);
        SyntaxNode? node = token.Parent;
        for (int depth = 0; node is not null && depth < 8; depth++, node = node.Parent)
        {
            for (int index = 0; index < 2; index++)
            {
                if (TryGetOperator(node, index, out StatementBreakOperator candidate)
                    && candidate.OperatorSpan == diagnosticSpan)
                {
                    result = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    public static bool SpansMultipleLines(
        StatementBreakOperator operation,
        SourceText source,
        CancellationToken cancellationToken)
    {
        if (operation.OperatorSpan.IsEmpty
            || operation.PreviousToken.RawKind == 0
            || operation.NextToken.RawKind == 0)
        {
            return false;
        }

        return operation.Node switch
        {
            BinaryExpressionSyntax expression =>
                TryGetPrecedenceGroupRoot(expression, cancellationToken, out BinaryExpressionSyntax groupRoot)
                && TryCollectPrecedenceGroup(
                    groupRoot,
                    source,
                    cancellationToken,
                    out _,
                    out bool hasBreak)
                && hasBreak,
            BinaryPatternSyntax pattern =>
                TryGetPrecedenceGroupRoot(pattern, cancellationToken, out BinaryPatternSyntax groupRoot)
                && TryCollectPrecedenceGroup(
                    groupRoot,
                    source,
                    cancellationToken,
                    out _,
                    out bool hasBreak)
                && hasBreak,
            _ => DirectlySpansMultipleLines(operation, source)
        };
    }

    public static bool IsPrecedenceGroupRoot(BinaryExpressionSyntax expression) =>
        expression.Parent is not BinaryExpressionSyntax parent || !HaveSamePrecedence(expression, parent);

    public static bool IsPrecedenceGroupRoot(BinaryPatternSyntax pattern) =>
        pattern.Parent is not BinaryPatternSyntax parent || !HaveSamePrecedence(pattern, parent);

    public static bool TryCollectPrecedenceGroup(
        BinaryExpressionSyntax groupRoot,
        SourceText source,
        CancellationToken cancellationToken,
        out List<StatementBreakOperator> operations,
        out bool hasBreak)
    {
        operations = [];
        hasBreak = false;
        OperatorPrecedence precedence = GetPrecedence(groupRoot);
        if (precedence == OperatorPrecedence.None || !IsPrecedenceGroupRoot(groupRoot))
        {
            return false;
        }

        List<BinaryExpressionSyntax> pending = [groupRoot];
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operations.Count == MaximumAncestorDepth)
            {
                operations = [];
                hasBreak = false;
                return false;
            }

            int lastIndex = pending.Count - 1;
            BinaryExpressionSyntax expression = pending[lastIndex];
            pending.RemoveAt(lastIndex);
            if (!TryGetOperator(expression, index: 0, out StatementBreakOperator operation))
            {
                operations = [];
                hasBreak = false;
                return false;
            }

            operations.Add(operation);
            hasBreak |= DirectlySpansMultipleLines(operation, source);

            if (expression.Right is BinaryExpressionSyntax right && GetPrecedence(right) == precedence)
            {
                pending.Add(right);
            }

            if (expression.Left is BinaryExpressionSyntax left && GetPrecedence(left) == precedence)
            {
                pending.Add(left);
            }
        }

        return true;
    }

    public static bool TryCollectPrecedenceGroup(
        BinaryPatternSyntax groupRoot,
        SourceText source,
        CancellationToken cancellationToken,
        out List<StatementBreakOperator> operations,
        out bool hasBreak)
    {
        operations = [];
        hasBreak = false;
        OperatorPrecedence precedence = GetPrecedence(groupRoot);
        if (precedence == OperatorPrecedence.None || !IsPrecedenceGroupRoot(groupRoot))
        {
            return false;
        }

        List<BinaryPatternSyntax> pending = [groupRoot];
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operations.Count == MaximumAncestorDepth)
            {
                operations = [];
                hasBreak = false;
                return false;
            }

            int lastIndex = pending.Count - 1;
            BinaryPatternSyntax pattern = pending[lastIndex];
            pending.RemoveAt(lastIndex);
            if (!TryGetOperator(pattern, index: 0, out StatementBreakOperator operation))
            {
                operations = [];
                hasBreak = false;
                return false;
            }

            operations.Add(operation);
            hasBreak |= DirectlySpansMultipleLines(operation, source);

            if (pattern.Right is BinaryPatternSyntax right && GetPrecedence(right) == precedence)
            {
                pending.Add(right);
            }

            if (pattern.Left is BinaryPatternSyntax left && GetPrecedence(left) == precedence)
            {
                pending.Add(left);
            }
        }

        return true;
    }

    private static bool DirectlySpansMultipleLines(StatementBreakOperator operation, SourceText source)
    {
        int previousLine = GetTokenEndLine(source, operation.PreviousToken).LineNumber;
        int operatorLine = source.Lines.GetLineFromPosition(operation.OperatorSpan.Start).LineNumber;
        int nextLine = source.Lines.GetLineFromPosition(operation.NextToken.SpanStart).LineNumber;
        return previousLine != operatorLine || operatorLine != nextLine;
    }

    private static bool TryGetPrecedenceGroupRoot(
        BinaryExpressionSyntax expression,
        CancellationToken cancellationToken,
        out BinaryExpressionSyntax groupRoot)
    {
        groupRoot = expression;
        for (int visits = 0;
            groupRoot.Parent is BinaryExpressionSyntax parent && HaveSamePrecedence(groupRoot, parent);
            visits++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (visits == MaximumAncestorDepth)
            {
                return false;
            }

            groupRoot = parent;
        }

        return true;
    }

    private static bool TryGetPrecedenceGroupRoot(
        BinaryPatternSyntax pattern,
        CancellationToken cancellationToken,
        out BinaryPatternSyntax groupRoot)
    {
        groupRoot = pattern;
        for (int visits = 0;
            groupRoot.Parent is BinaryPatternSyntax parent && HaveSamePrecedence(groupRoot, parent);
            visits++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (visits == MaximumAncestorDepth)
            {
                return false;
            }

            groupRoot = parent;
        }

        return true;
    }

    public static bool WouldCreateNestedTrailingOperator(
        StatementBreakOperator operation,
        SourceText source,
        StatementBreakChangeKind changeKind)
    {
        if (changeKind != StatementBreakChangeKind.LeadingOperator)
        {
            return false;
        }

        SyntaxNode? node = operation.NextToken.Parent;
        for (int depth = 0; node is not null && depth < 4; depth++, node = node.Parent)
        {
            for (int index = 0; index < 2; index++)
            {
                if (!TryGetOperator(node, index, out StatementBreakOperator nested)
                    || nested.TrailingPlacement
                    || nested.OperatorSpan.Start != operation.NextToken.SpanStart
                    || nested.PreviousToken.Span != operation.OperatorSpan)
                {
                    continue;
                }

                int operatorEndLine = source.Lines.GetLineFromPosition(nested.OperatorSpan.End - 1).LineNumber;
                int operandLine = source.Lines.GetLineFromPosition(nested.NextToken.SpanStart).LineNumber;
                return operatorEndLine != operandLine;
            }
        }

        return false;
    }

    public static bool TryGetChange(
        StatementBreakOperator operation,
        SourceText source,
        string indentationUnit,
        CancellationToken cancellationToken,
        out StatementBreakChangeKind changeKind,
        out TextSpan replacementSpan,
        out StatementBreakIndentation indentation) =>
        TryGetChange(
            operation,
            source,
            indentationUnit,
            precedenceGroupHasBreak: false,
            cancellationToken,
            out changeKind,
            out replacementSpan,
            out indentation);

    public static bool TryGetPrecedenceGroupChange(
        StatementBreakOperator operation,
        SourceText source,
        string indentationUnit,
        CancellationToken cancellationToken,
        out StatementBreakChangeKind changeKind,
        out TextSpan replacementSpan,
        out StatementBreakIndentation indentation) =>
        TryGetChange(
            operation,
            source,
            indentationUnit,
            precedenceGroupHasBreak: true,
            cancellationToken,
            out changeKind,
            out replacementSpan,
            out indentation);

    private static bool TryGetChange(
        StatementBreakOperator operation,
        SourceText source,
        string indentationUnit,
        bool precedenceGroupHasBreak,
        CancellationToken cancellationToken,
        out StatementBreakChangeKind changeKind,
        out TextSpan replacementSpan,
        out StatementBreakIndentation indentation)
    {
        changeKind = default;
        replacementSpan = default;
        indentation = default;
        if (operation.OperatorSpan.IsEmpty
            || operation.OperatorText.Length == 0
            || operation.PreviousToken.RawKind == 0
            || operation.PreviousToken.IsMissing
            || operation.NextToken.RawKind == 0
            || operation.NextToken.IsMissing
            || !IsWithinAncestorBudget(operation.Node, cancellationToken)
            || !HasValidInternalOperatorTrivia(operation.Node, source, cancellationToken))
        {
            return false;
        }

        TextLine previousLine = GetTokenEndLine(source, operation.PreviousToken);
        TextLine operatorLine = source.Lines.GetLineFromPosition(operation.OperatorSpan.Start);
        TextLine operatorEndLine = source.Lines.GetLineFromPosition(operation.OperatorSpan.End - 1);
        TextLine nextLine = source.Lines.GetLineFromPosition(operation.NextToken.SpanStart);
        bool operatorAndOperandsShareLine = previousLine.LineNumber == operatorLine.LineNumber
            && operatorLine.LineNumber == nextLine.LineNumber;
        if (operatorAndOperandsShareLine
            && !precedenceGroupHasBreak
            && !SpansMultipleLines(operation, source, cancellationToken))
        {
            return false;
        }

        if (!TryGetExpectedIndentation(
            operation.Node,
            source,
            indentationUnit,
            cancellationToken,
            out indentation))
        {
            return false;
        }

        if (operation.TrailingPlacement)
        {
            if (operatorAndOperandsShareLine)
            {
                if (!IsWhitespace(
                    source,
                    operation.PreviousToken.Span.End,
                    operation.OperatorSpan.Start,
                    cancellationToken)
                    || !IsWhitespace(
                        source,
                        operation.OperatorSpan.End,
                        operation.NextToken.SpanStart,
                        cancellationToken))
                {
                    return false;
                }

                changeKind = StatementBreakChangeKind.BreakAfterOperator;
                replacementSpan = TextSpan.FromBounds(
                    operation.PreviousToken.Span.End,
                    operation.NextToken.SpanStart);
                return IsChangeAllowed(operation, source, changeKind, replacementSpan, indentation);
            }

            if (previousLine.LineNumber != operatorLine.LineNumber)
            {
                if (!IsWhitespace(
                    source,
                    operation.PreviousToken.Span.End,
                    operation.OperatorSpan.Start,
                    cancellationToken)
                    || !IsWhitespace(
                        source,
                        operation.OperatorSpan.End,
                        operation.NextToken.SpanStart,
                        cancellationToken))
                {
                    return false;
                }

                changeKind = StatementBreakChangeKind.TrailingOperator;
                replacementSpan = TextSpan.FromBounds(
                    operation.PreviousToken.Span.End,
                    operation.NextToken.SpanStart);
                return IsChangeAllowed(operation, source, changeKind, replacementSpan, indentation);
            }

            if (operatorLine.LineNumber == nextLine.LineNumber)
            {
                return false;
            }

            replacementSpan = TextSpan.FromBounds(nextLine.Start, operation.NextToken.SpanStart);
            if (!IsWhitespace(source, replacementSpan.Start, replacementSpan.End, cancellationToken)
                || indentation.Matches(source, replacementSpan))
            {
                return false;
            }

            changeKind = StatementBreakChangeKind.Indentation;
            return IsChangeAllowed(operation, source, changeKind, replacementSpan, indentation);
        }

        if (previousLine.LineNumber == operatorLine.LineNumber
            || operatorLine.LineNumber != operatorEndLine.LineNumber)
        {
            if (!IsWhitespace(
                source,
                operation.PreviousToken.Span.End,
                operation.OperatorSpan.Start,
                cancellationToken)
                || !IsWhitespace(
                    source,
                    operation.OperatorSpan.End,
                    operation.NextToken.SpanStart,
                    cancellationToken))
            {
                return false;
            }

            changeKind = operatorAndOperandsShareLine
                ? StatementBreakChangeKind.BreakBeforeOperator
                : StatementBreakChangeKind.LeadingOperator;
            replacementSpan = TextSpan.FromBounds(
                operation.PreviousToken.Span.End,
                operation.NextToken.SpanStart);
            return IsChangeAllowed(operation, source, changeKind, replacementSpan, indentation);
        }

        replacementSpan = TextSpan.FromBounds(operatorLine.Start, operation.OperatorSpan.Start);
        if (!IsWhitespace(source, replacementSpan.Start, replacementSpan.End, cancellationToken)
            || indentation.Matches(source, replacementSpan))
        {
            return false;
        }

        changeKind = StatementBreakChangeKind.Indentation;
        return IsChangeAllowed(operation, source, changeKind, replacementSpan, indentation);
    }

    private static bool IsChangeAllowed(
        StatementBreakOperator operation,
        SourceText source,
        StatementBreakChangeKind changeKind,
        TextSpan replacementSpan,
        StatementBreakIndentation indentation) =>
        StatementBreakDiagnosticData.IsChangeSizeAllowed(
            source,
            replacementSpan,
            changeKind,
            operation.OperatorText,
            operation.SpaceAfter,
            indentation);

    public static bool TryGetDependentIndentationChanges(
        StatementBreakOperator operation,
        SourceText source,
        StatementBreakChangeKind changeKind,
        StatementBreakIndentation indentation,
        int maximumReplacementCharacters,
        CancellationToken cancellationToken,
        out List<TextChange> changes)
    {
        changes = [];
        if (!operation.TrailingPlacement)
        {
            return TryGetLeadingInvocationIndentationChanges(
                operation,
                source,
                changeKind,
                indentation,
                maximumReplacementCharacters,
                cancellationToken,
                out changes);
        }

        bool blockLikeContinuation = HasBlockLikeContinuation(
            operation.Node,
            source,
            cancellationToken);
        if (changeKind is not (StatementBreakChangeKind.Indentation
                or StatementBreakChangeKind.TrailingOperator
                or StatementBreakChangeKind.BreakAfterOperator)
            || changeKind == StatementBreakChangeKind.Indentation
                && operation.Node is not ArrowExpressionClauseSyntax
                    and not LambdaExpressionSyntax
                    and not SwitchExpressionArmSyntax
                && !blockLikeContinuation
            || !TryGetContinuationNode(operation, out SyntaxNode continuation))
        {
            return true;
        }

        TextLine continuationLine = source.Lines.GetLineFromPosition(operation.NextToken.SpanStart);
        TextLine operatorLine = source.Lines.GetLineFromPosition(operation.OperatorSpan.Start);
        int lastLineNumber = source.Lines.GetLineFromPosition(continuation.Span.End - 1).LineNumber;
        int physicalRangeStart = operatorLine.LineNumber < lastLineNumber
            ? operatorLine.EndIncludingLineBreak
            : continuation.Span.End;
        if (maximumReplacementCharacters < 0
            || continuation.Span.End - physicalRangeStart > StatementBreakDiagnosticData.MaximumChangeCharacters
            || continuation.Span.Length > StatementBreakDiagnosticData.MaximumChangeCharacters
            || ContainsMultilineString(continuation, source, cancellationToken)
            || !indentation.TryCreateText(source, out string targetIndentation))
        {
            return false;
        }

        if (!TryGetIndentationEnd(source, continuationLine, cancellationToken, out int oldIndentationEnd))
        {
            return false;
        }

        string oldIndentation = source.ToString(
            TextSpan.FromBounds(continuationLine.Start, oldIndentationEnd));
        if (changeKind == StatementBreakChangeKind.Indentation
            && blockLikeContinuation
            && !ClosingDelimiterMatchesIndentation(
                continuation,
                oldIndentation,
                source,
                cancellationToken))
        {
            return true;
        }

        if (string.Equals(oldIndentation, targetIndentation, StringComparison.Ordinal)
            || continuation.Span.IsEmpty)
        {
            return true;
        }

        SyntaxNode root = operation.Node.SyntaxTree.GetRoot(cancellationToken);
        int replacementCharacters = 0;
        for (int lineNumber = operatorLine.LineNumber + 1; lineNumber <= lastLineNumber; lineNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (lineNumber == continuationLine.LineNumber)
            {
                continue;
            }

            TextLine line = source.Lines[lineNumber];
            if (!TryGetIndentationEnd(source, line, cancellationToken, out int indentationEnd)
                || indentationEnd == line.End)
            {
                continue;
            }

            SyntaxToken token = root.FindToken(indentationEnd, findInsideTrivia: true);
            bool isShiftableToken = token.SpanStart == indentationEnd
                && continuation.Span.Contains(token.Span)
                && !TryGetFormattingNode(token, out _);
            SyntaxTrivia trivia = root.FindTrivia(indentationEnd);
            bool isShiftableComment = trivia.Span.Contains(indentationEnd)
                && IsShiftableComment(trivia)
                && continuation.FullSpan.Contains(trivia.Span);
            if (!isShiftableToken && !isShiftableComment)
            {
                continue;
            }

            TextSpan indentationSpan = TextSpan.FromBounds(line.Start, indentationEnd);
            string currentIndentation = source.ToString(indentationSpan);
            if (!currentIndentation.StartsWith(oldIndentation, StringComparison.Ordinal))
            {
                continue;
            }

            int suffixLength = currentIndentation.Length - oldIndentation.Length;
            int replacementLength = targetIndentation.Length + suffixLength;
            if (replacementLength > StatementBreakDiagnosticData.MaximumChangeCharacters
                || replacementCharacters > maximumReplacementCharacters - replacementLength)
            {
                changes = [];
                return false;
            }

            if (currentIndentation.Length != replacementLength
                || !currentIndentation.StartsWith(targetIndentation, StringComparison.Ordinal))
            {
                string replacement = string.Concat(
                    targetIndentation,
                    currentIndentation.Substring(oldIndentation.Length));
                changes.Add(new(indentationSpan, replacement));
                replacementCharacters += replacementLength;
            }
        }

        return true;
    }

    private static bool TryGetLeadingInvocationIndentationChanges(
        StatementBreakOperator operation,
        SourceText source,
        StatementBreakChangeKind changeKind,
        StatementBreakIndentation indentation,
        int maximumReplacementCharacters,
        CancellationToken cancellationToken,
        out List<TextChange> changes)
    {
        changes = [];
        if (changeKind != StatementBreakChangeKind.Indentation
            || operation.Node is not BinaryExpressionSyntax binary
            || maximumReplacementCharacters < 0
            || !indentation.TryCreateText(source, out string operatorIndentation))
        {
            return true;
        }

        if (binary.Right.Span.Length > StatementBreakDiagnosticData.MaximumChangeCharacters)
        {
            return false;
        }

        TextLine operatorLine = source.Lines.GetLineFromPosition(operation.OperatorSpan.Start);
        InvocationExpressionSyntax? invocation = null;
        foreach (SyntaxNode candidate in binary.Right.DescendantNodesAndSelf())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate is InvocationExpressionSyntax currentInvocation)
            {
                ArgumentListSyntax arguments = currentInvocation.ArgumentList;
                TextLine openLine = source.Lines.GetLineFromPosition(arguments.OpenParenToken.SpanStart);
                TextLine closeLine = source.Lines.GetLineFromPosition(arguments.CloseParenToken.SpanStart);
                if (openLine.LineNumber == operatorLine.LineNumber
                    && closeLine.LineNumber > openLine.LineNumber)
                {
                    invocation = currentInvocation;
                    break;
                }
            }
        }

        if (invocation is null || invocation.ArgumentList.Arguments.Count == 0)
        {
            return true;
        }

        ArgumentListSyntax argumentList = invocation.ArgumentList;
        if (argumentList.Span.Length > StatementBreakDiagnosticData.MaximumChangeCharacters
            || ContainsMultilineString(argumentList, source, cancellationToken))
        {
            return false;
        }

        string targetIndentation = string.Concat(operatorIndentation, indentation.Unit);
        TextLine firstArgumentLine = source.Lines.GetLineFromPosition(
            argumentList.Arguments[0].GetFirstToken().SpanStart);
        if (!TryGetIndentationEnd(source, firstArgumentLine, cancellationToken, out int oldIndentationEnd))
        {
            return false;
        }

        string oldIndentation = source.ToString(
            TextSpan.FromBounds(firstArgumentLine.Start, oldIndentationEnd));
        if (string.Equals(oldIndentation, targetIndentation, StringComparison.Ordinal))
        {
            return true;
        }

        SyntaxNode root = operation.Node.SyntaxTree.GetRoot(cancellationToken);
        int firstLineNumber = source.Lines.GetLineFromPosition(argumentList.OpenParenToken.SpanStart).LineNumber + 1;
        int lastLineNumber = source.Lines.GetLineFromPosition(argumentList.CloseParenToken.SpanStart).LineNumber;
        int replacementCharacters = 0;
        for (int lineNumber = firstLineNumber; lineNumber <= lastLineNumber; lineNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TextLine line = source.Lines[lineNumber];
            if (!TryGetIndentationEnd(source, line, cancellationToken, out int indentationEnd)
                || indentationEnd == line.End)
            {
                continue;
            }

            SyntaxToken token = root.FindToken(indentationEnd, findInsideTrivia: true);
            bool isShiftableToken = token.SpanStart == indentationEnd
                && argumentList.Span.Contains(token.Span)
                && !TryGetFormattingNode(token, out _);
            SyntaxTrivia trivia = root.FindTrivia(indentationEnd);
            bool isShiftableComment = trivia.Span.Contains(indentationEnd)
                && IsShiftableComment(trivia)
                && argumentList.FullSpan.Contains(trivia.Span);
            if (!isShiftableToken && !isShiftableComment)
            {
                continue;
            }

            TextSpan indentationSpan = TextSpan.FromBounds(line.Start, indentationEnd);
            string currentIndentation = source.ToString(indentationSpan);
            if (!currentIndentation.StartsWith(oldIndentation, StringComparison.Ordinal))
            {
                continue;
            }

            int suffixLength = currentIndentation.Length - oldIndentation.Length;
            int replacementLength = targetIndentation.Length + suffixLength;
            if (replacementLength > StatementBreakDiagnosticData.MaximumChangeCharacters
                || replacementCharacters > maximumReplacementCharacters - replacementLength)
            {
                changes = [];
                return false;
            }

            string replacement = string.Concat(
                targetIndentation,
                currentIndentation.Substring(oldIndentation.Length));
            changes.Add(new(indentationSpan, replacement));
            replacementCharacters += replacementLength;
        }

        return true;
    }

    private static bool ClosingDelimiterMatchesIndentation(
        SyntaxNode continuation,
        string indentation,
        SourceText source,
        CancellationToken cancellationToken)
    {
        SyntaxToken closeToken = continuation switch
        {
            BlockSyntax block => block.CloseBraceToken,
            CollectionExpressionSyntax collection => collection.CloseBracketToken,
            InitializerExpressionSyntax initializer => initializer.CloseBraceToken,
            _ => default
        };
        if (closeToken.RawKind == 0 || closeToken.IsMissing)
        {
            return false;
        }

        TextLine closeLine = source.Lines.GetLineFromPosition(closeToken.SpanStart);
        return TryGetIndentationEnd(source, closeLine, cancellationToken, out int indentationEnd)
            && string.Equals(
                source.ToString(TextSpan.FromBounds(closeLine.Start, indentationEnd)),
                indentation,
                StringComparison.Ordinal);
    }

    private static bool IsShiftableComment(SyntaxTrivia trivia) => trivia.IsKind(
        SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

    private static bool ContainsMultilineString(
        SyntaxNode node,
        SourceText source,
        CancellationToken cancellationToken)
    {
        foreach (SyntaxNode candidate in node.DescendantNodesAndSelf())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate is not LiteralExpressionSyntax and not InterpolatedStringExpressionSyntax
                || candidate.Span.IsEmpty)
            {
                continue;
            }

            int startLine = source.Lines.GetLineFromPosition(candidate.Span.Start).LineNumber;
            int endLine = source.Lines.GetLineFromPosition(candidate.Span.End - 1).LineNumber;
            if (startLine != endLine)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetContinuationNode(
        StatementBreakOperator operation,
        out SyntaxNode continuation)
    {
        SyntaxNode? candidate = operation.Node switch
        {
            AssignmentExpressionSyntax expression => expression.Right,
            BinaryExpressionSyntax expression when expression.IsKind(SyntaxKind.IsExpression) => expression.Right,
            IsPatternExpressionSyntax expression => expression.Pattern,
            EqualsValueClauseSyntax clause => clause.Value,
            LetClauseSyntax clause => clause.Expression,
            NameEqualsSyntax clause => GetNameEqualsContinuation(clause),
            ArrowExpressionClauseSyntax clause => clause.Expression,
            LambdaExpressionSyntax expression => expression.Body,
            SwitchExpressionArmSyntax arm => arm.Expression,
            _ => null
        };

        continuation = candidate!;
        return candidate is not null;
    }

    private static SyntaxNode? GetNameEqualsContinuation(NameEqualsSyntax clause) => clause.Parent switch
    {
        AttributeArgumentSyntax attributeArgument => attributeArgument.Expression,
        AnonymousObjectMemberDeclaratorSyntax anonymousMember => anonymousMember.Expression,
        UsingDirectiveSyntax { Name: not null } usingDirective => usingDirective.Name,
        _ => null
    };

    private static StatementBreakOperator Leading(
        SyntaxNode node,
        SyntaxToken previousToken,
        SyntaxToken operatorToken,
        SyntaxToken nextToken,
        bool spaceAfter) =>
        new(
            node,
            previousToken,
            operatorToken.Span,
            operatorToken.Text,
            nextToken,
            spaceAfter,
            trailingPlacement: false);

    private static StatementBreakOperator Trailing(
        SyntaxNode node,
        SyntaxToken previousToken,
        SyntaxToken operatorToken,
        SyntaxToken nextToken) =>
        new(
            node,
            previousToken,
            operatorToken.Span,
            operatorToken.Text,
            nextToken,
            spaceAfter: false,
            trailingPlacement: true);

    private static bool TryGetConditionalAccessOperator(
        ConditionalAccessExpressionSyntax expression,
        out StatementBreakOperator result)
    {
        result = default;
        if (!TryGetConditionalAccessParts(
            expression,
            out SyntaxToken bindingToken,
            out SyntaxToken rightToken,
            out string operatorText))
        {
            return false;
        }

        result = new(
            expression,
            expression.Expression.GetLastToken(),
            TextSpan.FromBounds(expression.OperatorToken.SpanStart, bindingToken.Span.End),
            operatorText,
            rightToken,
            spaceAfter: false,
            trailingPlacement: false);
        return true;
    }

    private static bool TryGetExpectedIndentation(
        SyntaxNode node,
        SourceText source,
        string indentationUnit,
        CancellationToken cancellationToken,
        out StatementBreakIndentation expectedIndentation)
    {
        if (!TryGetFirstPrecedenceGroupNode(
            node,
            source,
            cancellationToken,
            out SyntaxNode firstGroupNode))
        {
            expectedIndentation = default;
            return false;
        }

        if (firstGroupNode != node)
        {
            return TryGetExpectedIndentation(
                firstGroupNode,
                source,
                indentationUnit,
                cancellationToken,
                out expectedIndentation);
        }

        if (TryGetPreviousPrimaryFormattingNode(
            node,
            cancellationToken,
            out SyntaxNode previousPrimaryNode))
        {
            return TryGetExpectedIndentation(
                previousPrimaryNode,
                source,
                indentationUnit,
                cancellationToken,
                out expectedIndentation);
        }

        SyntaxNode current = node;
        int indentationLevels = 1;
        int ancestorVisits = 0;
        SyntaxNode root = node.SyntaxTree.GetRoot(cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxToken anchor;
            SyntaxNode? anchorExpression = null;
            bool useAnchorEndLine = false;
            bool alignWithAnchor = false;
            if (TryGetTrailingOperatorAnchor(current, out anchor))
            {
                useAnchorEndLine = true;
                alignWithAnchor = HasBlockLikeContinuation(
                    current,
                    source,
                    cancellationToken);
            }
            else if (current is ConditionalExpressionSyntax conditional)
            {
                anchorExpression = conditional.Condition;
                anchor = conditional.Condition.GetLastToken();
                useAnchorEndLine = true;
            }
            else if (current is ConditionalAccessExpressionSyntax conditionalAccess
                && current == node)
            {
                anchorExpression = conditionalAccess.Expression;
                anchor = conditionalAccess.Expression.GetLastToken();
                useAnchorEndLine = true;
            }
            else if (current is MemberAccessExpressionSyntax memberAccess
                && current == node)
            {
                anchorExpression = memberAccess.Expression;
                anchor = memberAccess.Expression.GetLastToken();
                useAnchorEndLine = true;
            }
            else if (current is RangeExpressionSyntax { LeftOperand: not null } range
                && current == node)
            {
                anchorExpression = range.LeftOperand;
                anchor = range.LeftOperand.GetLastToken();
                useAnchorEndLine = true;
            }
            else if (current is BinaryExpressionSyntax binary
                && current == node)
            {
                anchorExpression = binary.Left;
                bool structuralStart = UsesStructuralStartAnchor(binary.Left, source);
                anchor = structuralStart ? binary.Left.GetFirstToken() : binary.Left.GetLastToken();
                useAnchorEndLine = !structuralStart;
            }
            else if (current is BinaryPatternSyntax pattern
                && current == node)
            {
                anchorExpression = pattern.Left;
                bool structuralStart = UsesStructuralStartAnchor(pattern.Left, source);
                anchor = structuralStart ? pattern.Left.GetFirstToken() : pattern.Left.GetLastToken();
                useAnchorEndLine = !structuralStart;
            }
            else if (current is ArrowExpressionClauseSyntax arrow)
            {
                anchor = arrow.ArrowToken.GetPreviousToken();
                useAnchorEndLine = true;
                alignWithAnchor = HasBlockLikeContinuation(
                    arrow,
                    source,
                    cancellationToken);
            }
            else if (current is LambdaExpressionSyntax lambda
                && HasBlockLikeContinuation(lambda, source, cancellationToken)
                && current == node)
            {
                anchor = lambda.GetFirstToken();
                alignWithAnchor = true;
            }
            else if (current is SwitchExpressionArmSyntax arm
                && HasBlockLikeContinuation(arm, source, cancellationToken)
                && current == node)
            {
                anchor = arm.GetFirstToken();
                alignWithAnchor = true;
            }
            else if (!TryGetIndentationAnchor(current, ref ancestorVisits, out anchor))
            {
                expectedIndentation = default;
                return false;
            }

            TextLine anchorLine = useAnchorEndLine
                ? GetTokenEndLine(source, anchor)
                : source.Lines.GetLineFromPosition(anchor.SpanStart);
            if (!TryGetIndentationEnd(source, anchorLine, cancellationToken, out int indentationEnd))
            {
                expectedIndentation = default;
                return false;
            }

            if (current == node
                && current is BinaryExpressionSyntax or BinaryPatternSyntax
                && root.FindToken(indentationEnd) is { } structuralLineStart
                && structuralLineStart.SpanStart == indentationEnd
                && !TryGetFormattingNode(structuralLineStart, out _)
                && !TryAddSameLineParenthesizedScopes(
                    current,
                    anchorLine,
                    source,
                    cancellationToken,
                    ref ancestorVisits,
                    ref indentationLevels))
            {
                expectedIndentation = default;
                return false;
            }

            if (current is LambdaExpressionSyntax lambdaExpression
                && TryGetLambdaInvocationTarget(lambdaExpression, out SyntaxNode invocationTarget)
                && root.FindToken(indentationEnd) is { } lineStartToken
                && lineStartToken.SpanStart == indentationEnd
                && IsInvocationTargetLineStart(lineStartToken, invocationTarget)
                && TryGetExpectedIndentation(
                    invocationTarget,
                    source,
                    indentationUnit,
                    cancellationToken,
                    out StatementBreakIndentation targetIndentation))
            {
                StatementBreakIndentation lambdaIndentation = new(
                    targetIndentation.BaseSpan,
                    targetIndentation.Unit,
                    targetIndentation.Levels + indentationLevels);
                if (!StatementBreakDiagnosticData.IsValidIndentation(lambdaIndentation))
                {
                    expectedIndentation = default;
                    return false;
                }

                expectedIndentation = lambdaIndentation;
                return true;
            }

            if (current == node
                && current is BinaryExpressionSyntax
                    or ConditionalExpressionSyntax
                    or MemberAccessExpressionSyntax
                    or ConditionalAccessExpressionSyntax
                    or RangeExpressionSyntax
                    or RelationalPatternSyntax
                && TryGetContainingChangedContinuationIndentation(
                    current,
                    source,
                    indentationUnit,
                    cancellationToken,
                    out SyntaxNode containingContinuation,
                    out StatementBreakIndentation continuationIndentation)
                && TryGetRelativeIndentationLevels(
                    containingContinuation.GetFirstToken(),
                    anchorLine,
                    source,
                    indentationUnit,
                    cancellationToken,
                    out int relativeLevels))
            {
                StatementBreakIndentation conditionalIndentation = new(
                    continuationIndentation.BaseSpan,
                    continuationIndentation.Unit,
                    continuationIndentation.Levels + relativeLevels + indentationLevels);
                if (!StatementBreakDiagnosticData.IsValidIndentation(conditionalIndentation))
                {
                    expectedIndentation = default;
                    return false;
                }

                expectedIndentation = conditionalIndentation;
                return true;
            }

            if (current is not LambdaExpressionSyntax
                && indentationEnd < anchorLine.End
                && root.FindToken(indentationEnd) is { } token
                && token.SpanStart == indentationEnd)
            {
                if (current is ConditionalExpressionSyntax
                    && anchorExpression is not null
                    && TryGetTrailingOperatorContinuation(
                        anchorExpression,
                        token,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _)
                    && TryGetExpectedIndentation(
                        anchorExpression,
                        source,
                        indentationUnit,
                        cancellationToken,
                        out StatementBreakIndentation conditionIndentation))
                {
                    StatementBreakIndentation conditionalIndentation = new(
                        conditionIndentation.BaseSpan,
                        conditionIndentation.Unit,
                        conditionIndentation.Levels + indentationLevels);
                    if (!StatementBreakDiagnosticData.IsValidIndentation(conditionalIndentation))
                    {
                        expectedIndentation = default;
                        return false;
                    }

                    expectedIndentation = conditionalIndentation;
                    return true;
                }

                if (anchorExpression is not null
                    && TryGetFormattingNode(token, out SyntaxNode nestedFormattingNode)
                    && nestedFormattingNode != current
                    && (anchorExpression.Span.Contains(nestedFormattingNode.Span)
                        || nestedFormattingNode.Span.Contains(current.Span)))
                {
                    if (!TryGetExpectedIndentation(
                        nestedFormattingNode,
                        source,
                        indentationUnit,
                        cancellationToken,
                        out StatementBreakIndentation nestedIndentation))
                    {
                        expectedIndentation = default;
                        return false;
                    }

                    bool precedencePeers = current is BinaryExpressionSyntax
                            && nestedFormattingNode is BinaryExpressionSyntax
                        || current is BinaryPatternSyntax
                            && nestedFormattingNode is BinaryPatternSyntax
                        || current is MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax
                            && nestedFormattingNode is MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax;
                    int additionalLevels = precedencePeers
                        && AreInSamePrecedenceGroup(
                            current,
                            nestedFormattingNode,
                            cancellationToken)
                            ? indentationLevels - 1
                            : indentationLevels;
                    StatementBreakIndentation normalizedIndentation = new(
                        nestedIndentation.BaseSpan,
                        nestedIndentation.Unit,
                        nestedIndentation.Levels + additionalLevels);
                    if (!StatementBreakDiagnosticData.IsValidIndentation(normalizedIndentation))
                    {
                        expectedIndentation = default;
                        return false;
                    }

                    expectedIndentation = normalizedIndentation;
                    return true;
                }

                if (current == node
                    && node is BinaryExpressionSyntax
                        or BinaryPatternSyntax
                        or MemberAccessExpressionSyntax
                        or ConditionalAccessExpressionSyntax
                        or RangeExpressionSyntax
                        or RelationalPatternSyntax)
                {
                    bool normalizedOperator = TryGetNormalizedFormattingNode(
                        token,
                        current,
                        source,
                        cancellationToken,
                        ref ancestorVisits,
                        out SyntaxNode operatorFormattingNode);
                    if (ancestorVisits > MaximumAncestorDepth)
                    {
                        expectedIndentation = default;
                        return false;
                    }

                    if (normalizedOperator
                        && operatorFormattingNode is ArrowExpressionClauseSyntax
                            or LambdaExpressionSyntax
                            or SwitchExpressionArmSyntax)
                    {
                        if (indentationLevels == MaximumAncestorDepth)
                        {
                            expectedIndentation = default;
                            return false;
                        }

                        current = operatorFormattingNode;
                        indentationLevels++;
                        continue;
                    }

                    StatementBreakIndentation binaryIndentation = new(
                        TextSpan.FromBounds(anchorLine.Start, indentationEnd),
                        indentationUnit,
                        indentationLevels);
                    if (!StatementBreakDiagnosticData.IsValidIndentation(binaryIndentation))
                    {
                        expectedIndentation = default;
                        return false;
                    }

                    expectedIndentation = binaryIndentation;
                    return true;
                }

                bool normalized = TryGetNormalizedFormattingNode(
                    token,
                    current,
                    source,
                    cancellationToken,
                    ref ancestorVisits,
                    out SyntaxNode formattingNode);
                if (ancestorVisits > MaximumAncestorDepth)
                {
                    expectedIndentation = default;
                    return false;
                }

                if (normalized)
                {
                    if (indentationLevels == MaximumAncestorDepth)
                    {
                        expectedIndentation = default;
                        return false;
                    }

                    current = formattingNode;
                    indentationLevels++;
                    continue;
                }
            }

            StatementBreakIndentation indentation = new(
                TextSpan.FromBounds(anchorLine.Start, indentationEnd),
                indentationUnit,
                alignWithAnchor ? indentationLevels - 1 : indentationLevels);
            if (!StatementBreakDiagnosticData.IsValidIndentation(indentation))
            {
                expectedIndentation = default;
                return false;
            }

            expectedIndentation = indentation;
            return true;
        }
    }

    private static bool UsesStructuralStartAnchor(SyntaxNode operand, SourceText source)
    {
        if (operand is not InvocationExpressionSyntax
            and not ParenthesizedExpressionSyntax
            and not ParenthesizedPatternSyntax)
        {
            return false;
        }

        int firstLine = source.Lines.GetLineFromPosition(operand.GetFirstToken().SpanStart).LineNumber;
        int lastLine = GetTokenEndLine(source, operand.GetLastToken()).LineNumber;
        return firstLine != lastLine;
    }

    private static bool TryAddSameLineParenthesizedScopes(
        SyntaxNode node,
        TextLine anchorLine,
        SourceText source,
        CancellationToken cancellationToken,
        ref int ancestorVisits,
        ref int indentationLevels)
    {
        SyntaxNode? candidate = node.Parent;
        while (candidate is ExpressionSyntax or PatternSyntax)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryVisitAncestor(ref ancestorVisits))
            {
                return false;
            }

            SyntaxToken openToken = candidate switch
            {
                ParenthesizedExpressionSyntax parenthesized => parenthesized.OpenParenToken,
                ParenthesizedPatternSyntax parenthesized => parenthesized.OpenParenToken,
                _ => default
            };
            if (openToken.RawKind != 0
                && source.Lines.GetLineFromPosition(openToken.SpanStart).LineNumber == anchorLine.LineNumber
                && !StartsInlineOperatorContinuation(openToken, source))
            {
                if (indentationLevels == MaximumAncestorDepth)
                {
                    return false;
                }

                indentationLevels++;
            }

            candidate = candidate.Parent;
        }

        return true;
    }

    private static bool StartsInlineOperatorContinuation(SyntaxToken openToken, SourceText source)
    {
        SyntaxToken previousToken = openToken.GetPreviousToken();
        if (previousToken.RawKind == 0
            || GetTokenEndLine(source, previousToken).LineNumber
                != source.Lines.GetLineFromPosition(openToken.SpanStart).LineNumber)
        {
            return false;
        }

        return TryGetFormattingNode(previousToken, out _)
            || previousToken.Parent is PrefixUnaryExpressionSyntax prefix
                && prefix.OperatorToken == previousToken
            || previousToken.Parent is UnaryPatternSyntax unaryPattern
                && unaryPattern.OperatorToken == previousToken;
    }

    private static bool HasBlockLikeContinuation(
        SyntaxNode node,
        SourceText source,
        CancellationToken cancellationToken)
    {
        SyntaxNode? continuation = node switch
        {
            AssignmentExpressionSyntax expression => expression.Right,
            EqualsValueClauseSyntax clause => clause.Value,
            LetClauseSyntax clause => clause.Expression,
            NameEqualsSyntax clause => GetNameEqualsContinuation(clause),
            ArrowExpressionClauseSyntax clause => clause.Expression,
            LambdaExpressionSyntax expression => expression.Body,
            SwitchExpressionArmSyntax arm => arm.Expression,
            _ => null
        };

        return continuation switch
        {
            BlockSyntax => true,
            CollectionExpressionSyntax collection => HasBlockLikeDelimiters(
                collection.OpenBracketToken,
                collection.CloseBracketToken,
                source,
                cancellationToken),
            InitializerExpressionSyntax initializer => HasBlockLikeDelimiters(
                initializer.OpenBraceToken,
                initializer.CloseBraceToken,
                source,
                cancellationToken),
            _ => false
        };
    }

    private static bool HasBlockLikeDelimiters(
        SyntaxToken openToken,
        SyntaxToken closeToken,
        SourceText source,
        CancellationToken cancellationToken)
    {
        if (openToken.IsMissing || closeToken.IsMissing)
        {
            return false;
        }

        TextLine openLine = source.Lines.GetLineFromPosition(openToken.SpanStart);
        TextLine closeLine = source.Lines.GetLineFromPosition(closeToken.SpanStart);
        return openLine.LineNumber != closeLine.LineNumber
            && IsWhitespace(source, openToken.Span.End, openLine.End, cancellationToken)
            && IsWhitespace(source, closeLine.Start, closeToken.SpanStart, cancellationToken);
    }

    private static bool TryGetFirstPrecedenceGroupNode(
        SyntaxNode node,
        SourceText source,
        CancellationToken cancellationToken,
        out SyntaxNode firstGroupNode)
    {
        List<StatementBreakOperator> operations = [];
        bool collected = node switch
        {
            BinaryExpressionSyntax expression =>
                TryGetPrecedenceGroupRoot(expression, cancellationToken, out BinaryExpressionSyntax groupRoot)
                && TryCollectPrecedenceGroup(
                    groupRoot,
                    source,
                    cancellationToken,
                    out operations,
                    out _),
            BinaryPatternSyntax pattern =>
                TryGetPrecedenceGroupRoot(pattern, cancellationToken, out BinaryPatternSyntax groupRoot)
                && TryCollectPrecedenceGroup(
                    groupRoot,
                    source,
                    cancellationToken,
                    out operations,
                    out _),
            _ => false
        };

        if (!collected)
        {
            firstGroupNode = node;
            return node is not BinaryExpressionSyntax and not BinaryPatternSyntax;
        }

        StatementBreakOperator first = operations[0];
        for (int index = 1; index < operations.Count; index++)
        {
            if (operations[index].OperatorSpan.Start < first.OperatorSpan.Start)
            {
                first = operations[index];
            }
        }

        firstGroupNode = first.Node;
        return true;
    }

    private static bool TryGetTrailingOperatorAnchor(SyntaxNode node, out SyntaxToken anchor)
    {
        anchor = node switch
        {
            AssignmentExpressionSyntax expression => expression.Left.GetLastToken(),
            BinaryExpressionSyntax expression when expression.IsKind(SyntaxKind.IsExpression) =>
                expression.Left.GetLastToken(),
            IsPatternExpressionSyntax expression => expression.Expression.GetLastToken(),
            EqualsValueClauseSyntax clause => clause.EqualsToken.GetPreviousToken(),
            LetClauseSyntax clause => clause.Identifier,
            NameEqualsSyntax clause => clause.Name.GetLastToken(),
            _ => default
        };

        return anchor.RawKind != 0 && !anchor.IsMissing;
    }

    private static bool HaveSamePrecedence(SyntaxNode first, SyntaxNode second) =>
        GetPrecedence(first) is OperatorPrecedence precedence
        && precedence != OperatorPrecedence.None
        && precedence == GetPrecedence(second);

    private static bool AreInSamePrecedenceGroup(
        SyntaxNode first,
        SyntaxNode second,
        CancellationToken cancellationToken)
    {
        if (first is BinaryExpressionSyntax firstExpression
            && second is BinaryExpressionSyntax secondExpression)
        {
            return TryGetPrecedenceGroupRoot(
                firstExpression,
                cancellationToken,
                out BinaryExpressionSyntax firstRoot)
                && TryGetPrecedenceGroupRoot(
                    secondExpression,
                    cancellationToken,
                    out BinaryExpressionSyntax secondRoot)
                && firstRoot.Span == secondRoot.Span;
        }

        if (first is BinaryPatternSyntax firstPattern
            && second is BinaryPatternSyntax secondPattern)
        {
            return TryGetPrecedenceGroupRoot(
                firstPattern,
                cancellationToken,
                out BinaryPatternSyntax firstRoot)
                && TryGetPrecedenceGroupRoot(
                    secondPattern,
                    cancellationToken,
                    out BinaryPatternSyntax secondRoot)
                && firstRoot.Span == secondRoot.Span;
        }

        return HaveSamePrecedence(first, second);
    }

    private static bool TryGetPreviousPrimaryFormattingNode(
        SyntaxNode node,
        CancellationToken cancellationToken,
        out SyntaxNode previousPrimaryNode)
    {
        SyntaxNode? candidate = node switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
            ConditionalAccessExpressionSyntax conditionalAccess => conditionalAccess.Expression,
            _ => null
        };

        for (int visits = 0; candidate is not null && visits < MaximumAncestorDepth; visits++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (candidate)
            {
                case MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax:
                    previousPrimaryNode = candidate;
                    return true;
                case InvocationExpressionSyntax invocation:
                    candidate = invocation.Expression;
                    break;
                case ElementAccessExpressionSyntax elementAccess:
                    candidate = elementAccess.Expression;
                    break;
                case PostfixUnaryExpressionSyntax postfix:
                    candidate = postfix.Operand;
                    break;
                case MemberBindingExpressionSyntax memberBinding
                    when memberBinding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>() is { } owner:
                    previousPrimaryNode = owner;
                    return true;
                case ElementBindingExpressionSyntax elementBinding
                    when elementBinding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>() is { } owner:
                    previousPrimaryNode = owner;
                    return true;
                default:
                    previousPrimaryNode = null!;
                    return false;
            }
        }

        previousPrimaryNode = null!;
        return false;
    }

    private static bool TryGetLambdaInvocationTarget(
        LambdaExpressionSyntax lambda,
        out SyntaxNode invocationTarget)
    {
        if (lambda.Parent is ArgumentSyntax
            {
                Parent: ArgumentListSyntax
                {
                    Parent: InvocationExpressionSyntax invocation
                }
            }
            && GetInvocationFormattingTarget(invocation) is { } target)
        {
            invocationTarget = target;
            return true;
        }

        invocationTarget = null!;
        return false;
    }

    private static SyntaxNode? GetInvocationFormattingTarget(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax => invocation.Expression,
            MemberBindingExpressionSyntax memberBinding =>
                memberBinding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>(),
            _ => null
        };

    private static bool IsInvocationTargetLineStart(
        SyntaxToken token,
        SyntaxNode invocationTarget)
    {
        if (TryGetFormattingNode(token, out SyntaxNode formattingNode))
        {
            return formattingNode == invocationTarget;
        }

        SyntaxNode? conditionalAccess = token.Parent switch
        {
            MemberBindingExpressionSyntax memberBinding when memberBinding.OperatorToken == token =>
                memberBinding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>(),
            BracketedArgumentListSyntax argumentList when argumentList.OpenBracketToken == token =>
                argumentList.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>(),
            _ => null
        };
        return conditionalAccess == invocationTarget;
    }

    private static bool TryGetContainingChangedContinuationIndentation(
        SyntaxNode node,
        SourceText source,
        string indentationUnit,
        CancellationToken cancellationToken,
        out SyntaxNode continuation,
        out StatementBreakIndentation indentation)
    {
        SyntaxNode? candidate = node.Parent;
        for (int visits = 0; candidate is not null && visits < MaximumAncestorDepth; visits++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetOperator(candidate, index: 0, out StatementBreakOperator operation)
                && TryGetContinuationNode(operation, out continuation)
                && continuation.Span.Contains(node.Span)
                && TryGetChange(
                    operation,
                    source,
                    indentationUnit,
                    cancellationToken,
                    out StatementBreakChangeKind changeKind,
                    out _,
                    out indentation)
                && changeKind is StatementBreakChangeKind.Indentation
                    or StatementBreakChangeKind.TrailingOperator
                    or StatementBreakChangeKind.BreakAfterOperator)
            {
                return true;
            }

            candidate = candidate.Parent;
        }

        continuation = null!;
        indentation = default;
        return false;
    }

    private static bool TryGetRelativeIndentationLevels(
        SyntaxToken bodyToken,
        TextLine nestedLine,
        SourceText source,
        string indentationUnit,
        CancellationToken cancellationToken,
        out int levels)
    {
        levels = 0;
        TextLine bodyLine = source.Lines.GetLineFromPosition(bodyToken.SpanStart);
        if (!TryGetIndentationEnd(source, bodyLine, cancellationToken, out int bodyIndentationEnd)
            || !TryGetIndentationEnd(source, nestedLine, cancellationToken, out int nestedIndentationEnd))
        {
            return false;
        }

        int bodyLength = bodyIndentationEnd - bodyLine.Start;
        int nestedLength = nestedIndentationEnd - nestedLine.Start;
        int difference = nestedLength - bodyLength;
        if (difference < 0 || difference % indentationUnit.Length != 0)
        {
            return false;
        }

        for (int index = 0; index < bodyLength; index++)
        {
            if (source[bodyLine.Start + index] != source[nestedLine.Start + index])
            {
                return false;
            }
        }

        for (int index = bodyLength; index < nestedLength; index++)
        {
            if (source[nestedLine.Start + index]
                != indentationUnit[(index - bodyLength) % indentationUnit.Length])
            {
                return false;
            }
        }

        levels = difference / indentationUnit.Length;
        return levels <= MaximumAncestorDepth;
    }

    private static OperatorPrecedence GetPrecedence(SyntaxNode node) => node.Kind() switch
    {
        SyntaxKind.SimpleMemberAccessExpression
            or SyntaxKind.PointerMemberAccessExpression
            or SyntaxKind.ConditionalAccessExpression => OperatorPrecedence.Primary,
        SyntaxKind.RangeExpression => OperatorPrecedence.Range,
        SyntaxKind.MultiplyExpression
            or SyntaxKind.DivideExpression
            or SyntaxKind.ModuloExpression => OperatorPrecedence.Multiplicative,
        SyntaxKind.AddExpression
            or SyntaxKind.SubtractExpression => OperatorPrecedence.Additive,
        SyntaxKind.LeftShiftExpression
            or SyntaxKind.RightShiftExpression
            or SyntaxKind.UnsignedRightShiftExpression => OperatorPrecedence.Shift,
        SyntaxKind.LessThanExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.AsExpression => OperatorPrecedence.Relational,
        SyntaxKind.EqualsExpression
            or SyntaxKind.NotEqualsExpression
            or SyntaxKind.IsExpression
            or SyntaxKind.IsPatternExpression => OperatorPrecedence.Equality,
        SyntaxKind.BitwiseAndExpression => OperatorPrecedence.LogicalAnd,
        SyntaxKind.ExclusiveOrExpression => OperatorPrecedence.LogicalXor,
        SyntaxKind.BitwiseOrExpression => OperatorPrecedence.LogicalOr,
        SyntaxKind.LogicalAndExpression => OperatorPrecedence.ConditionalAnd,
        SyntaxKind.LogicalOrExpression => OperatorPrecedence.ConditionalOr,
        SyntaxKind.CoalesceExpression => OperatorPrecedence.NullCoalescing,
        SyntaxKind.ConditionalExpression => OperatorPrecedence.Conditional,
        SyntaxKind.SimpleAssignmentExpression
            or SyntaxKind.AddAssignmentExpression
            or SyntaxKind.SubtractAssignmentExpression
            or SyntaxKind.MultiplyAssignmentExpression
            or SyntaxKind.DivideAssignmentExpression
            or SyntaxKind.ModuloAssignmentExpression
            or SyntaxKind.AndAssignmentExpression
            or SyntaxKind.ExclusiveOrAssignmentExpression
            or SyntaxKind.OrAssignmentExpression
            or SyntaxKind.LeftShiftAssignmentExpression
            or SyntaxKind.RightShiftAssignmentExpression
            or SyntaxKind.UnsignedRightShiftAssignmentExpression
            or SyntaxKind.CoalesceAssignmentExpression
            or SyntaxKind.EqualsValueClause
            or SyntaxKind.LetClause
            or SyntaxKind.NameEquals
            or SyntaxKind.ArrowExpressionClause
            or SyntaxKind.SimpleLambdaExpression
            or SyntaxKind.ParenthesizedLambdaExpression
            or SyntaxKind.SwitchExpressionArm => OperatorPrecedence.Assignment,
        SyntaxKind.RelationalPattern => OperatorPrecedence.PatternRelational,
        SyntaxKind.AndPattern => OperatorPrecedence.PatternAnd,
        SyntaxKind.OrPattern => OperatorPrecedence.PatternOr,
        _ => OperatorPrecedence.None
    };

    private static bool TryGetIndentationEnd(
        SourceText source,
        TextLine line,
        CancellationToken cancellationToken,
        out int indentationEnd)
    {
        indentationEnd = line.Start;
        while (indentationEnd < line.End && source[indentationEnd] is ' ' or '\t')
        {
            if (indentationEnd - line.Start == StatementBreakDiagnosticData.MaximumChangeCharacters)
            {
                return false;
            }

            if ((indentationEnd - line.Start & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            indentationEnd++;
        }

        return true;
    }

    private static bool IsWithinAncestorBudget(SyntaxNode node, CancellationToken cancellationToken)
    {
        SyntaxNode? current = node;
        for (int visits = 0; current.Parent is not null; visits++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (visits == MaximumAncestorDepth)
            {
                return false;
            }

            current = current.Parent;
        }

        return true;
    }

    private static bool TryGetNormalizedFormattingNode(
        SyntaxToken lineStartToken,
        SyntaxNode current,
        SourceText source,
        CancellationToken cancellationToken,
        ref int ancestorVisits,
        out SyntaxNode formattingNode)
    {
        if (TryGetFormattingNode(lineStartToken, out SyntaxNode operatorNode)
            && operatorNode != current
            && CanUseLineStartFormattingNode(operatorNode, source, cancellationToken))
        {
            if (operatorNode is RelationalPatternSyntax
                && current is BinaryPatternSyntax
                && operatorNode.GetFirstToken() == current.GetFirstToken())
            {
                formattingNode = operatorNode;
                return true;
            }

            if (TryGetFormattingOwner(
                operatorNode,
                current,
                cancellationToken,
                ref ancestorVisits,
                out formattingNode))
            {
                return true;
            }
        }

        SyntaxNode? candidate = lineStartToken.Parent;
        while (candidate is not null)
        {
            if (!TryVisitAncestor(ref ancestorVisits))
            {
                formattingNode = null!;
                return false;
            }

            if (TryGetTrailingOperatorContinuation(
                candidate,
                lineStartToken,
                out SyntaxToken previousToken,
                out TextSpan operatorSpan,
                out SyntaxToken nextToken,
                out bool trailingPlacement,
                out bool allowTriviaAfterOperator)
                && DefinesNormalizedContinuation(
                    candidate,
                    previousToken,
                    operatorSpan,
                    nextToken,
                    trailingPlacement,
                    allowTriviaAfterOperator,
                    source,
                    cancellationToken)
                && TryGetFormattingOwner(
                    candidate,
                    current,
                    cancellationToken,
                    ref ancestorVisits,
                    out formattingNode))
            {
                return true;
            }

            candidate = candidate.Parent;
        }

        formattingNode = null!;
        return false;
    }

    private static bool CanUseLineStartFormattingNode(
        SyntaxNode node,
        SourceText source,
        CancellationToken cancellationToken)
    {
        if (node is ConditionalAccessExpressionSyntax)
        {
            return HasValidInternalOperatorTrivia(node, source, cancellationToken);
        }

        SyntaxToken continuationToken = node switch
        {
            ArrowExpressionClauseSyntax clause => clause.Expression.GetFirstToken(),
            LambdaExpressionSyntax expression => expression.Body.GetFirstToken(),
            SwitchExpressionArmSyntax arm => arm.Expression.GetFirstToken(),
            _ => default
        };
        if (continuationToken.RawKind == 0)
        {
            return true;
        }

        return TryGetTrailingOperatorContinuation(
            node,
            continuationToken,
            out SyntaxToken previousToken,
            out TextSpan operatorSpan,
            out SyntaxToken nextToken,
            out bool trailingPlacement,
            out bool allowTriviaAfterOperator)
            && DefinesNormalizedContinuation(
                node,
                previousToken,
                operatorSpan,
                nextToken,
                trailingPlacement,
                allowTriviaAfterOperator,
                source,
                cancellationToken);
    }

    private static bool HasValidInternalOperatorTrivia(
        SyntaxNode node,
        SourceText source,
        CancellationToken cancellationToken)
    {
        if (node is not ConditionalAccessExpressionSyntax conditionalAccess
            || !TryGetConditionalAccessParts(conditionalAccess, out SyntaxToken bindingToken, out _, out _))
        {
            return true;
        }

        return IsWhitespace(
            source,
            conditionalAccess.OperatorToken.Span.End,
            bindingToken.SpanStart,
            cancellationToken);
    }

    private static bool DefinesNormalizedContinuation(
        SyntaxNode node,
        SyntaxToken previousToken,
        TextSpan operatorSpan,
        SyntaxToken nextToken,
        bool trailingPlacement,
        bool allowTriviaAfterOperator,
        SourceText source,
        CancellationToken cancellationToken)
    {
        int previousLine = GetTokenEndLine(source, previousToken).LineNumber;
        int operatorLine = source.Lines.GetLineFromPosition(operatorSpan.Start).LineNumber;
        int operatorEndLine = source.Lines.GetLineFromPosition(operatorSpan.End - 1).LineNumber;
        int nextLine = source.Lines.GetLineFromPosition(nextToken.SpanStart).LineNumber;

        if (trailingPlacement)
        {
            if (previousLine == operatorLine)
            {
                return operatorLine != nextLine;
            }

            return IsWhitespace(source, previousToken.Span.End, operatorSpan.Start, cancellationToken)
                && IsWhitespace(source, operatorSpan.End, nextToken.SpanStart, cancellationToken);
        }

        if (!HasValidInternalOperatorTrivia(node, source, cancellationToken)
            || previousLine != operatorLine && operatorLine == operatorEndLine)
        {
            return false;
        }

        return (operatorLine != operatorEndLine || operatorLine != nextLine)
            && IsWhitespace(source, previousToken.Span.End, operatorSpan.Start, cancellationToken)
            && (allowTriviaAfterOperator
                || IsWhitespace(source, operatorSpan.End, nextToken.SpanStart, cancellationToken));
    }

    private static bool TryGetFormattingOwner(
        SyntaxNode formattingNode,
        SyntaxNode current,
        CancellationToken cancellationToken,
        ref int ancestorVisits,
        out SyntaxNode owner)
    {
        owner = formattingNode;
        while (!owner.Span.Contains(current.Span))
        {
            if (!TryVisitAncestor(ref ancestorVisits))
            {
                owner = null!;
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            SyntaxNode? parent = owner.Parent;
            if (parent is not null && IsTransparentFormattingOwner(parent, owner))
            {
                owner = parent;
                continue;
            }

            owner = null!;
            return false;
        }

        return owner != current;
    }

    private static bool IsTransparentFormattingOwner(SyntaxNode parent, SyntaxNode owner) =>
        parent is ExpressionSyntax
        || parent is PatternSyntax
        || parent is ArgumentSyntax
        || parent is BaseArgumentListSyntax
        || parent is AttributeArgumentSyntax
        || parent is AttributeArgumentListSyntax
        || parent is InterpolationSyntax
        || parent is SwitchExpressionArmSyntax
        || parent is QueryClauseSyntax
        || owner is NameEqualsSyntax && IsNameEqualsOwner(parent, owner);

    private static bool TryGetTrailingOperatorContinuation(
        SyntaxNode node,
        SyntaxToken lineStartToken,
        out SyntaxToken previousToken,
        out TextSpan operatorSpan,
        out SyntaxToken nextToken,
        out bool trailingPlacement,
        out bool allowTriviaAfterOperator)
    {
        trailingPlacement = false;
        allowTriviaAfterOperator = false;
        if (node is ConditionalAccessExpressionSyntax conditionalAccess
            && TryGetConditionalAccessParts(
                conditionalAccess,
                out SyntaxToken bindingToken,
                out SyntaxToken conditionalRightToken,
                out _)
            && (conditionalRightToken == lineStartToken || bindingToken == lineStartToken))
        {
            previousToken = conditionalAccess.Expression.GetLastToken();
            operatorSpan = TextSpan.FromBounds(
                conditionalAccess.OperatorToken.SpanStart,
                bindingToken.Span.End);
            nextToken = conditionalRightToken;
            return true;
        }

        if (node is UsingDirectiveSyntax { Alias: not null, Name: not null } usingDirective
            && usingDirective.Name.GetFirstToken() == lineStartToken)
        {
            previousToken = usingDirective.Alias.Name.GetLastToken();
            operatorSpan = usingDirective.Alias.EqualsToken.Span;
            nextToken = lineStartToken;
            trailingPlacement = true;
            return true;
        }

        if (node is AttributeArgumentSyntax { NameEquals: not null } attributeArgument
            && attributeArgument.Expression.GetFirstToken() == lineStartToken)
        {
            previousToken = attributeArgument.NameEquals.Name.GetLastToken();
            operatorSpan = attributeArgument.NameEquals.EqualsToken.Span;
            nextToken = lineStartToken;
            trailingPlacement = true;
            return true;
        }

        if (node is AnonymousObjectMemberDeclaratorSyntax { NameEquals: not null } anonymousMember
            && anonymousMember.Expression.GetFirstToken() == lineStartToken)
        {
            previousToken = anonymousMember.NameEquals.Name.GetLastToken();
            operatorSpan = anonymousMember.NameEquals.EqualsToken.Span;
            nextToken = lineStartToken;
            trailingPlacement = true;
            return true;
        }

        switch (node)
        {
            case BinaryExpressionSyntax expression when expression.Right.GetFirstToken() == lineStartToken:
                previousToken = expression.Left.GetLastToken();
                operatorSpan = expression.OperatorToken.Span;
                nextToken = lineStartToken;
                trailingPlacement = expression.IsKind(SyntaxKind.IsExpression);
                return true;
            case AssignmentExpressionSyntax expression when expression.Right.GetFirstToken() == lineStartToken:
                previousToken = expression.Left.GetLastToken();
                operatorSpan = expression.OperatorToken.Span;
                nextToken = lineStartToken;
                trailingPlacement = true;
                return true;
            case ConditionalExpressionSyntax expression when expression.WhenTrue.GetFirstToken() == lineStartToken:
                previousToken = expression.Condition.GetLastToken();
                operatorSpan = expression.QuestionToken.Span;
                nextToken = lineStartToken;
                return true;
            case ConditionalExpressionSyntax expression when expression.WhenFalse.GetFirstToken() == lineStartToken:
                previousToken = expression.WhenTrue.GetLastToken();
                operatorSpan = expression.ColonToken.Span;
                nextToken = lineStartToken;
                return true;
            case MemberAccessExpressionSyntax expression when expression.Name.GetFirstToken() == lineStartToken:
                previousToken = expression.Expression.GetLastToken();
                operatorSpan = expression.OperatorToken.Span;
                nextToken = lineStartToken;
                return true;
            case RangeExpressionSyntax { LeftOperand: not null, RightOperand: not null } expression
                when expression.RightOperand.GetFirstToken() == lineStartToken:
                previousToken = expression.LeftOperand.GetLastToken();
                operatorSpan = expression.OperatorToken.Span;
                nextToken = lineStartToken;
                return true;
            case IsPatternExpressionSyntax expression when expression.Pattern.GetFirstToken() == lineStartToken:
                previousToken = expression.Expression.GetLastToken();
                operatorSpan = expression.IsKeyword.Span;
                nextToken = lineStartToken;
                trailingPlacement = true;
                return true;
            case BinaryPatternSyntax pattern when pattern.Right.GetFirstToken() == lineStartToken:
                previousToken = pattern.Left.GetLastToken();
                operatorSpan = pattern.OperatorToken.Span;
                nextToken = lineStartToken;
                return true;
            case RelationalPatternSyntax pattern when pattern.Expression.GetFirstToken() == lineStartToken:
                previousToken = pattern.OperatorToken.GetPreviousToken();
                operatorSpan = pattern.OperatorToken.Span;
                nextToken = lineStartToken;
                return true;
            case EqualsValueClauseSyntax clause when clause.Value.GetFirstToken() == lineStartToken:
                previousToken = clause.EqualsToken.GetPreviousToken();
                operatorSpan = clause.EqualsToken.Span;
                nextToken = lineStartToken;
                trailingPlacement = true;
                return true;
            case LetClauseSyntax clause when clause.Expression.GetFirstToken() == lineStartToken:
                previousToken = clause.Identifier;
                operatorSpan = clause.EqualsToken.Span;
                nextToken = lineStartToken;
                trailingPlacement = true;
                return true;
            case ArrowExpressionClauseSyntax clause when clause.Expression.GetFirstToken() == lineStartToken:
                previousToken = clause.ArrowToken.GetPreviousToken();
                operatorSpan = clause.ArrowToken.Span;
                nextToken = lineStartToken;
                trailingPlacement = true;
                allowTriviaAfterOperator = true;
                return true;
            case LambdaExpressionSyntax expression when expression.Body.GetFirstToken() == lineStartToken:
                previousToken = expression.ArrowToken.GetPreviousToken();
                operatorSpan = expression.ArrowToken.Span;
                nextToken = lineStartToken;
                trailingPlacement = true;
                allowTriviaAfterOperator = true;
                return true;
            case SwitchExpressionArmSyntax arm when arm.Expression.GetFirstToken() == lineStartToken:
                previousToken = arm.EqualsGreaterThanToken.GetPreviousToken();
                operatorSpan = arm.EqualsGreaterThanToken.Span;
                nextToken = lineStartToken;
                trailingPlacement = true;
                allowTriviaAfterOperator = true;
                return true;
            default:
                previousToken = default;
                operatorSpan = default;
                nextToken = default;
                return false;
        }
    }

    private static bool TryGetConditionalAccessParts(
        ConditionalAccessExpressionSyntax expression,
        out SyntaxToken bindingToken,
        out SyntaxToken rightToken,
        out string operatorText)
    {
        bindingToken = expression.WhenNotNull.GetFirstToken();
        if (bindingToken.Parent is MemberBindingExpressionSyntax memberBinding
            && memberBinding.OperatorToken == bindingToken)
        {
            rightToken = memberBinding.Name.GetFirstToken();
            operatorText = "?.";
            return true;
        }

        if (bindingToken.Parent is BracketedArgumentListSyntax
            {
                Parent: ElementBindingExpressionSyntax elementBinding,
                Arguments.Count: > 0
            } argumentList
            && argumentList.OpenBracketToken == bindingToken)
        {
            rightToken = elementBinding.ArgumentList.Arguments[0].GetFirstToken();
            operatorText = "?[";
            return true;
        }

        rightToken = default;
        operatorText = string.Empty;
        return false;
    }

    private static bool IsNameEqualsOwner(SyntaxNode? parent, SyntaxNode nameEquals) =>
        parent is UsingDirectiveSyntax usingDirective && usingDirective.Alias == nameEquals
        || parent is AttributeArgumentSyntax attributeArgument && attributeArgument.NameEquals == nameEquals
        || parent is AnonymousObjectMemberDeclaratorSyntax anonymousMember
            && anonymousMember.NameEquals == nameEquals;

    private static bool TryGetFormattingNode(SyntaxToken token, out SyntaxNode node)
    {
        SyntaxNode? candidate = token.Parent switch
        {
            BinaryExpressionSyntax expression when expression.OperatorToken == token => expression,
            AssignmentExpressionSyntax expression when expression.OperatorToken == token => expression,
            ConditionalExpressionSyntax expression when expression.QuestionToken == token
                || expression.ColonToken == token => expression,
            MemberAccessExpressionSyntax expression when expression.OperatorToken == token => expression,
            ConditionalAccessExpressionSyntax expression when expression.OperatorToken == token => expression,
            RangeExpressionSyntax expression when expression.OperatorToken == token => expression,
            IsPatternExpressionSyntax expression when expression.IsKeyword == token => expression,
            BinaryPatternSyntax pattern when pattern.OperatorToken == token => pattern,
            RelationalPatternSyntax pattern when pattern.OperatorToken == token => pattern,
            EqualsValueClauseSyntax clause when clause.EqualsToken == token => clause,
            LetClauseSyntax clause when clause.EqualsToken == token => clause,
            NameEqualsSyntax clause when clause.EqualsToken == token => clause,
            ArrowExpressionClauseSyntax clause when clause.ArrowToken == token => clause,
            LambdaExpressionSyntax expression when expression.ArrowToken == token => expression,
            SwitchExpressionArmSyntax arm when arm.EqualsGreaterThanToken == token => arm,
            _ => null
        };

        if (candidate is not null)
        {
            node = candidate;
            return true;
        }

        node = null!;
        return false;
    }

    private static bool TryGetIndentationAnchor(
        SyntaxNode node,
        ref int ancestorVisits,
        out SyntaxToken anchor)
    {
        if (node is LambdaExpressionSyntax or SwitchExpressionArmSyntax)
        {
            anchor = node.GetFirstToken();
            return true;
        }

        SyntaxNode current = node;
        while (current.Parent is { } parent)
        {
            if (!TryVisitAncestor(ref ancestorVisits))
            {
                anchor = default;
                return false;
            }

            switch (parent)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    anchor = parenthesized.OpenParenToken;
                    return true;
                case ParenthesizedPatternSyntax parenthesized:
                    anchor = parenthesized.OpenParenToken;
                    return true;
                case ArgumentListSyntax arguments:
                    anchor = arguments.OpenParenToken;
                    return true;
                case BracketedArgumentListSyntax arguments:
                    anchor = arguments.OpenBracketToken;
                    return true;
                case AttributeArgumentListSyntax arguments:
                    anchor = arguments.OpenParenToken;
                    return true;
                case TupleExpressionSyntax tuple:
                    anchor = tuple.OpenParenToken;
                    return true;
                case InitializerExpressionSyntax initializer:
                    anchor = initializer.OpenBraceToken;
                    return true;
                case AnonymousObjectCreationExpressionSyntax anonymousObject:
                    anchor = anonymousObject.OpenBraceToken;
                    return true;
                case CollectionExpressionSyntax collection:
                    anchor = collection.OpenBracketToken;
                    return true;
                case InterpolationSyntax interpolation:
                    anchor = interpolation.OpenBraceToken;
                    return true;
                case CatchFilterClauseSyntax filter:
                    anchor = filter.WhenKeyword;
                    return true;
                case AccessorDeclarationSyntax accessor:
                    anchor = accessor.GetFirstToken();
                    return true;
                case ArrowExpressionClauseSyntax arrow:
                    anchor = arrow.Parent?.GetFirstToken() ?? arrow.GetFirstToken();
                    return true;
                case LambdaExpressionSyntax lambda:
                    anchor = lambda.GetFirstToken();
                    return true;
                case SwitchExpressionArmSyntax arm
                    when node is RelationalPatternSyntax
                        && node.GetFirstToken() == arm.Pattern.GetFirstToken()
                        && arm.Parent is SwitchExpressionSyntax switchExpression:
                    anchor = switchExpression.OpenBraceToken;
                    return true;
                case SwitchExpressionArmSyntax arm:
                    anchor = arm.GetFirstToken();
                    return true;
                case StatementSyntax statement:
                    anchor = statement.GetFirstToken();
                    return true;
                case MemberDeclarationSyntax member:
                    anchor = member.GetFirstToken();
                    return true;
            }

            current = parent;
        }

        anchor = node.GetFirstToken();
        return anchor.RawKind != 0 && !anchor.IsMissing;
    }

    private static bool TryVisitAncestor(ref int ancestorVisits)
    {
        if (ancestorVisits >= MaximumAncestorDepth)
        {
            ancestorVisits = MaximumAncestorDepth + 1;
            return false;
        }

        ancestorVisits++;
        return true;
    }

    private static TextLine GetTokenEndLine(SourceText source, SyntaxToken token)
    {
        int position = token.Span.End > token.SpanStart ? token.Span.End - 1 : token.SpanStart;
        return source.Lines.GetLineFromPosition(position);
    }

    private static bool IsWhitespace(
        SourceText source,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        if (end - start > StatementBreakDiagnosticData.MaximumChangeCharacters)
        {
            return false;
        }

        for (int index = start; index < end; index++)
        {
            if ((index - start & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!char.IsWhiteSpace(source[index]))
            {
                return false;
            }
        }

        return true;
    }
}