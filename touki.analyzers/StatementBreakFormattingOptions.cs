// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

internal static class StatementBreakFormattingOptions
{
    public const int DefaultIndentSize = 4;
    public const int MaximumIndentSize = 16;

    private const string IndentSizeOption = "indent_size";
    private const string IndentStyleOption = "indent_style";
    private const string TabWidthOption = "tab_width";

    private static readonly string[] s_spaceIndentationUnits =
    [
        " ",
        "  ",
        "   ",
        "    ",
        "     ",
        "      ",
        "       ",
        "        ",
        "         ",
        "          ",
        "           ",
        "            ",
        "             ",
        "              ",
        "               ",
        "                "
    ];

    public static string GetIndentationUnit(AnalyzerConfigOptions options)
    {
        bool useTabs = options.TryGetValue(IndentStyleOption, out string? indentStyle)
            && string.Equals(indentStyle.Trim(), "tab", StringComparison.OrdinalIgnoreCase);
        if (useTabs)
        {
            return "\t";
        }

        int indentSize = options.TryGetPositiveInteger(IndentSizeOption, out int configuredIndentSize)
            && configuredIndentSize <= MaximumIndentSize
                ? configuredIndentSize
                : DefaultIndentSize;
        if (options.TryGetValue(IndentSizeOption, out string? configured)
            && string.Equals(configured.Trim(), "tab", StringComparison.OrdinalIgnoreCase)
            && options.TryGetPositiveInteger(TabWidthOption, out int tabWidth)
            && tabWidth <= MaximumIndentSize)
        {
            indentSize = tabWidth;
        }

        return s_spaceIndentationUnits[indentSize - 1];
    }
}