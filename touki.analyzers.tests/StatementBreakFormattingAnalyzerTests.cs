// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

[TestClass]
public partial class StatementBreakFormattingAnalyzerTests
{
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [StatementBreakFormattingAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        Dictionary<string, string>? options = null) =>
        AnalyzerTestHarness.GetDiagnosticsAsync(
            new StatementBreakFormattingAnalyzer(),
            source,
            options,
            diagnosticOptions: s_enabled);

    [TestMethod]
    public async Task Analyze_DiagnosticNotExplicitlyEnabled_ReportsNothing()
    {
        const string source = "class Sample { int Add(int left, int right) { return left +\nright; } }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new StatementBreakFormattingAnalyzer(),
            source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("return left *\n            right;", "*")]
    [DataRow("return left /\n            right;", "/")]
    [DataRow("return left %\n            right;", "%")]
    [DataRow("return left +\n            right;", "+")]
    [DataRow("return left -\n            right;", "-")]
    [DataRow("return left <<\n            1;", "<<")]
    [DataRow("return left >>\n            1;", ">>")]
    [DataRow("return (int)left >>>\n            1;", ">>>")]
    [DataRow("return left <\n            right;", "<")]
    [DataRow("return left >\n            right;", ">")]
    [DataRow("return left <=\n            right;", "<=")]
    [DataRow("return left >=\n            right;", ">=")]
    [DataRow("return left ==\n            right;", "==")]
    [DataRow("return left !=\n            right;", "!=")]
    [DataRow("return left &\n            right;", "&")]
    [DataRow("return left ^\n            right;", "^")]
    [DataRow("return left |\n            right;", "|")]
    [DataRow("return left &&\n            right;", "&&")]
    [DataRow("return left ||\n            right;", "||")]
    [DataRow("return left ??\n            right;", "??")]
    [DataRow("return left as\n            object;", "as")]
    [DataRow("return (int)value is > 0 and\n            < 10;", "and")]
    [DataRow("return (int)value is 1 or\n            2;", "or")]
    [DataRow("return value.\n            Length;", ".")]
    [DataRow("return value?.\n            Length;", "?.")]
    [DataRow("return value?[\n            0];", "?[")]
    [DataRow("return start..\n            end;", "..")]
    public async Task Analyze_LeadingOperatorAtEndOfLine_ReportsOperator(
        string statement,
        string expectedOperator)
    {
        string source = $$"""
            class Sample
            {
                int GetValue() => 0;

                object Method(dynamic left, dynamic right, dynamic value, int start, int end)
                {
                    {{statement}}
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)
            .Should().Be(expectedOperator);
    }

    [TestMethod]
    [DataRow("=")]
    [DataRow("+=")]
    [DataRow("-=")]
    [DataRow("*=")]
    [DataRow("/=")]
    [DataRow("%=")]
    [DataRow("&=")]
    [DataRow("^=")]
    [DataRow("|=")]
    [DataRow("<<=")]
    [DataRow(">>=")]
    [DataRow("??=")]
    public async Task Analyze_AssignmentOperatorAtEndOfLine_ReportsNothing(string assignmentOperator)
    {
        string source = $$"""
            class Sample
            {
                dynamic Method(dynamic left, dynamic right)
                {
                    left {{assignmentOperator}}
                        right;
                    return left;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("=")]
    [DataRow("+=")]
    [DataRow("-=")]
    [DataRow("*=")]
    [DataRow("/=")]
    [DataRow("%=")]
    [DataRow("&=")]
    [DataRow("^=")]
    [DataRow("|=")]
    [DataRow("<<=")]
    [DataRow(">>=")]
    [DataRow("??=")]
    public async Task Analyze_AssignmentOperatorAtBeginningOfLine_ReportsOperator(string assignmentOperator)
    {
        string source = $$"""
            class Sample
            {
                dynamic Method(dynamic left, dynamic right)
                {
                    left
                        {{assignmentOperator}} right;
                    return left;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be(assignmentOperator);
    }

    [TestMethod]
    public async Task Analyze_UnsignedRightShiftAssignmentAtEndOfLine_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right)
                {
                    left >>>=
                        right;
                    return left;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_UnsignedRightShiftAssignmentAtBeginningOfLine_ReportsOperator()
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right)
                {
                    left
                        >>>= right;
                    return left;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be(">>>=");
    }

    [TestMethod]
    public async Task Analyze_PointerMemberAccessAtEndOfLine_ReportsOperator()
    {
        const string source = """
            unsafe struct Value
            {
                public int Number;
            }

            unsafe class Sample
            {
                int Method(Value* value)
                {
                    return value->
                        Number;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("->");
    }

    [TestMethod]
    public async Task Analyze_RelationalPatternOperatorAtEndOfLine_ReportsOperator()
    {
        const string source = """
            class Sample
            {
                bool Method(int value)
                {
                    return value is >
                        0;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be(">");
    }

    [TestMethod]
    public async Task Analyze_ConditionalAccessSplitInsideOperator_ReportsOperatorPair()
    {
        const string source = """
            class Sample
            {
                int? Method(string value)
                {
                    return value?
                        .Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject)
            .Should().Be($"?{Environment.NewLine}            .");
    }

    [TestMethod]
    [DataRow("return value?.\n            Clone();", "?.")]
    [DataRow("return value?[\n            0].Trim();", "?[")]
    public async Task Analyze_InvokedConditionalAccessAtEndOfLine_ReportsOperator(
        string statement,
        string expectedOperator)
    {
        string source = $$"""
            class Sample
            {
                object Method(string[] value)
                {
                    {{statement}}
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be(expectedOperator);
    }

    [TestMethod]
    public async Task Analyze_ConditionalAccessPairSplitAcrossContinuationLines_ReportsOperatorPair()
    {
        const string source = """
            class Sample
            {
                int? Method(string value)
                {
                    return value
                        ?
                        .Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject)
            .Should().Be($"?{Environment.NewLine}            .");
    }

    [TestMethod]
    public async Task Analyze_ConditionalAccessWithCommentInsideOperator_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int? Method(string value)
                {
                    return value? /* Keep. */
                        .Length;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ConditionalAccessWithInternalComment_ReportsIndependentNestedOperator()
    {
        const string source = """
            class Sample
            {
                bool? Method(Wrapper value, bool first, bool second) =>
                    value? /* Keep. */
                        .Check(
                            first
                            || second);
            }

            class Wrapper
            {
                public bool Check(bool value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("||");
    }

    [TestMethod]
    public async Task Analyze_OverindentedConditionalAccessWithComment_ReportsIndependentNestedOperator()
    {
        const string source = """
            class Sample
            {
                bool? Method(Wrapper value, bool first, bool second) =>
                    value
                        ? /* Keep. */ .Check(
                            first
                            || second);
            }

            class Wrapper
            {
                public bool Check(bool value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("||");
    }

    [TestMethod]
    public async Task Analyze_OverindentedArrowWithLeadingComment_ReportsIndependentNestedOperator()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second)
                    // Keep this association.
                        => Check(
                            first
                            || second);

                bool Check(bool value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("||");
    }

    [TestMethod]
    public async Task Analyze_ConditionalOperatorsAtEndOfLines_ReportsBothOperators()
    {
        const string source = """
            class Sample
            {
                int Method(bool condition, int whenTrue, int whenFalse)
                {
                    return condition ?
                        whenTrue :
                        whenFalse;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Select(GetDiagnosticText).Should().BeEquivalentTo(["?", ":"]);
    }

    [TestMethod]
    public async Task Analyze_ConditionalAfterMultilineInvocation_IndentsBeyondConditionBlock()
    {
        const string source = """
            class Sample
            {
                int Method(bool value)
                {
                    return Check(
                        value)
                            ? 1
                            : 0;
                }

                bool Check(bool value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ConditionalAfterMultilineInvocationAtStatementLevel_ReportsBothOperators()
    {
        const string source = """
            class Sample
            {
                int Method(bool value)
                {
                    return Check(
                        value)
                        ? 1
                        : 0;
                }

                bool Check(bool value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Select(GetDiagnosticText).Should().BeEquivalentTo(["?", ":"]);
    }

    [TestMethod]
    public async Task Analyze_ExpressionBodiedConditionalAfterMultilineInvocation_IndentsBeyondLastArgument()
    {
        const string source = """
            class Sample
            {
                int Method(
                    bool value) => Check(
                        value)
                            ? 1
                            : 0;

                bool Check(bool value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_NestedConditional_IndentsBeyondNestedConditionLine()
    {
        const string source = """
            class Sample
            {
                int Method(bool first, bool second)
                {
                    return first
                        ? 1
                        : second
                            ? 2
                            : 3;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ExpressionBodiedComparison_IndentsBeyondOperandLine()
    {
        const string source = """
            class Sample
            {
                bool Method() =>
                    GetValue()
                        != 0;

                int GetValue() => 0;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ExpressionBodiedLogicalChain_IndentsBeyondOperandLine()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second) =>
                    first
                        && second;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_SamePrecedenceChain_OperatorsRemainAligned()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, bool third) =>
                    first
                        && second
                        && third;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_SamePrecedencePeersAroundMultilineOperand_RemainAligned()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, bool third, bool fourth) =>
                    first
                        && (second
                            || third)
                        && fourth;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_SamePrecedenceInsideParentheses_AddsScopeLevel()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, bool third) =>
                    first
                        && (second
                            && third);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ParenthesesAfterInlineOperator_DoNotAddAnotherScopeLevel()
    {
        const string source = """
            class Sample
            {
                bool First(bool first, bool second, bool third)
                {
                    return first || (second
                        && third);
                }

                bool Second(bool first, bool second, bool third, bool fourth)
                {
                    return first && ((second && third)
                        || fourth);
                }

                bool Pattern(int value)
                {
                    return value is not (1
                        or 2);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ConditionalAndThenConditionalOr_IndentsNewPrecedenceLevel()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, bool third) =>
                    first
                        && second
                            || third;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ConditionalAndThenEquality_IndentsNewPrecedenceLevel()
    {
        const string source = """
            class Sample
            {
                bool Method(int first, int second, int third) =>
                    first < second
                        && second
                            == third;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_BinaryOperatorInArgument_IndentsBeyondOperandLine()
    {
        const string source = """
            class Sample
            {
                System.Exception Method() => new System.InvalidOperationException(
                    "first"
                        + "second");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_IsAtEndOfLineWithIndentedPattern_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                bool Method(object value) =>
                    value is
                        string;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_PartiallyBrokenSamePrecedenceChain_ReportsInlinePeers()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, bool third, bool fourth) =>
                    first && second
                        && third && fourth;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);
        int firstOperator = source.IndexOf("&&", StringComparison.Ordinal);
        int lastOperator = source.LastIndexOf("&&", StringComparison.Ordinal);

        diagnostics.Select(diagnostic => diagnostic.Location.SourceSpan.Start)
            .Should().BeEquivalentTo([firstOperator, lastOperator]);
    }

    [TestMethod]
    public async Task Analyze_PartiallyBrokenAdditiveCategory_ReportsInlinePeer()
    {
        const string source = """
            class Sample
            {
                int Method(int first, int second, int third) =>
                    first + second
                        - third;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("+");
    }

    [TestMethod]
    public async Task Analyze_ConditionalAccessInBrokenAssignment_IndentsBeyondAssignmentOperand()
    {
        const string source = """
            class Sample
            {
                string? Method(object value)
                {
                    string? result =
                        (value as string)
                            ?.ToString();
                    return result;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_BlockLambdaBodyAlignedWithContainingLine_ReportsNothing()
    {
        const string source = """
            using System;

            class Sample
            {
                static Action Action = () =>
                {
                    Console.WriteLine();
                };
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_LocalFunctionLogicalChain_IndentsBeyondOperandLine()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second)
                {
                    return Local();

                    bool Local() =>
                        first
                            || second;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ConditionalEndingInMultilineRawLiteral_UsesClosingLineIndentation()
    {
        const string source = """"
            class Sample
            {
                int Method(string value)
                {
                    return value == """
                        text
                        """
                            ? 1
                            : 0;
                }
            }
            """";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_LeadingOperatorsIndentedOneLevel_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, bool third)
                {
                    return first
                        && (second
                            || third);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_PatternOperatorInCatchFilter_UsesFilterScopeIndentation()
    {
        const string source = """
            using System;

            class Sample
            {
                void Method()
                {
                    try
                    {
                    }
                    catch (Exception exception)
                        when (exception is ArgumentException
                            or InvalidOperationException)
                    {
                    }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_LeadingOperatorWithWrongIndentation_ReportsOperator()
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right)
                {
                    return left
                          + right;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("+");
    }

    [TestMethod]
    public async Task Analyze_IndentSizeTwo_UsesConfiguredIndentation()
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right)
                {
                    return left
                      + right;
                }
            }
            """;
        Dictionary<string, string> options = new() { ["indent_size"] = "2" };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_TabIndentStyle_UsesOneTabPerLevel()
    {
        const string source =
            "class Sample\n"
            + "{\n"
            + "\tint Method(int left, int right)\n"
            + "\t{\n"
            + "\t\treturn left\n"
            + "\t\t\t+ right;\n"
            + "\t}\n"
            + "}\n";
        Dictionary<string, string> options = new() { ["indent_style"] = "tab" };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_IndentSizeTab_UsesConfiguredTabWidth()
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right)
                {
                    return left
                      + right;
                }
            }
            """;
        Dictionary<string, string> options = new()
        {
            ["indent_size"] = "tab",
            ["tab_width"] = "2"
        };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("17")]
    [DataRow("invalid")]
    public async Task Analyze_InvalidIndentSize_UsesFourSpaces(string indentSize)
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right)
                {
                    return left
                        + right;
                }
            }
            """;
        Dictionary<string, string> options = new() { ["indent_size"] = indentSize };

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, options).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("int Value\n        => 1;", "=>")]
    [DataRow("System.Func<int, int> Transform = value\n        => value;", "=>")]
    [DataRow("int Method(int value) => value switch { 0\n        => 1, _ => 2 };", "=>")]
    public async Task Analyze_ArrowAtBeginningOfLine_ReportsArrow(string member, string expectedOperator)
    {
        string source = $$"""
            class Sample
            {
                {{member}}
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be(expectedOperator);
    }

    [TestMethod]
    public async Task Analyze_ArrowAtEndWithIndentedBody_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int Value =>
                    1;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ArgumentLambdaBodyIndentedFromLambda_ReportsNothing()
    {
        const string source = """
            using System;

            class Sample
            {
                int Method(Func<int, int> transform) => transform(1);

                int Run() => Method(
                    value =>
                        value + 1);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_LambdasInMemberChain_AlignsOperatorsAndIndentsBodies()
    {
        const string source = """
            using System.Linq;

            class Sample
            {
                string[] Method(string[] values) =>
                    values
                        .Where(static value =>
                            value.Length != 0)
                        .Select(static value =>
                            value.Trim())
                        .ToArray();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_SwitchArmBodyIndentedFromArm_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int Method(int value) => value switch
                {
                    0 =>
                        1,
                    _ =>
                        2
                };
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_DirectRelationalSwitchArms_UseSwitchScopeIndentation()
    {
        const string source = """
            class Sample
            {
                int Method(int value) => value switch
                {
                    < 0 => -1,
                    > 0 => 1,
                    _ => 0
                };
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_LeftmostRelationalPatternInBinarySwitchArm_UsesSwitchScopeIndentation()
    {
        const string source = """
            class Sample
            {
                int Method(int value) => value switch
                {
                    < 0 or > 10 => -1,
                    _ => 0
                };
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MultilineBinarySwitchArm_OverindentedOperator_ReportsOperator()
    {
        const string source = """
            class Sample
            {
                int Method(int value) => value switch
                {
                    < 0
                            or > 10 => -1,
                    _ => 0
                };
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("or");
    }

    [TestMethod]
    public async Task Analyze_ExpressionBodiedAccessorBodyIndentedFromAccessor_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int Value
                {
                    get =>
                        1;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_AllExpressionBodiedAccessors_UseAccessorIndentation()
    {
        const string source = """
            using System;

            class Sample
            {
                int _value;
                Action? _changed;

                int Value
                {
                    get =>
                        _value;
                    set =>
                        _value = value;
                }

                int InitValue
                {
                    get =>
                        _value;
                    init =>
                        _value = value;
                }

                event Action Changed
                {
                    add =>
                        _changed += value;
                    remove =>
                        _changed -= value;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MultilineRawLiteralBeforeBinaryOperator_ReportsOperator()
    {
        const string source = """"
            class Sample
            {
                string Value => """
                    left
                    """ +
                    "right";
            }
            """";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("+");
    }

    [TestMethod]
    public async Task Analyze_MultilineRawPatternBeforeSwitchArmArrow_ReportsNothing()
    {
        const string source = """"
            class Sample
            {
                int Method(string value) => value switch
                {
                    """
                    left
                    """ =>
                        1,
                    _ => 0
                };
            }
            """";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ArrowBodyWithWrongIndentation_ReportsArrow()
    {
        const string source = """
            class Sample
            {
                int Value =>
                      1;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("=>");
    }

    [TestMethod]
    public async Task Analyze_ExpressionBodyAfterMultilineParameters_IndentsBeyondParameterBlock()
    {
        const string source = """
            class Sample
            {
                int Method(
                    int value) =>
                        value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_CollectionExpressionBody_AlignsOpeningBracketWithDeclaration()
    {
        const string source = """
            class Sample
            {
                int[] Values =>
                [
                    1,
                    2
                ];
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_CollectionAndInitializerAssignments_AlignOpeningDelimitersWithDeclaration()
    {
        const string source = """
            class Sample
            {
                int[] Collection =
                [
                    1
                ];

                int[] Initializer =
                {
                    1
                };
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_SingleLineCollectionsAndInitializers_UseContinuationIndentation()
    {
        const string source = """
            class Sample
            {
                int[] CollectionField =
                    [1, 2];

                int[] InitializerField =
                    { 1, 2 };

                void Method()
                {
                    int[] collectionLocal =
                        [1, 2];

                    int[] initializerLocal =
                        { 1, 2 };
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_AnonymousCollections_DistinguishesMultilineDelimiters()
    {
        const string source = """
            class Sample
            {
                object Value => new
                {
                    Multiline =
                    [
                        1
                    ],
                    SingleLine =
                        [1]
                };
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new StatementBreakFormattingAnalyzer(),
            source,
            diagnosticOptions: s_enabled,
            expectedCompilerDiagnosticIds: ["CS9176", "CS9176"]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_CollectionExpressionLambda_AlignsOpeningBracketWithLambda()
    {
        const string source = """
            using System;

            class Sample
            {
                Func<int[]> Values = () =>
                [
                    1
                ];
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_CollectionExpressionSwitchArm_AlignsOpeningBracketWithArm()
    {
        const string source = """
            class Sample
            {
                int[] Values(int value) => value switch
                {
                    _ =>
                    [
                        1
                    ]
                };
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ArrowBodyWithCommentAndWrongIndentation_ReportsArrow()
    {
        const string source = """
            class Sample
            {
                int Value => // Keep this comment.
                      1;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("=>");
    }

    [TestMethod]
    public async Task Analyze_OperatorRelocationWouldCrossComment_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right)
                {
                    return left + // Preserve this association.
                        right;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_OperatorRelocationWouldCrossDirective_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right)
                {
                    return left +
            #if USE_LEFT
                        left;
            #else
                        right;
            #endif
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_ArrowRelocationWouldCrossComment_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                int Value
                    // Preserve this association.
                    => 1;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_OneLineOperators_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                bool Method(int value) => value > 0 ? value + 1 > 2 : value is 0 or 1;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_OpenEndedRangeBrokenAfterOperator_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                System.Range Method(System.Index end)
                {
                    return ..
                        end;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_QueryLetEqualsAtEndOfLine_ReportsNothing()
    {
        const string source = """
            using System.Linq;

            class Sample
            {
                object Method(int[] values) =>
                    from value in values
                    let doubled =
                        value * 2
                    select doubled;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_QueryLetEqualsAtBeginningOfLine_ReportsEquals()
    {
        const string source = """
            using System.Linq;

            class Sample
            {
                object Method(int[] values) =>
                    from value in values
                    let doubled
                        = value * 2
                    select doubled;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("=");
    }

    [TestMethod]
    public async Task Analyze_AnonymousMemberEqualsAtEndOfLine_ReportsNothing()
    {
        const string source = """
            class Sample
            {
                object Method(int value) => new
                {
                    Result =
                        value
                };
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_AnonymousMemberEqualsAtBeginningOfLine_ReportsEquals()
    {
        const string source = """
            class Sample
            {
                object Method(int value) => Wrap(
                    new
                    {
                        Result
                        = value
                    });

                object Wrap(object value) => value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        GetDiagnosticText(diagnostics.Should().ContainSingle().Subject).Should().Be("=");
    }

    [TestMethod]
    public async Task Analyze_UsingAliasAndAttributeNameEqualsAtEndOfLine_ReportNothing()
    {
        const string source = """
            using System;
            using Text =
                System.String;

            sealed class MarkerAttribute : Attribute
            {
                public string Message { get; set; } = string.Empty;
            }

            [Marker(Message =
                "Use another type.")]
            class Sample
            {
                Text Value => string.Empty;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_GeneratedCode_ReportsNothing()
    {
        const string source = "// <auto-generated/>\nclass Sample { int Value\n    => 1; }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Analyze_MissingRightOperand_ReportsNothing()
    {
        const string source = "class Sample { int Method(int left) { return left\n        + ; } }";

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new StatementBreakFormattingAnalyzer(),
            source,
            diagnosticOptions: s_enabled,
            expectedCompilerDiagnosticIds: ["CS1525"]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    private static string GetDiagnosticText(Diagnostic diagnostic)
    {
        Location location = diagnostic.Location;
        SourceText source = location.SourceTree!.GetText();
        return source.ToString(location.SourceSpan);
    }
}