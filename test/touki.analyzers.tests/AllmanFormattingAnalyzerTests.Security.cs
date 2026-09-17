// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Touki.Analyzers;

public partial class AllmanFormattingAnalyzerTests
{
    [TestMethod]
    public async Task Analyze_WideDeeplyNestedLine_KeepsCharacterReadsLinear()
    {
        const int indentationLength = 64 * 1024;
        const int nestingDepth = 256;
        string indentation = new(' ', indentationLength);
        string openingBraces = string.Concat(Enumerable.Repeat("{ ", nestingDepth));
        string closingBraces = string.Concat(Enumerable.Repeat("} ", nestingDepth));
        CountingSourceText source = new(SourceText.From(
            $"{indentation}class Sample {{ void Method() {openingBraces}{closingBraces}}}\n"));
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AllmanFormattingAnalyzer(),
            source,
            source.Reset,
            options,
            s_enabled).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
        source.CharacterReads.Should().BeLessThan(source.Length * 50L);
    }

    [TestMethod]
    public void TryFindViolation_WideDeeplyNestedLine_BoundsAllocationAndWithholdsFix()
    {
        const int indentationLength = 64 * 1024;
        const int nestingDepth = 256;
        string indentation = new(' ', indentationLength);
        string openingBraces = string.Concat(Enumerable.Repeat("{ ", nestingDepth));
        string closingBraces = string.Concat(Enumerable.Repeat("} ", nestingDepth));
        SourceText source = SourceText.From(
            $"{indentation}class Sample {{ void Method() {openingBraces}{closingBraces}}}\n");
        SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();
        AllmanFormattingOptions options = new(
            requireBlankLineAfterClosingBrace: true,
            allowSingleLineBlocks: false,
            requireBlankLineAfterMultilineStatement: true,
            maxLineLength: 120,
            indentation: "    ");

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool found = AllmanFormatter.TryFindViolation(
            source,
            root,
            options,
            CancellationToken.None,
            out _,
            out bool fixAvailable);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        found.Should().BeTrue();
        fixAvailable.Should().BeFalse();
        allocated.Should().BeLessThan(2 * 1024 * 1024L);
    }

    [TestMethod]
    public void TryFindViolation_BraceEditCouldActivateDirective_WithholdsFix()
    {
        SourceText source = SourceText.From(
            "class Sample { #define ACTIVE\n#if ACTIVE\nclass Hidden { }\n#endif\n}\n");
        SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();
        AllmanFormattingOptions options = new(
            requireBlankLineAfterClosingBrace: true,
            allowSingleLineBlocks: false,
            requireBlankLineAfterMultilineStatement: true,
            maxLineLength: 120,
            indentation: "    ");

        bool found = AllmanFormatter.TryFindViolation(
            source,
            root,
            options,
            CancellationToken.None,
            out _,
            out bool fixAvailable);
        bool formatted = AllmanFormatter.TryFormat(
            source,
            root,
            options,
            CancellationToken.None,
            out SourceText formattedSource,
            out _);

        found.Should().BeTrue();
        fixAvailable.Should().BeFalse();
        formatted.Should().BeFalse();
        formattedSource.ToString().Should().Be(source.ToString());
    }

    [TestMethod]
    public void TryFindViolation_NewlineHeavyInactiveText_BoundsAllocation()
    {
        const int inactiveLineCount = 128 * 1024;
        SourceText source = SourceText.From(
            $"class Sample\n{{\n#if HIDDEN\n{new string('\n', inactiveLineCount)}#endif\n}}\n");
        _ = source.Lines.Count;
        SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();
        AllmanFormattingOptions options = new(
            requireBlankLineAfterClosingBrace: true,
            allowSingleLineBlocks: true,
            requireBlankLineAfterMultilineStatement: true,
            maxLineLength: 120,
            indentation: "    ");

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool found = AllmanFormatter.TryFindViolation(
            source,
            root,
            options,
            CancellationToken.None,
            out _,
            out _);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        found.Should().BeFalse();
        allocated.Should().BeLessThan(2 * 1024 * 1024L);
    }

    [TestMethod]
    public void TryFindViolation_NestedStatementsSharingTerminalSemicolon_TerminatesPromptly()
    {
        const int inactiveLineCount = 128 * 1024;
        const int nestingDepth = 1024;
        string nestedStatements = string.Concat(Enumerable.Repeat("        if (true)\n", nestingDepth));
        SourceText source = SourceText.From(
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + nestedStatements
            + "        Use(\n"
            + "            0);\n"
            + "#if HIDDEN\n"
            + new string('\n', inactiveLineCount)
            + "#endif\n"
            + "        Use(1);\n"
            + "    }\n"
            + "\n"
            + "    void Use(int value) { }\n"
            + "}\n");
        _ = source.Lines.Count;
        SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();
        AllmanFormattingOptions options = new(
            requireBlankLineAfterClosingBrace: true,
            allowSingleLineBlocks: true,
            requireBlankLineAfterMultilineStatement: true,
            maxLineLength: 120,
            indentation: "    ");

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool found = AllmanFormatter.TryFindViolation(
            source,
            root,
            options,
            CancellationToken.None,
            out _,
            out _);
        stopwatch.Stop();

        found.Should().BeTrue();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

}
