// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class OneStatementPerLineAnalyzerTests
{
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [OneStatementPerLineAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static string Wrap(string body) =>
        "class Sample\n{\n    void M()\n    {\n        "
            + body
            + "\n    }\n    void A() { }\n    void B() { }\n}\n";

    [TestMethod]
    [DataRow("A();\n        B();")]
    [DataRow("if (true) A();")]
    [DataRow("if (true) A(); else B();")]
    [DataRow("for (int i = 0; i < 1; i++) A();")]
    [DataRow("int a = 0, b = 1;")]
    [DataRow("System.Action action = () => { A(); };")]
    [DataRow("string value = \"A(); B();\";")]
    [DataRow("A(); /* B(); */")]
    [DataRow("A(); /* multiline\n        comment */ B();")]
    public async Task AnalyzeStatementList_AllowedLayout_ReportsNothing(string body)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            Wrap(body),
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("A(); B();", 1)]
    [DataRow("A();B();A();", 2)]
    [DataRow("if (true) A(); B();", 1)]
    [DataRow("if (true) { A(); } B();", 1)]
    [DataRow("{ A(); } { B(); }", 1)]
    [DataRow("void Local() { } A();", 1)]
    [DataRow("label: A(); B();", 1)]
    [DataRow("A(); /* comment */ B();", 1)]
    [DataRow("for (int i = 0; i < 1; i++) A(); B();", 1)]
    [DataRow("System.Action action = () => { A(); B(); };", 1)]
    [DataRow("switch (1) { case 1: A(); B(); break; default: break; }", 2)]
    [DataRow("A(\n        ); B();", 1)]
    public async Task AnalyzeStatementList_SharedLine_ReportsEachLaterStatement(string body, int expectedCount)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            Wrap(body),
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().HaveCount(expectedCount)
            .And.OnlyContain(diagnostic => diagnostic.Id == OneStatementPerLineAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeStatementList_SecondStatement_ReportsFirstToken()
    {
        string source = Wrap("A(); B();");
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            source,
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Location.SourceSpan.Start.Should().Be(source.IndexOf("B();", StringComparison.Ordinal));
        diagnostics[0].Location.SourceSpan.Length.Should().Be(1);
    }

    [TestMethod]
    public async Task AnalyzeStatementList_SingleStatementMethodsAndAccessors_ReportsNothing()
    {
        const string source = "class Sample { void M() { M(); } int Value { get { return 1; } } }";
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            source,
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeStatementList_AccessorWithTwoStatements_Reports()
    {
        const string source = "class Sample { int Value { get { int value = 1; return value; } } }";
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            source,
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeStatementList_TopLevelStatements_Reports()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            "System.Console.Write(1); System.Console.Write(2);",
            diagnosticOptions: s_enabled,
            expectedCompilerDiagnosticIds: ["CS8805"]).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeStatementList_DisabledByDefault_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            Wrap("A(); B();")).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeStatementList_GeneratedCode_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            "// <auto-generated/>\n" + Wrap("A(); B();"),
            fileName: "Sample.g.cs",
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeStatementList_MissingSemicolon_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            Wrap("A() B();"),
            diagnosticOptions: s_enabled,
            expectedCompilerDiagnosticIds: ["CS1002"]).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeStatementList_DeepBlocksAndManyStatements_ReportsWithoutRecursion()
    {
        string body = new string('{', 300)
            + string.Concat(Enumerable.Repeat("A();", 2_000))
            + new string('}', 300);

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new OneStatementPerLineAnalyzer(),
            Wrap(body),
            diagnosticOptions: s_enabled).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().HaveCount(1_999);
    }
}
