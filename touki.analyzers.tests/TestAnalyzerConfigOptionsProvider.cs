// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Supplies the same <see cref="TestAnalyzerConfigOptions"/> for every syntax tree, which is
///  all the single-file analyzer fixtures need.
/// </summary>
internal sealed class TestAnalyzerConfigOptionsProvider(TestAnalyzerConfigOptions options)
    : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions => options;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
}
