// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

[TestClass]
public partial class AllmanFormattingAnalyzerTests
{
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [AllmanFormattingAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        Dictionary<string, string>? options = null) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(
            new AllmanFormattingAnalyzer(),
            source,
            options,
            diagnosticOptions: s_enabled);

    [TestMethod]
    public async Task Analyze_DiagnosticNotExplicitlyEnabled_ReportsNothing()
    {
        const string source = "class Sample {\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AllmanFormattingAnalyzer(),
            source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("namespace Scope {\n}\n")]
    [DataRow("enum Sample {\n    Value\n}\n")]
    [DataRow("class Sample\n{\n    void Method() {\n    }\n}\n")]
    [DataRow("class Sample\n{\n    int Value {\n        get;\n    }\n}\n")]
    [DataRow("class Sample\n{\n    void Method(int value)\n    {\n        switch (value) {\n        }\n    }\n}\n")]
    [DataRow("class Item { public int Value; }\n\nclass Sample\n{\n    Item Create() => new Item {\n        Value = 1\n    };\n}\n")]
    [DataRow("class Sample\n{\n    int[] Values = new[] {\n        1\n    };\n}\n")]
    [DataRow("class Sample\n{\n    object Value => new {\n        Number = 1\n    };\n}\n")]
    [DataRow("record Item { public int Value; }\n\nclass Sample\n{\n    Item Copy(Item item) => item with {\n        Value = 1\n    };\n}\n")]
    [DataRow("class Sample\n{\n    int Convert(int value) => value switch {\n        _ => 0\n    };\n}\n")]
    [DataRow("record Item { public int Value; }\n\nclass Sample\n{\n    bool Matches(Item item) => item is {\n        Value: 1\n    };\n}\n")]
    public async Task Analyze_MultilineBraceDelimitedConstructWithSameLineOpeningBrace_ReportsDiagnostic(
        string source)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("{");
    }

    [TestMethod]
    public async Task Analyze_MultilineDeclarationWithSameLineOpeningBrace_ReportsDiagnostic()
    {
        const string source = "class Sample {\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be(AllmanFormattingAnalyzer.DiagnosticId);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("{");
    }

    [TestMethod]
    public async Task Analyze_CompleteSingleLineDeclarationWithinLimit_ReportsNothing()
    {
        const string source = "class Sample { }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_InterpolationDelimiters_ReportsNothing()
    {
        const string source = "class Sample\n{\n    string Format(int value) => $\"{value}\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_RawInterpolationDelimiters_ReportsNothing()
    {
        const string source =
            "class Sample\n{\n    string Format(int value) => $$\"\"\"{{value}}\"\"\";\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_EmptyPropertyPatternWithSingleLineBlocksDisabled_ReportsNothing()
    {
        const string source = "class Sample\n{\n    bool IsNotNull(object? value) => value is { } nonNull;\n}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_StructuralBraceInsideCSharp10Interpolation_ReportsNothing()
    {
        const string source =
            "class Sample\n{\n    string Format() => $\"{new int[] { 1 }}\";\n}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = "20"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AllmanFormattingAnalyzer(),
            source,
            options,
            diagnosticOptions: s_enabled,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp10)).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_StructuralBraceInsideCSharp11Interpolation_ReportsDiagnostic()
    {
        const string source =
            "class Sample\n{\n    string Format() => $\"{new int[] { 1 }}\";\n}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = "20"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AllmanFormattingAnalyzer(),
            source,
            options,
            diagnosticOptions: s_enabled,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp11)).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_ExtensionBlockWithSameLineOpeningBrace_ReportsDiagnostic()
    {
        const string source = """
            static class Extensions
            {
                extension(string value) {
                    public int Length => value.Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AllmanFormattingAnalyzer(),
            source,
            diagnosticOptions: s_enabled,
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview)).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("{");
    }

    [TestMethod]
    public async Task Analyze_SingleLineConstructAtConfiguredLimit_ReportsNothing()
    {
        const string source = "class Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = "16"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_SingleLineConstructOverConfiguredLimit_ReportsDiagnostic()
    {
        const string source = "class Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = "15"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_RuleSpecificLimit_OverridesStandardLimit()
    {
        const string source = "class Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = "16",
            ["max_line_length"] = "15"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_StandardLimitUsedWhenRuleSpecificLimitMissing_ReportsDiagnostic()
    {
        const string source = "class Sample { }\n";
        Dictionary<string, string> options = new() { ["max_line_length"] = "15" };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("2147483648")]
    public async Task Analyze_InvalidRuleSpecificLimit_UsesStandardLimit(string configured)
    {
        const string source = "class Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = configured,
            ["max_line_length"] = "15"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_InvalidAllowSingleLineBlocks_UsesEnabledDefault()
    {
        const string source = "class Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "invalid"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_InvalidClosingBraceRequirement_UsesEnabledDefault()
    {
        const string source = "class Sample\n{\n    void First()\n    {\n    }\n    void Second() { }\n}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.RequireBlankLineAfterClosingBraceOption] = "invalid",
            [AllmanFormattingAnalyzer.RequireBlankLineAfterMultilineStatementOption] = "false"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_InvalidMultilineStatementRequirement_UsesEnabledDefault()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    int value =
                        GetValue();
                    Use(value);
                }

                int GetValue() => 0;
                void Use(int value) { }
            }
            """;
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.RequireBlankLineAfterClosingBraceOption] = "false",
            [AllmanFormattingAnalyzer.RequireBlankLineAfterMultilineStatementOption] = "invalid"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_SingleLineConstructsDisabled_ReportsDiagnostic()
    {
        const string source = "class Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Analyze_ClosingBraceWithoutFollowingBlankLine_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void First()
                {
                }
                void Second() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_ClosingBraceBeforeClosingBraceOrEndOfFile_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ClosingBraceRequirementDisabled_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                void First()
                {
                }
                void Second() { }
            }
            """;
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.RequireBlankLineAfterClosingBraceOption] = "false"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ClosingBraceBeforeElse_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                void Method(bool condition)
                {
                    if (condition)
                    {
                    }
                    else
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ClosingBraceBeforeCatchOrFinally_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    try
                    {
                    }
                    catch (System.Exception)
                    {
                    }
                    finally
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_DoWhileContinuation_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    int count = 0;
                    do
                    {
                        count++;
                    }
                    while (count < 1);

                    Use(count);
                }

                void Use(int value) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_BlankLineBeforeDoWhileClause_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    do
                    {
                    }

                    while (false);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_SameLineDoWhileClause_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    do
                    {
                    } while (false);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_CompactDoWhileStatement_ReportsNothing()
    {
        const string source = "class Sample\n{\n    void Method()\n    {\n        do { } while (false);\n    }\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_BlankLineBeforeContinuationClause_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method(bool condition)
                {
                    if (condition)
                    {
                    }

                    else
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_SameLineContinuationClause_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method(bool condition)
                {
                    if (condition)
                    {
                    } else
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_CompactBlockBeforeMultilineContinuation_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method(bool condition)
                {
                    if (condition) { } else
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_BlockCommentBeforeSameLineContinuation_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method(bool condition)
                {
                    if (condition)
                    {
                    } /* First branch. */ else
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_DirectivesAndBlankLineBeforeContinuationClause_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method(bool condition)
                {
                    if (condition)
                    {
                    }

            #if TRACE
            #endif

                    else
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_InactiveClosingBraceBeforeCode_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void First()
                {
                }
            #if HIDDEN

            }
            #endif
                void Second() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AllmanFormattingAnalyzer(),
            source,
            diagnosticOptions: s_enabled,
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview)).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_ElseDirectiveWithAdjacentComment_ReportsNothing()
    {
        const string source = """
            class Sample
            {
            #if ACTIVE
                void First()
                {
                }
            #else// Alternate branch.
                void Second() { }
            #endif
            }
            """;
        CSharpParseOptions parseOptions = new(
            LanguageVersion.Preview,
            preprocessorSymbols: ["ACTIVE"]);

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AllmanFormattingAnalyzer(),
            source,
            diagnosticOptions: s_enabled,
            parseOptions: parseOptions).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_DirectivesBetweenClosingBraceAndBlankOrClosingBrace_ReportsNothing()
    {
        const string source = """
            class Sample
            {
            #if false
                void Hidden() { }
            #else
                void First()
                {
                }
            #endif

                void Second()
                {
                }
            #if true
            #endif
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_DirectivesBetweenClosingBraceAndCode_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void First()
                {
                }
            #if true
            #endif
                void Second() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_DirectivesBetweenMultilineStatementAndCode_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    int value =
                        GetValue();
            #if true
            #endif
                    Use(value);
                }

                int GetValue() => 0;
                void Use(int value) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be(";");
    }

    [TestMethod]
    public async Task Analyze_ConditionalAlternativeBlocksAndStatements_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int GetValue()
            #if true
                {
                    return 0;
                }
            #else
                {
                    return 1;
                }
            #endif

                bool GetFlag()
                {
                    return true
            #if true
                        && true;
            #else
                        && false;
            #endif
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ClosingBraceWithTrailingComment_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                void First()
                {
                } // First
                void Second() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_CompactAccessorListsOnAdjacentLines_ReportsNothing()
    {
        const string source = "class Sample\n{\n    int First { get; }\n    int Second { get; }\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_AdjacentAccessorBodies_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int this[int index]
                {
                    get
                    {
                        return index;
                    }
                    set
                    {
                    }
                }

                event System.Action Changed
                {
                    add
                    {
                    }
                    remove
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_BlankLineBetweenAccessorBodies_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                int this[int index]
                {
                    get
                    {
                        return index;
                    }

                    set
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("}");
    }

    [TestMethod]
    public async Task Analyze_MultilineSwitchExpressionWithoutFollowingBlankLine_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                int MiniportIfIndex => 1;
                int LowerIfIndex => 2;
                int MetadataSize => 3;

                object? PayloadValue(int index) => index switch
                {
                    0 => MiniportIfIndex,
                    1 => LowerIfIndex,
                    2 => MetadataSize,
                    _ => null
                };
                void Next() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be(";");
    }

    [TestMethod]
    public async Task Analyze_MultilineSwitchExpressionWithFollowingBlankLine_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                object? PayloadValue(int index) => index switch
                {
                    0 => index,
                    _ => null
                };

                void Next() { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MultilineSwitchExpressionInForHeader_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                void Method(int index)
                {
                    for (int value = index switch
                    {
                        _ => 0
                    };
                        value < 1;
                        value++)
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MultilineStatementWithoutFollowingBlankLine_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    int value =
                        GetValue();
                    Use(value);
                }

                int GetValue() => 0;
                void Use(int value) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be(";");
    }

    [TestMethod]
    public async Task Analyze_SpacingViolationBeforeLaterBraceViolation_ReportsOriginalSemicolon()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue();\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "\n"
            + "    void Later() {\n"
            + "    }\n"
            + "}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        int expectedStart = source.IndexOf(";\n        Use(value)", StringComparison.Ordinal);
        diagnostic.Location.SourceSpan.Should().Be(new TextSpan(expectedStart, 1));
    }

    [TestMethod]
    public async Task Analyze_MultilineStatementRequirementDisabled_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    int value =
                        GetValue();
                    Use(value);
                }

                int GetValue() => 0;
                void Use(int value) { }
            }
            """;
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.RequireBlankLineAfterMultilineStatementOption] = "false"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MultilineStatementWithTrailingCommentWithoutBlankLine_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    int value =
                        GetValue(); // Keep this comment.
                    Use(value);
                }

                int GetValue() => 0;
                void Use(int value) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be(";");
    }

    [TestMethod]
    public async Task Analyze_MultilineStatementFollowedByStatementOnSameLine_ReportsDiagnostic()
    {
        const string source = """
            class Sample
            {
                void Method()
                {
                    int value =
                        GetValue(); Use(value);
                }

                int GetValue() => 0;
                void Use(int value) { }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be(";");
    }

    [TestMethod]
    public async Task Analyze_MultilineStatementBeforeContainingClosingBrace_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int GetValue() => 0;

                int Method()
                {
                    return GetValue()
                        + 1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MultilineFieldDeclaration_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int _value =
                    1;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_CrlfSourceWithRequiredSpacing_ReportsNothing()
    {
        const string source =
            "class Sample\r\n"
            + "{\r\n"
            + "    void First() { }\r\n"
            + "\r\n"
            + "    void Second() { }\r\n"
            + "}\r\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ClosingBraceAtEndOfFileWithoutNewline_ReportsNothing()
    {
        const string source = "class Sample\n{\n}";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_GeneratedCode_ReportsNothing()
    {
        const string source = "// <auto-generated/>\nclass Sample {\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_DisabledTextContainingBrace_ReportsNothing()
    {
        const string source = "class Sample\n{\n#if false\n    if (true) {\n#endif\n}\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MalformedOuterDeclaration_ReportsCompleteInnerBraceViolationWithoutThrowing()
    {
        const string source = "class Sample {\n    void Method() {\n    }\n";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AllmanFormattingAnalyzer(),
            source,
            diagnosticOptions: s_enabled,
            expectedCompilerDiagnosticIds: ["CS1513"]).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("{");
    }
}
