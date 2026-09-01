// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

internal static class StatementBreakDiagnosticData
{
    public const int MaximumChangeCharacters = 4 * 1024;

    private const string ChangeKindProperty = "ChangeKind";
    private const string OperatorTextProperty = "OperatorText";
    private const string SpaceAfterProperty = "SpaceAfter";
    private const string IndentationUnitProperty = "IndentationUnit";
    private const string IndentationLevelsProperty = "IndentationLevels";

    public static ImmutableDictionary<string, string?> CreateProperties(
        StatementBreakChangeKind changeKind,
        string operatorText,
        bool spaceAfter,
        StatementBreakIndentation indentation) =>
        ImmutableDictionary<string, string?>.Empty
            .Add(ChangeKindProperty, ((int)changeKind).ToString(CultureInfo.InvariantCulture))
            .Add(OperatorTextProperty, operatorText)
            .Add(SpaceAfterProperty, spaceAfter ? "1" : "0")
            .Add(IndentationUnitProperty, indentation.Unit)
            .Add(IndentationLevelsProperty, indentation.Levels.ToString(CultureInfo.InvariantCulture));

    public static bool TryCreateTextChange(
        Diagnostic diagnostic,
        SyntaxNode currentRoot,
        SourceText source,
        string currentIndentationUnit,
        out TextChange change) =>
        TryCreateTextChange(
            diagnostic,
            currentRoot,
            source,
            currentIndentationUnit,
            CancellationToken.None,
            out change,
            out _);

    public static bool TryCreateTextChange(
        Diagnostic diagnostic,
        SyntaxNode currentRoot,
        SourceText source,
        string currentIndentationUnit,
        CancellationToken cancellationToken,
        out TextChange change) =>
        TryCreateTextChange(
            diagnostic,
            currentRoot,
            source,
            currentIndentationUnit,
            cancellationToken,
            out change,
            out _);

    public static bool TryCreateTextChange(
        Diagnostic diagnostic,
        SyntaxNode currentRoot,
        SourceText source,
        string currentIndentationUnit,
        CancellationToken cancellationToken,
        out TextChange change,
        out bool intentionalNoFix)
    {
        intentionalNoFix = false;
        cancellationToken.ThrowIfCancellationRequested();
        if (diagnostic.AdditionalLocations.Count != 2
            || diagnostic.Location.SourceTree is null
            || diagnostic.AdditionalLocations[0] is not { SourceTree: not null } replacementLocation
            || diagnostic.AdditionalLocations[1] is not { SourceTree: not null } baseIndentationLocation
            || replacementLocation.SourceTree != diagnostic.Location.SourceTree
            || baseIndentationLocation.SourceTree != diagnostic.Location.SourceTree
            || !TryGetChangeKind(diagnostic.Properties, out StatementBreakChangeKind changeKind)
            || !diagnostic.Properties.TryGetValue(OperatorTextProperty, out string? operatorText)
            || operatorText is null
            || operatorText.Length is < 1 or > 4
            || !TryGetBoolean(diagnostic.Properties, SpaceAfterProperty, out bool spaceAfter)
            || !diagnostic.Properties.TryGetValue(IndentationUnitProperty, out string? indentationUnit)
            || indentationUnit is null
            || !string.Equals(indentationUnit, currentIndentationUnit, StringComparison.Ordinal)
            || !TryGetNonNegativeInteger(diagnostic.Properties, IndentationLevelsProperty, out int indentationLevels)
            || indentationLevels > 256)
        {
            change = default;
            return false;
        }

        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
        TextSpan replacementSpan = replacementLocation.SourceSpan;
        StatementBreakIndentation indentation = new(
            baseIndentationLocation.SourceSpan,
            indentationUnit,
            indentationLevels);
        SourceText diagnosticSource = diagnostic.Location.SourceTree.GetText();
        if (!StatementBreakFormatting.AreParseOptionsCompatible(
            currentRoot.SyntaxTree.Options,
            diagnostic.Location.SourceTree.Options)
            || !StatementBreakFormatting.ContentEquals(
                currentRoot.SyntaxTree.GetText(cancellationToken),
                source,
                cancellationToken)
            || diagnosticSpan.Length > MaximumChangeCharacters
            || diagnosticSpan.End > source.Length
            || diagnosticSpan.End > diagnosticSource.Length
            || replacementSpan.End > source.Length
            || replacementSpan.End > diagnosticSource.Length
            || indentation.BaseSpan.End > source.Length
            || indentation.BaseSpan.End > diagnosticSource.Length
            || !IsChangeSizeAllowed(
                source,
                replacementSpan,
                changeKind,
                operatorText,
                spaceAfter,
                indentation)
            || !SpansEqual(diagnosticSource, source, diagnosticSpan, cancellationToken)
            || !SpansEqual(diagnosticSource, source, replacementSpan, cancellationToken)
            || !SpansEqual(diagnosticSource, source, indentation.BaseSpan, cancellationToken)
            || !StatementBreakFormatting.TryFindOperator(
                currentRoot,
                diagnosticSpan,
                out StatementBreakOperator operation)
            || !string.Equals(operation.OperatorText, operatorText, StringComparison.Ordinal)
            || operation.SpaceAfter != spaceAfter
            || !StatementBreakFormatting.TryGetChange(
                operation,
                source,
                currentIndentationUnit,
                cancellationToken,
                out StatementBreakChangeKind expectedChangeKind,
                out TextSpan expectedReplacementSpan,
                out StatementBreakIndentation expectedIndentation)
            || expectedChangeKind != changeKind
            || expectedReplacementSpan != replacementSpan
            || expectedIndentation.BaseSpan != indentation.BaseSpan
            || !string.Equals(expectedIndentation.Unit, indentation.Unit, StringComparison.Ordinal)
            || expectedIndentation.Levels != indentation.Levels)
        {
            change = default;
            return false;
        }

        if (StatementBreakFormatting.WouldCreateNestedTrailingOperator(
            operation,
            source,
            expectedChangeKind))
        {
            change = default;
            intentionalNoFix = true;
            return false;
        }

        if (!indentation.TryCreateText(source, out string indentationText))
        {
            change = default;
            return false;
        }

        string replacement;
        switch (changeKind)
        {
            case StatementBreakChangeKind.Indentation:
                replacement = indentationText;
                break;
            case StatementBreakChangeKind.LeadingOperator:
                if (!TryGetLineBreak(source, replacementSpan, out string leadingLineBreak))
                {
                    change = default;
                    return false;
                }

                replacement = string.Concat(
                    leadingLineBreak,
                    indentationText,
                    operatorText,
                    spaceAfter ? " " : string.Empty);
                break;
            case StatementBreakChangeKind.TrailingOperator:
                if (!TryGetLineBreak(source, replacementSpan, out string trailingLineBreak))
                {
                    change = default;
                    return false;
                }

                replacement = string.Concat(" ", operatorText, trailingLineBreak, indentationText);
                break;
            case StatementBreakChangeKind.BreakBeforeOperator:
                if (!TryGetAdjacentLineBreak(source, replacementSpan, out string beforeLineBreak))
                {
                    change = default;
                    return false;
                }

                replacement = string.Concat(
                    beforeLineBreak,
                    indentationText,
                    operatorText,
                    spaceAfter ? " " : string.Empty);
                break;
            case StatementBreakChangeKind.BreakAfterOperator:
                if (!TryGetAdjacentLineBreak(source, replacementSpan, out string afterLineBreak))
                {
                    change = default;
                    return false;
                }

                replacement = string.Concat(" ", operatorText, afterLineBreak, indentationText);
                break;
            default:
                change = default;
                return false;
        }

        if (replacement.Length > MaximumChangeCharacters)
        {
            change = default;
            return false;
        }

        change = new(replacementSpan, replacement);
        return true;
    }

    public static bool TryCreateTextChanges(
        Diagnostic diagnostic,
        SyntaxNode currentRoot,
        SourceText source,
        string currentIndentationUnit,
        CancellationToken cancellationToken,
        out ImmutableArray<TextChange> changes,
        out bool intentionalNoFix)
    {
        if (!TryCreateTextChange(
            diagnostic,
            currentRoot,
            source,
            currentIndentationUnit,
            cancellationToken,
            out TextChange primaryChange,
            out intentionalNoFix)
            || !StatementBreakFormatting.TryFindOperator(
                currentRoot,
                diagnostic.Location.SourceSpan,
                out StatementBreakOperator operation)
            || !StatementBreakFormatting.TryGetChange(
                operation,
                source,
                currentIndentationUnit,
                cancellationToken,
                out StatementBreakChangeKind changeKind,
                out _,
                out StatementBreakIndentation indentation))
        {
            changes = [];
            return false;
        }

        int primaryReplacementCharacters = primaryChange.NewText?.Length ?? 0;
        if (!StatementBreakFormatting.TryGetDependentIndentationChanges(
                operation,
                source,
                changeKind,
                indentation,
            MaximumChangeCharacters - primaryReplacementCharacters,
                cancellationToken,
                out List<TextChange> dependentChanges))
        {
            changes = [];
            intentionalNoFix = true;
            return false;
        }

        ImmutableArray<TextChange>.Builder builder = ImmutableArray.CreateBuilder<TextChange>(
            dependentChanges.Count + 1);
        builder.Add(primaryChange);
        builder.AddRange(dependentChanges);
        changes = builder.MoveToImmutable();
        return true;
    }

    public static bool IsChangeSizeAllowed(
        SourceText source,
        TextSpan replacementSpan,
        StatementBreakChangeKind changeKind,
        string operatorText,
        bool spaceAfter,
        StatementBreakIndentation indentation)
    {
        if (replacementSpan.Length > MaximumChangeCharacters
            || !IsValidIndentation(indentation))
        {
            return false;
        }

        long projectedLength = indentation.Length;
        if (changeKind is StatementBreakChangeKind.LeadingOperator
            or StatementBreakChangeKind.TrailingOperator
            or StatementBreakChangeKind.BreakBeforeOperator
            or StatementBreakChangeKind.BreakAfterOperator)
        {
            bool foundLineBreak = changeKind is StatementBreakChangeKind.BreakBeforeOperator
                or StatementBreakChangeKind.BreakAfterOperator
                    ? TryGetAdjacentLineBreakSpan(source, replacementSpan, out TextSpan lineBreakSpan)
                    : TryGetLineBreakSpan(source, replacementSpan, out lineBreakSpan);
            if (!foundLineBreak)
            {
                return false;
            }

            projectedLength += operatorText.Length + lineBreakSpan.Length;
            projectedLength += changeKind is StatementBreakChangeKind.TrailingOperator
                or StatementBreakChangeKind.BreakAfterOperator || spaceAfter
                    ? 1
                    : 0;
        }

        return projectedLength <= MaximumChangeCharacters;
    }

    private static bool SpansEqual(
        SourceText left,
        SourceText right,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        for (int index = span.Start; index < span.End; index++)
        {
            if ((index - span.Start & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsValidIndentation(StatementBreakIndentation indentation)
    {
        if (indentation.BaseSpan.Length < 0
            || indentation.Unit.Length is < 1 or > StatementBreakFormattingOptions.MaximumIndentSize
            || indentation.Levels is < 0 or > 256
            || indentation.Length > MaximumChangeCharacters)
        {
            return false;
        }

        char character = indentation.Unit[0];
        if (character is not (' ' or '\t'))
        {
            return false;
        }

        for (int index = 1; index < indentation.Unit.Length; index++)
        {
            if (indentation.Unit[index] != character)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetChangeKind(
        ImmutableDictionary<string, string?> properties,
        out StatementBreakChangeKind changeKind)
    {
        if (properties.TryGetValue(ChangeKindProperty, out string? value)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int rawKind)
            && rawKind is >= (int)StatementBreakChangeKind.Indentation
                and <= (int)StatementBreakChangeKind.BreakAfterOperator)
        {
            changeKind = (StatementBreakChangeKind)rawKind;
            return true;
        }

        changeKind = default;
        return false;
    }

    private static bool TryGetBoolean(
        ImmutableDictionary<string, string?> properties,
        string key,
        out bool value)
    {
        if (properties.TryGetValue(key, out string? rawValue))
        {
            if (rawValue == "1")
            {
                value = true;
                return true;
            }

            if (rawValue == "0")
            {
                value = false;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static bool TryGetNonNegativeInteger(
        ImmutableDictionary<string, string?> properties,
        string key,
        out int value)
    {
        value = 0;
        return properties.TryGetValue(key, out string? rawValue)
            && int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    }

    private static bool TryGetLineBreak(SourceText source, TextSpan span, out string lineBreak)
    {
        if (TryGetLineBreakSpan(source, span, out TextSpan lineBreakSpan))
        {
            lineBreak = source.ToString(lineBreakSpan);
            return true;
        }

        lineBreak = string.Empty;
        return false;
    }

    private static bool TryGetAdjacentLineBreak(SourceText source, TextSpan span, out string lineBreak)
    {
        if (TryGetAdjacentLineBreakSpan(source, span, out TextSpan lineBreakSpan))
        {
            lineBreak = source.ToString(lineBreakSpan);
            return true;
        }

        lineBreak = string.Empty;
        return false;
    }

    private static bool TryGetAdjacentLineBreakSpan(SourceText source, TextSpan span, out TextSpan lineBreakSpan)
    {
        TextLine line = source.Lines.GetLineFromPosition(span.Start);
        lineBreakSpan = TextSpan.FromBounds(line.End, line.EndIncludingLineBreak);
        if (!lineBreakSpan.IsEmpty)
        {
            return true;
        }

        if (line.LineNumber == 0)
        {
            return false;
        }

        TextLine previous = source.Lines[line.LineNumber - 1];
        lineBreakSpan = TextSpan.FromBounds(previous.End, previous.EndIncludingLineBreak);
        return !lineBreakSpan.IsEmpty;
    }

    private static bool TryGetLineBreakSpan(SourceText source, TextSpan span, out TextSpan lineBreakSpan)
    {
        TextLine line = source.Lines.GetLineFromPosition(span.Start);
        lineBreakSpan = TextSpan.FromBounds(line.End, line.EndIncludingLineBreak);
        return !lineBreakSpan.IsEmpty
            && lineBreakSpan.Start >= span.Start
            && lineBreakSpan.End <= span.End;
    }

}