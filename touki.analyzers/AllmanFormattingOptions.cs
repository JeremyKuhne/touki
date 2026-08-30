// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Captures the configured Allman formatting policies and indentation.
/// </summary>
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
