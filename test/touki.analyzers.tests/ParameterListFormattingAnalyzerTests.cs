// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class ParameterListFormattingAnalyzerTests
{
    internal static readonly Dictionary<string, ReportDiagnostic> Enabled =
        new() { [ParameterListFormattingAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    internal static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        IReadOnlyDictionary<string, string>? options = null) =>
            AnalyzerTestHarness.GetDiagnosticsAsync(
                new ParameterListFormattingAnalyzer(),
                source,
                options,
                diagnosticOptions: Enabled);

    [TestMethod]
    [DataRow("class C\n{\n    void M(int first, int second) { }\n}")]
    [DataRow("class C\n{\n    void M(\n        int first,\n        int second) { }\n}")]
    [DataRow("class C\n{\n    void M() => M();\n}")]
    [DataRow("class C\n{\n    int M(int a, int b) => 0;\n    int V => M(M(\n        1,\n        2), 3);\n}")]
    [DataRow("class C\n{\n    void M(System.Action action, int n) { }\n    void N() => M(() =>\n    {\n    }, 3);\n}")]
    [DataRow("class C\n{\n    void M(params int[] values) { }\n    void N() => M(1, 2);\n}")]
    public async Task AnalyzeList_AllowedLayout_ReportsNothing(string source)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source).ConfigureAwait(
                continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("class C\n{\n    void M(int first,\n        int second) { }\n}")]
    [DataRow("class C\n{\n    void M(\n        int first, int second) { }\n}")]
    [DataRow("class C\n{\n    void M(\n      int first,\n        int second) { }\n}")]
    [DataRow("class C\n{\n    void M(\n        int first,\n        int second\n    ) { }\n}")]
    [DataRow("class C\n{\n    void M(int a, int b) { }\n    void N() => M(1,\n        2);\n}")]
    [DataRow("class C\n{\n    C(int a, int b) { }\n    static C N() => new C(1,\n        2);\n}")]
    [DataRow("record C(int first,\n    int second);")]
    [DataRow("delegate void D(int first,\n    int second);")]
    [DataRow("class C\n{\n    int this[int first,\n        int second] => 0;\n}")]
    [DataRow("class C\n{\n    int this[int first, int second] => 0;\n    int V => this[1,\n        2];\n}")]
    [DataRow("[System.Obsolete(\"text\",\n    false)]\nclass C { }")]
    [DataRow("class C\n{\n    System.Func<int, int, int> V => (int first,\n        int second) => first;\n}")]
    public async Task AnalyzeList_MixedOrMisindentedLayout_ReportsAtOpeningDelimiter(string source)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source).ConfigureAwait(
                continueOnCapturedContext: false);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Location.SourceSpan.Length.Should().Be(1);
        source[diagnostics[0].Location.SourceSpan.Start].Should().BeOneOf('(', '[');
    }

    [TestMethod]
    [DataRow(139, false)]
    [DataRow(140, false)]
    [DataRow(141, true)]
    public async Task AnalyzeList_DefaultLineLength_Uses140CharacterBoundary(int length, bool expected)
    {
        const string prefix = "    void M(int ";
        const string suffix = ") { }";
        string line = prefix + new string('p', length - prefix.Length - suffix.Length) + suffix;
        string source = "class C\n{\n" + line + "\n}";

        line.Length.Should().Be(length);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Length.Should().Be(expected ? 1 : 0);
    }

    [TestMethod]
    public async Task AnalyzeList_Rule140Override_TakesPrecedenceOverStandard120()
    {
        const string prefix = "    void M(int ";
        const string suffix = ") { }";
        string line = prefix + new string('p', 130 - prefix.Length - suffix.Length) + suffix;
        string source = "class C\n{\n" + line + "\n}";
        Dictionary<string, string> options = new()
        {
            [ParameterListFormattingAnalyzer.MaxLineLengthOption] = "140",
            ["max_line_length"] = "120"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(-1, false)]
    [DataRow(0, false)]
    [DataRow(1, true)]
    public async Task AnalyzeList_LengthBoundary_UsesFullPhysicalLine(int offset, bool expected)
    {
        const string line = "    void M(int first, int second) { }";
        const string source = "class C\n{\n" + line + "\n}";
        Dictionary<string, string> options = new()
        {
            [ParameterListFormattingAnalyzer.MaxLineLengthOption] = (line.Length - offset).ToString()
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            options).ConfigureAwait(
                continueOnCapturedContext: false);

        diagnostics.Length.Should().Be(expected ? 1 : 0);
    }

    [TestMethod]
    [DataRow("0", "20", true)]
    [DataRow("-1", "20", true)]
    [DataRow("invalid", "20", true)]
    [DataRow("200", "20", false)]
    [DataRow("0", "invalid", false)]
    public async Task AnalyzeList_LengthOptions_UsesPositiveOverrideThenStandardThenDefault(
        string ruleLength,
        string standardLength,
        bool expected)
    {
        const string source = "class C\n{\n    void M(int first, int second) { }\n}";
        Dictionary<string, string> options = new()
        {
            [ParameterListFormattingAnalyzer.MaxLineLengthOption] = ruleLength,
            ["max_line_length"] = standardLength
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            options).ConfigureAwait(
                continueOnCapturedContext: false);

        diagnostics.Length.Should().Be(expected ? 1 : 0);
    }

    [TestMethod]
    public async Task AnalyzeList_TabIndentation_UsesConfiguredUnit()
    {
        const string source = "class C\n{\n\tvoid M(\n\t\tint first,\n\t\tint second) { }\n}";
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            new Dictionary<string, string>
            {
                ["indent_style"] = "tab"
            }).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeList_DisabledByDefault_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new ParameterListFormattingAnalyzer(),
            "class C { void M(int first,\nint second) { } }").ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeList_GeneratedCode_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new ParameterListFormattingAnalyzer(),
            "// <auto-generated/>\nclass C { void M(int first,\nint second) { } }",
            fileName: "C.g.cs",
            diagnosticOptions: Enabled).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeList_NameofOperator_IsNotAnArgumentList()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "class C { string Value => nameof(C); }",
            new Dictionary<string, string>
            {
                ["max_line_length"] = "10"
            }).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeList_UnescapedNameofMethod_IsChecked()
    {
        const string source = "class C { int nameof(int x) => x; int V => nameof(1\n); }";
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnalyzeList_EscapedNameofMethod_IsChecked()
    {
        const string source = "class C { int @nameof(int value) => value; int V => @nameof(1); }";
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            source,
            new Dictionary<string, string>
            {
                ["max_line_length"] = "10"
            }).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task AnalyzeList_IncompleteParameter_ReportsNoAnalyzerFailure()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new ParameterListFormattingAnalyzer(),
            "class C { void M(int first,\n) { } }",
            diagnosticOptions: Enabled,
            expectedCompilerDiagnosticIds: ["CS1031", "CS1001"]).ConfigureAwait(continueOnCapturedContext: false);

        diagnostics.Should().BeEmpty();
    }
}
