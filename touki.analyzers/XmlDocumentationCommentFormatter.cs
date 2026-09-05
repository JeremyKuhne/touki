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

/// <summary>
///  Produces the canonical text for one structured single-line XML documentation comment.
/// </summary>
internal static partial class XmlDocumentationCommentFormatter
{
    internal const int MaximumCommentLength = 1024 * 1024;
    internal const int MaximumReplacementLength = 4 * 1024 * 1024;
    private const int MaximumIndentationColumns = 4096;
    private const int MaximumStructuredNodeCount = 4096;
    private const int MaximumXmlNestingDepth = 128;

    public static bool TryFormat(
        SourceText source,
        DocumentationCommentTriviaSyntax documentation,
        TextSpan commentSpan,
        string newLine,
        int indentSize,
        int maxLineLength,
        CancellationToken cancellationToken,
        out string replacement)
    {
        if (commentSpan.Length > MaximumCommentLength)
        {
            replacement = string.Empty;
            return false;
        }

        List<XmlElementSyntax> elements = [];
        List<XmlEmptyElementSyntax> emptyElements = [];
        List<XmlCDataSectionSyntax> cdataSections = [];
        List<XmlTextAttributeSyntax> textAttributes = [];
        HashSet<XmlElementSyntax> preservedElements = [];
        int nodeCount = 0;

        foreach (SyntaxNode node in documentation.DescendantNodes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (++nodeCount > MaximumStructuredNodeCount)
            {
                replacement = string.Empty;
                return false;
            }

            if (node is XmlElementSyntax element)
            {
                if (GetXmlNestingDepth(element) > MaximumXmlNestingDepth)
                {
                    replacement = string.Empty;
                    return false;
                }

                elements.Add(element);
                if (IsEffectiveXmlSpacePreserve(source, element, preservedElements))
                {
                    preservedElements.Add(element);
                }
            }
            else if (node is XmlEmptyElementSyntax emptyElement)
            {
                emptyElements.Add(emptyElement);
            }
            else if (node is XmlCDataSectionSyntax cdata)
            {
                cdataSections.Add(cdata);
            }
            else if (node is XmlTextAttributeSyntax textAttribute)
            {
                textAttributes.Add(textAttribute);
            }
        }

        List<TextChange> changes = [];
        List<TextSpan> compactedSpans = [];
        Dictionary<int, int> exteriorIndexCache = [];
        Dictionary<int, int> contentStartCache = [];
        HashSet<int> crowdedTopLevelLines = GetCrowdedTopLevelLines(
            source,
            elements,
            emptyElements);
        long projectedLength = commentSpan.Length;

        foreach (XmlElementSyntax element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (preservedElements.Contains(element)
                || HasElementAncestor(element)
                || crowdedTopLevelLines.Contains(GetLineNumber(source, element.SpanStart))
                || !TryCreateCompactChange(
                    source,
                    element,
                    maxLineLength,
                    exteriorIndexCache,
                    out TextChange change))
            {
                continue;
            }

            if (!TryReserveChange(change.NewText!.Length, change.Span.Length, ref projectedLength))
            {
                replacement = string.Empty;
                return false;
            }

            changes.Add(change);
            compactedSpans.Add(element.Span);
        }

        compactedSpans.Sort(static (left, right) => left.Start.CompareTo(right.Start));

        List<TextSpan> opaqueSpans = GetOpaqueSpans(elements, cdataSections);
        Dictionary<int, BreakBoundary> boundaries = [];
        HashSet<TextSpan> generatedProseBlocks = [];

        foreach (XmlElementSyntax element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsWithinAny(element.SpanStart, compactedSpans)
                || preservedElements.Contains(element)
                || HasCodeAncestor(element))
            {
                continue;
            }

            string name = element.StartTag.Name.LocalName.ValueText;
            int depth = GetBlockAncestorDepth(element);

            if (string.Equals(name, "code", StringComparison.Ordinal))
            {
                if (!IsInPreservedElement(element.Parent, preservedElements))
                {
                    AddOpaqueNodeBoundaries(
                        source,
                        element,
                        depth,
                        boundaries,
                        generatedProseBlocks,
                        exteriorIndexCache,
                        contentStartCache);
                }

                continue;
            }

            bool isTopLevel = depth == 0;
            bool isInline = IsInlineElement(name);
            if (!isTopLevel && isInline)
            {
                continue;
            }

            bool simpleTopLevel = isTopLevel
                && !string.Equals(name, "summary", StringComparison.Ordinal)
                && HasOnlyInlineContent(element);

            if (simpleTopLevel && IsSingleLine(source, element))
            {
                if (ExistingLineFits(source, element, maxLineLength))
                {
                    continue;
                }

                int lineNumber = GetLineNumber(source, element.SpanStart);
                if (crowdedTopLevelLines.Contains(lineNumber)
                    && TryKeepCrowdedElementCompact(
                        source,
                        element,
                        maxLineLength,
                        exteriorIndexCache,
                        contentStartCache,
                        boundaries,
                        changes,
                        ref projectedLength))
                {
                    continue;
                }
            }

            AddBlockElementBoundaries(
                source,
                element,
                depth,
                preserveExterior: IsInPreservedElement(element.Parent, preservedElements),
                boundaries,
                generatedProseBlocks,
                exteriorIndexCache,
                contentStartCache);
        }

        foreach (XmlCDataSectionSyntax cdata in cdataSections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsWithinAny(cdata.SpanStart, compactedSpans)
                && !IsInPreservedElement(cdata, preservedElements)
                && !HasCodeAncestor(cdata))
            {
                AddOpaqueNodeBoundaries(
                    source,
                    cdata,
                    GetBlockAncestorDepth(cdata),
                    boundaries,
                    generatedProseBlocks,
                    exteriorIndexCache,
                    contentStartCache);
            }
        }

        foreach (BreakBoundary boundary in boundaries.Values)
        {
            if (!TryCreateBoundaryChange(
                source,
                boundary,
                newLine,
                indentSize,
                exteriorIndexCache,
                ref projectedLength,
                out TextChange change))
            {
                replacement = string.Empty;
                return false;
            }

            changes.Add(change);
        }

        List<TextSpan> structuralChangeSpans = new(changes.Count);
        foreach (TextChange change in changes)
        {
            structuralChangeSpans.Add(change.Span);
        }

        structuralChangeSpans.Sort(static (left, right) =>
        {
            int result = left.Start.CompareTo(right.Start);
            return result != 0 ? result : left.Length.CompareTo(right.Length);
        });

        HashSet<int> protectedAttributeValueLines = GetProtectedAttributeValueLines(source, textAttributes);
        HashSet<int> cdataDelimiterLines = GetMultilineCDataDelimiterLines(
            source,
            cdataSections,
            exteriorIndexCache,
            contentStartCache);

        if (!TryAddIndentationChanges(
            source,
            documentation,
            commentSpan,
            indentSize,
            opaqueSpans,
            cdataDelimiterLines,
            protectedAttributeValueLines,
            preservedElements,
            generatedProseBlocks,
            structuralChangeSpans,
            exteriorIndexCache,
            changes,
            ref projectedLength,
            cancellationToken))
        {
            replacement = string.Empty;
            return false;
        }

        if (changes.Count == 0)
        {
            replacement = string.Empty;
            return false;
        }

        changes.Sort(static (left, right) =>
        {
            int result = left.Span.Start.CompareTo(right.Span.Start);
            return result != 0 ? result : left.Span.Length.CompareTo(right.Span.Length);
        });

        if (!AreChangesIndependent(changes, commentSpan))
        {
            replacement = string.Empty;
            return false;
        }

        SourceText comment = SourceText.From(source.ToString(commentSpan));
        TextChange[] relativeChanges = new TextChange[changes.Count];
        for (int index = 0; index < changes.Count; index++)
        {
            TextChange change = changes[index];
            relativeChanges[index] = new(
                new TextSpan(change.Span.Start - commentSpan.Start, change.Span.Length),
                change.NewText!);
        }

        replacement = comment.WithChanges(relativeChanges).ToString();
        return !string.Equals(comment.ToString(), replacement, StringComparison.Ordinal);
    }

    /// <summary>
    ///  Finds the zero-based column where the <c>///</c> documentation-comment exterior begins after leading
    ///  source indentation. The index is relative to <see cref="TextLine.Start"/>; when the line does not start
    ///  with a documentation-comment exterior, returns <see langword="false"/> and sets the index to <c>-1</c>.
    /// </summary>
    internal static bool TryGetExteriorIndex(SourceText source, TextLine line, out int exteriorIndex)
    {
        int index = line.Start;
        while (index < line.End && char.IsWhiteSpace(source[index]))
        {
            index++;
        }

        if (index + 2 < line.End
            && source[index] == '/'
            && source[index + 1] == '/'
            && source[index + 2] == '/')
        {
            exteriorIndex = index - line.Start;
            return true;
        }

        exteriorIndex = -1;
        return false;
    }

    private static HashSet<int> GetCrowdedTopLevelLines(
        SourceText source,
        List<XmlElementSyntax> elements,
        List<XmlEmptyElementSyntax> emptyElements)
    {
        Dictionary<int, int> counts = [];
        HashSet<int> crowded = [];

        foreach (XmlElementSyntax element in elements)
        {
            if (HasElementAncestor(element) || !IsSingleLine(source, element))
            {
                continue;
            }

            AddCrowdedLine(GetLineNumber(source, element.SpanStart), counts, crowded);
        }

        foreach (XmlEmptyElementSyntax element in emptyElements)
        {
            if (HasElementAncestor(element)
                || GetLineNumber(source, element.SpanStart) != GetLineNumber(source, element.Span.End - 1))
            {
                continue;
            }

            AddCrowdedLine(GetLineNumber(source, element.SpanStart), counts, crowded);
        }

        return crowded;
    }

    private static void AddCrowdedLine(
        int line,
        Dictionary<int, int> counts,
        HashSet<int> crowded)
    {
        if (counts.TryGetValue(line, out int count))
        {
            counts[line] = count + 1;
            crowded.Add(line);
        }
        else
        {
            counts.Add(line, 1);
        }
    }

    private static bool TryKeepCrowdedElementCompact(
        SourceText source,
        XmlElementSyntax element,
        int maxLineLength,
        Dictionary<int, int> exteriorIndexCache,
        Dictionary<int, int> contentStartCache,
        Dictionary<int, BreakBoundary> boundaries,
        List<TextChange> changes,
        ref long projectedLength)
    {
        string candidate = TryGetMeaningfulContentBounds(element, out SyntaxToken first, out SyntaxToken last)
            ? CreateCompactText(source, element, first, last)
            : source.ToString(element.Span).Trim();

        if (!FitsOnOwnLine(source, element, candidate, maxLineLength, exteriorIndexCache))
        {
            return false;
        }

        string current = source.ToString(element.Span);
        if (!string.Equals(current, candidate, StringComparison.Ordinal))
        {
            if (!TryReserveChange(candidate.Length, element.Span.Length, ref projectedLength))
            {
                return false;
            }

            changes.Add(new(element.Span, candidate));
        }

        AddBreakBeforeIfNeeded(
            source,
            element.SpanStart,
            depth: 0,
            boundaries,
            exteriorIndexCache,
            contentStartCache);
        AddBreakAfterIfNeeded(source, element.Span.End, depth: 0, boundaries);
        return true;
    }

    private static bool FitsOnOwnLine(
        SourceText source,
        XmlElementSyntax element,
        string candidate,
        int maxLineLength,
        Dictionary<int, int> exteriorIndexCache)
    {
        TextLine line = source.Lines.GetLineFromPosition(element.SpanStart);
        int suffixLength = HasOnlyWhitespaceAfter(source, element.Span.End)
            ? line.End - element.Span.End
            : 0;
        return TryGetExteriorIndex(source, line, exteriorIndexCache, out int exteriorIndex)
            && (long)exteriorIndex + 4 + candidate.Length + suffixLength <= maxLineLength;
    }

    private static bool TryCreateCompactChange(
        SourceText source,
        XmlElementSyntax element,
        int maxLineLength,
        Dictionary<int, int> exteriorIndexCache,
        out TextChange change)
    {
        string name = element.StartTag.Name.LocalName.ValueText;
        int startLine = GetLineNumber(source, element.StartTag.SpanStart);
        int endLine = GetLineNumber(source, element.EndTag.Span.End - 1);

        if (string.Equals(name, "summary", StringComparison.Ordinal)
            || string.Equals(name, "code", StringComparison.Ordinal)
            || !HasOnlyInlineContent(element)
            || !TryGetMeaningfulContentBounds(element, out SyntaxToken first, out SyntaxToken last))
        {
            change = default;
            return false;
        }

        string candidate = CreateCompactText(source, element, first, last);
        if (startLine == endLine)
        {
            string current = source.ToString(element.Span);
            if (ExistingLineFits(source, element, maxLineLength)
                || string.Equals(current, candidate, StringComparison.Ordinal)
                || !FitsOnLineAfterReplacement(
                    source,
                    element,
                    candidate,
                    maxLineLength,
                    exteriorIndexCache))
            {
                change = default;
                return false;
            }

            change = new(element.Span, candidate);
            return true;
        }

        if (endLine - startLine != 2
            || GetLineNumber(source, first.SpanStart) != startLine + 1
            || GetLineNumber(source, last.Span.End - 1) != startLine + 1
            || !LineContainsOnly(source, element.StartTag.Span)
            || !LineContainsOnly(source, TextSpan.FromBounds(first.SpanStart, last.Span.End))
            || !LineContainsOnly(source, element.EndTag.Span))
        {
            change = default;
            return false;
        }

        if (!FitsOnLineAfterReplacement(
            source,
            element,
            candidate,
            maxLineLength,
            exteriorIndexCache))
        {
            change = default;
            return false;
        }

        change = new(element.Span, candidate);
        return true;
    }

    private static void AddBlockElementBoundaries(
        SourceText source,
        XmlElementSyntax element,
        int depth,
        bool preserveExterior,
        Dictionary<int, BreakBoundary> boundaries,
        HashSet<TextSpan> generatedProseBlocks,
        Dictionary<int, int> exteriorIndexCache,
        Dictionary<int, int> contentStartCache)
    {
        if (!preserveExterior)
        {
            AddBreakBeforeIfNeeded(
                source,
                element.StartTag.SpanStart,
                depth,
                boundaries,
                exteriorIndexCache,
                contentStartCache);
            if (AddBreakAfterIfNeeded(source, element.EndTag.Span.End, depth, boundaries)
                && TryGetFollowingProseBlockSpan(element, out TextSpan followingProseBlock))
            {
                generatedProseBlocks.Add(followingProseBlock);
            }
        }

        if (!TryGetMeaningfulContentBounds(element, out SyntaxToken first, out SyntaxToken last))
        {
            if (GetLineNumber(source, element.StartTag.Span.End - 1)
                == GetLineNumber(source, element.EndTag.SpanStart))
            {
                AddBoundary(source, element.StartTag.Span.End, depth, includeBlankLine: true, boundaries);
            }

            return;
        }

        if (GetLineNumber(source, element.StartTag.Span.End - 1)
            == GetLineNumber(source, first.SpanStart))
        {
            AddBoundary(source, element.StartTag.Span.End, depth + 1, includeBlankLine: false, boundaries);
            if (TryGetProseBlockSpan(first, out TextSpan generatedProseBlock))
            {
                generatedProseBlocks.Add(generatedProseBlock);
            }
        }

        if (GetLineNumber(source, last.Span.End - 1)
            == GetLineNumber(source, element.EndTag.SpanStart))
        {
            AddBoundary(source, element.EndTag.SpanStart, depth, includeBlankLine: false, boundaries);
        }

    }

    private static void AddOpaqueNodeBoundaries(
        SourceText source,
        XmlNodeSyntax node,
        int depth,
        Dictionary<int, BreakBoundary> boundaries,
        HashSet<TextSpan> generatedProseBlocks,
        Dictionary<int, int> exteriorIndexCache,
        Dictionary<int, int> contentStartCache)
    {
        AddBreakBeforeIfNeeded(
            source,
            node.SpanStart,
            depth,
            boundaries,
            exteriorIndexCache,
            contentStartCache);
        if (AddBreakAfterIfNeeded(source, node.Span.End, depth, boundaries)
            && TryGetFollowingProseBlockSpan(node, out TextSpan followingProseBlock))
        {
            generatedProseBlocks.Add(followingProseBlock);
        }
    }

    private static void AddBreakBeforeIfNeeded(
        SourceText source,
        int position,
        int depth,
        Dictionary<int, BreakBoundary> boundaries,
        Dictionary<int, int> exteriorIndexCache,
        Dictionary<int, int> contentStartCache)
    {
        if (!HasOnlyWhitespaceBefore(
            source,
            position,
            exteriorIndexCache,
            contentStartCache))
        {
            AddBoundary(source, position, depth, includeBlankLine: false, boundaries);
        }
    }

    private static bool AddBreakAfterIfNeeded(
        SourceText source,
        int position,
        int depth,
        Dictionary<int, BreakBoundary> boundaries)
    {
        if (HasOnlyWhitespaceAfter(source, position))
        {
            return false;
        }

        AddBoundary(source, position, depth, includeBlankLine: false, boundaries);
        return true;
    }

    private static void AddBoundary(
        SourceText source,
        int position,
        int depth,
        bool includeBlankLine,
        Dictionary<int, BreakBoundary> boundaries)
    {
        TextLine line = source.Lines.GetLineFromPosition(Math.Min(position, source.Length));
        int start = position;
        int end = position;

        while (start > line.Start && IsHorizontalWhitespace(source[start - 1]))
        {
            start--;
        }

        while (end < line.End && IsHorizontalWhitespace(source[end]))
        {
            end++;
        }

        if (boundaries.TryGetValue(start, out BreakBoundary? boundary))
        {
            boundary.End = Math.Max(boundary.End, end);
            boundary.Depth = Math.Min(boundary.Depth, depth);
            boundary.IncludeBlankLine |= includeBlankLine;
        }
        else
        {
            boundaries.Add(start, new(start, end, depth, includeBlankLine));
        }
    }

    private static bool TryAddIndentationChanges(
        SourceText source,
        DocumentationCommentTriviaSyntax documentation,
        TextSpan commentSpan,
        int indentSize,
        List<TextSpan> opaqueSpans,
        HashSet<int> cdataDelimiterLines,
        HashSet<int> protectedAttributeValueLines,
        HashSet<XmlElementSyntax> preservedElements,
        HashSet<TextSpan> generatedProseBlocks,
        List<TextSpan> structuralChangeSpans,
        Dictionary<int, int> exteriorIndexCache,
        List<TextChange> changes,
        ref long projectedLength,
        CancellationToken cancellationToken)
    {
        int firstLine = GetLineNumber(source, commentSpan.Start);
        int lastLine = GetLineNumber(source, Math.Max(commentSpan.Start, commentSpan.End - 1));
        Dictionary<int, string> indentationCache = [];
        int structuralChangeIndex = 0;
        int currentBlockStart = -1;
        int currentBlockKind = -1;
        int currentBlockIndentationDelta = 0;

        for (int lineNumber = firstLine; lineNumber <= lastLine; lineNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TextLine line = source.Lines[lineNumber];
            if (protectedAttributeValueLines.Contains(lineNumber)
                || !TryGetExteriorIndex(
                    source,
                    line,
                    exteriorIndexCache,
                    out int exteriorIndex))
            {
                currentBlockStart = -1;
                continue;
            }

            int whitespaceStart = line.Start + exteriorIndex + 3;
            int contentStart = whitespaceStart;
            while (contentStart < line.End && IsHorizontalWhitespace(source[contentStart]))
            {
                contentStart++;
            }

            int opacityProbe = contentStart < line.End ? contentStart : whitespaceStart;
            if (IsWithinAny(opacityProbe, opaqueSpans)
                && !cdataDelimiterLines.Contains(lineNumber))
            {
                currentBlockStart = -1;
                continue;
            }

            int tokenPosition = contentStart < line.End
                ? contentStart
                : Math.Min(line.End, Math.Max(commentSpan.Start, commentSpan.End - 1));
            SyntaxToken token = documentation.FindToken(tokenPosition, findInsideTrivia: true);
            if (IsIndentationOwnedByPreservedElement(token, preservedElements))
            {
                currentBlockStart = -1;
                continue;
            }

            TextSpan indentationSpan = TextSpan.FromBounds(whitespaceStart, contentStart);
            if (OverlapsStructuralChange(
                indentationSpan,
                structuralChangeSpans,
                ref structuralChangeIndex))
            {
                currentBlockStart = -1;
                continue;
            }

            if (contentStart == line.End)
            {
                currentBlockStart = -1;
                if (indentationSpan.Length > 0)
                {
                    if (!TryReserveChange(0, indentationSpan.Length, ref projectedLength))
                    {
                        return false;
                    }

                    changes.Add(new(indentationSpan, string.Empty));
                }

                continue;
            }

            SyntaxNode? tag = null;
            for (SyntaxNode? node = token.Parent; node is not null; node = node.Parent)
            {
                if (node is XmlElementStartTagSyntax or XmlElementEndTagSyntax or XmlEmptyElementSyntax)
                {
                    tag = node;
                    break;
                }
            }

            int blockStart = -1;
            int blockKind = 0;
            bool generatedProseBlock = false;

            if (tag is XmlElementStartTagSyntax startTag
                && startTag.Parent is XmlElementSyntax startElement
                && !IsInlineElement(startElement.StartTag.Name.LocalName.ValueText))
            {
                blockStart = startTag.SpanStart;
                blockKind = 1;
            }
            else if (tag is XmlElementEndTagSyntax endTag
                && endTag.Parent is XmlElementSyntax endElement
                && !IsInlineElement(endElement.StartTag.Name.LocalName.ValueText))
            {
                blockStart = endTag.SpanStart;
                blockKind = 2;
            }
            else if (tag is XmlEmptyElementSyntax emptyElement
                && !IsInlineElement(emptyElement.Name.LocalName.ValueText))
            {
                blockStart = emptyElement.SpanStart;
                blockKind = 3;
            }

            if (blockStart < 0)
            {
                if (TryGetProseBlockSpan(token, out TextSpan proseBlock))
                {
                    blockStart = proseBlock.Start;
                    generatedProseBlock = generatedProseBlocks.Contains(proseBlock);
                }
                else
                {
                    blockStart = documentation.SpanStart;
                }

                blockKind = 4;
            }

            if (blockStart != currentBlockStart || blockKind != currentBlockKind)
            {
                currentBlockStart = blockStart;
                currentBlockKind = blockKind;

                if (generatedProseBlock)
                {
                    currentBlockIndentationDelta = 0;
                }
                else
                {
                    int depth = GetIndentationDepth(source, lineNumber, token);
                    if (!TryGetIndentationCount(depth, indentSize, out int expectedIndentation))
                    {
                        return false;
                    }

                    currentBlockIndentationDelta = expectedIndentation - indentationSpan.Length;
                }
            }

            if (currentBlockIndentationDelta != 0)
            {
                int count = indentationSpan.Length + currentBlockIndentationDelta;
                if (count < 0)
                {
                    return false;
                }

                if (!TryReserveChange(count, indentationSpan.Length, ref projectedLength)
                    || !TryGetIndentation(count, indentationCache, out string indentation))
                {
                    return false;
                }

                changes.Add(new(indentationSpan, indentation));
            }
        }

        return true;
    }

    private static int GetIndentationDepth(SourceText source, int lineNumber, SyntaxToken token)
    {
        if (token.Parent is null)
        {
            return 0;
        }

        SyntaxNode? tag = null;
        for (SyntaxNode? node = token.Parent; node is not null; node = node.Parent)
        {
            if (node is XmlElementStartTagSyntax or XmlElementEndTagSyntax or XmlEmptyElementSyntax)
            {
                tag = node;
                break;
            }
        }

        XmlElementSyntax? taggedElement = tag is XmlElementStartTagSyntax or XmlElementEndTagSyntax
            ? tag.Parent as XmlElementSyntax
            : null;
        int depth = 0;

        for (SyntaxNode? node = token.Parent; node is not null; node = node.Parent)
        {
            if (node is XmlElementSyntax element
                && !ReferenceEquals(element, taggedElement)
                && !IsInlineElement(element.StartTag.Name.LocalName.ValueText))
            {
                depth++;
            }
        }

        SyntaxNode? attributeContainer = GetAttributeContainer(token.Parent);
        if (attributeContainer is not null
            && HasAttributeAncestor(token.Parent)
            && lineNumber > GetLineNumber(source, attributeContainer.SpanStart))
        {
            depth++;
        }

        return depth;
    }

    private static bool TryGetIndentation(
        int count,
        Dictionary<int, string> cache,
        out string indentation)
    {
        if (!cache.TryGetValue(count, out indentation!))
        {
            indentation = new(' ', count);
            cache.Add(count, indentation);
        }

        return true;
    }

    private static bool TryCreateBoundaryChange(
        SourceText source,
        BreakBoundary boundary,
        string newLine,
        int indentSize,
        Dictionary<int, int> exteriorIndexCache,
        ref long projectedLength,
        out TextChange change)
    {
        if (!TryGetIndentationCount(boundary.Depth, indentSize, out int indentationCount))
        {
            change = default;
            return false;
        }

        TextLine line = source.Lines.GetLineFromPosition(Math.Min(boundary.Start, source.Length));
        int codeIndentationLength = TryGetExteriorIndex(
            source,
            line,
            exteriorIndexCache,
            out int exteriorIndex)
            ? exteriorIndex
            : 0;
        long newTextLength = boundary.IncludeBlankLine
            ? (long)newLine.Length * 2 + (long)codeIndentationLength * 2 + 6 + indentationCount
            : (long)newLine.Length + codeIndentationLength + 3 + indentationCount;

        if (!TryReserveChange(
            newTextLength,
            boundary.End - boundary.Start,
            ref projectedLength))
        {
            change = default;
            return false;
        }

        string codeIndentation = codeIndentationLength == 0
            ? string.Empty
            : source.ToString(TextSpan.FromBounds(line.Start, line.Start + codeIndentationLength));
        string prefix = $"{codeIndentation}///{new string(' ', indentationCount)}";
        string newText = boundary.IncludeBlankLine
            ? $"{newLine}{codeIndentation}///{newLine}{prefix}"
            : $"{newLine}{prefix}";
        change = new(TextSpan.FromBounds(boundary.Start, boundary.End), newText);
        return true;
    }

    private static bool TryGetIndentationCount(int depth, int indentSize, out int count)
    {
        long columns = 1L + (long)depth * indentSize;
        if (columns > MaximumIndentationColumns)
        {
            count = 0;
            return false;
        }

        count = (int)columns;
        return true;
    }

    private static HashSet<int> GetProtectedAttributeValueLines(
        SourceText source,
        List<XmlTextAttributeSyntax> attributes)
    {
        HashSet<int> lines = [];

        foreach (XmlTextAttributeSyntax attribute in attributes)
        {
            int startLine = GetLineNumber(source, attribute.StartQuoteToken.Span.End);
            int endPosition = attribute.EndQuoteToken.IsMissing
                ? attribute.Span.End
                : attribute.EndQuoteToken.SpanStart;
            int endLine = GetLineNumber(source, Math.Max(attribute.StartQuoteToken.Span.End, endPosition));

            for (int line = startLine + 1; line <= endLine; line++)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static List<TextSpan> GetOpaqueSpans(
        List<XmlElementSyntax> elements,
        List<XmlCDataSectionSyntax> cdataSections)
    {
        List<TextSpan> spans = [];

        foreach (XmlElementSyntax element in elements)
        {
            if (string.Equals(element.StartTag.Name.LocalName.ValueText, "code", StringComparison.Ordinal)
                && element.StartTag.Span.End < element.EndTag.SpanStart)
            {
                spans.Add(TextSpan.FromBounds(element.StartTag.Span.End, element.EndTag.SpanStart));
            }
        }

        foreach (XmlCDataSectionSyntax cdata in cdataSections)
        {
            if (cdata.StartCDataToken.Span.End < cdata.EndCDataToken.SpanStart)
            {
                spans.Add(TextSpan.FromBounds(
                    cdata.StartCDataToken.Span.End,
                    cdata.EndCDataToken.SpanStart));
            }
        }

        spans.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        CoalesceSpans(spans);
        return spans;
    }

    private static HashSet<int> GetMultilineCDataDelimiterLines(
        SourceText source,
        List<XmlCDataSectionSyntax> cdataSections,
        Dictionary<int, int> exteriorIndexCache,
        Dictionary<int, int> contentStartCache)
    {
        HashSet<int> lines = [];

        foreach (XmlCDataSectionSyntax cdata in cdataSections)
        {
            int startLine = GetLineNumber(source, cdata.StartCDataToken.SpanStart);
            int endLine = GetLineNumber(source, cdata.EndCDataToken.SpanStart);
            if (startLine != endLine)
            {
                if (HasOnlyWhitespaceBefore(
                    source,
                    cdata.StartCDataToken.SpanStart,
                    exteriorIndexCache,
                    contentStartCache))
                {
                    lines.Add(startLine);
                }

                if (HasOnlyWhitespaceBefore(
                    source,
                    cdata.EndCDataToken.SpanStart,
                    exteriorIndexCache,
                    contentStartCache))
                {
                    lines.Add(endLine);
                }
            }
        }

        return lines;
    }

    private static void CoalesceSpans(List<TextSpan> spans)
    {
        if (spans.Count < 2)
        {
            return;
        }

        int writeIndex = 0;
        TextSpan current = spans[0];

        for (int readIndex = 1; readIndex < spans.Count; readIndex++)
        {
            TextSpan next = spans[readIndex];
            if (next.Start <= current.End)
            {
                current = TextSpan.FromBounds(current.Start, Math.Max(current.End, next.End));
                continue;
            }

            spans[writeIndex++] = current;
            current = next;
        }

        spans[writeIndex++] = current;
        if (writeIndex < spans.Count)
        {
            spans.RemoveRange(writeIndex, spans.Count - writeIndex);
        }
    }

    private static bool HasCodeAncestor(SyntaxNode node)
    {
        for (SyntaxNode? ancestorNode = node.Parent; ancestorNode is not null; ancestorNode = ancestorNode.Parent)
        {
            if (ancestorNode is XmlElementSyntax ancestor
                && string.Equals(
                    ancestor.StartTag.Name.LocalName.ValueText,
                    "code",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasElementAncestor(SyntaxNode element)
    {
        for (SyntaxNode? node = element.Parent; node is not null; node = node.Parent)
        {
            if (node is XmlElementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether whitespace preservation is effective for <paramref name="element"/>. An explicit
    ///  <c>xml:space="preserve"</c> enables preservation, <c>xml:space="default"</c> resets it, and no recognized
    ///  local value inherits the nearest parent element's effective mode.
    /// </summary>
    private static bool IsEffectiveXmlSpacePreserve(
        SourceText source,
        XmlElementSyntax element,
        HashSet<XmlElementSyntax> preservedElements)
    {
        foreach (XmlAttributeSyntax attribute in element.StartTag.Attributes)
        {
            if (attribute is not XmlTextAttributeSyntax textAttribute
                || !SourceTextEquals(source, textAttribute.Name.Span, "xml:space"))
            {
                continue;
            }

            if (AttributeValueEquals(textAttribute, "preserve"))
            {
                return true;
            }

            if (AttributeValueEquals(textAttribute, "default"))
            {
                return false;
            }

            break;
        }

        for (SyntaxNode? node = element.Parent; node is not null; node = node.Parent)
        {
            if (node is XmlElementSyntax parent)
            {
                return preservedElements.Contains(parent);
            }
        }

        return false;
    }

    private static bool IsInPreservedElement(
        SyntaxNode? node,
        HashSet<XmlElementSyntax> preservedElements)
    {
        for (SyntaxNode? ancestor = node; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is XmlElementSyntax element)
            {
                return preservedElements.Contains(element);
            }
        }

        return false;
    }

    private static bool IsIndentationOwnedByPreservedElement(
        SyntaxToken token,
        HashSet<XmlElementSyntax> preservedElements)
    {
        if (token.IsKind(SyntaxKind.LessThanToken))
        {
            if (token.Parent is XmlElementStartTagSyntax startTag
                && startTag.Parent is XmlElementSyntax element)
            {
                return IsInPreservedElement(element.Parent, preservedElements);
            }

            if (token.Parent is XmlEmptyElementSyntax emptyElement)
            {
                return IsInPreservedElement(emptyElement.Parent, preservedElements);
            }
        }

        return IsInPreservedElement(token.Parent, preservedElements);
    }

    private static bool AttributeValueEquals(XmlTextAttributeSyntax attribute, string value)
    {
        int valueIndex = 0;

        foreach (SyntaxToken token in attribute.TextTokens)
        {
            string tokenValue = token.ValueText;
            for (int tokenIndex = 0; tokenIndex < tokenValue.Length; tokenIndex++)
            {
                if (valueIndex >= value.Length || tokenValue[tokenIndex] != value[valueIndex++])
                {
                    return false;
                }
            }
        }

        return valueIndex == value.Length;
    }

    private static bool SourceTextEquals(SourceText source, TextSpan span, string value)
    {
        if (span.Length != value.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (source[span.Start + index] != value[index])
            {
                return false;
            }
        }

        return true;
    }

    private static int GetBlockAncestorDepth(SyntaxNode node)
    {
        int depth = 0;

        for (SyntaxNode? ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is XmlElementSyntax element
                && !IsInlineElement(element.StartTag.Name.LocalName.ValueText))
            {
                depth++;
            }
        }

        return depth;
    }

    private static int GetXmlNestingDepth(XmlElementSyntax element)
    {
        int depth = 1;

        for (SyntaxNode? node = element.Parent; node is not null; node = node.Parent)
        {
            if (node is XmlElementSyntax)
            {
                depth++;
            }
        }

        return depth;
    }

    private static bool HasOnlyInlineContent(XmlElementSyntax element)
    {
        foreach (XmlNodeSyntax content in element.Content)
        {
            switch (content)
            {
                case XmlTextSyntax:
                    break;
                case XmlEmptyElementSyntax emptyElement when IsInlineElement(emptyElement.Name.LocalName.ValueText):
                    break;
                case XmlElementSyntax child when IsInlineElement(child.StartTag.Name.LocalName.ValueText)
                    && HasOnlyInlineContent(child):
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryGetProseBlockSpan(SyntaxToken token, out TextSpan span)
    {
        for (SyntaxNode? node = token.Parent; node is not null; node = node.Parent)
        {
            if (node is not XmlNodeSyntax content)
            {
                continue;
            }

            if (content.Parent is DocumentationCommentTriviaSyntax documentation)
            {
                return TryGetProseBlockSpan(documentation.Content, content, out span);
            }

            if (content.Parent is XmlElementSyntax element
                && !IsInlineElement(element.StartTag.Name.LocalName.ValueText))
            {
                return TryGetProseBlockSpan(element.Content, content, out span);
            }
        }

        span = default;
        return false;
    }

    private static bool TryGetProseBlockSpan(
        SyntaxList<XmlNodeSyntax> content,
        XmlNodeSyntax target,
        out TextSpan span)
    {
        int targetIndex = GetContentIndex(content, target);
        if (targetIndex < 0 || !IsInlineContent(content[targetIndex]))
        {
            span = default;
            return false;
        }

        int startIndex = targetIndex;
        while (startIndex > 0 && IsInlineContent(content[startIndex - 1]))
        {
            startIndex--;
        }

        int endIndex = targetIndex;
        while (endIndex + 1 < content.Count && IsInlineContent(content[endIndex + 1]))
        {
            endIndex++;
        }

        span = TextSpan.FromBounds(content[startIndex].SpanStart, content[endIndex].Span.End);
        return true;
    }

    private static bool TryGetFollowingProseBlockSpan(XmlNodeSyntax node, out TextSpan span)
    {
        SyntaxList<XmlNodeSyntax> content;

        if (node.Parent is DocumentationCommentTriviaSyntax documentation)
        {
            content = documentation.Content;
        }
        else if (node.Parent is XmlElementSyntax element)
        {
            content = element.Content;
        }
        else
        {
            span = default;
            return false;
        }

        int nodeIndex = GetContentIndex(content, node);
        span = default;
        return nodeIndex >= 0
            && nodeIndex + 1 < content.Count
            && TryGetProseBlockSpan(content, content[nodeIndex + 1], out span);
    }

    private static int GetContentIndex(SyntaxList<XmlNodeSyntax> content, XmlNodeSyntax target)
    {
        for (int index = 0; index < content.Count; index++)
        {
            XmlNodeSyntax candidate = content[index];
            if (candidate.RawKind == target.RawKind && candidate.Span == target.Span)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsInlineContent(XmlNodeSyntax content) => content switch
    {
        XmlTextSyntax => true,
        XmlEmptyElementSyntax emptyElement => IsInlineElement(emptyElement.Name.LocalName.ValueText),
        XmlElementSyntax element => IsInlineElement(element.StartTag.Name.LocalName.ValueText)
            && HasOnlyInlineContent(element),
        _ => false
    };

    private static bool IsInlineElement(string name) => name is
        "a" or "b" or "br" or "c" or "em" or "i" or "paramref" or "see" or "strong" or "sub" or "sup"
        or "typeparamref" or "u";

    private static bool TryGetMeaningfulContentBounds(
        XmlElementSyntax element,
        out SyntaxToken first,
        out SyntaxToken last)
    {
        first = default;
        last = default;

        foreach (XmlNodeSyntax content in element.Content)
        {
            foreach (SyntaxToken token in content.DescendantTokens())
            {
                if (token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken)
                    || token.IsKind(SyntaxKind.XmlTextLiteralToken) && IsLayoutWhitespace(token.ValueText))
                {
                    continue;
                }

                if (first == default)
                {
                    first = token;
                }

                last = token;
            }
        }

        return first != default;
    }

    private static bool IsLayoutWhitespace(string value)
    {
        foreach (char character in value)
        {
            if (!IsHorizontalWhitespace(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ExistingLineFits(SourceText source, XmlElementSyntax element, int maxLineLength)
    {
        TextLine line = source.Lines.GetLineFromPosition(element.SpanStart);
        return (long)line.End - line.Start <= maxLineLength;
    }

    private static bool FitsOnLineAfterReplacement(
        SourceText source,
        XmlElementSyntax element,
        string candidate,
        int maxLineLength,
        Dictionary<int, int> exteriorIndexCache)
    {
        TextLine startLine = source.Lines.GetLineFromPosition(element.SpanStart);
        TextLine endLine = source.Lines.GetLineFromPosition(Math.Max(element.SpanStart, element.Span.End - 1));
        long prefixLength = element.SpanStart - startLine.Start;

        if (TryGetExteriorIndex(source, startLine, exteriorIndexCache, out int exteriorIndex)
            && ContainsOnlyHorizontalWhitespace(
                source,
                startLine.Start + exteriorIndex + 3,
                element.SpanStart))
        {
            prefixLength = exteriorIndex + 4L;
        }

        long suffixLength = endLine.End - element.Span.End;
        return prefixLength + candidate.Length + suffixLength <= maxLineLength;
    }

    private static string CreateCompactText(
        SourceText source,
        XmlElementSyntax element,
        SyntaxToken first,
        SyntaxToken last) =>
        $"{source.ToString(element.StartTag.Span)}"
        + GetHorizontallyTrimmedText(source, TextSpan.FromBounds(first.SpanStart, last.Span.End))
        + source.ToString(element.EndTag.Span);

    private static string GetHorizontallyTrimmedText(SourceText source, TextSpan span)
    {
        int start = span.Start;
        int end = span.End;

        while (start < end && IsHorizontalWhitespace(source[start]))
        {
            start++;
        }

        while (end > start && IsHorizontalWhitespace(source[end - 1]))
        {
            end--;
        }

        return source.ToString(TextSpan.FromBounds(start, end));
    }

    private static bool IsSingleLine(SourceText source, XmlElementSyntax element) =>
        GetLineNumber(source, element.SpanStart) == GetLineNumber(source, element.Span.End - 1);

    private static bool LineContainsOnly(SourceText source, TextSpan span) =>
        HasOnlyWhitespaceBefore(source, span.Start) && HasOnlyWhitespaceAfter(source, span.End);

    private static bool HasOnlyWhitespaceBefore(SourceText source, int position)
    {
        TextLine line = source.Lines.GetLineFromPosition(position);
        if (!TryGetExteriorIndex(source, line, out int exteriorIndex))
        {
            return false;
        }

        int start = line.Start + exteriorIndex + 3;
        for (int index = start; index < position; index++)
        {
            if (!IsHorizontalWhitespace(source[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasOnlyWhitespaceBefore(
        SourceText source,
        int position,
        Dictionary<int, int> exteriorIndexCache,
        Dictionary<int, int> contentStartCache)
    {
        TextLine line = source.Lines.GetLineFromPosition(position);
        if (!TryGetExteriorIndex(source, line, exteriorIndexCache, out int exteriorIndex))
        {
            return false;
        }

        if (!contentStartCache.TryGetValue(line.LineNumber, out int contentStart))
        {
            contentStart = line.Start + exteriorIndex + 3;
            while (contentStart < line.End && IsHorizontalWhitespace(source[contentStart]))
            {
                contentStart++;
            }

            contentStartCache.Add(line.LineNumber, contentStart);
        }

        return position <= contentStart;
    }

    private static bool HasOnlyWhitespaceAfter(SourceText source, int position)
    {
        TextLine line = source.Lines.GetLineFromPosition(Math.Min(position, source.Length));
        for (int index = position; index < line.End; index++)
        {
            if (!IsHorizontalWhitespace(source[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWithinAny(int position, List<TextSpan> spans)
    {
        int low = 0;
        int high = spans.Count - 1;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            TextSpan span = spans[middle];
            if (span.Contains(position))
            {
                return true;
            }

            if (position < span.Start)
            {
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return false;
    }

    private static bool OverlapsStructuralChange(
        TextSpan span,
        List<TextSpan> structuralChanges,
        ref int index)
    {
        while (index < structuralChanges.Count)
        {
            TextSpan change = structuralChanges[index];
            if (change.End < span.Start
                || change.End == span.Start && !change.IsEmpty)
            {
                index++;
                continue;
            }

            return span.OverlapsWith(change)
                || span.Start == change.Start && (span.IsEmpty || change.IsEmpty);
        }

        return false;
    }

    private static bool AreChangesIndependent(List<TextChange> changes, TextSpan commentSpan)
    {
        int previousEnd = commentSpan.Start;
        int previousStart = -1;

        foreach (TextChange change in changes)
        {
            if (change.Span.Start < commentSpan.Start
                || change.Span.End > commentSpan.End
                || change.Span.Start < previousEnd
                || change.Span.Start == previousStart && change.Span.IsEmpty)
            {
                return false;
            }

            previousStart = change.Span.Start;
            previousEnd = change.Span.End;
        }

        return true;
    }

    private static int GetLineNumber(SourceText source, int position) =>
        source.Lines.GetLinePosition(Math.Min(position, source.Length)).Line;

    private static bool IsHorizontalWhitespace(char value) => value is ' ' or '\t';

    private static bool ContainsOnlyHorizontalWhitespace(SourceText source, int start, int end)
    {
        for (int index = start; index < end; index++)
        {
            if (!IsHorizontalWhitespace(source[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Gets the zero-based column of the <c>///</c> documentation-comment exterior, caching the result by line
    ///  number for repeated formatting decisions within one comment.
    /// </summary>
    private static bool TryGetExteriorIndex(
        SourceText source,
        TextLine line,
        Dictionary<int, int> cache,
        out int exteriorIndex)
    {
        if (cache.TryGetValue(line.LineNumber, out exteriorIndex))
        {
            return exteriorIndex >= 0;
        }

        bool found = TryGetExteriorIndex(source, line, out exteriorIndex);
        cache.Add(line.LineNumber, found ? exteriorIndex : -1);
        return found;
    }

    private static bool TryReserveChange(long newTextLength, int oldLength, ref long projectedLength)
    {
        long nextLength = projectedLength - oldLength + newTextLength;
        if (nextLength is < 0 or > MaximumReplacementLength)
        {
            return false;
        }

        projectedLength = nextLength;
        return true;
    }

    private static SyntaxNode? GetAttributeContainer(SyntaxNode node)
    {
        for (SyntaxNode? ancestor = node; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is XmlElementStartTagSyntax or XmlEmptyElementSyntax)
            {
                return ancestor;
            }
        }

        return null;
    }

    private static bool HasAttributeAncestor(SyntaxNode node)
    {
        for (SyntaxNode? ancestor = node; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is XmlAttributeSyntax)
            {
                return true;
            }

            if (ancestor is XmlElementStartTagSyntax or XmlEmptyElementSyntax)
            {
                return false;
            }
        }

        return false;
    }

}
