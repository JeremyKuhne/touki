// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace Touki.Analyzers;

[TestClass]
public class AllmanFormattingCodeFixTests
{
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [AllmanFormattingAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static async Task<string> ApplyFixAsync(
        string source,
        Dictionary<string, string>? options = null,
        CSharpParseOptions? parseOptions = null) =>
        await CodeFixTestHarness.ApplyFixAsync(
            new AllmanFormattingAnalyzer(),
            new FormatAllmanCodeFixProvider(),
            source,
            AllmanFormattingAnalyzer.DiagnosticId,
            options,
            s_enabled,
            parseOptions: parseOptions).ConfigureAwait(false);

    [TestMethod]
    public void GetFixAllProvider_Default_IsDocumentBased()
    {
        FixAllProvider provider = new FormatAllmanCodeFixProvider().GetFixAllProvider();

        provider.Should().NotBeSameAs(WellKnownFixAllProviders.BatchFixer);
        provider.GetSupportedFixAllScopes().Should().BeEquivalentTo(
            [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution]);
    }

    [TestMethod]
    public async Task Format_OverlappingViolations_FormatsWholeDocumentAndIsIdempotent()
    {
        const string source = """
            class Sample {
                void Method() {
                    int value =
                        GetValue();
                    Use(value);
                }
                int GetValue() => 0;

                void Use(int value) { }
            }
            """;
        const string expected = """
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

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
        fixedAgain.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_CompactNestedConstructsDisallowed_IndentsByNesting()
    {
        const string source = "class Sample { int Value { get; } }\n";
        const string expected = "class Sample\n{\n    int Value\n    {\n        get;\n    }\n}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_CompactNestedBlockStartingLine_PreservesExistingIndentation()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        { int value = 0; }\n"
            + "    }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        {\n"
            + "            int value = 0;\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = "25"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_InterpolationDelimiters_LeavesThemUnchanged()
    {
        const string source = "class Sample { string Value => $\"{1}\"; }\n";
        const string expected = "class Sample\n{\n    string Value => $\"{1}\";\n}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_EmptyPropertyPatternWithSingleLineBlocksDisabled_LeavesPatternUnchanged()
    {
        const string source = "class Sample\n{\n    bool IsNotNull(object? value) => value is { } nonNull;\n}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task Format_StructuralBraceInsideCSharp10Interpolation_LeavesSourceUnchanged()
    {
        const string source =
            "class Sample\n{\n    string Format() => $\"{new int[] { 1 }}\";\n}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = "20"
        };

        string fixedSource = await ApplyFixAsync(
            source,
            options,
            new CSharpParseOptions(LanguageVersion.CSharp10)).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task Format_StructuralBraceInsideCSharp11Interpolation_FormatsAndCompiles()
    {
        const string source =
            "class Sample\n{\n    string Format() => $\"{new int[] { 1 }}\";\n}\n";
        const string expected =
            "class Sample\n{\n    string Format() => $\"{new int[]\n    {\n        1\n    }}\";\n}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = "20"
        };

        string fixedSource = await ApplyFixAsync(
            source,
            options,
            new CSharpParseOptions(LanguageVersion.CSharp11)).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Format_LinkedCSharp10And11DocumentsWithDivergentFormatting_OffersNoFix(bool fixAll)
    {
        const string source = "class Sample\n{\n    string Format() => $\"{new int[] { 1 }}\";\n}\n";
        (string Name, string FilePath, string Source)[] sources = [("Shared.cs", "Shared.cs", source)];
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.MaxLineLengthOption] = "20"
        };

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new AllmanFormattingAnalyzer(),
            new FormatAllmanCodeFixProvider(),
            sources,
            AllmanFormattingAnalyzer.DiagnosticId,
            fixAll,
            options,
            s_enabled,
            addLinkedProject: true,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp11),
            linkedProjectParseOptions: new CSharpParseOptions(LanguageVersion.CSharp10)).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
        if (fixAll)
        {
            result.CodeFixActionOffered.Should().BeNull();
            result.FixAllActionOffered.Should().BeFalse();
        }
        else
        {
            result.CodeFixActionOffered.Should().BeFalse();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Format_LinkedDocumentsWithCompatibleFormatting_UpdatesBothDocuments(bool fixAll)
    {
        const string source = "class Sample {\n}\n";
        (string Name, string FilePath, string Source)[] sources = [("Shared.cs", "Shared.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new AllmanFormattingAnalyzer(),
            new FormatAllmanCodeFixProvider(),
            sources,
            AllmanFormattingAnalyzer.DiagnosticId,
            fixAll,
            diagnosticOptions: s_enabled,
            addLinkedProject: true,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp11),
            linkedProjectParseOptions: new CSharpParseOptions(LanguageVersion.CSharp10)).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Should().HaveCount(2)
            .And.OnlyContain(document => document.Source == "class Sample\n{\n}\n");
        result.CodeFixActionOffered.Should().BeTrue();
        if (fixAll)
        {
            result.FixAllActionOffered.Should().BeTrue();
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Format_LinkedDocumentsWithConflictingOptions_OffersNoFix(
        bool stricterProjectFirst,
        bool fixAll)
    {
        const string source = "class Sample { }\n";
        (string Name, string FilePath, string Source)[] sources = [("Shared.cs", "Shared.cs", source)];
        Dictionary<string, string> strict = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };
        Dictionary<string, string> loose = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "true"
        };
        Dictionary<string, string> firstOptions = stricterProjectFirst ? strict : loose;
        Dictionary<string, string> linkedOptions = stricterProjectFirst ? loose : strict;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new AllmanFormattingAnalyzer(),
            new FormatAllmanCodeFixProvider(),
            sources,
            AllmanFormattingAnalyzer.DiagnosticId,
            fixAll,
            firstOptions,
            s_enabled,
            addLinkedProject: true,
            linkedProjectOptions: linkedOptions).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
        if (fixAll)
        {
            result.CodeFixActionOffered.Should().BeNull();
            result.FixAllActionOffered.Should().BeFalse();
        }
        else
        {
            result.CodeFixActionOffered.Should().BeFalse();
        }
    }

    [TestMethod]
    public async Task Format_CrlfWithTabIndentation_PreservesConfiguredLayout()
    {
        const string source = "class Sample {\r\n\tvoid Method() {\r\n\t}\r\n}\r\n";
        const string expected = "class Sample\r\n{\r\n\tvoid Method()\r\n\t{\r\n\t}\r\n}\r\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false",
            ["indent_style"] = "tab"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("invalid", 4)]
    [DataRow("0", 4)]
    [DataRow("-1", 4)]
    [DataRow("16", 16)]
    [DataRow("17", 4)]
    [DataRow("2147483647", 4)]
    public async Task Format_IndentSizeBoundary_UsesBoundedIndentation(string configured, int expectedSize)
    {
        const string source = "class Sample { int Value; }\n";
        string expected = $"class Sample\n{{\n{new string(' ', expectedSize)}int Value;\n}}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false",
            ["indent_size"] = configured
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ProjectedExpansionAboveLimit_OffersNoFix()
    {
        const int indentationLength = 64 * 1024;
        const int nestingDepth = 256;
        string indentation = new(' ', indentationLength);
        string openingBraces = string.Concat(Enumerable.Repeat("{ ", nestingDepth));
        string closingBraces = string.Concat(Enumerable.Repeat("} ", nestingDepth));
        string source = $"{indentation}class Sample {{ void Method() {openingBraces}{closingBraces}}}\n";
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task Format_ProjectedExpansionAboveLimit_RegistersNoCodeAction()
    {
        const int indentationLength = 64 * 1024;
        const int nestingDepth = 256;
        string indentation = new(' ', indentationLength);
        string openingBraces = string.Concat(Enumerable.Repeat("{ ", nestingDepth));
        string closingBraces = string.Concat(Enumerable.Repeat("} ", nestingDepth));
        string source = $"{indentation}class Sample {{ void Method() {openingBraces}{closingBraces}}}\n";
        (string Name, string FilePath, string Source)[] sources = [("Large.cs", "A-Large.cs", source)];
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new AllmanFormattingAnalyzer(),
            new FormatAllmanCodeFixProvider(),
            sources,
            AllmanFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            options,
            s_enabled).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(source);
    }

    [TestMethod]
    public async Task Format_MultilineStatementWithTrailingComment_InsertsBlankLineAfterPhysicalLine()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void First()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue(); // Value.\n"
            + "        Use(value);\n"
            + "    } // First.\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void First()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue(); // Value.\n"
            + "\n"
            + "        Use(value);\n"
            + "    } // First.\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_MultilineStatementWithTrailingBlockComment_InsertsBlankLineAfterComment()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue(); /* Value\n"
            + "                           details. */\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue(); /* Value\n"
            + "                           details. */\n"
            + "\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_MultilineStatementWithTrailingDocumentationComment_InsertsBlankLineAfterComment()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue(); /// <summary>\n"
            + "                        ///  Value details.\n"
            + "                        /// </summary>\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue(); /// <summary>\n"
            + "                        ///  Value details.\n"
            + "                        /// </summary>\n"
            + "\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_MultilineStatementWithTrailingDocumentationBlock_InsertsBlankLineAfterComment()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue(); /** <summary>\n"
            + "                            Value details.\n"
            + "                          </summary> */\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue(); /** <summary>\n"
            + "                            Value details.\n"
            + "                          </summary> */\n"
            + "\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_OversizedAndEligibleDocuments_FormatsOnlyEligibleDocument()
    {
        const int indentationLength = 64 * 1024;
        const int nestingDepth = 256;
        string indentation = new(' ', indentationLength);
        string openingBraces = string.Concat(Enumerable.Repeat("{ ", nestingDepth));
        string closingBraces = string.Concat(Enumerable.Repeat("} ", nestingDepth));
        string largeSource =
            $"{indentation}class Large {{ void Method() {openingBraces}{closingBraces}}}\n";
        (string Name, string FilePath, string Source)[] sources =
        [
            ("Large.cs", "A-Large.cs", largeSource),
            ("Small.cs", "B-Small.cs", "class Small {\n}\n")
        ];
        Dictionary<string, string> options = new()
        {
            [AllmanFormattingAnalyzer.AllowSingleLineBlocksOption] = "false"
        };

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new AllmanFormattingAnalyzer(),
            new FormatAllmanCodeFixProvider(),
            sources,
            AllmanFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            options,
            s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.Documents.Single(document => document.Name == "Large.cs").Source.Should().Be(largeSource);
        result.Documents.Single(document => document.Name == "Small.cs").Source.Should().Be(
            "class Small\n{\n}\n");
        result.AnalyzerDiagnostics.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Format_MultilineStatementWithSameLineSuccessor_MovesSuccessorAfterBlankLine()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue(); Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue();\n"
            + "\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ContinuationClauses_LeavesSourceUnchanged()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition)\n"
            + "        {\n"
            + "        }\n"
            + "        else\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "        try\n"
            + "        {\n"
            + "        }\n"
            + "        catch (System.Exception)\n"
            + "        {\n"
            + "        }\n"
            + "        finally\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task Format_BlankLinesBetweenAccessorBodies_RemovesBlankLines()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    int this[int index]\n"
            + "    {\n"
            + "        get\n"
            + "        {\n"
            + "            return index;\n"
            + "        }\n"
            + "\n"
            + "        set\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "\n"
            + "    event System.Action Changed\n"
            + "    {\n"
            + "        add\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "        remove\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    int this[int index]\n"
            + "    {\n"
            + "        get\n"
            + "        {\n"
            + "            return index;\n"
            + "        }\n"
            + "        set\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "\n"
            + "    event System.Action Changed\n"
            + "    {\n"
            + "        add\n"
            + "        {\n"
            + "        }\n"
            + "        remove\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_BlankLinesBeforeContinuationClauses_RemovesBlankLines()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition)\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "        else\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "        try\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "        catch (System.Exception)\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "        finally\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition)\n"
            + "        {\n"
            + "        }\n"
            + "        else\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "        try\n"
            + "        {\n"
            + "        }\n"
            + "        catch (System.Exception)\n"
            + "        {\n"
            + "        }\n"
            + "        finally\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_SameLineContinuationClauses_MovesClausesToNextLine()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition)\n"
            + "        {\n"
            + "        } else\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "        try\n"
            + "        {\n"
            + "        } catch (System.Exception)\n"
            + "        {\n"
            + "        } finally\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition)\n"
            + "        {\n"
            + "        }\n"
            + "        else\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "        try\n"
            + "        {\n"
            + "        }\n"
            + "        catch (System.Exception)\n"
            + "        {\n"
            + "        }\n"
            + "        finally\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_CompactBlockBeforeMultilineContinuation_MovesClauseToNextLine()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition) { } else\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition) { }\n"
            + "        else\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_BlockCommentBeforeSameLineContinuation_PreservesComment()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition)\n"
            + "        {\n"
            + "        } /* First branch. */ else\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition)\n"
            + "        {\n"
            + "        } /* First branch. */\n"
            + "        else\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_DirectivesAndBlankLineBeforeContinuationClause_RemovesOnlyBlankLine()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition)\n"
            + "        {\n"
            + "        }\n"
            + "\n"
            + "#if TRACE\n"
            + "#endif\n"
            + "\n"
            + "        else\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void Method(bool condition)\n"
            + "    {\n"
            + "        if (condition)\n"
            + "        {\n"
            + "        }\n"
            + "#if TRACE\n"
            + "#endif\n"
            + "        else\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_InactiveCodeBetweenClosingBraceAndCode_InsertsAfterDirectiveSequence()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void First()\n"
            + "    {\n"
            + "    }\n"
            + "#if HIDDEN\n"
            + "    void Hidden() { }\n"
            + "\n"
            + "#endif\n"
            + "    void Second() { }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void First()\n"
            + "    {\n"
            + "    }\n"
            + "#if HIDDEN\n"
            + "    void Hidden() { }\n"
            + "\n"
            + "#endif\n"
            + "\n"
            + "    void Second() { }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ConditionalDirectiveBoundaries_LeavesSourceUnchanged()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    int GetValue()\n"
            + "#if true\n"
            + "    {\n"
            + "        return 0;\n"
            + "    }\n"
            + "#else\n"
            + "    {\n"
            + "        return 1;\n"
            + "    }\n"
            + "#endif\n"
            + "\n"
            + "    bool GetFlag()\n"
            + "    {\n"
            + "        return true\n"
            + "#if true\n"
            + "            && true;\n"
            + "#else\n"
            + "            && false;\n"
            + "#endif\n"
            + "    }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task Format_DirectivesBetweenRequiredBlankLineAndCode_InsertsAfterDirectives()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void First()\n"
            + "    {\n"
            + "    }\n"
            + "#if true\n"
            + "#endif\n"
            + "    void Second()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue();\n"
            + "#if true\n"
            + "#endif\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    void First()\n"
            + "    {\n"
            + "    }\n"
            + "#if true\n"
            + "#endif\n"
            + "\n"
            + "    void Second()\n"
            + "    {\n"
            + "        int value =\n"
            + "            GetValue();\n"
            + "#if true\n"
            + "#endif\n"
            + "\n"
            + "        Use(value);\n"
            + "    }\n"
            + "\n"
            + "    int GetValue() => 0;\n"
            + "    void Use(int value) { }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_MultilineSwitchExpression_InsertsBlankLineAfterSemicolon()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    int MiniportIfIndex => 1;\n"
            + "    int LowerIfIndex => 2;\n"
            + "    int MetadataSize => 3;\n"
            + "\n"
            + "    object? PayloadValue(int index) => index switch\n"
            + "    {\n"
            + "        0 => MiniportIfIndex,\n"
            + "        1 => LowerIfIndex,\n"
            + "        2 => MetadataSize,\n"
            + "        _ => null\n"
            + "    };\n"
            + "    void Next() { }\n"
            + "}\n";
        const string expected =
            "class Sample\n"
            + "{\n"
            + "    int MiniportIfIndex => 1;\n"
            + "    int LowerIfIndex => 2;\n"
            + "    int MetadataSize => 3;\n"
            + "\n"
            + "    object? PayloadValue(int index) => index switch\n"
            + "    {\n"
            + "        0 => MiniportIfIndex,\n"
            + "        1 => LowerIfIndex,\n"
            + "        2 => MetadataSize,\n"
            + "        _ => null\n"
            + "    };\n"
            + "\n"
            + "    void Next() { }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
        fixedAgain.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_MultilineSwitchExpressionInForHeader_LeavesSourceUnchanged()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "    void Method(int index)\n"
            + "    {\n"
            + "        for (int value = index switch\n"
            + "        {\n"
            + "            _ => 0\n"
            + "        };\n"
            + "            value < 1;\n"
            + "            value++)\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task Format_MultilineInitializer_PreservesTerminatingSemicolon()
    {
        const string source =
            "class Item { public int Value; }\n"
            + "\n"
            + "class Sample\n"
            + "{\n"
            + "    Item Create() => new Item {\n"
            + "        Value = 1\n"
            + "    };\n"
            + "}\n";
        const string expected =
            "class Item { public int Value; }\n"
            + "\n"
            + "class Sample\n"
            + "{\n"
            + "    Item Create() => new Item\n"
            + "    {\n"
            + "        Value = 1\n"
            + "    };\n"
            + "}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(FixAllScope.Document)]
    [DataRow(FixAllScope.Project)]
    [DataRow(FixAllScope.Solution)]
    public async Task FormatAll_Scope_FormatsDocumentsWithinScope(FixAllScope scope)
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            ("First.cs", "A-First.cs", "class First {\n}\n"),
            ("Second.cs", "B-Second.cs", "class Second {\n}\n")
        ];
        (string Name, string FilePath, string Source)[] additionalProjectSources =
        [
            ("Additional.cs", "Z-Additional.cs", "class Additional {\n}\n")
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new AllmanFormattingAnalyzer(),
            new FormatAllmanCodeFixProvider(),
            sources,
            AllmanFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: scope,
            additionalProjectSources: additionalProjectSources).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.InitialAnalyzerDiagnosticCount.Should().Be(3);

        CodeFixTestDocument first = result.Documents.Single(document => document.Name == "First.cs");
        CodeFixTestDocument second = result.Documents.Single(document => document.Name == "Second.cs");
        CodeFixTestDocument additional = result.Documents.Single(document => document.Name == "Additional.cs");
        first.Source.Should().Be("class First\n{\n}\n");

        switch (scope)
        {
            case FixAllScope.Document:
                second.Source.Should().Be("class Second {\n}\n");
                additional.Source.Should().Be("class Additional {\n}\n");
                result.AnalyzerDiagnostics.Should().HaveCount(2);
                break;
            case FixAllScope.Project:
                second.Source.Should().Be("class Second\n{\n}\n");
                additional.Source.Should().Be("class Additional {\n}\n");
                result.AnalyzerDiagnostics.Should().ContainSingle();
                break;
            case FixAllScope.Solution:
                second.Source.Should().Be("class Second\n{\n}\n");
                additional.Source.Should().Be("class Additional\n{\n}\n");
                result.AnalyzerDiagnostics.Should().BeEmpty();
                break;
        }
    }

    [TestMethod]
    public async Task FormatAll_Canceled_ThrowsOperationCanceledException()
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            ("Sample.cs", "Sample.cs", "class Sample {\n}\n")
        ];
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Func<Task> action = async () => await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new AllmanFormattingAnalyzer(),
            new FormatAllmanCodeFixProvider(),
            sources,
            AllmanFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllCancellationToken: cancellation.Token).ConfigureAwait(false);

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }
}
