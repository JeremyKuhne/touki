// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

internal readonly struct AllmanFormattingOptions
{
    public const string RequireBlankLineAfterClosingBraceOption =
        "dotnet_code_quality.TOUKI0027.require_blank_line_after_closing_brace";
    public const string AllowSingleLineBlocksOption =
        "dotnet_code_quality.TOUKI0027.allow_single_line_blocks";
    public const string RequireBlankLineAfterMultilineStatementOption =
        "dotnet_code_quality.TOUKI0027.require_blank_line_after_multiline_statement";
    public const string MaxLineLengthOption = "dotnet_code_quality.TOUKI0027.max_line_length";
    public const int DefaultMaxLineLength = 120;
    public const int DefaultIndentSize = 4;
    public const int MaximumIndentSize = 16;

    private const string StandardMaxLineLengthOption = "max_line_length";
    private const string IndentSizeOption = "indent_size";
    private const string IndentStyleOption = "indent_style";
    private const string RequireBlankLineAfterClosingBraceProperty =
        nameof(RequireBlankLineAfterClosingBrace);
    private const string AllowSingleLineBlocksProperty = nameof(AllowSingleLineBlocks);
    private const string RequireBlankLineAfterMultilineStatementProperty =
        nameof(RequireBlankLineAfterMultilineStatement);
    private const string MaxLineLengthProperty = nameof(MaxLineLength);
    private const string IndentationProperty = nameof(Indentation);
    private const string FixAvailableProperty = "FixAvailable";

    public AllmanFormattingOptions(
        bool requireBlankLineAfterClosingBrace,
        bool allowSingleLineBlocks,
        bool requireBlankLineAfterMultilineStatement,
        int maxLineLength,
        string indentation)
    {
        RequireBlankLineAfterClosingBrace = requireBlankLineAfterClosingBrace;
        AllowSingleLineBlocks = allowSingleLineBlocks;
        RequireBlankLineAfterMultilineStatement = requireBlankLineAfterMultilineStatement;
        MaxLineLength = maxLineLength;
        Indentation = indentation;
    }

    public bool RequireBlankLineAfterClosingBrace { get; }

    public bool AllowSingleLineBlocks { get; }

    public bool RequireBlankLineAfterMultilineStatement { get; }

    public int MaxLineLength { get; }

    public string Indentation { get; }

    public static AllmanFormattingOptions GetOptions(AnalyzerConfigOptions options)
    {
        int maxLineLength = options.TryGetPositiveInteger(MaxLineLengthOption, out int configuredMaxLineLength)
            || options.TryGetPositiveInteger(StandardMaxLineLengthOption, out configuredMaxLineLength)
                ? configuredMaxLineLength
                : DefaultMaxLineLength;
        int indentSize = options.TryGetPositiveInteger(IndentSizeOption, out int configuredIndentSize)
            && configuredIndentSize <= MaximumIndentSize
                ? configuredIndentSize
                : DefaultIndentSize;
        bool useTabs = options.TryGetValue(IndentStyleOption, out string? indentStyle)
            && string.Equals(indentStyle.Trim(), "tab", StringComparison.OrdinalIgnoreCase);

        return new(
            requireBlankLineAfterClosingBrace: GetBooleanOption(
                options,
                RequireBlankLineAfterClosingBraceOption,
                defaultValue: true),
            allowSingleLineBlocks: GetBooleanOption(options, AllowSingleLineBlocksOption, defaultValue: true),
            requireBlankLineAfterMultilineStatement: GetBooleanOption(
                options,
                RequireBlankLineAfterMultilineStatementOption,
                defaultValue: true),
            maxLineLength,
            indentation: useTabs ? "\t" : new string(' ', indentSize));
    }

    public ImmutableDictionary<string, string?> ToDiagnosticProperties(bool fixAvailable) =>
        ImmutableDictionary<string, string?>.Empty
            .Add(RequireBlankLineAfterClosingBraceProperty, RequireBlankLineAfterClosingBrace.ToString())
            .Add(AllowSingleLineBlocksProperty, AllowSingleLineBlocks.ToString())
            .Add(
                RequireBlankLineAfterMultilineStatementProperty,
                RequireBlankLineAfterMultilineStatement.ToString())
            .Add(MaxLineLengthProperty, MaxLineLength.ToString(CultureInfo.InvariantCulture))
            .Add(IndentationProperty, Indentation)
            .Add(FixAvailableProperty, fixAvailable.ToString());

    public static bool TryGetDiagnosticOptions(
        ImmutableDictionary<string, string?> properties,
        out AllmanFormattingOptions options,
        out bool fixAvailable)
    {
        if (TryGetBoolean(properties, RequireBlankLineAfterClosingBraceProperty, out bool requireAfterBrace)
            && TryGetBoolean(properties, AllowSingleLineBlocksProperty, out bool allowSingleLine)
            && TryGetBoolean(
                properties,
                RequireBlankLineAfterMultilineStatementProperty,
                out bool requireAfterStatement)
            && properties.TryGetValue(MaxLineLengthProperty, out string? maxLineLengthText)
            && int.TryParse(
                maxLineLengthText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int maxLineLength)
            && maxLineLength > 0
            && properties.TryGetValue(IndentationProperty, out string? indentation)
            && indentation is not null
            && TryGetBoolean(properties, FixAvailableProperty, out fixAvailable))
        {
            options = new(
                requireAfterBrace,
                allowSingleLine,
                requireAfterStatement,
                maxLineLength,
                indentation);
            return true;
        }

        options = default;
        fixAvailable = false;
        return false;
    }

    private static bool GetBooleanOption(
        AnalyzerConfigOptions options,
        string key,
        bool defaultValue) =>
        options.TryGetValue(key, out string? configured) && bool.TryParse(configured.Trim(), out bool value)
            ? value
            : defaultValue;

    private static bool TryGetBoolean(
        ImmutableDictionary<string, string?> properties,
        string key,
        out bool value)
    {
        if (properties.TryGetValue(key, out string? text) && bool.TryParse(text, out value))
        {
            return true;
        }

        value = false;
        return false;
    }
}

internal static class AllmanFormatter
{
    public const int MaximumAddedCharacters = 4 * 1024 * 1024;

    public static bool TryFindViolation(
        SourceText source,
        SyntaxNode root,
        AllmanFormattingOptions options,
        CancellationToken cancellationToken,
        out TextSpan firstViolation,
        out bool fixAvailable)
    {
        List<BracePair> pairs = GetBracePairs(root, cancellationToken);
        Dictionary<int, LineLayout> lineLayouts = [];
        Dictionary<int, int> braceIndentationLengths = [];
        PreprocessorLineMap preprocessorLines = new(source, root, cancellationToken);
        firstViolation = default;
        bool foundViolation = false;
        long projectedAddedCharacters = 0;
        int maximumGeneratedIndentationLength = options.Indentation.Length;
        bool relocatesPotentialDirective = false;

        foreach (BracePair pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TextLine openLine = source.Lines.GetLineFromPosition(pair.OpenBrace.SpanStart);
            TextLine closeLine = source.Lines.GetLineFromPosition(pair.CloseBrace.SpanStart);
            LineLayout openLayout = GetLineLayout(source, openLine, lineLayouts);
            LineLayout closeLayout = GetLineLayout(source, closeLine, lineLayouts);
            int braceIndentationLength = GetBraceIndentationLength(
                source,
                pair,
                braceIndentationLengths,
                options.Indentation.Length,
                lineLayouts);
            braceIndentationLengths.Add(pair.OpenBrace.SpanStart, braceIndentationLength);
            maximumGeneratedIndentationLength = Math.Max(
                maximumGeneratedIndentationLength,
                AddWithoutOverflow(braceIndentationLength, options.Indentation.Length));

            bool allowedSingleLine = options.AllowSingleLineBlocks
                && openLine.LineNumber == closeLine.LineNumber
                && openLine.Span.Length <= options.MaxLineLength;
            if (TryGetSameLineContinuationSpan(source, pair.CloseBrace, out _)
                && (!allowedSingleLine || !CompleteContinuationIsOnSameLine(source, pair.CloseBrace)))
            {
                RecordViolation(pair.CloseBrace.Span, ref foundViolation, ref firstViolation);
                AddProjectedCharacters(ref projectedAddedCharacters, 2L + braceIndentationLength);
            }

            if (HasContinuationBlankLineViolation(
                source,
                pair.CloseBrace,
                preprocessorLines,
                lineLayouts,
                cancellationToken))
            {
                RecordViolation(pair.CloseBrace.Span, ref foundViolation, ref firstViolation);
            }

            if (allowedSingleLine)
            {
                continue;
            }

            if (openLayout.FirstNonWhitespace < pair.OpenBrace.SpanStart)
            {
                RecordViolation(pair.OpenBrace.Span, ref foundViolation, ref firstViolation);
                AddProjectedCharacters(ref projectedAddedCharacters, 2L + braceIndentationLength);
                relocatesPotentialDirective |= openLayout.ContainsHash;
            }

            if (openLayout.LastNonWhitespaceExclusive > pair.OpenBrace.Span.End)
            {
                RecordViolation(pair.OpenBrace.Span, ref foundViolation, ref firstViolation);
                AddProjectedCharacters(
                    ref projectedAddedCharacters,
                    2L + braceIndentationLength + options.Indentation.Length);
                relocatesPotentialDirective |= openLayout.ContainsHash;
            }

            if (closeLayout.FirstNonWhitespace < pair.CloseBrace.SpanStart)
            {
                RecordViolation(pair.CloseBrace.Span, ref foundViolation, ref firstViolation);
                AddProjectedCharacters(ref projectedAddedCharacters, 2L + braceIndentationLength);
                relocatesPotentialDirective |= closeLayout.ContainsHash;
            }
        }

        if (options.RequireBlankLineAfterClosingBrace)
        {
            AddProjectedCharacters(ref projectedAddedCharacters, 2L * pairs.Count);
            foreach (BracePair pair in pairs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SyntaxToken spacingToken = GetClosingBraceSpacingToken(
                    source,
                    pair.CloseBrace,
                    lineLayouts);
                if (TryGetBlankLineChange(
                    source,
                    spacingToken,
                    canMoveSameLineSuccessor: false,
                    requireTokenOnlyLine: spacingToken == pair.CloseBrace,
                    preprocessorLines,
                    lineLayouts,
                    cancellationToken,
                    out _))
                {
                    RecordViolation(spacingToken.Span, ref foundViolation, ref firstViolation);
                }
            }
        }

        HashSet<int> processedSemicolons = [];
        foreach (StatementSyntax statement in root.DescendantNodes().OfType<StatementSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxToken semicolon = statement.GetLastToken();
            if (!semicolon.IsKind(SyntaxKind.SemicolonToken)
                || semicolon.IsMissing
                || !processedSemicolons.Add(semicolon.SpanStart))
            {
                continue;
            }

            if (options.RequireBlankLineAfterMultilineStatement)
            {
                TextLine statementLine = source.Lines.GetLineFromPosition(statement.SpanStart);
                LineLayout statementLayout = GetLineLayout(source, statementLine, lineLayouts);
                int projectedIndentationLength = Math.Max(
                    statementLayout.IndentationEnd - statementLine.Start,
                    maximumGeneratedIndentationLength);
                AddProjectedCharacters(ref projectedAddedCharacters, 4L + projectedIndentationLength);

                if (statementLine.LineNumber
                    != source.Lines.GetLineFromPosition(semicolon.SpanStart).LineNumber
                    && TryGetBlankLineChange(
                        source,
                        semicolon,
                        canMoveSameLineSuccessor: true,
                        requireTokenOnlyLine: false,
                        preprocessorLines,
                        lineLayouts,
                        cancellationToken,
                        out _))
                {
                    RecordViolation(semicolon.Span, ref foundViolation, ref firstViolation);
                }
            }
        }

        fixAvailable = !relocatesPotentialDirective
            && projectedAddedCharacters <= MaximumAddedCharacters;
        return foundViolation;
    }

    public static bool TryFormat(
        SourceText source,
        SyntaxNode root,
        AllmanFormattingOptions options,
        CancellationToken cancellationToken,
        out SourceText formatted,
        out TextSpan firstViolation)
    {
        if (!TryFindViolation(
            source,
            root,
            options,
            cancellationToken,
            out firstViolation,
            out bool fixAvailable)
            || !fixAvailable)
        {
            formatted = source;
            return false;
        }

        List<BracePair> pairs = GetBracePairs(root, cancellationToken);
        Dictionary<TextSpan, string> replacements = [];
        Dictionary<int, string> braceIndentations = [];
        Dictionary<int, LineLayout> lineLayouts = [];
        string fallbackLineBreak = GetFallbackLineBreak(source);
        TextSpan formattedViolation = default;
        bool foundBraceViolation = false;

        foreach (BracePair pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TextLine openLine = source.Lines.GetLineFromPosition(pair.OpenBrace.SpanStart);
            TextLine closeLine = source.Lines.GetLineFromPosition(pair.CloseBrace.SpanStart);
            LineLayout openLayout = GetLineLayout(source, openLine, lineLayouts);
            LineLayout closeLayout = GetLineLayout(source, closeLine, lineLayouts);
            string braceIndentation = GetBraceIndentation(
                source,
                pair,
                braceIndentations,
                options.Indentation,
                lineLayouts);
            braceIndentations.Add(pair.OpenBrace.SpanStart, braceIndentation);
            bool allowedSingleLine = options.AllowSingleLineBlocks
                && openLine.LineNumber == closeLine.LineNumber
                && openLine.Span.Length <= options.MaxLineLength;
            bool moveContinuation = TryGetSameLineContinuationSpan(
                source,
                pair.CloseBrace,
                out TextSpan continuationSpan)
                && (!allowedSingleLine || !CompleteContinuationIsOnSameLine(source, pair.CloseBrace));
            if (moveContinuation)
            {
                AddReplacement(
                    continuationSpan,
                    GetLineBreak(source, closeLine, fallbackLineBreak) + braceIndentation,
                    replacements);
                RecordViolation(pair.CloseBrace.Span, ref foundBraceViolation, ref formattedViolation);
            }

            if (allowedSingleLine)
            {
                continue;
            }

            string lineBreak = GetLineBreak(source, openLine, fallbackLineBreak);
            if (openLayout.FirstNonWhitespace < pair.OpenBrace.SpanStart)
            {
                AddBreakBefore(
                    source,
                    pair.OpenBrace,
                    lineBreak,
                    braceIndentation,
                    replacements);
                RecordViolation(pair.OpenBrace.Span, ref foundBraceViolation, ref formattedViolation);
            }

            if (openLayout.LastNonWhitespaceExclusive > pair.OpenBrace.Span.End)
            {
                bool empty = openLine.LineNumber == closeLine.LineNumber
                    && pair.OpenBrace.GetNextToken() == pair.CloseBrace
                    && IsWhitespaceOnly(
                        source,
                        TextSpan.FromBounds(pair.OpenBrace.Span.End, pair.CloseBrace.SpanStart));
                AddBreakAfter(
                    source,
                    pair.OpenBrace,
                    lineBreak,
                    empty ? braceIndentation : braceIndentation + options.Indentation,
                    replacements);
                RecordViolation(pair.OpenBrace.Span, ref foundBraceViolation, ref formattedViolation);
            }

            if (closeLayout.FirstNonWhitespace < pair.CloseBrace.SpanStart)
            {
                AddBreakBefore(
                    source,
                    pair.CloseBrace,
                    GetLineBreak(source, closeLine, fallbackLineBreak),
                    braceIndentation,
                    replacements);
                RecordViolation(pair.CloseBrace.Span, ref foundBraceViolation, ref formattedViolation);
            }

        }

        List<TextChange> braceChanges = GetTextChanges(replacements);
        long replacementCharacters = GetReplacementCharacterCount(braceChanges);
        if (replacementCharacters > MaximumAddedCharacters)
        {
            formatted = source;
            return false;
        }

        SourceText braceFormatted = ApplyChanges(source, braceChanges);
        if (braceChanges.Count > 0)
        {
            root = root.SyntaxTree.WithChangedText(braceFormatted).GetRoot(cancellationToken);
        }

        replacements.Clear();
        lineLayouts.Clear();
        bool foundSpacingViolation = false;
        TextSpan firstSpacingViolation = default;
        AddBlankLineReplacements(
            braceFormatted,
            root,
            options,
            fallbackLineBreak,
            cancellationToken,
            replacements,
            lineLayouts,
            ref foundSpacingViolation,
            ref firstSpacingViolation);

        List<TextChange> spacingChanges = GetTextChanges(replacements);
        replacementCharacters += GetReplacementCharacterCount(spacingChanges);
        if (replacementCharacters > MaximumAddedCharacters)
        {
            formatted = source;
            return false;
        }

        formatted = ApplyChanges(braceFormatted, spacingChanges);
        return true;
    }

    private static void AddBlankLineReplacements(
        SourceText source,
        SyntaxNode root,
        AllmanFormattingOptions options,
        string fallbackLineBreak,
        CancellationToken cancellationToken,
        Dictionary<TextSpan, string> replacements,
        Dictionary<int, LineLayout> lineLayouts,
        ref bool foundViolation,
        ref TextSpan firstViolation)
    {
        PreprocessorLineMap preprocessorLines = new(source, root, cancellationToken);
        foreach (BracePair pair in GetBracePairs(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AddContinuationBlankLineReplacements(
                source,
                pair.CloseBrace,
                preprocessorLines,
                lineLayouts,
                replacements,
                cancellationToken))
            {
                RecordViolation(pair.CloseBrace.Span, ref foundViolation, ref firstViolation);
            }
        }

        if (options.RequireBlankLineAfterClosingBrace)
        {
            foreach (BracePair pair in GetBracePairs(root, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                SyntaxToken spacingToken = GetClosingBraceSpacingToken(
                    source,
                    pair.CloseBrace,
                    lineLayouts);
                if (TryAddBlankLineAfter(
                    source,
                    spacingToken,
                    replacements,
                    sameLineIndentation: null,
                    fallbackLineBreak,
                    requireTokenOnlyLine: spacingToken == pair.CloseBrace,
                    preprocessorLines,
                    lineLayouts,
                    cancellationToken))
                {
                    RecordViolation(spacingToken.Span, ref foundViolation, ref firstViolation);
                }
            }
        }

        if (!options.RequireBlankLineAfterMultilineStatement)
        {
            return;
        }

        HashSet<int> processedSemicolons = [];
        foreach (StatementSyntax statement in root.DescendantNodes().OfType<StatementSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxToken semicolon = statement.GetLastToken();
            if (!semicolon.IsKind(SyntaxKind.SemicolonToken)
                || semicolon.IsMissing
            || !processedSemicolons.Add(semicolon.SpanStart)
                || source.Lines.GetLineFromPosition(statement.SpanStart).LineNumber
                    == source.Lines.GetLineFromPosition(semicolon.SpanStart).LineNumber)
            {
                continue;
            }

            string statementIndentation = GetLineIndentation(source, statement.SpanStart, lineLayouts);
            if (TryAddBlankLineAfter(
                source,
                semicolon,
                replacements,
                statementIndentation,
                fallbackLineBreak,
                requireTokenOnlyLine: false,
                preprocessorLines,
                lineLayouts,
                cancellationToken))
            {
                RecordViolation(semicolon.Span, ref foundViolation, ref firstViolation);
            }
        }
    }

    private static bool TryAddBlankLineAfter(
        SourceText source,
        SyntaxToken token,
        Dictionary<TextSpan, string> replacements,
        string? sameLineIndentation,
        string fallbackLineBreak,
        bool requireTokenOnlyLine,
        PreprocessorLineMap preprocessorLines,
        Dictionary<int, LineLayout> lineLayouts,
        CancellationToken cancellationToken)
    {
        if (!TryGetBlankLineChange(
            source,
            token,
            canMoveSameLineSuccessor: sameLineIndentation is not null,
            requireTokenOnlyLine,
            preprocessorLines,
            lineLayouts,
            cancellationToken,
            out BlankLineChange change))
        {
            return false;
        }

        string lineBreak = GetLineBreak(source, change.Line, fallbackLineBreak);
        string replacement = change.MovesSameLineSuccessor
            ? lineBreak + lineBreak + sameLineIndentation
            : lineBreak;
        AddReplacement(change.Span, replacement, replacements);
        return true;
    }

    private static bool TryGetBlankLineChange(
        SourceText source,
        SyntaxToken token,
        bool canMoveSameLineSuccessor,
        bool requireTokenOnlyLine,
        PreprocessorLineMap preprocessorLines,
        Dictionary<int, LineLayout> lineLayouts,
        CancellationToken cancellationToken,
        out BlankLineChange change)
    {
        TextLine tokenLine = source.Lines.GetLineFromPosition(token.Span.End);
        LineLayout tokenLayout = GetLineLayout(source, tokenLine, lineLayouts);
        if (requireTokenOnlyLine
            && (tokenLayout.FirstNonWhitespace != token.SpanStart
                || tokenLayout.LastNonWhitespaceExclusive != token.Span.End))
        {
            change = default;
            return false;
        }

        TextLine line = requireTokenOnlyLine
            ? tokenLine
            : GetLineIncludingAttachedComment(source, token, tokenLine);
        SyntaxToken nextToken = token.GetNextToken();
        if (IsContinuation(token, nextToken))
        {
            change = default;
            return false;
        }

        if (!nextToken.IsKind(SyntaxKind.EndOfFileToken)
            && source.Lines.GetLineFromPosition(nextToken.SpanStart).LineNumber == line.LineNumber)
        {
            if (!canMoveSameLineSuccessor)
            {
                change = default;
                return false;
            }

            int start = nextToken.SpanStart;
            while (start > token.Span.End && IsHorizontalWhitespace(source[start - 1]))
            {
                start--;
            }

            change = new(
                TextSpan.FromBounds(start, nextToken.SpanStart),
                line,
                movesSameLineSuccessor: true);
            return true;
        }

        TextLine insertionLine = line;
        for (int lineNumber = line.LineNumber + 1; lineNumber < source.Lines.Count; lineNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TextLine nextLine = source.Lines[lineNumber];
            LineLayout nextLayout = GetLineLayout(source, nextLine, lineLayouts);
            int nextContent = nextLayout.FirstNonWhitespace;
            PreprocessorLineKind preprocessorKind = preprocessorLines.GetKind(lineNumber);

            if (preprocessorKind == PreprocessorLineKind.AlternateBranch)
            {
                change = default;
                return false;
            }

            if (preprocessorKind is PreprocessorLineKind.Directive or PreprocessorLineKind.DisabledText)
            {
                insertionLine = nextLine;
                continue;
            }

            if (nextContent == nextLine.End)
            {
                change = default;
                return false;
            }

            if (source[nextContent] == '}')
            {
                change = default;
                return false;
            }

            change = new(
                new(insertionLine.EndIncludingLineBreak, 0),
                insertionLine,
                movesSameLineSuccessor: false);
            return true;
        }

        change = default;
        return false;
    }

    private static SyntaxToken GetClosingBraceSpacingToken(
        SourceText source,
        SyntaxToken closeBrace,
        Dictionary<int, LineLayout> lineLayouts)
    {
        if (closeBrace.Parent is not SwitchExpressionSyntax)
        {
            return closeBrace;
        }

        TextLine closeLine = source.Lines.GetLineFromPosition(closeBrace.SpanStart);
        LineLayout closeLayout = GetLineLayout(source, closeLine, lineLayouts);
        SyntaxToken nextToken = closeBrace.GetNextToken();
        return closeLayout.FirstNonWhitespace == closeBrace.SpanStart
            && nextToken.IsKind(SyntaxKind.SemicolonToken)
            && nextToken.Parent?.GetLastToken() == nextToken
                ? nextToken
                : closeBrace;
    }

    private static bool TryGetSameLineContinuationSpan(
        SourceText source,
        SyntaxToken closeBrace,
        out TextSpan span)
    {
        SyntaxToken nextToken = closeBrace.GetNextToken();
        if (!IsContinuation(closeBrace, nextToken))
        {
            span = default;
            return false;
        }

        TextLine closeLine = source.Lines.GetLineFromPosition(closeBrace.Span.End);
        TextLine continuationLine = source.Lines.GetLineFromPosition(nextToken.SpanStart);
        if (continuationLine.LineNumber != closeLine.LineNumber)
        {
            span = default;
            return false;
        }

        int start = nextToken.SpanStart;
        while (start > closeBrace.Span.End && IsHorizontalWhitespace(source[start - 1]))
        {
            start--;
        }

        span = TextSpan.FromBounds(start, nextToken.SpanStart);
        return true;
    }

    private static bool CompleteContinuationIsOnSameLine(SourceText source, SyntaxToken closeBrace)
    {
        SyntaxToken continuation = closeBrace.GetNextToken();
        SyntaxNode? completeConstruct = continuation.Kind() switch
        {
            SyntaxKind.ElseKeyword => continuation.Parent?.FirstAncestorOrSelf<IfStatementSyntax>(),
            SyntaxKind.CatchKeyword or SyntaxKind.FinallyKeyword =>
                continuation.Parent?.FirstAncestorOrSelf<TryStatementSyntax>(),
            SyntaxKind.WhileKeyword => GetDoStatement(closeBrace, continuation),
            _ => GetAccessorList(closeBrace, continuation)
        };
        if (completeConstruct is null)
        {
            return false;
        }

        int closeLine = source.Lines.GetLineFromPosition(closeBrace.Span.End).LineNumber;
        SyntaxToken finalToken = completeConstruct.GetLastToken();
        return source.Lines.GetLineFromPosition(finalToken.Span.End).LineNumber == closeLine;
    }

    private static bool HasContinuationBlankLineViolation(
        SourceText source,
        SyntaxToken closeBrace,
        PreprocessorLineMap preprocessorLines,
        Dictionary<int, LineLayout> lineLayouts,
        CancellationToken cancellationToken)
    {
        if (!TryGetContinuationLineRange(
            source,
            closeBrace,
            preprocessorLines,
            lineLayouts,
            out int firstLine,
            out int continuationLine,
            cancellationToken))
        {
            return false;
        }

        for (int lineNumber = firstLine; lineNumber < continuationLine; lineNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (preprocessorLines.GetKind(lineNumber) == PreprocessorLineKind.None
                && GetLineLayout(source, source.Lines[lineNumber], lineLayouts).FirstNonWhitespace
                    == source.Lines[lineNumber].End)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AddContinuationBlankLineReplacements(
        SourceText source,
        SyntaxToken closeBrace,
        PreprocessorLineMap preprocessorLines,
        Dictionary<int, LineLayout> lineLayouts,
        Dictionary<TextSpan, string> replacements,
        CancellationToken cancellationToken)
    {
        if (!TryGetContinuationLineRange(
            source,
            closeBrace,
            preprocessorLines,
            lineLayouts,
            out int firstLine,
            out int continuationLine,
            cancellationToken))
        {
            return false;
        }

        bool added = false;
        for (int lineNumber = firstLine; lineNumber < continuationLine; lineNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TextLine line = source.Lines[lineNumber];
            if (preprocessorLines.GetKind(lineNumber) == PreprocessorLineKind.None
                && GetLineLayout(source, line, lineLayouts).FirstNonWhitespace == line.End)
            {
                AddReplacement(line.SpanIncludingLineBreak, string.Empty, replacements);
                added = true;
            }
        }

        return added;
    }

    private static bool TryGetContinuationLineRange(
        SourceText source,
        SyntaxToken closeBrace,
        PreprocessorLineMap preprocessorLines,
        Dictionary<int, LineLayout> lineLayouts,
        out int firstLine,
        out int continuationLine,
        CancellationToken cancellationToken)
    {
        SyntaxToken nextToken = closeBrace.GetNextToken();
        if (!IsContinuation(closeBrace, nextToken))
        {
            firstLine = 0;
            continuationLine = 0;
            return false;
        }

        firstLine = source.Lines.GetLineFromPosition(closeBrace.Span.End).LineNumber + 1;
        continuationLine = source.Lines.GetLineFromPosition(nextToken.SpanStart).LineNumber;
        if (firstLine >= continuationLine)
        {
            return false;
        }

        for (int lineNumber = firstLine; lineNumber < continuationLine; lineNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreprocessorLineKind kind = preprocessorLines.GetKind(lineNumber);
            if (kind == PreprocessorLineKind.AlternateBranch)
            {
                return false;
            }

            if (kind is PreprocessorLineKind.Directive or PreprocessorLineKind.DisabledText)
            {
                continue;
            }

            TextLine line = source.Lines[lineNumber];
            if (GetLineLayout(source, line, lineLayouts).FirstNonWhitespace != line.End)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsContinuation(SyntaxToken closeBrace, SyntaxToken nextToken) =>
        closeBrace.IsKind(SyntaxKind.CloseBraceToken)
        && (nextToken.Kind() is
                SyntaxKind.ElseKeyword or
                SyntaxKind.CatchKeyword or
                SyntaxKind.FinallyKeyword
            || GetDoStatement(closeBrace, nextToken) is not null
            || GetAccessorList(closeBrace, nextToken) is not null);

    private static DoStatementSyntax? GetDoStatement(SyntaxToken closeBrace, SyntaxToken nextToken)
    {
        if (!nextToken.IsKind(SyntaxKind.WhileKeyword))
        {
            return null;
        }

        DoStatementSyntax? doStatement = nextToken.Parent?.FirstAncestorOrSelf<DoStatementSyntax>();
        return doStatement?.Statement.GetLastToken() == closeBrace
            ? doStatement
            : null;
    }

    private static AccessorListSyntax? GetAccessorList(SyntaxToken closeBrace, SyntaxToken nextToken)
    {
        AccessorDeclarationSyntax? accessor = closeBrace.Parent?.FirstAncestorOrSelf<AccessorDeclarationSyntax>();
        AccessorDeclarationSyntax? nextAccessor = nextToken.Parent?.FirstAncestorOrSelf<AccessorDeclarationSyntax>();
        return accessor is not null
            && nextAccessor is not null
            && accessor != nextAccessor
            && accessor.Parent == nextAccessor.Parent
                ? accessor.Parent as AccessorListSyntax
                : null;
    }

    private static TextLine GetLineIncludingAttachedComment(
        SourceText source,
        SyntaxToken token,
        TextLine tokenLine)
    {
        TextLine attachedLine = tokenLine;
        foreach (SyntaxTrivia trivia in token.TrailingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                continue;
            }

            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                return attachedLine;
            }

            if (!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || source.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber != attachedLine.LineNumber)
            {
                return attachedLine;
            }

            int commentEnd = Math.Max(trivia.SpanStart, trivia.Span.End - 1);
            attachedLine = source.Lines.GetLineFromPosition(commentEnd);
        }

        SyntaxToken nextToken = token.GetNextToken();
        foreach (SyntaxTrivia trivia in nextToken.LeadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                continue;
            }

            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                if (source.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber == tokenLine.LineNumber)
                {
                    int commentEnd = Math.Max(trivia.SpanStart, trivia.Span.End - 1);
                    attachedLine = source.Lines.GetLineFromPosition(commentEnd);
                }
            }

            break;
        }

        return attachedLine;
    }

    private static List<TextChange> GetTextChanges(
        Dictionary<TextSpan, string> replacements)
    {
        List<TextChange> changes = new(replacements.Count);
        foreach (KeyValuePair<TextSpan, string> replacement in replacements)
        {
            changes.Add(new(replacement.Key, replacement.Value));
        }

        changes.Sort(static (left, right) => left.Span.Start.CompareTo(right.Span.Start));
        return changes;
    }

    private static long GetReplacementCharacterCount(List<TextChange> changes)
    {
        long count = 0;
        foreach (TextChange change in changes)
        {
            count += change.NewText?.Length ?? 0;
        }

        return count;
    }

    private static SourceText ApplyChanges(SourceText source, List<TextChange> changes) =>
        changes.Count == 0 ? source : source.WithChanges(changes);

    private static List<BracePair> GetBracePairs(SyntaxNode root, CancellationToken cancellationToken)
    {
        List<BracePair> pairs = [];
        Stack<SyntaxToken> openBraces = new();
        Stack<SyntaxNodeOrToken> pending = new();
        bool supportsMultilineInterpolations = SupportsMultilineInterpolations(root.SyntaxTree.Options);
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxNodeOrToken item = pending.Pop();
            if (item.IsNode)
            {
                SyntaxNode node = item.AsNode()!;
                if (!supportsMultilineInterpolations && node is InterpolationSyntax
                    || node is PropertyPatternClauseSyntax { Subpatterns.Count: 0 })
                {
                    continue;
                }

                ChildSyntaxList children = node.ChildNodesAndTokens();
                for (int index = children.Count - 1; index >= 0; index--)
                {
                    pending.Push(children[index]);
                }

                continue;
            }

            SyntaxToken token = item.AsToken();
            if (token.IsMissing || token.Parent is InterpolationSyntax)
            {
                continue;
            }

            if (token.IsKind(SyntaxKind.OpenBraceToken))
            {
                openBraces.Push(token);
            }
            else if (token.IsKind(SyntaxKind.CloseBraceToken) && openBraces.Count > 0)
            {
                SyntaxToken openBrace = openBraces.Pop();
                int parentOpenBracePosition = openBraces.Count > 0
                    ? openBraces.Peek().SpanStart
                    : -1;
                pairs.Add(new(openBrace, token, parentOpenBracePosition));
            }
        }

        pairs.Sort(static (left, right) => left.OpenBrace.SpanStart.CompareTo(right.OpenBrace.SpanStart));
        return pairs;
    }

    private static bool SupportsMultilineInterpolations(ParseOptions parseOptions)
    {
        LanguageVersion languageVersion = ((CSharpParseOptions)parseOptions).LanguageVersion;
        return languageVersion is LanguageVersion.Default or >= LanguageVersion.CSharp11;
    }

    private static string GetBraceIndentation(
        SourceText source,
        BracePair pair,
        Dictionary<int, string> braceIndentations,
        string indentation,
        Dictionary<int, LineLayout> lineLayouts)
    {
        TextLine openLine = source.Lines.GetLineFromPosition(pair.OpenBrace.SpanStart);
        LineLayout openLayout = GetLineLayout(source, openLine, lineLayouts);
        if (openLayout.FirstNonWhitespace == pair.OpenBrace.SpanStart)
        {
            return source.ToString(TextSpan.FromBounds(openLine.Start, openLayout.IndentationEnd));
        }

        if (pair.ParentOpenBracePosition >= 0
            && source.Lines.GetLineFromPosition(pair.ParentOpenBracePosition).LineNumber
                == source.Lines.GetLineFromPosition(pair.OpenBrace.SpanStart).LineNumber
            && braceIndentations.TryGetValue(pair.ParentOpenBracePosition, out string? parentIndentation))
        {
            return parentIndentation + indentation;
        }

        SyntaxNode owner = pair.OpenBrace.Parent!;
        SyntaxToken anchor = owner.GetFirstToken();
        while (anchor == pair.OpenBrace && owner.Parent is SyntaxNode parent)
        {
            owner = parent;
            anchor = parent.GetFirstToken();
        }

        TextLine line = source.Lines.GetLineFromPosition(anchor.SpanStart);
        LineLayout layout = GetLineLayout(source, line, lineLayouts);
        return source.ToString(TextSpan.FromBounds(line.Start, layout.IndentationEnd));
    }

    private static int GetBraceIndentationLength(
        SourceText source,
        BracePair pair,
        Dictionary<int, int> braceIndentationLengths,
        int indentationLength,
        Dictionary<int, LineLayout> lineLayouts)
    {
        TextLine openLine = source.Lines.GetLineFromPosition(pair.OpenBrace.SpanStart);
        LineLayout openLayout = GetLineLayout(source, openLine, lineLayouts);
        if (openLayout.FirstNonWhitespace == pair.OpenBrace.SpanStart)
        {
            return openLayout.IndentationEnd - openLine.Start;
        }

        if (pair.ParentOpenBracePosition >= 0
            && source.Lines.GetLineFromPosition(pair.ParentOpenBracePosition).LineNumber
                == source.Lines.GetLineFromPosition(pair.OpenBrace.SpanStart).LineNumber
            && braceIndentationLengths.TryGetValue(pair.ParentOpenBracePosition, out int parentLength))
        {
            return AddWithoutOverflow(parentLength, indentationLength);
        }

        SyntaxNode owner = pair.OpenBrace.Parent!;
        SyntaxToken anchor = owner.GetFirstToken();
        while (anchor == pair.OpenBrace && owner.Parent is SyntaxNode parent)
        {
            owner = parent;
            anchor = parent.GetFirstToken();
        }

        TextLine line = source.Lines.GetLineFromPosition(anchor.SpanStart);
        return GetLineLayout(source, line, lineLayouts).IndentationEnd - line.Start;
    }

    private static int AddWithoutOverflow(int left, int right) =>
        left > int.MaxValue - right ? int.MaxValue : left + right;

    private static void AddProjectedCharacters(ref long projectedCharacters, long additionalCharacters)
    {
        if (projectedCharacters > MaximumAddedCharacters)
        {
            return;
        }

        projectedCharacters = additionalCharacters > MaximumAddedCharacters - projectedCharacters
            ? MaximumAddedCharacters + 1L
            : projectedCharacters + additionalCharacters;
    }

    private static string GetLineIndentation(
        SourceText source,
        int position,
        Dictionary<int, LineLayout> lineLayouts)
    {
        TextLine line = source.Lines.GetLineFromPosition(position);
        LineLayout layout = GetLineLayout(source, line, lineLayouts);
        return source.ToString(TextSpan.FromBounds(line.Start, layout.IndentationEnd));
    }

    private static LineLayout GetLineLayout(
        SourceText source,
        TextLine line,
        Dictionary<int, LineLayout> lineLayouts)
    {
        if (lineLayouts.TryGetValue(line.LineNumber, out LineLayout cached))
        {
            return cached;
        }

        int firstNonWhitespace = line.Start;
        while (firstNonWhitespace < line.End && char.IsWhiteSpace(source[firstNonWhitespace]))
        {
            firstNonWhitespace++;
        }

        int lastNonWhitespaceExclusive = line.End;
        while (lastNonWhitespaceExclusive > firstNonWhitespace
            && char.IsWhiteSpace(source[lastNonWhitespaceExclusive - 1]))
        {
            lastNonWhitespaceExclusive--;
        }

        int indentationEnd = line.Start;
        while (indentationEnd < line.End && IsHorizontalWhitespace(source[indentationEnd]))
        {
            indentationEnd++;
        }

        bool containsHash = false;
        for (int index = firstNonWhitespace; index < lastNonWhitespaceExclusive; index++)
        {
            if (source[index] == '#')
            {
                containsHash = true;
                break;
            }
        }

        LineLayout layout = new(
            firstNonWhitespace,
            lastNonWhitespaceExclusive,
            indentationEnd,
            containsHash);
        lineLayouts.Add(line.LineNumber, layout);
        return layout;
    }

    private static string GetLineBreak(
        SourceText source,
        TextLine line,
        string fallbackLineBreak)
    {
        if (line.EndIncludingLineBreak > line.End)
        {
            return source.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
        }

        return fallbackLineBreak;
    }

    private static string GetFallbackLineBreak(SourceText source)
    {
        foreach (TextLine candidate in source.Lines)
        {
            if (candidate.EndIncludingLineBreak > candidate.End)
            {
                return source.ToString(TextSpan.FromBounds(candidate.End, candidate.EndIncludingLineBreak));
            }
        }

        return "\n";
    }

    private static void AddBreakBefore(
        SourceText source,
        SyntaxToken token,
        string lineBreak,
        string indentation,
        Dictionary<TextSpan, string> replacements)
    {
        TextLine line = source.Lines.GetLineFromPosition(token.SpanStart);
        int start = token.SpanStart;
        while (start > line.Start && IsHorizontalWhitespace(source[start - 1]))
        {
            start--;
        }

        AddReplacement(
            TextSpan.FromBounds(start, token.SpanStart),
            lineBreak + indentation,
            replacements);
    }

    private static void AddBreakAfter(
        SourceText source,
        SyntaxToken token,
        string lineBreak,
        string indentation,
        Dictionary<TextSpan, string> replacements)
    {
        TextLine line = source.Lines.GetLineFromPosition(token.Span.End);
        int end = token.Span.End;
        while (end < line.End && IsHorizontalWhitespace(source[end]))
        {
            end++;
        }

        AddReplacement(
            TextSpan.FromBounds(token.Span.End, end),
            lineBreak + indentation,
            replacements);
    }

    private static void AddReplacement(
        TextSpan span,
        string replacement,
        Dictionary<TextSpan, string> replacements)
    {
        if (replacements.ContainsKey(span))
        {
            return;
        }

        replacements.Add(span, replacement);
    }

    private static bool ContainsNonWhitespace(SourceText source, TextSpan span)
    {
        for (int index = span.Start; index < span.End; index++)
        {
            if (!char.IsWhiteSpace(source[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWhitespaceOnly(SourceText source, TextSpan span) =>
        !ContainsNonWhitespace(source, span);

    private static bool IsHorizontalWhitespace(char value) => value is ' ' or '\t';

    private static void RecordViolation(
        TextSpan candidate,
        ref bool foundViolation,
        ref TextSpan firstViolation)
    {
        if (!foundViolation || candidate.Start < firstViolation.Start)
        {
            firstViolation = candidate;
            foundViolation = true;
        }
    }

    private readonly struct BracePair
    {
        public BracePair(
            SyntaxToken openBrace,
            SyntaxToken closeBrace,
            int parentOpenBracePosition)
        {
            OpenBrace = openBrace;
            CloseBrace = closeBrace;
            ParentOpenBracePosition = parentOpenBracePosition;
        }

        public SyntaxToken OpenBrace { get; }

        public SyntaxToken CloseBrace { get; }

        public int ParentOpenBracePosition { get; }
    }

    private sealed class PreprocessorLineMap
    {
        private readonly Dictionary<int, PreprocessorLineKind> _directiveLines = [];
        private readonly List<LineRange> _disabledRanges = [];

        public PreprocessorLineMap(
            SourceText source,
            SyntaxNode root,
            CancellationToken cancellationToken)
        {
            foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (trivia.GetStructure() is DirectiveTriviaSyntax directive)
                {
                    int lineNumber = source.Lines.GetLineFromPosition(directive.SpanStart).LineNumber;
                    _directiveLines[lineNumber] = directive.Kind() is
                        SyntaxKind.ElseDirectiveTrivia or SyntaxKind.ElifDirectiveTrivia
                            ? PreprocessorLineKind.AlternateBranch
                            : PreprocessorLineKind.Directive;
                    continue;
                }

                if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia) || trivia.Span.IsEmpty)
                {
                    continue;
                }

                int firstLine = source.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
                int finalPosition = Math.Max(trivia.SpanStart, trivia.Span.End - 1);
                int finalLine = source.Lines.GetLineFromPosition(finalPosition).LineNumber;
                if (_disabledRanges.Count > 0)
                {
                    LineRange previous = _disabledRanges[_disabledRanges.Count - 1];
                    if (firstLine <= previous.FinalLine + 1)
                    {
                        _disabledRanges[_disabledRanges.Count - 1] = new(
                            previous.FirstLine,
                            Math.Max(previous.FinalLine, finalLine));
                        continue;
                    }
                }

                _disabledRanges.Add(new(firstLine, finalLine));
            }
        }

        public PreprocessorLineKind GetKind(int lineNumber)
        {
            if (_directiveLines.TryGetValue(lineNumber, out PreprocessorLineKind kind))
            {
                return kind;
            }

            int lower = 0;
            int upper = _disabledRanges.Count - 1;
            while (lower <= upper)
            {
                int middle = lower + ((upper - lower) / 2);
                LineRange range = _disabledRanges[middle];
                if (lineNumber < range.FirstLine)
                {
                    upper = middle - 1;
                }
                else if (lineNumber > range.FinalLine)
                {
                    lower = middle + 1;
                }
                else
                {
                    return PreprocessorLineKind.DisabledText;
                }
            }

            return PreprocessorLineKind.None;
        }

        private readonly struct LineRange
        {
            public LineRange(int firstLine, int finalLine)
            {
                FirstLine = firstLine;
                FinalLine = finalLine;
            }

            public int FirstLine { get; }

            public int FinalLine { get; }
        }
    }

    private enum PreprocessorLineKind
    {
        None,
        Directive,
        AlternateBranch,
        DisabledText
    }

    private readonly struct LineLayout
    {
        public LineLayout(
            int firstNonWhitespace,
            int lastNonWhitespaceExclusive,
            int indentationEnd,
            bool containsHash)
        {
            FirstNonWhitespace = firstNonWhitespace;
            LastNonWhitespaceExclusive = lastNonWhitespaceExclusive;
            IndentationEnd = indentationEnd;
            ContainsHash = containsHash;
        }

        public int FirstNonWhitespace { get; }

        public int LastNonWhitespaceExclusive { get; }

        public int IndentationEnd { get; }

        public bool ContainsHash { get; }
    }

    private readonly struct BlankLineChange
    {
        public BlankLineChange(
            TextSpan span,
            TextLine line,
            bool movesSameLineSuccessor)
        {
            Span = span;
            Line = line;
            MovesSameLineSuccessor = movesSameLineSuccessor;
        }

        public TextSpan Span { get; }

        public TextLine Line { get; }

        public bool MovesSameLineSuccessor { get; }
    }
}