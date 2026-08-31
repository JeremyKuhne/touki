// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Describes indentation as a source prefix followed by configured indentation levels.
/// </summary>
internal readonly struct StatementBreakIndentation(
    TextSpan baseSpan,
    string unit,
    int levels)
{
    public TextSpan BaseSpan { get; } = baseSpan;

    public string Unit { get; } = unit;

    public int Levels { get; } = levels;

    public long Length => BaseSpan.Length + ((long)Unit.Length * Levels);

    public bool Matches(SourceText source, TextSpan span)
    {
        if (span.Length != Length)
        {
            return false;
        }

        for (int index = 0; index < BaseSpan.Length; index++)
        {
            if (source[span.Start + index] != source[BaseSpan.Start + index])
            {
                return false;
            }
        }

        for (int index = BaseSpan.Length; index < span.Length; index++)
        {
            if (source[span.Start + index] != Unit[(index - BaseSpan.Length) % Unit.Length])
            {
                return false;
            }
        }

        return true;
    }

    public bool TryCreateText(SourceText source, out string text)
    {
        if (!StatementBreakDiagnosticData.IsValidIndentation(this))
        {
            text = string.Empty;
            return false;
        }

        string baseIndentation = source.ToString(BaseSpan);
        string additionalIndentation = new(Unit[0], Unit.Length * Levels);
        text = string.Concat(baseIndentation, additionalIndentation);
        return true;
    }
}