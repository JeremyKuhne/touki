// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

[TestClass]
public class OneStatementPerLineCodeFixTests
{
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [OneStatementPerLineAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static Task<string> ApplyFixAsync(string source) =>
        CodeFixTestHarness.ApplyFixAsync(
            new OneStatementPerLineAnalyzer(),
            new OneStatementPerLineCodeFixProvider(),
            source,
            OneStatementPerLineAnalyzer.DiagnosticId,
            diagnosticOptions: s_enabled);

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    [DataRow("\r")]
    public async Task RegisterCodeFixesAsync_ConsecutiveStatements_PreservesLineEndings(string newLine)
    {
        string source = "class Sample\n{\n    void M()\n    {\n        M(); M();\n    }\n}\n"
            .Replace("\n", newLine, StringComparison.Ordinal);

        string expected = source.Replace("M(); M();", "M();" + newLine + "        M();", StringComparison.Ordinal);

        string result = await ApplyFixAsync(source).ConfigureAwait(continueOnCapturedContext: false);

        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(
        "class Sample { void M() { M();M(); } }",
        "class Sample { void M() { M();\n        M(); } }")]
    [DataRow(
        "class Sample { int Value { get { int x = 1; return x; } } }",
        "class Sample { int Value { get { int x = 1;\n            return x; } } }")]
    [DataRow(
        "class Sample { void M() { M(); /* keep */ M(); } }",
        "class Sample { void M() { M(); /* keep */\n        M(); } }")]
    [DataRow(
        "class Sample { void M() { if (true) M(); M(); } }",
        "class Sample { void M() { if (true) M();\n        M(); } }")]
    [DataRow(
        "class Sample { void M() { switch (1) { case 1: M(); break; } } }",
        "class Sample { void M() { switch (1) { case 1: M();\n                break; } } }")]
    [DataRow(
        "class Sample { void M() { System.Action action = () => { M(); M(); }; } }",
        "class Sample { void M() { System.Action action = () => { M();\n            M(); }; } }")]
    public async Task RegisterCodeFixesAsync_CompactBodies_OnlySeparatesStatements(string source, string expected)
    {
        string result = await ApplyFixAsync(source).ConfigureAwait(continueOnCapturedContext: false);

        result.Should().Be(expected);
    }

    [TestMethod]
    public async Task RegisterCodeFixesAsync_AlreadySeparated_LeavesSourceUnchanged()
    {
        const string source = "class Sample { void M() { M();\n        M(); } }";

        string result = await ApplyFixAsync(source).ConfigureAwait(continueOnCapturedContext: false);

        result.Should().Be(source);
    }

    [TestMethod]
    public async Task RegisterCodeFixesAsync_MultilinePreviousStatement_UsesStatementIndentation()
    {
        const string source = "class Sample\n{\n    void M()\n    {\n        M(\n            ); M();\n    }\n}\n";
        const string expected = "class Sample\n{\n    void M()\n    {\n        M(\n            );\n        M();\n    }\n}\n";

        string result = await ApplyFixAsync(source).ConfigureAwait(continueOnCapturedContext: false);

        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(FixAllScope.Document)]
    [DataRow(FixAllScope.Project)]
    [DataRow(FixAllScope.Solution)]
    public async Task FixAll_MultipleAndNestedStatements_AppliesOneDocumentTransformation(FixAllScope scope)
    {
        const string source = "class Sample\n{\n    void M()\n    {\n        M(); M(); M();\n        { M(); M(); } M();\n    }\n}\n";
        const string expected = "class Sample\n{\n    void M()\n    {\n        M();\n        M();\n        M();\n        { M();\n            M(); }\n        M();\n    }\n}\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new OneStatementPerLineAnalyzer(),
            new OneStatementPerLineCodeFixProvider(),
            [("Sample.cs", "Sample.cs", source)],
            OneStatementPerLineAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: scope).ConfigureAwait(continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(4);
        result.FixAllActionOffered.Should().BeTrue();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
        ImmutableArray<Diagnostic> remaining = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            result.Documents[0].Source,
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        remaining.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(FixAllScope.Document, 1)]
    [DataRow(FixAllScope.Project, 2)]
    [DataRow(FixAllScope.Solution, 3)]
    public async Task FixAll_MultipleProjects_RespectsScope(FixAllScope scope, int expectedChangedDocuments)
    {
        const string source = "class One { void M() { M(); M(); M(); } }";
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new OneStatementPerLineAnalyzer(),
            new OneStatementPerLineCodeFixProvider(),
            [
                ("One.cs", "One.cs", source),
                ("Two.cs", "Two.cs", source.Replace("One", "Two", StringComparison.Ordinal))
            ],
            OneStatementPerLineAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: scope,
            additionalProjectSources:
                [("Three.cs", "Three.cs", source.Replace("One", "Three", StringComparison.Ordinal))])
            .ConfigureAwait(continueOnCapturedContext: false);

        result.FixAllActionOffered.Should().BeTrue();
        result.Documents.Count(document => document.Source.Contains("M();\n", StringComparison.Ordinal))
            .Should().Be(expectedChangedDocuments);

        result.Documents.Should().OnlyContain(document =>
            !document.Source.Contains("M();\n", StringComparison.Ordinal)
                || document.Source.Contains("M();\n        M();\n        M();", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("space", "2", "    ", "lf", "\n")]
    [DataRow("tab", "4", "\t\t", "crlf", "\r\n")]
    [DataRow("space", "3", "      ", "cr", "\r")]
    public async Task RegisterCodeFixesAsync_CompactBody_UsesConfiguredIndentation(
        string style,
        string size,
        string indentation,
        string endOfLine,
        string newLine)
    {
        const string source = "class Sample { void M() { M(); M(); } }";
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            source,
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        using AdhocWorkspace workspace = new();
        Project project = workspace.AddProject("Test", LanguageNames.CSharp);
        project = project.AddAnalyzerConfigDocument(
            "Test.globalconfig",
            SourceText.From(
                $"is_global = true\nindent_style = {style}\nindent_size = {size}\nend_of_line = {endOfLine}\n"),
            filePath: Path.Join(Path.GetTempPath(), "Test.globalconfig")).Project;

        Document document = project.AddDocument(
            "Sample.cs",
            SourceText.From(source),
            filePath: Path.Join(Path.GetTempPath(), "Sample.cs"));

        List<CodeAction> actions = [];
        OneStatementPerLineCodeFixProvider provider = new();
        await provider.RegisterCodeFixesAsync(new(
            document,
            diagnostics[0],
            (action, _) => actions.Add(action),
            CancellationToken.None)).ConfigureAwait(continueOnCapturedContext: false);

        actions.Should().ContainSingle();
        ImmutableArray<CodeActionOperation> operations =
            await actions[
                0].GetOperationsAsync(
                    CancellationToken.None).ConfigureAwait(
                        continueOnCapturedContext: false);

        ApplyChangesOperation operation = operations.OfType<ApplyChangesOperation>().Single();
        SourceText text = await operation.ChangedSolution.GetRequiredDocument(document.Id).GetTextAsync()
            .ConfigureAwait(continueOnCapturedContext: false);

        text.ToString().Should().Be("class Sample { void M() { M();" + newLine + indentation + "M(); } }");
    }

    [TestMethod]
    public async Task RegisterCodeFixesAsync_LinkedFile_OffersNoAction()
    {
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new OneStatementPerLineAnalyzer(),
            new OneStatementPerLineCodeFixProvider(),
            [("Sample.cs", "Sample.cs", "class Sample { void M() { M(); M(); } }")],
            OneStatementPerLineAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled,
            addLinkedProject: true).ConfigureAwait(continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task FixAll_LinkedFile_LeavesBothContextsUnchanged()
    {
        const string source = "class Sample { void M() { M(); M(); } }";
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new OneStatementPerLineAnalyzer(),
            new OneStatementPerLineCodeFixProvider(),
            [("Sample.cs", "Sample.cs", source)],
            OneStatementPerLineAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            addLinkedProject: true).ConfigureAwait(continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
    }

    [TestMethod]
    public async Task RegisterCodeFixesAsync_Interpolation_ReportsButOffersNoAction()
    {
        const string source = "class Sample { string M() => $\"{((System.Func<int>)(() => { int x = 1; return x; }))()}\"; }";
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new OneStatementPerLineAnalyzer(),
            new OneStatementPerLineCodeFixProvider(),
            [("Sample.cs", "Sample.cs", source)],
            OneStatementPerLineAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
    }
}
