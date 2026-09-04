// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public partial class XmlDocumentationFormattingAnalyzerTests
{
    [TestMethod]
    public async Task Analyze_CommentAtSafetyLimit_ReportsDiagnostic()
    {
        const int maximumCommentLength = 1024 * 1024;
        const string prefix = "/// <summary>";
        const string suffix = "</summary>";
        string content = new('x', maximumCommentLength - prefix.Length - suffix.Length);
        string source = $"{prefix}{content}{suffix}\nclass Sample {{ }}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_CommentJustOverSafetyLimit_ReportsNothing()
    {
        const int maximumCommentLength = 1024 * 1024;
        const string prefix = "/// <summary>";
        const string suffix = "</summary>";
        string content = new('x', maximumCommentLength - prefix.Length - suffix.Length + 1);
        string source = $"{prefix}{content}{suffix}\nclass Sample {{ }}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_CommentJustOverSafetyLimitImmediatelyAfterMember_ReportsLeadingBlankLine()
    {
        const int maximumCommentLength = 1024 * 1024;
        const string prefix = "    /// <summary>";
        const string suffix = "</summary>";
        string content = new('x', maximumCommentLength - prefix.Length - suffix.Length + 1);
        string source = $"class Sample\n{{\n    int First => 1;\n{prefix}{content}{suffix}\n}}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be("\n");
    }

    [TestMethod]
    public async Task Analyze_OversizedIndentedComment_RejectsBeforeRescanningIndentation()
    {
        string indentation = new(' ', (1024 * 1024) + 1);
        CountingSourceText source = new(SourceText.From(
            $"{indentation}/// <summary>Text.</summary>\nclass Sample {{ }}\n"));

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new XmlDocumentationFormattingAnalyzer(),
            source,
            source.Reset,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
        source.CharacterReads.Should().BeLessThan(source.Length * 2L);
    }

    [TestMethod]
    public async Task Analyze_NodeCountAtSafetyLimit_ReportsDiagnostic()
    {
        string source =
            $"/// <summary>x{string.Concat(Enumerable.Repeat("<br/>", 2044))}</summary>\n"
            + "class Sample { }\n";

        GetStructuredNodeCount(source).Should().Be(4096);

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_NodeCountJustOverSafetyLimit_ReportsNothing()
    {
        string source =
            $"/// <summary>x{string.Concat(Enumerable.Repeat("<br/>", 2044))}y</summary>\n"
            + "class Sample { }\n";

        GetStructuredNodeCount(source).Should().Be(4097);

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_NestingAtSafetyLimit_ReportsDiagnostic()
    {
        string opening = string.Concat(Enumerable.Repeat("<para>", 127));
        string closing = string.Concat(Enumerable.Repeat("</para>", 127));
        string source = $"/// <summary>{opening}Text.{closing}</summary>\nclass Sample {{ }}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_NestingJustOverSafetyLimit_ReportsNothing()
    {
        string opening = string.Concat(Enumerable.Repeat("<para>", 128));
        string closing = string.Concat(Enumerable.Repeat("</para>", 128));
        string source = $"/// <summary>{opening}Text.{closing}</summary>\nclass Sample {{ }}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ReplacementAtSafetyLimitImmediatelyAfterCode_ReportsOnlyLeadingBlankLine()
    {
        string indentation = new(' ', 838_847);
        string source =
            $"class First {{ }}\n{indentation}/// <remarks><para>Text.more</para></remarks>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be("\n");
    }

    [TestMethod]
    public async Task Analyze_ReplacementAtSafetyLimitAfterBlankLine_ReportsFullReplacement()
    {
        string indentation = new(' ', 838_847);
        string source =
            $"class First {{ }}\n\n{indentation}/// <remarks><para>Text.more</para></remarks>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Length.Should().Be(4 * 1024 * 1024);
    }

    [TestMethod]
    public async Task Analyze_ReplacementJustOverSafetyLimit_ReportsNothing()
    {
        string indentation = new(' ', 838_847);
        string source = $"{indentation}/// <remarks><para>Text.more!</para></remarks>\nclass Sample {{ }}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ReplacementJustOverSafetyLimitImmediatelyAfterCode_ReportsOnlyLeadingBlankLine()
    {
        string indentation = new(' ', 838_847);
        string source =
            $"class First {{ }}\n{indentation}/// <remarks><para>Text.more!</para></remarks>\n"
            + "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be("\n");
    }

    [TestMethod]
    public async Task Analyze_ManyLinesBelowSafetyLimit_CompletesWithinBound()
    {
        string content = string.Concat(Enumerable.Repeat("/// Text.\n", 30_000));
        CountingSourceText source = new(SourceText.From(
            $"/// <summary>\n{content}/// </summary>\nclass Sample {{ }}\n"));

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new XmlDocumentationFormattingAnalyzer(),
            source,
            source.Reset,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        source.CharacterReads.Should().BeLessThan(source.Length * 20L);
    }

    [TestMethod]
    public async Task Analyze_ManySeparatedComments_KeepsCharacterReadsLinear()
    {
        string comments = string.Concat(Enumerable.Repeat("/// <value>x</value>\n\n", 2_000));
        CountingSourceText source = new(SourceText.From($"{comments}class Sample {{ }}\n"));

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new XmlDocumentationFormattingAnalyzer(),
            source,
            source.Reset,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
        source.CharacterReads.Should().BeLessThan(source.Length * 20L);
    }

    [TestMethod]
    public async Task Analyze_WideSingleLineBelowSafetyLimit_KeepsCharacterReadsLinear()
    {
        string indentation = new(' ', 512 * 1024);
        string paragraphs = string.Concat(Enumerable.Repeat("<para>x</para>", 400));
        CountingSourceText source = new(SourceText.From(
            $"{indentation}/// <remarks>{paragraphs}</remarks>\nclass Sample {{ }}\n"));

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new XmlDocumentationFormattingAnalyzer(),
            source,
            source.Reset,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
        source.CharacterReads.Should().BeLessThan(source.Length * 20L);
    }

    [TestMethod]
    public async Task Analyze_WidePostExteriorWhitespaceBelowSafetyLimit_KeepsCharacterReadsLinear()
    {
        string padding = new(' ', 512 * 1024);
        string paragraphs = string.Concat(Enumerable.Repeat("<para>x</para>", 400));
        CountingSourceText source = new(SourceText.From(
            $"///{padding}<remarks>{paragraphs}</remarks>\nclass Sample {{ }}\n"));

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new XmlDocumentationFormattingAnalyzer(),
            source,
            source.Reset,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        source.CharacterReads.Should().BeLessThan(source.Length * 20L);
    }

    private static int GetStructuredNodeCount(string source)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();
        DocumentationCommentTriviaSyntax documentation = root
            .DescendantTrivia(descendIntoTrivia: true)
            .Select(static trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .Single();

        return documentation.DescendantNodes().Count();
    }
}
