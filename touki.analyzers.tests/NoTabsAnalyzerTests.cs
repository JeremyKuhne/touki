// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class NoTabsAnalyzerTests
{
    // The rule ships disabled, so every test that expects a report has to turn it on the way a project would.
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [NoTabsAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    // Spelled out rather than referenced: the code fix hardcodes this name too, so it is a contract between
    // two assemblies and a rename has to break something.
    private const string ReplacementProperty = "Replacement";

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        Dictionary<string, string>? options = null)
        => await AnalyzerTestHarness
            .GetDiagnosticsAsync(new NoTabsAnalyzer(), source, options, fileName: null, s_enabled)
            .ConfigureAwait(false);

    private static string ReportedText(Diagnostic diagnostic)
    {
        Location location = diagnostic.Location;
        return location.SourceTree!.GetText().ToString(location.SourceSpan);
    }

    private static string Replacement(Diagnostic diagnostic) =>
        diagnostic.Properties[ReplacementProperty]!;

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabIndentedLine_ReportsTab()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(NoTabsAnalyzer.DiagnosticId);
        ReportedText(diagnostics[0]).Should().Be("\t");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_DisabledByDefault_ReportsNothing()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness
            .GetDiagnosticsAsync(new NoTabsAnalyzer(), source)
            .ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_ConsecutiveTabs_ReportsOneRun()
    {
        string source = "class Sample\n{\n\t\tint Value;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        ReportedText(diagnostics[0]).Should().Be("\t\t");
        Replacement(diagnostics[0]).Should().Be(new string(' ', 8));
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInComment_ReportsTab()
    {
        string source = "class Sample\n{\n    // name\tvalue\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        ReportedText(diagnostics[0]).Should().Be("\t");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInStringLiteral_ReportsNothing()
    {
        string source = "class Sample\n{\n    string Value = \"a\\tb\";\n    string Literal = \"a\tb\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInVerbatimString_ReportsNothing()
    {
        string source = "class Sample\n{\n    string Value = @\"a\tb\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInRawStringLiteral_ReportsNothing()
    {
        string source = "class Sample\n{\n    string Value = \"\"\"\n        a\tb\n        \"\"\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInSingleLineRawStringLiteral_ReportsNothing()
    {
        string source = "class Sample\n{\n    string Value = \"\"\"a\tb\"\"\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInUtf8StringLiteral_ReportsNothing()
    {
        string source = "class Sample\n{\n    static System.ReadOnlySpan<byte> Value => \"a\tb\"u8;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInUtf8SingleLineRawStringLiteral_ReportsNothing()
    {
        string source = "class Sample\n{\n    static System.ReadOnlySpan<byte> Value => \"\"\"a\tb\"\"\"u8;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInUtf8MultiLineRawStringLiteral_ReportsNothing()
    {
        string source =
            "class Sample\n{\n    static System.ReadOnlySpan<byte> Value => \"\"\"\n        a\tb\n        \"\"\"u8;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInCharacterLiteral_ReportsNothing()
    {
        string source = "class Sample\n{\n    char Value = '\t';\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInInterpolatedRawStringLiteral_ReportsNothing()
    {
        string source =
            "class Sample\n{\n    string Name = \"n\";\n    string Value => $\"\"\"a\tb{Name}\"\"\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabInDisabledText_ReportsNothing()
    {
        string source = "class Sample\n{\n#if UNDEFINED_SYMBOL\n\tint Value;\n#endif\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NoTabs_ReportsNothing()
    {
        string source = "class Sample\n{\n    int Value;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_GeneratedCode_ReportsNothing()
    {
        string source = "// <auto-generated/>\nclass Sample\n{\n\tint Value;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_MidLineTab_ExpandsToNextTabStop()
    {
        // The tab starts at column 5 ("int x" then a tab), so with a width of 4 it advances 3 columns to 8,
        // not a fixed 4.
        string source = "class Sample\n{\n    int x\t= 1;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        Replacement(diagnostics[0]).Should().Be("   ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabAfterEarlierTab_CountsExpandedColumns()
    {
        // The first tab expands 0 -> 4, so "public" ends at column 10 and the second tab advances 2 to 12.
        string source = "class Sample\n{\n\tpublic\tint Value;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
        Replacement(diagnostics[0]).Should().Be("    ");
        Replacement(diagnostics[1]).Should().Be("  ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_SpacesPerTabOption_OverridesDefault()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";
        Dictionary<string, string> options = new() { [NoTabsAnalyzer.SpacesPerTabOption] = "2" };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        Replacement(diagnostics.Single()).Should().Be("  ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabWidthOption_UsedWhenRuleOptionAbsent()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";
        Dictionary<string, string> options = new() { ["tab_width"] = "8" };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        Replacement(diagnostics.Single()).Should().Be(new string(' ', 8));
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_IndentSizeOption_UsedWhenTabWidthAbsent()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";
        Dictionary<string, string> options = new() { ["indent_size"] = "3" };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        Replacement(diagnostics.Single()).Should().Be("   ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_RuleOptionBeatsTabWidth()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";
        Dictionary<string, string> options = new()
        {
            [NoTabsAnalyzer.SpacesPerTabOption] = "2",
            ["tab_width"] = "8"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        Replacement(diagnostics.Single()).Should().Be("  ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TabWidthBeatsIndentSize()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";
        Dictionary<string, string> options = new()
        {
            ["tab_width"] = "2",
            ["indent_size"] = "8"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        Replacement(diagnostics.Single()).Should().Be("  ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_NonNumericIndentSize_FallsBackToDefault()
    {
        // "indent_size = tab" is legal EditorConfig and means "whatever tab_width is". With no tab_width to
        // read it has to fall through rather than fail.
        string source = "class Sample\n{\n\tint Value;\n}\n";
        Dictionary<string, string> options = new() { ["indent_size"] = "tab" };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        Replacement(diagnostics.Single()).Should().Be(new string(' ', NoTabsAnalyzer.DefaultSpacesPerTab));
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_ZeroSpacesPerTab_FallsBackToDefault()
    {
        string source = "class Sample\n{\n\tint Value;\n}\n";
        Dictionary<string, string> options = new() { [NoTabsAnalyzer.SpacesPerTabOption] = "0" };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        Replacement(diagnostics.Single()).Should().Be(new string(' ', NoTabsAnalyzer.DefaultSpacesPerTab));
    }
}
