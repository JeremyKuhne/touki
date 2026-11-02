// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

[TestClass]
public class ParameterListFormattingCodeFixTests
{
    [TestMethod]
    public async Task RegisterCodeFixesAsync_FileScopedCrLfOnLfSource_UsesConfiguredLineBreaks()
    {
        const string source = "class C\n{\n    void M(int first,\n        int second) { }\n}";
        string directory = Path.Join(Path.GetTempPath(), $"touki-parameter-line-endings-{Guid.NewGuid():N}");
        using AdhocWorkspace workspace = new();
        Project project = workspace.AddProject("Test", LanguageNames.CSharp)
            .AddMetadataReferences(RoslynTestEnvironment.References)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        project = project.AddAnalyzerConfigDocument(
            ".editorconfig",
            SourceText.From("root = true\n[*.cs]\nend_of_line = crlf\n"),
            filePath: Path.Join(directory, ".editorconfig")).Project;

        Document document = project.AddDocument(
            "C.cs",
            SourceText.From(source),
            filePath: Path.Join(directory, "C.cs"));

        Compilation compilation = await document.Project.GetRequiredCompilationAsync()
            .ConfigureAwait(continueOnCapturedContext: false);

        compilation = RoslynTestEnvironment.ApplyDiagnosticOptions(
            compilation,
            ParameterListFormattingAnalyzerTests.Enabled);

        ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers(
            [new ParameterListFormattingAnalyzer()],
            document.Project.AnalyzerOptions).GetAnalyzerDiagnosticsAsync().ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().ContainSingle();
        List<CodeAction> actions = [];
        FormatParameterListsCodeFixProvider provider = new();
        await provider.RegisterCodeFixesAsync(new(
            document,
            diagnostics[0],
            (action, _) => actions.Add(action),
            CancellationToken.None)).ConfigureAwait(continueOnCapturedContext: false);

        actions.Should().ContainSingle();
        ImmutableArray<CodeActionOperation> operations = await actions[0].GetOperationsAsync(CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        ApplyChangesOperation operation = operations.OfType<ApplyChangesOperation>().Single();
        SourceText text = await operation.ChangedSolution.GetRequiredDocument(document.Id).GetTextAsync()
            .ConfigureAwait(continueOnCapturedContext: false);

        text.ToString().Should().Contain("M(\r\n        int first,\r\n        int second)");
    }

    [TestMethod]
    [DataRow(data: false)]
    [DataRow(data: true)]
    public async Task FixAll_GlobalCrLfOnLfSource_UsesConfiguredLineBreaks(bool fixAll)
    {
        const string source = "class C\n{\n    void M(int first,\n        int second) { }\n}";
        Dictionary<string, string> options = new()
        {
            ["end_of_line"] = "crlf"
        };

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", source)],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll,
            options,
            ParameterListFormattingAnalyzerTests.Enabled,
            addLinkedProject: true,
            linkedProjectOptions: options).ConfigureAwait(continueOnCapturedContext: false);

        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document =>
            document.Source.Contains("M(\r\n        int first,\r\n        int second)", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RegisterCodeFixesAsync_UnsafeNestedGroup_OffersNoAction()
    {
        const string source = """
            class C
            {
                int F(int a, int b) => a + b;
                int G(int a, int b) => a + b;

                void M()
                {
                    F(G(1, /* keep */
                        2),
                        3);
                }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", source)],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: ParameterListFormattingAnalyzerTests.Enabled).ConfigureAwait(continueOnCapturedContext: false);

        result.CompilerErrors.Should().BeEmpty();
        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeFalse();
        result.Documents[0].Source.Should().Be(source);
    }

    [TestMethod]
    [DataRow(FixAllScope.Document)]
    [DataRow(FixAllScope.Project)]
    [DataRow(FixAllScope.Solution)]
    public async Task FixAll_UnsafeNestedGroup_PreservesGroupAndFixesSafeSibling(FixAllScope scope)
    {
        string source = """
            class C
            {
                int F(int a, int b) => a + b;
                int G(int a, int b) => a + b;

                void M()
                {
                    F(G(1, /* keep */
                        2),
                        3);
                    F(4,
                        5);
                }
            }
            """.ReplaceLineEndings("\n");

        string expected = """
            class C
            {
                int F(int a, int b) => a + b;
                int G(int a, int b) => a + b;

                void M()
                {
                    F(G(1, /* keep */
                        2),
                        3);
                    F(
                        4,
                        5);
                }
            }
            """.ReplaceLineEndings("\n");

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", source)],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: ParameterListFormattingAnalyzerTests.Enabled,
            fixAllScope: scope).ConfigureAwait(continueOnCapturedContext: false);

        result.CompilerErrors.Should().BeEmpty();
        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.FixAllActionOffered.Should().BeTrue();
        result.Documents[0].Source.Should().Be(expected);
        ImmutableArray<Diagnostic> remaining = await ParameterListFormattingAnalyzerTests.AnalyzeAsync(
            result.Documents[0].Source).ConfigureAwait(continueOnCapturedContext: false);

        remaining.Should().HaveCount(2);
    }

    [TestMethod]
    [DataRow(data: false)]
    [DataRow(data: true)]
    public async Task FixAll_TabIndentation_PreservesWholeTabLevels(bool fixAll)
    {
        const string source = "class C\n{\n\tvoid M(int first,\n\t\tint second) { }\n\tvoid N(int first,\n\t\tint second) { }\n}";
        Dictionary<string, string> options = new()
        {
            ["indent_style"] = "tab",
            ["tab_width"] = "4"
        };

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", source)],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll,
            options,
            ParameterListFormattingAnalyzerTests.Enabled,
            transformDiagnostics: diagnostics =>
            {
                diagnostics.Should().OnlyContain(diagnostic => diagnostic.Properties["Indentation"] == "\t");
                return diagnostics;
            }).ConfigureAwait(continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.Documents[0].Source.Should().Contain("void M(\n\t\tint first,\n\t\tint second)");
        ImmutableArray<Diagnostic> remaining = await ParameterListFormattingAnalyzerTests.AnalyzeAsync(
            result.Documents[0].Source,
            options).ConfigureAwait(continueOnCapturedContext: false);

        remaining.Length.Should().Be(fixAll ? 0 : 1);
    }

    [TestMethod]
    public async Task FixAll_NameofMethodAndOperator_OnlyWrapsTheMethodInvocation()
    {
        const string source = "static class Methods\n{\n    public static string nameof(string x, int y) => x;\n}\n"
            + "class C\n{\n    string V => Methods.nameof(nameof(C),\n        1);\n}";

        Dictionary<string, string> options = new()
        {
            ["max_line_length"] = "10"
        };

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", source)],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            options,
            ParameterListFormattingAnalyzerTests.Enabled).ConfigureAwait(continueOnCapturedContext: false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.Documents[0].Source.Should().Contain("nameof(C)");
        result.Documents[0].Source.Should().Contain("Methods.nameof(\n        nameof(C),\n        1)");
        ImmutableArray<Diagnostic> remaining = await ParameterListFormattingAnalyzerTests.AnalyzeAsync(
            result.Documents[0].Source,
            options).ConfigureAwait(continueOnCapturedContext: false);

        remaining.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RegisterCodeFixesAsync_ConfiguredLength_WrapsAndStaysStable()
    {
        const string source = "class C\n{\n    void M(int first, int second) { }\n}";
        Dictionary<string, string> options = new()
        {
            [ParameterListFormattingAnalyzer.MaxLineLengthOption] = "30",
            ["indent_size"] = "2"
        };

        string result = await CodeFixTestHarness.ApplyFixAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            source,
            ParameterListFormattingAnalyzer.DiagnosticId,
            options,
            ParameterListFormattingAnalyzerTests.Enabled).ConfigureAwait(continueOnCapturedContext: false);

        result.Should().Contain("M(\n      int first,\n      int second)");
        ImmutableArray<Diagnostic> remaining = await ParameterListFormattingAnalyzerTests.AnalyzeAsync(result, options)
            .ConfigureAwait(continueOnCapturedContext: false);

        remaining.Should().BeEmpty();
    }

    [TestMethod]
    public async Task FixAll_LinkedIndentationDisagreement_OffersNoAction()
    {
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", "class C { void M(int first,\nint second) { } }")],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            addLinkedProject: true,
            linkedProjectOptions: new Dictionary<string, string>
            {
                ["indent_size"] = "2"
            },

            diagnosticOptions: ParameterListFormattingAnalyzerTests.Enabled).ConfigureAwait(
                continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task FixAll_ManySiblingLists_CompletesOneBulkAction()
    {
        string source = "class C\n{\n"
            + string.Concat(Enumerable.Range(0, 100).Select(index =>
                $"    void M{index}(int first,\n        int second) {{ }}\n"))
            + "}";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", source)],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: ParameterListFormattingAnalyzerTests.Enabled).ConfigureAwait(
                continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(100);
        result.FixAllActionOffered.Should().BeTrue();
        ImmutableArray<Diagnostic> remaining = await ParameterListFormattingAnalyzerTests.AnalyzeAsync(
            result.Documents[
                0].Source)
            .ConfigureAwait(continueOnCapturedContext: false);

        remaining.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    public async Task RegisterCodeFixesAsync_MixedParameters_WrapsAndIndents(string newLine)
    {
        string source = "class C\n{\n    void M(int first,\n        int second) { }\n}"
            .Replace("\n", newLine, StringComparison.Ordinal);

        string expected = "class C\n{\n    void M(\n        int first,\n        int second)\n    { }\n}"
            .Replace("\n", newLine, StringComparison.Ordinal);

        string result = await CodeFixTestHarness.ApplyFixAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            source,
            ParameterListFormattingAnalyzer.DiagnosticId,
            diagnosticOptions: ParameterListFormattingAnalyzerTests.Enabled).ConfigureAwait(
                continueOnCapturedContext: false);

        result.Should().Be(expected);
        ImmutableArray<Diagnostic> remaining = await ParameterListFormattingAnalyzerTests.AnalyzeAsync(result)
            .ConfigureAwait(continueOnCapturedContext: false);

        remaining.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(FixAllScope.Document)]
    [DataRow(FixAllScope.Project)]
    [DataRow(FixAllScope.Solution)]
    public async Task FixAll_NestedAndSiblingLists_CombinesEditsAndLeavesNoViolations(FixAllScope scope)
    {
        string source = """
            class C
            {
                int M(int first,
                    int second) => 0;
                int V => M(M(1,
                    2),
                    M(3,
                    4));
            }
            """.ReplaceLineEndings("\n");

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", source)],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: ParameterListFormattingAnalyzerTests.Enabled,
            fixAllScope: scope).ConfigureAwait(continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(4);
        result.FixAllActionOffered.Should().BeTrue();
        result.Documents[0].Source.Should().Contain("int M(\n        int first,\n        int second)");
        ImmutableArray<Diagnostic> remaining = await ParameterListFormattingAnalyzerTests.AnalyzeAsync(
            result.Documents[
                0].Source)
            .ConfigureAwait(continueOnCapturedContext: false);

        remaining.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("class C { void M(int first, /* keep */\nint second) { } }")]
    [DataRow("class C { string V => $\"{M(1,\n2)}\"; int M(int a, int b) => 0; }")]
    public async Task RegisterCodeFixesAsync_UnsafeBoundary_ReportsButOffersNoAction(string source)
    {
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", source)],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: ParameterListFormattingAnalyzerTests.Enabled).ConfigureAwait(
                continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().BeGreaterThan(0);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task RegisterCodeFixesAsync_LinkedFile_UpdatesBothContexts()
    {
        const string source = "class C { void M(int first,\nint second) { } }";
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ParameterListFormattingAnalyzer(),
            new FormatParameterListsCodeFixProvider(),
            [("C.cs", "C.cs", source)],
            ParameterListFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            addLinkedProject: true,
            diagnosticOptions: ParameterListFormattingAnalyzerTests.Enabled).ConfigureAwait(
                continueOnCapturedContext: false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeTrue();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source.Contains(
            "M(\n    int first,\n    int second)",
            StringComparison.Ordinal));
    }
}
