// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

internal static class XmlDocumentationFormattingOptions
{
    public const string IndentSizeOption = "dotnet_code_quality.TOUKI0024.indent_size";
    public const string MaxLineLengthOption = "dotnet_code_quality.TOUKI0024.max_line_length";
    public const int DefaultIndentSize = 1;
    public const int MaximumIndentSize = 16;
    public const int DefaultMaxLineLength = 120;

    private const string StandardMaxLineLengthOption = "max_line_length";

    public static (int IndentSize, int MaxLineLength) GetOptions(AnalyzerConfigOptions options)
    {
        int indentSize = options.TryGetPositiveInteger(IndentSizeOption, out int configuredIndentSize)
            && configuredIndentSize <= MaximumIndentSize
                ? configuredIndentSize
                : DefaultIndentSize;
        int maxLineLength = options.TryGetPositiveInteger(MaxLineLengthOption, out int configuredMaxLineLength)
            || options.TryGetPositiveInteger(StandardMaxLineLengthOption, out configuredMaxLineLength)
                ? configuredMaxLineLength
                : DefaultMaxLineLength;
        return (indentSize, maxLineLength);
    }
}