// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Supplies effective <see cref="TestAnalyzerConfigOptions"/> for analyzer test syntax trees.
/// </summary>
internal sealed class TestAnalyzerConfigOptionsProvider(
    TestAnalyzerConfigOptions options,
    Dictionary<string, TestAnalyzerConfigOptions>? optionsByFile = null)
    : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions => options;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
        optionsByFile is not null && optionsByFile.TryGetValue(tree.FilePath, out TestAnalyzerConfigOptions? treeOptions)
            ? treeOptions
            : options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
}
