// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class TrailingWhitespaceAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        => await AnalyzerTestHarness
            .GetDiagnosticsAsync(new TrailingWhitespaceAnalyzer(), source)
            .ConfigureAwait(false);

    private static string ReportedText(Diagnostic diagnostic)
    {
        Location location = diagnostic.Location;
        return location.SourceTree!.GetText().ToString(location.SourceSpan);
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TrailingSpaces_ReportsExactRun()
    {
        string source = "class Sample\n{\n    int Value;   \n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(TrailingWhitespaceAnalyzer.DiagnosticId);
        ReportedText(diagnostics[0]).Should().Be("   ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_TrailingTab_Reports()
    {
        string source = "class Sample\n{\n    int Value;\t\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        ReportedText(diagnostics[0]).Should().Be("\t");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_WhitespaceOnlyLine_ReportsWholeLine()
    {
        string source = "class Sample\n{\n    \n    int Value;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        ReportedText(diagnostics[0]).Should().Be("    ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_LastLineWithoutNewLine_Reports()
    {
        string source = "class Sample\n{\n    int Value;\n}   ";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        ReportedText(diagnostics[0]).Should().Be("   ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_CleanSource_ReportsNothing()
    {
        string source = "class Sample\n{\n    int Value;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_CarriageReturnLineEndings_ReportsNothing()
    {
        // The carriage return is part of the line break, not trailing whitespace on the line.
        string source = "class Sample\r\n{\r\n    int Value;\r\n}\r\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_InsideRawStringLiteral_ReportsNothing()
    {
        // The trailing space on the content line is part of the string's value. Removing it would change
        // what the program does, so it must not be reported.
        string source = "class Sample\n{\n    string Value = \"\"\"\n        a   \n        b\n        \"\"\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_InsideVerbatimString_ReportsNothing()
    {
        string source = "class Sample\n{\n    string Value = @\"a   \nb\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_InsideInterpolatedVerbatimString_ReportsNothing()
    {
        string source = "class Sample\n{\n    string Name = \"n\";\n    string Value => $@\"{Name}   \nb\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_InsideInterpolatedRawString_ReportsNothing()
    {
        string source =
            "class Sample\n{\n    string Name = \"n\";\n    string Value => $\"\"\"\n        {Name}   \n        b\n        \"\"\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_InsideUtf8RawString_ReportsNothing()
    {
        string source =
            "class Sample\n{\n    static System.ReadOnlySpan<byte> Value => \"\"\"\n        a   \n        b\n        \"\"\"u8;\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_InsideDisabledText_ReportsNothing()
    {
        // The parser never interprets excluded text, so a raw string could be hiding in there.
        string source = "class Sample\n{\n#if UNDEFINED_SYMBOL\n    int Value;   \n#endif\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_SpaceCarryingCombiningMark_ReportsNothing()
    {
        // U+0301 COMBINING ACUTE ACCENT attaches to the space before it. The mark is not whitespace, so the
        // backwards scan stops there and the space it sits on is never reported.
        string source = "class Sample\n{\n    // caret: \u0020\u0301\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_SpaceAfterCombiningMark_ReportsOnlyTheTrailingSpace()
    {
        string source = "class Sample\n{\n    // caret: \u0020\u0301 \n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        ReportedText(diagnostics[0]).Should().Be(" ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_InsideComment_Reports()
    {
        string source = "class Sample\n{\n    // note   \n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        ReportedText(diagnostics[0]).Should().Be("   ");
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_MultipleLines_ReportsEach()
    {
        string source = "class Sample \n{\n    int Value;  \n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task AnalyzeSyntaxTree_GeneratedCode_ReportsNothing()
    {
        string source = "// <auto-generated/>\nclass Sample\n{\n    int Value;   \n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}
