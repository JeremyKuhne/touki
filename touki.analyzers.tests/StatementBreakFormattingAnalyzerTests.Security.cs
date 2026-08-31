// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public partial class StatementBreakFormattingAnalyzerTests
{
    [TestMethod]
    public async Task Analyze_OperatorGroupWithinBudget_ReportsEveryOperator()
    {
        string source = CreateDeepBinaryChain(additionalOperators: 200);

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(201);
    }

    [TestMethod]
    public async Task Analyze_OperatorBeyondAncestorBudget_ReportsNothing()
    {
        string source = CreateDeepBinaryChain(additionalOperators: 300);

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ReplacementOverLimit_ReportsNothing()
    {
        const int maximumReplacementCharacters = 4 * 1024;
        string source =
            "class Sample\n"
            + "{\n"
            + "    int Method(int left, int right)\n"
            + "    {\n"
            + "        return left +\n"
            + new string(' ', maximumReplacementCharacters + 1)
            + "right;\n"
            + "    }\n"
            + "}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ReplacementAtLimit_ReportsDiagnostic()
    {
        const int baseIndentationLength = 4081;
        string baseIndentation = new(' ', baseIndentationLength);
        string statementIndentation = new(' ', baseIndentationLength + 8);
        string continuationIndentation = new(' ', baseIndentationLength + 12);
        string source =
            $"{baseIndentation}class Sample\n"
            + $"{baseIndentation}{{\n"
            + $"{baseIndentation}    int Method(int left, int right)\n"
            + $"{baseIndentation}    {{\n"
            + $"{statementIndentation}return left +\n"
            + $"{continuationIndentation}right;\n"
            + $"{baseIndentation}    }}\n"
            + $"{baseIndentation}}}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_ProjectedReplacementOverLimit_ReportsNothing()
    {
        const int baseIndentationLength = 4082;
        string baseIndentation = new(' ', baseIndentationLength);
        string statementIndentation = new(' ', baseIndentationLength + 8);
        string source =
            $"{baseIndentation}class Sample\n"
            + $"{baseIndentation}{{\n"
            + $"{baseIndentation}    int Method(int left, int right)\n"
            + $"{baseIndentation}    {{\n"
            + $"{statementIndentation}return left +\n"
            + "right;\n"
            + $"{baseIndentation}    }}\n"
            + $"{baseIndentation}}}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ManyViolationsWithWideSharedIndentation_KeepsDiagnosticPropertiesCompact()
    {
        const int indentationLength = 4088;
        const int violationCount = 128;
        string source =
            new string(' ', indentationLength)
            + "class Sample { bool Method(bool value) => value\n"
            + string.Concat(Enumerable.Repeat("&& value\n", violationCount))
            + "; }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(violationCount);
        diagnostics.Should().OnlyContain(diagnostic =>
            diagnostic.Properties.Values.Sum(value => value!.Length) < 64);
    }

    [TestMethod]
    public async Task Analyze_ManyViolationsWithOverLimitSharedIndentation_KeepsCharacterReadsLinear()
    {
        const int indentationLength = 64 * 1024;
        const int violationCount = 64;
        CountingSourceText source = new(SourceText.From(
            new string(' ', indentationLength)
            + "class Sample { bool Method(bool value) => value\n"
            + string.Concat(Enumerable.Repeat("&& value\n", violationCount))
            + "; }\n"));

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new StatementBreakFormattingAnalyzer(),
            source,
            source.Reset,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
        source.CharacterReads.Should().BeLessThan(source.Length * 20L);
    }

    [TestMethod]
    public async Task Analyze_PostfixChainWithinAncestorBudget_ReportsMemberAndArgumentOperators()
    {
        string source = CreateDeepPostfixChain(chainLength: 100);

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Select(GetDiagnosticText).Should().BeEquivalentTo([".", "||"]);
    }

    [TestMethod]
    public async Task Analyze_PostfixChainBeyondAncestorBudget_SuppressesOnlyDeepMemberOperator()
    {
        string source = CreateDeepPostfixChain(chainLength: 140);

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("||");
    }

    private static string CreateDeepBinaryChain(int additionalOperators) =>
        "class Sample\n"
        + "{\n"
        + "    bool Method()\n"
        + "    {\n"
        + "        return true\n"
        + "              && true"
        + string.Concat(Enumerable.Repeat(" && true", additionalOperators))
        + ";\n"
        + "    }\n"
        + "}\n";

    private static string CreateDeepPostfixChain(int chainLength) =>
        "class Chain\n"
        + "{\n"
        + "    public Chain Next() => this;\n"
        + "    public bool Check(bool value) => value;\n"
        + "}\n"
        + "\n"
        + "class Sample\n"
        + "{\n"
        + "    bool Method(Chain chain, bool left, bool right) =>\n"
        + "        chain\n"
        + "        .Next()"
        + string.Concat(Enumerable.Repeat(".Next()", chainLength))
        + ".Check(\n"
        + "            left\n"
        + "              || right);\n"
        + "}\n";
}