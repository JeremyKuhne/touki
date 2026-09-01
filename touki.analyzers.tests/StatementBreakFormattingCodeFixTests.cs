// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

[TestClass]
public class StatementBreakFormattingCodeFixTests
{
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [StatementBreakFormattingAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static async Task<string> ApplyFixAsync(
        string source,
        Dictionary<string, string>? options = null) =>
        await CodeFixTestHarness.ApplyFixAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            source,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            options,
            s_enabled).ConfigureAwait(false);

    [TestMethod]
    public void GetFixAllProvider_Default_IsDocumentBased()
    {
        FixAllProvider provider = new FormatStatementBreaksCodeFixProvider().GetFixAllProvider();

        provider.Should().NotBeSameAs(WellKnownFixAllProviders.BatchFixer);
        provider.GetSupportedFixAllScopes().Should().BeEquivalentTo(
            [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution]);
    }

    [TestMethod]
    public void GetPathComparer_DirectorySeparator_UsesPlatformPathIdentity()
    {
        StatementBreakFormattingOptions.GetPathComparer('\\').Equals("A.cs", "a.cs").Should().BeTrue();
        StatementBreakFormattingOptions.GetPathComparer('/').Equals("A.cs", "a.cs").Should().BeFalse();
    }

    [TestMethod]
    public void TryReserveDiagnostics_AtAndOverLimit_EnforcesOperationBudget()
    {
        StatementBreakFixAllBudget exact = default;
        StatementBreakFixAllBudget over = default;

        exact.TryReserveDiagnostics(StatementBreakFixAllBudget.MaximumDiagnostics).Should().BeTrue();
        exact.TryReserveDiagnostics(1).Should().BeFalse();
        over.TryReserveDiagnostics(StatementBreakFixAllBudget.MaximumDiagnostics + 1).Should().BeFalse();
        over.TryReserveDiagnostics(StatementBreakFixAllBudget.MaximumDiagnostics).Should().BeTrue();
    }

    [TestMethod]
    public void TryReserveReplacementCharacters_AtAndOverLimit_EnforcesOperationBudget()
    {
        StatementBreakFixAllBudget exact = default;
        StatementBreakFixAllBudget over = default;

        exact.TryReserveReplacementCharacters(StatementBreakFixAllBudget.MaximumReplacementCharacters)
            .Should().BeTrue();
        exact.TryReserveReplacementCharacters(1).Should().BeFalse();
        over.TryReserveReplacementCharacters(StatementBreakFixAllBudget.MaximumReplacementCharacters + 1)
            .Should().BeFalse();
        over.TryReserveReplacementCharacters(StatementBreakFixAllBudget.MaximumReplacementCharacters)
            .Should().BeTrue();
    }

    [TestMethod]
    [DataRow(65_536, true)]
    [DataRow(65_537, false)]
    public async Task FormatAll_DiagnosticBudget_EnforcesLimitThroughProvider(
        int diagnosticCount,
        bool actionExpected)
    {
        const string source = "class Sample\n{\n    int Value\n        => 1;\n}\n";
        const string expected = "class Sample\n{\n    int Value =>\n        1;\n}\n";
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            transformDiagnostics: diagnostics => RepeatDiagnostics(diagnostics, diagnosticCount))
            .ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(diagnosticCount);
        result.FixAllActionOffered.Should().Be(actionExpected);
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(actionExpected ? expected : source);
    }

    [TestMethod]
    [DataRow(512, true)]
    [DataRow(513, false)]
    public async Task FormatAll_ReplacementBudgetAcrossDocuments_EnforcesLimitThroughProvider(
        int secondDocumentRepeats,
        bool actionExpected)
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            ("One.cs", "One.cs", CreateMaximumReplacementSource("One")),
            ("Two.cs", "Two.cs", CreateMaximumReplacementSource("Two"))
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: FixAllScope.Project,
            transformDiagnostics: diagnostics => RepeatDiagnosticsByDocument(
                diagnostics,
                firstDocumentRepeats: 512,
                secondDocumentRepeats))
            .ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(512 + secondDocumentRepeats);
        result.FixAllActionOffered.Should().Be(actionExpected);
        foreach (CodeFixTestDocument document in result.Documents)
        {
            string className = Path.GetFileNameWithoutExtension(document.Name);
            document.Source.Should().Be(
                actionExpected
                    ? CreateMaximumReplacementExpected(className)
                    : CreateMaximumReplacementSource(className));
        }
    }

    [TestMethod]
    public async Task FormatAll_LinkedDiagnosticMissingFromInitialSet_FetchesOnceAndCaches()
    {
        const string source = "class Sample\n{\n    int Value\n        => 1;\n}\n";
        const string expected = "class Sample\n{\n    int Value =>\n        1;\n}\n";
        (string Name, string FilePath, string Source)[] sources =
            [("Shared.cs", "Shared.cs", source)];
        int documentRequests = 0;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: FixAllScope.Document,
            addLinkedProject: true,
            transformDiagnostics: diagnostics => [diagnostics[0]],
            transformFixAllDiagnostics: diagnostics => diagnostics,
            onFixAllDocumentDiagnosticsRequested: _ => documentRequests++)
            .ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == expected);
        documentRequests.Should().Be(2);
    }

    [TestMethod]
    [DataRow(1022, true)]
    [DataRow(1023, false)]
    public async Task FormatAll_RejectedLinkedGroup_ConsumesOperationBudget(
        int laterDocumentRepeats,
        bool actionExpected)
    {
        string sharedSource =
            CreateMaximumReplacementSource("SharedOne")
            + "\n"
            + CreateMaximumReplacementSource("SharedTwo");
        string laterSource = CreateMaximumReplacementSource("Later");
        (string Name, string FilePath, string Source)[] sources =
            [("Shared.cs", "Shared.cs", sharedSource)];
        (string Name, string FilePath, string Source)[] additionalProjectSources =
            [("Later.cs", "Later.cs", laterSource)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: FixAllScope.Solution,
            addLinkedProject: true,
            additionalProjectSources: additionalProjectSources,
            transformDiagnostics: diagnostics =>
                [diagnostics.First(diagnostic => diagnostic.Location.SourceTree!.FilePath.EndsWith(
                    "Shared.cs",
                    StringComparison.Ordinal))],
            transformFixAllDiagnostics: diagnostics => CreateRejectedLinkedBudgetDiagnostics(
                diagnostics,
                laterDocumentRepeats))
            .ConfigureAwait(false);

        result.FixAllActionOffered.Should().Be(actionExpected);
        result.Documents.Where(document => document.Name == "Shared.cs")
            .Should().OnlyContain(document => document.Source == sharedSource);
        result.Documents.Single(document => document.Name == "Later.cs").Source.Should().Be(
            actionExpected ? CreateMaximumReplacementExpected("Later") : laterSource);
    }

    [TestMethod]
    public async Task TryCreateTextChange_OperatorChangedSinceDiagnostic_ReturnsFalse()
    {
        const string source = "class Sample\n{\n    int Value =>\n          1;\n}\n";
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new StatementBreakFormattingAnalyzer(),
            source,
            diagnosticOptions: s_enabled).ConfigureAwait(false);
        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        SourceText changedSource = SourceText.From(source.Replace("=>", "==", StringComparison.Ordinal));

        bool created = TryCreateTextChange(
            diagnostic,
            changedSource,
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("OperatorText", "++")]
    [DataRow("ChangeKind", "2")]
    [DataRow("SpaceAfter", "0")]
    public async Task TryCreateTextChange_MalformedProperty_ReturnsFalse(string propertyName, string value)
    {
        const string source = "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left\n              + right;\n    }\n}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        Diagnostic malformed = CreateDiagnostic(
            diagnostic,
            [.. diagnostic.AdditionalLocations],
            diagnostic.Properties.SetItem(propertyName, value));

        bool created = TryCreateTextChange(
            malformed,
            diagnostic.Location.SourceTree!.GetText(),
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    public async Task TryCreateTextChange_BaseIndentationContainsCode_ReturnsFalse()
    {
        const string source = "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left\n              + right;\n    }\n}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SyntaxTree tree = diagnostic.Location.SourceTree!;
        ImmutableArray<Location> locations =
        [
            diagnostic.AdditionalLocations[0],
            Location.Create(tree, new TextSpan(0, "class".Length))
        ];
        Diagnostic malformed = CreateDiagnostic(diagnostic, locations, diagnostic.Properties);

        bool created = TryCreateTextChange(
            malformed,
            tree.GetText(),
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    public async Task TryCreateTextChange_IndentationReplacementContainsCode_ReturnsFalse()
    {
        const string source = "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left\n              + right;\n    }\n}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SyntaxTree tree = diagnostic.Location.SourceTree!;
        int codeStart = source.IndexOf("left", StringComparison.Ordinal);
        ImmutableArray<Location> locations =
        [
            Location.Create(tree, new TextSpan(codeStart, "left".Length)),
            diagnostic.AdditionalLocations[1]
        ];
        Diagnostic malformed = CreateDiagnostic(diagnostic, locations, diagnostic.Properties);

        bool created = TryCreateTextChange(
            malformed,
            tree.GetText(),
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    public async Task TryCreateTextChange_RelocationSpanContainsCode_ReturnsFalse()
    {
        const string source = "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left +\n            right;\n    }\n}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SyntaxTree tree = diagnostic.Location.SourceTree!;
        int leftStart = source.IndexOf("left +", StringComparison.Ordinal);
        ImmutableArray<Location> locations =
        [
            Location.Create(
                tree,
                TextSpan.FromBounds(leftStart, diagnostic.AdditionalLocations[0].SourceSpan.End)),
            diagnostic.AdditionalLocations[1]
        ];
        Diagnostic malformed = CreateDiagnostic(diagnostic, locations, diagnostic.Properties);

        bool created = TryCreateTextChange(
            malformed,
            tree.GetText(),
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    public async Task TryCreateTextChange_PrimarySpanOverLimit_ReturnsFalse()
    {
        string source = new(' ', 4097);
        source += "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left\n              + right;\n    }\n}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SyntaxTree tree = diagnostic.Location.SourceTree!;
        Diagnostic malformed = Diagnostic.Create(
            diagnostic.Descriptor,
            Location.Create(tree, new TextSpan(0, 4097)),
            [.. diagnostic.AdditionalLocations],
            diagnostic.Properties,
            "+");

        bool created = TryCreateTextChange(
            malformed,
            tree.GetText(),
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    public async Task TryCreateTextChanges_DependentContinuationOverLimit_ReturnsFalse()
    {
        string payload = new('a', StatementBreakDiagnosticData.MaximumChangeCharacters);
        string source =
            "class Sample\n"
            + "{\n"
            + "    string Method(\n"
            + "        string value)\n"
            + "        => Read(\n"
            + $"            \"{payload}\",\n"
            + "            value);\n"
            + "\n"
            + "    string Read(string first, string second) => first + second;\n"
            + "}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SyntaxTree tree = diagnostic.Location.SourceTree!;

        bool created = StatementBreakDiagnosticData.TryCreateTextChanges(
            diagnostic,
            tree.GetRoot(),
            tree.GetText(),
            currentIndentationUnit: "    ",
            CancellationToken.None,
            out _,
            out bool intentionalNoFix);

        created.Should().BeFalse();
        intentionalNoFix.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(StatementBreakDiagnosticData.MaximumChangeCharacters, true)]
    [DataRow(StatementBreakDiagnosticData.MaximumChangeCharacters + 1, false)]
    public async Task TryCreateTextChanges_DependentPhysicalRange_EnforcesLimit(
        int physicalRangeLength,
        bool expected)
    {
        const string commentPrefix = "        //";
        const string continuation = "\n        Read(\n            value)";
        int payloadLength = physicalRangeLength - commentPrefix.Length - continuation.Length;
        string source =
            "class Sample\n"
            + "{\n"
            + "    int Method(\n"
            + "        int value) =>\n"
            + commentPrefix
            + new string('x', payloadLength)
            + continuation
            + ";\n"
            + "\n"
            + "    int Read(int value) => value;\n"
            + "}\n";
        int rangeStart = source.IndexOf(commentPrefix, StringComparison.Ordinal);
        int rangeEnd = source.IndexOf(");", rangeStart, StringComparison.Ordinal) + 1;
        (rangeEnd - rangeStart).Should().Be(physicalRangeLength);
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SyntaxTree tree = diagnostic.Location.SourceTree!;

        bool created = StatementBreakDiagnosticData.TryCreateTextChanges(
            diagnostic,
            tree.GetRoot(),
            tree.GetText(),
            currentIndentationUnit: "    ",
            CancellationToken.None,
            out _,
            out bool intentionalNoFix);

        created.Should().Be(expected);
        intentionalNoFix.Should().Be(!expected);
    }

    [TestMethod]
    public async Task TryCreateTextChanges_AggregateDependentReplacementOverLimit_ReturnsFalse()
    {
        string baseIndentation = new(' ', 1024);
        string source =
            $"{baseIndentation}class Sample\n"
            + $"{baseIndentation}{{\n"
            + $"{baseIndentation}    string Method(\n"
            + $"{baseIndentation}        string value)\n"
            + "        => Read(\n"
            + "            value,\n"
            + "            value,\n"
            + "            value,\n"
            + "            value);\n"
            + $"{baseIndentation}    string Read(\n"
            + $"{baseIndentation}        string first,\n"
            + $"{baseIndentation}        string second,\n"
            + $"{baseIndentation}        string third,\n"
            + $"{baseIndentation}        string fourth) => first;\n"
            + $"{baseIndentation}}}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SyntaxTree tree = diagnostic.Location.SourceTree!;

        bool created = StatementBreakDiagnosticData.TryCreateTextChanges(
            diagnostic,
            tree.GetRoot(),
            tree.GetText(),
            currentIndentationUnit: "    ",
            CancellationToken.None,
            out _,
            out bool intentionalNoFix);

        created.Should().BeFalse();
        intentionalNoFix.Should().BeTrue();
    }

    [TestMethod]
    public async Task FormatAll_OversizedDependentContinuation_SkipsOnlyThatDiagnostic()
    {
        string payload = new('a', StatementBreakDiagnosticData.MaximumChangeCharacters);
        string source =
            "class Sample\n"
            + "{\n"
            + "    string Method(\n"
            + "        string value)\n"
            + "        => Read(\n"
            + $"            \"{payload}\",\n"
            + "            value);\n"
            + "\n"
            + "    int Value\n"
            + "        => 1;\n"
            + "\n"
            + "    string Read(string first, string second) => first + second;\n"
            + "}\n";
        string expected = source.Replace(
            "    int Value\n        => 1;",
            "    int Value =>\n        1;");
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().ContainSingle();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    public async Task TryCreateTextChange_PrimarySpanInsideRawLiteral_ReturnsFalse()
    {
        const string source = """"
            class Sample
            {
                string Text => """
                    +
                    """;

                int Method(int left, int right)
                {
                    return left
                          + right;
                }
            }
            """";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SyntaxTree tree = diagnostic.Location.SourceTree!;
        int literalOperator = source.IndexOf("    +\n", StringComparison.Ordinal) + 4;
        Diagnostic malformed = Diagnostic.Create(
            diagnostic.Descriptor,
            Location.Create(tree, new TextSpan(literalOperator, 1)),
            [.. diagnostic.AdditionalLocations],
            diagnostic.Properties,
            "+");

        bool created = TryCreateTextChange(
            malformed,
            tree.GetText(),
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    public async Task TryCreateTextChange_ReplacementSpanPointsToUnrelatedWhitespace_ReturnsFalse()
    {
        const string source = "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left\n              + right;\n    }\n}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SyntaxTree tree = diagnostic.Location.SourceTree!;
        ImmutableArray<Location> locations =
        [
            Location.Create(tree, new TextSpan(source.IndexOf("    int", StringComparison.Ordinal), 4)),
            diagnostic.AdditionalLocations[1]
        ];
        Diagnostic malformed = CreateDiagnostic(diagnostic, locations, diagnostic.Properties);

        bool created = TryCreateTextChange(
            malformed,
            tree.GetText(),
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    public async Task TryCreateTextChange_Canceled_ThrowsOperationCanceledException()
    {
        const string source = "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left\n              + right;\n    }\n}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Action action = () => TryCreateTextChange(
            diagnostic,
            diagnostic.Location.SourceTree!.GetText(),
            cancellation.Token,
            out _);

        action.Should().Throw<OperationCanceledException>();
    }

    [TestMethod]
    public async Task TryCreateTextChange_IndentationOptionChangedSinceDiagnostic_ReturnsFalse()
    {
        const string source = "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left\n              + right;\n    }\n}\n";
        Diagnostic diagnostic = await GetSingleDiagnosticAsync(source).ConfigureAwait(false);
        SourceText currentSource = diagnostic.Location.SourceTree!.GetText();
        SyntaxNode currentRoot = diagnostic.Location.SourceTree.GetRoot();

        bool created = StatementBreakDiagnosticData.TryCreateTextChange(
            diagnostic,
            currentRoot,
            currentSource,
            currentIndentationUnit: "  ",
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    public async Task TryCreateTextChange_LanguageVersionChangedSinceDiagnostic_ReturnsFalse()
    {
        const string source = "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left >>>\n            right;\n    }\n}\n";
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new StatementBreakFormattingAnalyzer(),
            source,
            diagnosticOptions: s_enabled,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp11)).ConfigureAwait(false);
        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        SourceText currentSource = diagnostic.Location.SourceTree!.GetText();
        SyntaxNode currentRoot = CSharpSyntaxTree.ParseText(
            currentSource,
            new CSharpParseOptions(LanguageVersion.CSharp10)).GetRoot();

        bool created = StatementBreakDiagnosticData.TryCreateTextChange(
            diagnostic,
            currentRoot,
            currentSource,
            currentIndentationUnit: "    ",
            out _);

        created.Should().BeFalse();
    }

    [TestMethod]
    public async Task Format_LeadingOperatorAtEndOfLine_MovesOperatorToContinuationLine()
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right)
                {
                    return left +
                        right;
                }
            }
            """;
        const string expected = """
            class Sample
            {
                int Method(int left, int right)
                {
                    return left
                        + right;
                }
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_LeadingOperatorWithWrongIndentation_IndentsOneLevel()
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
        const string expected = """
            class Sample
            {
                int Method(int left, int right)
                {
                    return left
                        + right;
                }
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ArrowAtBeginningOfLine_MovesArrowAndBreaksBody()
    {
        const string source = """
            class Sample
            {
                int Value
                    => 1;
            }
            """;
        const string expected = """
            class Sample
            {
                int Value =>
                    1;
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ExpressionBodyAfterConstraintClause_IndentsBeyondConstraint() =>
        await AssertExpressionBodyAfterConstraintClauseAsync(fixAll: false).ConfigureAwait(false);

    [TestMethod]
    public async Task FormatAll_ExpressionBodyAfterConstraintClause_IndentsBeyondConstraint() =>
        await AssertExpressionBodyAfterConstraintClauseAsync(fixAll: true).ConfigureAwait(false);

    private static async Task AssertExpressionBodyAfterConstraintClauseAsync(bool fixAll)
    {
        const string source = """
            namespace System.Com
            {
                public struct IUnknown
                {
                    public struct Vtbl { }
                }
            }

            #if NET
            unsafe partial class Sample
            {
                static partial void PopulateIUnknownImpl<TComInterface>(System.Com.IUnknown.Vtbl* vtable)
                    where TComInterface : unmanaged
                    => IUnknownVtableProvider.Populate(vtable);

                static class IUnknownVtableProvider
                {
                    public static void Populate(System.Com.IUnknown.Vtbl* vtable) { }
                }
            }
            #endif
            """;
        const string expected = """
            namespace System.Com
            {
                public struct IUnknown
                {
                    public struct Vtbl { }
                }
            }

            #if NET
            unsafe partial class Sample
            {
                static partial void PopulateIUnknownImpl<TComInterface>(System.Com.IUnknown.Vtbl* vtable)
                    where TComInterface : unmanaged =>
                        IUnknownVtableProvider.Populate(vtable);

                static class IUnknownVtableProvider
                {
                    public static void Populate(System.Com.IUnknown.Vtbl* vtable) { }
                }
            }
            #endif
            """;
        const string generatedSource = """
            // <auto-generated/>
            unsafe partial class Sample
            {
                static partial void PopulateIUnknownImpl<TComInterface>(System.Com.IUnknown.Vtbl* vtable)
                    where TComInterface : unmanaged;
            }
            """;
        (string Name, string FilePath, string Source)[] sources =
        [
            ("Sample.cs", "Sample.cs", source),
            ("Sample.g.cs", "Sample.g.cs", generatedSource)
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll,
            diagnosticOptions: s_enabled,
            addLinkedProject: true,
            parseOptions: new CSharpParseOptions(
                LanguageVersion.Preview,
                preprocessorSymbols: ["NET"]),
            linkedProjectParseOptions: new CSharpParseOptions(LanguageVersion.Preview)).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Where(document => document.Name == "Sample.cs")
            .Should().HaveCount(2).And.OnlyContain(document => document.Source == expected);
        result.Documents.Where(document => document.Name == "Sample.g.cs")
            .Should().HaveCount(2).And.OnlyContain(document => document.Source == generatedSource);
        if (fixAll)
        {
            result.FixAllActionOffered.Should().BeTrue();
        }
        else
        {
            result.CodeFixActionOffered.Should().BeTrue();
        }
    }

    [TestMethod]
    public async Task Format_ArrowBodyWithWrongIndentation_IndentsBodyOneLevel()
    {
        const string source = """
            class Sample
            {
                int Value =>
                      1;
            }
            """;
        const string expected = """
            class Sample
            {
                int Value =>
                    1;
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ExpressionBodyAfterMultilineParameters_IndentsBeyondParameterBlock()
    {
        const string source = """
            class Sample
            {
                int Method(
                    int value) =>
                    value;
            }
            """;
        const string expected = """
            class Sample
            {
                int Method(
                    int value) =>
                        value;
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_MovedExpressionBody_ReindentsMultilineArguments()
    {
        const string source = """
            class Sample
            {
                int Read(
                    int first,
                    int second)
                    => ReadCore(
                        first,
                        second: second);

                int ReadCore(int first, int second) => first + second;
            }
            """;
        const string expected = """
            class Sample
            {
                int Read(
                    int first,
                    int second) =>
                        ReadCore(
                            first,
                            second: second);

                int ReadCore(int first, int second) => first + second;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MovedExpressionBody_ReindentsArgumentComments()
    {
        const string source = """
            class Sample
            {
                int Read(
                    int first,
                    int second)
                    => ReadCore(
                        // Keep this comment with the arguments.
                        first,
                        second);

                int ReadCore(int first, int second) => first + second;
            }
            """;
        const string expected = """
            class Sample
            {
                int Read(
                    int first,
                    int second) =>
                        ReadCore(
                            // Keep this comment with the arguments.
                            first,
                            second);

                int ReadCore(int first, int second) => first + second;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ReindentedExpressionBody_ReindentsMultilineArguments()
    {
        const string source = """
            class Sample
            {
                int Read(
                    int first,
                    int second) =>
                    ReadCore(
                        first,
                        second: second);

                int ReadCore(int first, int second) => first + second;
            }
            """;
        const string expected = """
            class Sample
            {
                int Read(
                    int first,
                    int second) =>
                        ReadCore(
                            first,
                            second: second);

                int ReadCore(int first, int second) => first + second;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ReindentedExpressionBody_ReindentsLeadingAndBlockComments()
    {
        const string source = """
            class Sample
            {
                int Read(
                    int first,
                    int second) =>
                    // Keep this comment with the body.
                    ReadCore(
                        /* Keep this block
                         * with the arguments. */
                        first,
                        second);

                int ReadCore(int first, int second) => first + second;
            }
            """;
        const string expected = """
            class Sample
            {
                int Read(
                    int first,
                    int second) =>
                        // Keep this comment with the body.
                        ReadCore(
                            /* Keep this block
                             * with the arguments. */
                            first,
                            second);

                int ReadCore(int first, int second) => first + second;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Format_MovedExpressionBodyWithInterpolatedRawString_OffersNoFix()
    {
        const string source = """"
            class Sample
            {
                string Method(
                    string value)
                    => Join($"""
                        {value}
                        """,
                        value);

                string Join(string first, string second) => first + second;
            }
            """";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task Format_MovedExpressionBodyWithVerbatimString_OffersNoFix()
    {
        const string source = """
            class Sample
            {
                string Method(
                    string value)
                    => Join(@"first
                second",
                        value);

                string Join(string first, string second) => first + second;
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task FormatAll_MovedExpressionBody_ReindentsSwitchExpression()
    {
        const string source = """
            class Sample
            {
                string? GetName(
                    int value)
                    => value switch
                    {
                        0 => null,
                        1 => "one",
                        _ => "other",
                    };
            }
            """;
        const string expected = """
            class Sample
            {
                string? GetName(
                    int value) =>
                        value switch
                        {
                            0 => null,
                            1 => "one",
                            _ => "other",
                        };
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Format_CollectionExpressionBody_AlignsOpeningBracketWithDeclaration()
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
        const string expected = """
            class Sample
            {
                int[] Values =>
                [
                    1,
                    2
                ];
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_CollectionExpressionAssignment_AlignsOpeningBracketWithDeclaration()
    {
        const string source = """
            class Sample
            {
                int[] Values =
                    [
                    1
                ];
            }
            """;
        const string expected = """
            class Sample
            {
                int[] Values =
                [
                    1
                ];
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_IndentedCollectionBlocks_ShiftWithOpeningDelimiter()
    {
        const string source = """
            class Sample
            {
                int[][] Values =>
                    [
                        // Keep this comment with the nested collection.
                        [
                            1,
                        ],
                    ];

                int[] Method() =>
                    [
                        2,
                    ];

                int[] Field =
                    {
                        3,
                    };
            }
            """;
        const string expected = """
            class Sample
            {
                int[][] Values =>
                [
                    // Keep this comment with the nested collection.
                    [
                        1,
                    ],
                ];

                int[] Method() =>
                [
                    2,
                ];

                int[] Field =
                {
                    3,
                };
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 3).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_SingleLineCollectionsAndInitializers_UseContinuationIndentation()
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
        const string expected = """
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

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 4).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Format_CollectionExpressionSwitchArm_AlignsOpeningBracketWithArm()
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
        const string expected = """
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

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ArrowBodyWithCommentAndWrongIndentation_PreservesComment()
    {
        const string source = """
            class Sample
            {
                int Value => // Keep this comment.
                      1;
            }
            """;
        const string expected = """
            class Sample
            {
                int Value => // Keep this comment.
                    1;
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ConditionalAccessSplitInsideOperator_MovesTokenPairTogether()
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
        const string expected = """
            class Sample
            {
                int? Method(string value)
                {
                    return value
                        ?.Length;
                }
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ConditionalAccessPairSplitAcrossContinuationLines_JoinsTokenPair()
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
        const string expected = """
            class Sample
            {
                int? Method(string value)
                {
                    return value
                        ?.Length;
                }
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_UnsignedRightShiftAssignment_MovesFourCharacterOperator()
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
        const string expected = """
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

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("\u0085")]
    [DataRow("\u2028")]
    [DataRow("\u2029")]
    public async Task Format_UnicodeLineBreak_PreservesLineBreak(string lineBreak)
    {
        string source =
            "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left +"
            + lineBreak
            + "            right;\n    }\n}\n";
        string expected =
            "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left"
            + lineBreak
            + "            + right;\n    }\n}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_ReplacementAtLimit_OffersFix()
    {
        const int baseIndentationLength = 4081;
        string baseIndentation = new(' ', baseIndentationLength);
        string statementIndentation = new(' ', baseIndentationLength + 8);
        string continuationIndentation = new(' ', baseIndentationLength + 12);
        string source =
            $"{baseIndentation}class Sample\n"
            + $"{baseIndentation}{{\n"
            + $"{baseIndentation}    int Method(int left, int right)\n"
            + $"{baseIndentation}    {{\n"
            + $"{statementIndentation}return left +\n"
            + $"{continuationIndentation}right;\n"
            + $"{baseIndentation}    }}\n"
            + $"{baseIndentation}}}\n";
        string expected =
            $"{baseIndentation}class Sample\n"
            + $"{baseIndentation}{{\n"
            + $"{baseIndentation}    int Method(int left, int right)\n"
            + $"{baseIndentation}    {{\n"
            + $"{statementIndentation}return left\n"
            + $"{continuationIndentation}+ right;\n"
            + $"{baseIndentation}    }}\n"
            + $"{baseIndentation}}}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_MultilineRawLiteralBeforeBinaryOperator_MovesOperatorAfterLiteral()
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
        const string expected = """"
            class Sample
            {
                string Value => """
                    left
                    """
                        + "right";
            }
            """";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_CrLfInput_PreservesCrLf()
    {
        const string source = "class Sample\r\n{\r\n    int Value\r\n        => 1;\r\n}\r\n";
        const string expected = "class Sample\r\n{\r\n    int Value =>\r\n        1;\r\n}\r\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_ConditionalAfterMultilineInvocation_IndentsBeyondConditionBlock()
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
        const string expected = """
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
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_OperatorsAfterMultilineInvocations_UseStructuralOperandAnchors()
    {
        const string source = """
            using System;

            class Sample
            {
                bool Method(string filePath)
                {
                    bool isSystemModule = filePath.StartsWith(
                            @"\windows\sys",
                            StringComparison.OrdinalIgnoreCase)
                                || (filePath.Length > 2
                                    && filePath.AsSpan(2).StartsWith(
                            @"\windows\sys",
                            StringComparison.OrdinalIgnoreCase));
                    return isSystemModule;
                }
            }
            """;
        const string expected = """
            using System;

            class Sample
            {
                bool Method(string filePath)
                {
                    bool isSystemModule = filePath.StartsWith(
                            @"\windows\sys",
                            StringComparison.OrdinalIgnoreCase)
                        || (filePath.Length > 2
                            && filePath.AsSpan(2).StartsWith(
                                @"\windows\sys",
                                StringComparison.OrdinalIgnoreCase));
                    return isSystemModule;
                }
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ParenthesizedPrecedenceGroups_PreserveStructuralAnchors()
    {
        const string source = """
            using System;

            class Sample
            {
                bool First(string name, string filePath)
                {
                    return (name.StartsWith("mscorlib.ni", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith(
                                "system.private.corelib",
                                StringComparison.OrdinalIgnoreCase))
                                    && !filePath.Contains(
                            "NativeImages_v2",
                            StringComparison.OrdinalIgnoreCase);
                }

                bool Later(string name)
                {
                    return (name.StartsWith("System.Runtime.CompilerServices.AsyncTaskMethodBuilder", StringComparison.Ordinal)
                        && name.Contains(".AwaitUnsafeOnCompleted", StringComparison.Ordinal))
                            || name.StartsWith("System.Threading.Tasks.Task.ScheduleAndStart", StringComparison.Ordinal)
                            || (name.StartsWith("System.Runtime.CompilerServices", StringComparison.Ordinal)
                                && name.Contains("TaskAwaiter", StringComparison.Ordinal)
                                && (name.Contains("OnCompleted", StringComparison.Ordinal)
                                    || name.Contains("OutputWaitEtwEvents", StringComparison.Ordinal)));
                }
            }
            """;
        const string expected = """
            using System;

            class Sample
            {
                bool First(string name, string filePath)
                {
                    return (name.StartsWith("mscorlib.ni", StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith(
                                "system.private.corelib",
                                StringComparison.OrdinalIgnoreCase))
                        && !filePath.Contains(
                            "NativeImages_v2",
                            StringComparison.OrdinalIgnoreCase);
                }

                bool Later(string name)
                {
                    return (name.StartsWith("System.Runtime.CompilerServices.AsyncTaskMethodBuilder", StringComparison.Ordinal)
                            && name.Contains(".AwaitUnsafeOnCompleted", StringComparison.Ordinal))
                        || name.StartsWith("System.Threading.Tasks.Task.ScheduleAndStart", StringComparison.Ordinal)
                        || (name.StartsWith("System.Runtime.CompilerServices", StringComparison.Ordinal)
                            && name.Contains("TaskAwaiter", StringComparison.Ordinal)
                            && (name.Contains("OnCompleted", StringComparison.Ordinal)
                                || name.Contains("OutputWaitEtwEvents", StringComparison.Ordinal)));
                }
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 8).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ExpressionBodiedConditionalAfterMultilineInvocation_IndentsBeyondLastArgument()
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
        const string expected = """
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
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_NestedGeneratorContinuations_ConvergesInOnePass()
    {
        const string source = """
            using System.Linq;

            class Sample
            {
                void Method(string[] values, string? text)
                {
                    (
                        string[] Values,
                        bool HasText) resolverModel =
                        (values, text is not null);

                    string? location = text?
                        .Trim()
                        .ToString();

                    if (!values.Any(value =>
                            value.Length != 0
                            && location is not null))
                    {
                    }

                    if (!values.Any(static value =>
                            value.Length != 0
                            && value[0] != '\0'))
                    {
                    }
                }
            }
            """;
        const string expected = """
            using System.Linq;

            class Sample
            {
                void Method(string[] values, string? text)
                {
                    (
                        string[] Values,
                        bool HasText) resolverModel =
                            (values, text is not null);

                    string? location = text
                        ?.Trim()
                        .ToString();

                    if (!values.Any(value =>
                        value.Length != 0
                            && location is not null))
                    {
                    }

                    if (!values.Any(static value =>
                        value.Length != 0
                            && value[0] != '\0'))
                    {
                    }
                }
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 4).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_LambdasInMemberChain_ConvergesInOnePass()
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
        const string expected = """
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

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 4).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_TernaryAfterMovedExpressionBody_ConvergesInOnePass()
    {
        const string source = """
            class Sample
            {
                int Method(
                    int value) =>
                value ==
                    0
                    ? 1
                    : 2;
            }
            """;
        const string expected = """
            class Sample
            {
                int Method(
                    int value) =>
                        value
                            == 0
                                ? 1
                                : 2;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 4).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_TernaryAfterMovedInvocation_ConvergesInOnePass()
    {
        const string source = """
            class Sample
            {
                int Method(
                    int value)
                    => TryGetValue(
                        value,
                        out int result)
                        ? result
                        : 0;

                bool TryGetValue(int value, out int result)
                {
                    result = value;
                    return true;
                }
            }
            """;
        const string expected = """
            class Sample
            {
                int Method(
                    int value) =>
                        TryGetValue(
                            value,
                            out int result)
                                ? result
                                : 0;

                bool TryGetValue(int value, out int result)
                {
                    result = value;
                    return true;
                }
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 3).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_BinaryPatternAfterMovedExpressionBody_ConvergesInOnePass()
    {
        const string source = """
            class Sample
            {
                bool Method(
                    string providerName,
                    string value) =>
                    IsProvider(providerName)
                    && value is "one"
                        or "two"
                        or "three";

                bool IsProvider(string value) => true;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(
                    string providerName,
                    string value) =>
                        IsProvider(providerName)
                            && value is "one"
                                or "two"
                                or "three";

                bool IsProvider(string value) => true;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 4).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_NestedConditional_IndentsBeyondNestedConditionLine()
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
        const string expected = """
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

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ExpressionBodiedComparison_IndentsBeyondOperandLine()
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
        const string expected = """
            class Sample
            {
                bool Method() =>
                    GetValue()
                        != 0;

                int GetValue() => 0;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ExpressionBodiedLogicalChain_IndentsBeyondOperandLine()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second) =>
                    first
                    && second;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(bool first, bool second) =>
                    first
                        && second;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_PartiallyBrokenSamePrecedenceChain_BreaksEveryOperator()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, bool third, bool fourth) =>
                    first && second
                        && third && fourth;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(bool first, bool second, bool third, bool fourth) =>
                    first
                        && second
                        && third
                        && fourth;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_PartiallyBrokenAdditiveCategory_BreaksEveryOperator()
    {
        const string source = """
            class Sample
            {
                int Method(int first, int second, int third) =>
                    first + second
                        - third;
            }
            """;
        const string expected = """
            class Sample
            {
                int Method(int first, int second, int third) =>
                    first
                        + second
                        - third;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MixedPrecedenceOperators_IndentByCategory()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, int left, int right) =>
                    first
                    && second
                    || left
                    == right;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(bool first, bool second, int left, int right) =>
                    first
                        && second
                            || left
                                == right;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 3).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Format_IsAtBeginningOfLine_MovesOperatorToEndOfOperandLine()
    {
        const string source = """
            class Sample
            {
                bool Method(object value) =>
                    value
                        is string;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(object value) =>
                    value is
                        string;
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_ExpressionBodiedAssignmentCoalesce_IndentsBeyondOperandLine()
    {
        const string source = """
            class Sample
            {
                object _value;

                object Method() =>
                    _value ??= CreateValue()
                    ?? throw new System.InvalidOperationException();

                object CreateValue() => null;
            }
            """;
        const string expected = """
            class Sample
            {
                object _value;

                object Method() =>
                    _value ??= CreateValue()
                        ?? throw new System.InvalidOperationException();

                object CreateValue() => null;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ConditionalAccessInBrokenAssignment_IndentsBeyondAssignmentOperand()
    {
        const string source = """
            class Sample
            {
                string? Method(object value)
                {
                    string? result
                        = (value as string)
                        ?.ToString();
                    return result;
                }
            }
            """;
        const string expected = """
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

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Format_BlockLambdaBody_AlignsWithContainingLine()
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
        const string expected = """
            using System;

            class Sample
            {
                static Action Action = () =>
                {
                    Console.WriteLine();
                };
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_PropertyInitializerConditional_IndentsBeyondOperandLines()
    {
        const string source = """
            class Sample
            {
                static bool Value =
            #if false
                    true;
            #else
                    First()
                    || Second()
                        ? true
                        : false;
            #endif

                static bool First() => true;

                static bool Second() => false;
            }
            """;
        const string expected = """
            class Sample
            {
                static bool Value =
            #if false
                    true;
            #else
                    First()
                        || Second()
                            ? true
                            : false;
            #endif

                static bool First() => true;

                static bool Second() => false;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 3).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MisindentedFinalConditionOperator_NormalizesTernaryInOnePass()
    {
        const string source = """
            class Sample
            {
                int Method(bool first, bool second)
                {
                    return first
                          && second
                              ? 1
                              : 0;
                }
            }
            """;
        const string expected = """
            class Sample
            {
                int Method(bool first, bool second)
                {
                    return first
                        && second
                            ? 1
                            : 0;
                }
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 3).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ConditionalEndingInMultilineRawLiteral_Converges()
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
        const string expected = """"
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

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MultipleOperatorFamilies_AppliesEveryChangeOnce()
    {
        const string source = """
            class Sample
            {
                int Method(int left, int right, bool condition)
                {
                    int sum =
                        left +
                            right;
                    return condition ?
                        sum :
                        left;
                }

                int Value
                    => 1;
            }
            """;
        const string expected = """
            class Sample
            {
                int Method(int left, int right, bool condition)
                {
                    int sum =
                        left
                            + right;
                    return condition
                        ? sum
                        : left;
                }

                int Value =>
                    1;
            }
            """;
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(4);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_MisindentedOuterOperator_NormalizesNestedScopeInSamePass()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, bool third) =>
                    first
                        && (second
                                || third);
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(bool first, bool second, bool third) =>
                    first
                        && (second
                            || third);
            }
            """;
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_OuterRelocationBeforeInvocation_NormalizesInnerOperatorInSamePass()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second, bool third) =>
                    first &&
                        Check(
                        second ||
                            third);

                bool Check(bool value) => value;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(bool first, bool second, bool third) =>
                    first
                        && Check(
                        second
                            || third);

                bool Check(bool value) => value;
            }
            """;
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_OperatorAtStartOfMemberChain_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Builder
            {
                public Builder Next() => this;
                public bool Check(bool value) => value;
            }

            class Sample
            {
                bool Method(Builder builder, bool first, bool second) =>
                    builder
                        .Next()
                        .Check(
                        first ||
                            second);
            }
            """;
        const string expected = """
            class Builder
            {
                public Builder Next() => this;
                public bool Check(bool value) => value;
            }

            class Sample
            {
                bool Method(Builder builder, bool first, bool second) =>
                    builder
                        .Next()
                        .Check(
                        first
                            || second);
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ConditionalInvocation_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Sample
            {
                string? Method(string? value, string first, string second) =>
                    value?.
                        Replace(
                        first +
                            second,
                        "");
            }
            """;
        const string expected = """
            class Sample
            {
                string? Method(string? value, string first, string second) =>
                    value
                        ?.Replace(
                        first
                            + second,
                        "");
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ConditionalElementAccess_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Sample
            {
                int? Method(int[]? value, int first, int second) =>
                    value?[
                        first +
                            second];
            }
            """;
        const string expected = """
            class Sample
            {
                int? Method(int[]? value, int first, int second) =>
                    value
                        ?[first
                            + second];
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_SplitConditionalInvocation_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Sample
            {
                string? Method(string? value, string first, string second) =>
                    value?
                        .Replace(
                        first +
                            second,
                        "");
            }
            """;
        const string expected = """
            class Sample
            {
                string? Method(string? value, string first, string second) =>
                    value
                        ?.Replace(
                        first
                            + second,
                        "");
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_SplitConditionalElementAccess_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Sample
            {
                int? Method(int[]? value, int first, int second) =>
                    value?
                        [
                        first +
                            second];
            }
            """;
        const string expected = """
            class Sample
            {
                int? Method(int[]? value, int first, int second) =>
                    value
                        ?[first
                            + second];
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MisplacedMemberArrow_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Sample
            {
                bool Method(bool first, bool second)
                    =>
                        Check(
                            first
                            || second);

                bool Check(bool value) => value;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(bool first, bool second) =>
                    Check(
                        first
                            || second);

                bool Check(bool value) => value;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MisplacedLambdaArrow_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            using System;

            class Sample
            {
                bool Method(bool first, bool second)
                {
                    Func<bool> predicate = ()
                        =>
                            Check(
                                first
                                || second);
                    return predicate();
                }

                bool Check(bool value) => value;
            }
            """;
        const string expected = """
            using System;

            class Sample
            {
                bool Method(bool first, bool second)
                {
                    Func<bool> predicate = () =>
                        Check(
                            first
                                || second);
                    return predicate();
                }

                bool Check(bool value) => value;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MisplacedSwitchArmArrow_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Sample
            {
                bool Method(int value, bool first, bool second) => value switch
                {
                    _
                        =>
                            Check(
                                first
                                || second)
                };

                bool Check(bool value) => value;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(int value, bool first, bool second) => value switch
                {
                    _ =>
                        Check(
                            first
                                || second)
                };

                bool Check(bool value) => value;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MovedBlockLambda_ShiftsCompleteBlock()
    {
        const string source = """
            using System;

            class Sample
            {
                Func<int> Value =
                    ()
                        =>
                        {
                            return 1;
                        };
            }
            """;
        const string expected = """
            using System;

            class Sample
            {
                Func<int> Value =
                    () =>
                    {
                        return 1;
                    };
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MovedCollectionExpression_ShiftsCompleteCollection()
    {
        const string source = """
            class Sample
            {
                int[] Values
                    =>
                    [
                        1
                    ];
            }
            """;
        const string expected = """
            class Sample
            {
                int[] Values =>
                [
                    1
                ];
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MovedArrayInitializer_ShiftsCompleteInitializer()
    {
        const string source = """
            class Sample
            {
                int[] Values
                    =
                    {
                        1
                    };
            }
            """;
        const string expected = """
            class Sample
            {
                int[] Values =
                {
                    1
                };
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MovedAnonymousCollection_ShiftsCompleteCollection()
    {
        const string source = """
            class Sample
            {
                object Value => new
                {
                    Result
                        =
                        [
                            1
                        ]
                };
            }
            """;
        const string expected = """
            class Sample
            {
                object Value => new
                {
                    Result =
                    [
                        1
                    ]
                };
            }
            """;
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CompilerErrors.Select(static diagnostic => diagnostic.Id).Should().Equal("CS9176");
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_MemberOperatorInsideBinary_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Builder
            {
                public bool Next() => true;
            }

            class Sample
            {
                bool Method(Builder builder, bool first, bool second) =>
                    builder
                        .Next() == Check(
                            first
                            || second);

                bool Check(bool value) => value;
            }
            """;
        const string expected = """
            class Builder
            {
                public bool Next() => true;
            }

            class Sample
            {
                bool Method(Builder builder, bool first, bool second) =>
                    builder
                        .Next() == Check(
                            first
                                || second);

                bool Check(bool value) => value;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MemberOperatorInsideParentheses_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Builder
            {
                public bool Next() => true;
            }

            class Sample
            {
                bool Method(Builder builder, bool first, bool second) =>
                    builder
                        .Next() == (
                            first
                            || second);
            }
            """;
        const string expected = """
            class Builder
            {
                public bool Next() => true;
            }

            class Sample
            {
                bool Method(Builder builder, bool first, bool second) =>
                    builder
                        .Next() == (
                            first
                                || second);
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MemberOperatorBeforeSiblingArguments_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Builder
            {
                public bool Check(bool first, bool second) => first && second;
            }

            class Sample
            {
                bool Method(Builder builder, bool first, bool second, bool third) =>
                    builder
                        .Check(
                            first,
                            second
                            || third);
            }
            """;
        const string expected = """
            class Builder
            {
                public bool Check(bool first, bool second) => first && second;
            }

            class Sample
            {
                bool Method(Builder builder, bool first, bool second, bool third) =>
                    builder
                        .Check(
                            first,
                            second
                                || third);
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MemberOperatorInsideIsPattern_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Builder
            {
                public int Value => 1;
            }

            class Sample
            {
                bool Method(Builder builder) =>
                    builder
                        .Value is (
                            > 0
                            and < 10);
            }
            """;
        const string expected = """
            class Builder
            {
                public int Value => 1;
            }

            class Sample
            {
                bool Method(Builder builder) =>
                    builder
                        .Value is (
                            > 0
                                and < 10);
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MemberOperatorInsideRange_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Builder
            {
                public int Start => 0;
            }

            class Sample
            {
                System.Range Method(Builder builder, int first, int second) =>
                    builder
                        .Start..(
                            first
                            + second);
            }
            """;
        const string expected = """
            class Builder
            {
                public int Start => 0;
            }

            class Sample
            {
                System.Range Method(Builder builder, int first, int second) =>
                    builder
                        .Start..(
                            first
                                + second);
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_AttributeNameEquals_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            using System;

            sealed class MarkerAttribute : Attribute
            {
                public string Value { get; set; } = string.Empty;
            }

            static class Some
            {
                public const int Type = 0;
            }

            [Marker(Value =
                    nameof(
                        Some.
                            Type))]
            class Sample
            {
            }
            """;
        const string expected = """
            using System;

            sealed class MarkerAttribute : Attribute
            {
                public string Value { get; set; } = string.Empty;
            }

            static class Some
            {
                public const int Type = 0;
            }

            [Marker(Value =
                nameof(
                        Some
                            .Type))]
            class Sample
            {
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_AnonymousNameEquals_NormalizesNestedOperatorInSamePass()
    {
        const string source = """
            class Sample
            {
                object Method(bool first, bool second) => new
                {
                    Result =
                        Check(
                        first ||
                            second)
                };

                bool Check(bool value) => value;
            }
            """;
        const string expected = """
            class Sample
            {
                object Method(bool first, bool second) => new
                {
                    Result =
                        Check(
                        first
                            || second)
                };

                bool Check(bool value) => value;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MovedAttributeNameEquals_ReindentsNameofArgument()
    {
        const string source = """
            using System;

            sealed class MarkerAttribute : Attribute
            {
                public string Value { get; set; } = string.Empty;
            }

            [Marker(Value
                    = nameof(
                        Sample))]
            class Sample
            {
            }
            """;
        const string expected = """
            using System;

            sealed class MarkerAttribute : Attribute
            {
                public string Value { get; set; } = string.Empty;
            }

            [Marker(Value =
                nameof(
                    Sample))]
            class Sample
            {
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MovedAnonymousNameEquals_ReindentsInvocationArguments()
    {
        const string source = """
            class Sample
            {
                object Method(string first, string second) => new
                {
                    Result
                            = string.Concat(
                                first,
                                second)
                };
            }
            """;
        const string expected = """
            class Sample
            {
                object Method(string first, string second) => new
                {
                    Result =
                        string.Concat(
                            first,
                            second)
                };
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_MovedUsingAliasEquals_ReindentsQualifiedName()
    {
        const string source = """
            using Alias
                    = System.
                        Text;

            class Sample
            {
                Alias.StringBuilder? Builder { get; }
            }
            """;
        const string expected = """
            using Alias =
                System.
                    Text;

            class Sample
            {
                Alias.StringBuilder? Builder { get; }
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_NestedOperatorUnderMovedAssignment_ConvergesInOnePass()
    {
        const string source = """
            class Sample
            {
                int Method(int first, int second)
                {
                    int result = 0;
                    result
                            = Read(
                                first
                                + second);
                    return result;
                }

                int Read(int value) => value;
            }
            """;
        const string expected = """
            class Sample
            {
                int Method(int first, int second)
                {
                    int result = 0;
                    result =
                        Read(
                            first
                                + second);
                    return result;
                }

                int Read(int value) => value;
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_ConditionalInvocationLambda_ConvergesInOnePass()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;

            class Sample
            {
                IEnumerable<int>? Method(IEnumerable<int>? values) =>
                    values?
                            .Where(value =>
                                value > 0
                                && value < 10);
            }
            """;
        const string expected = """
            using System.Collections.Generic;
            using System.Linq;

            class Sample
            {
                IEnumerable<int>? Method(IEnumerable<int>? values) =>
                    values
                        ?.Where(value =>
                            value > 0
                                && value < 10);
            }
            """;

        await AssertFixAllAsync(source, expected, expectedInitialDiagnostics: 2).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task FormatAll_BinaryPatternContainingRelationalPattern_SubsumesContainedChange()
    {
        const string source = """
            class Sample
            {
                bool Method(int value) => value is > 0 and
                            < 10;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Method(int value) => value is > 0
                    and < 10;
            }
            """;
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Format_IsBeforeRelationalPattern_AlreadyUsesTrailingPlacement(bool fixAll)
    {
        const string source = """
            class Sample
            {
                bool Method(int value) => value is
                    >
                        0;
            }
            """;
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(0);
        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(source);
        if (fixAll)
        {
            result.CodeFixActionOffered.Should().BeNull();
            result.FixAllActionOffered.Should().BeNull();
        }
        else
        {
            result.CodeFixActionOffered.Should().BeNull();
        }
    }

    [TestMethod]
    public async Task FormatAll_TrailingIsStillFixesUnrelatedOperator()
    {
        const string source = """
            class Sample
            {
                bool Pattern(int value) => value is
                    >
                        0;

                int Add(int left, int right) => left +
                    right;
            }
            """;
        const string expected = """
            class Sample
            {
                bool Pattern(int value) => value is
                    >
                        0;

                int Add(int left, int right) => left
                    + right;
            }
            """;
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_DirectRelationalSwitchArm_ConvergesInOneFix()
    {
        const string source = """
            class Sample
            {
                int Method(int value) => value switch
                {
                        < 0 => -1,
                    _ => 0
                };
            }
            """;
        const string expected = """
            class Sample
            {
                int Method(int value) => value switch
                {
                    < 0 => -1,
                    _ => 0
                };
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
        fixedAgain.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_LeftmostRelationalPatternInBinarySwitchArm_ConvergesInOneFix()
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
        const string expected = """
            class Sample
            {
                int Method(int value) => value switch
                {
                    < 0 or > 10 => -1,
                    _ => 0
                };
            }
            """;

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
        fixedAgain.Should().Be(expected);
    }

    [TestMethod]
    public async Task Format_MultilineBinarySwitchArm_ConvergesInOneFix()
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
        const string expected = """
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

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
        fixedAgain.Should().Be(expected);
    }

    [TestMethod]
    public async Task FormatAll_OverindentedRelationalAndBinarySwitchArm_ConvergesInOnePass()
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
        const string expected = """
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
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(FixAllScope.Document, 3)]
    [DataRow(FixAllScope.Project, 2)]
    [DataRow(FixAllScope.Solution, 0)]
    public async Task FormatAll_Scope_FormatsExpectedDocuments(FixAllScope scope, int remainingDiagnostics)
    {
        const string oneSource = "class One\n{\n    int Value\n        => 1;\n}\n";
        const string oneFixed = "class One\n{\n    int Value =>\n        1;\n}\n";
        const string twoSource =
            "class Two\n{\n    int Add(int left, int right)\n    {\n        return left +\n            right;\n    }\n}\n";
        const string twoFixed =
            "class Two\n{\n    int Add(int left, int right)\n    {\n        return left\n            + right;\n    }\n}\n";
        const string threeSource =
            "class Three\n{\n    int Select(bool condition) => condition ?\n        1 :\n        2;\n}\n";
        const string threeFixed =
            "class Three\n{\n    int Select(bool condition) => condition\n        ? 1\n        : 2;\n}\n";
        (string Name, string FilePath, string Source)[] sources =
        [
            ("One.cs", "A-One.cs", oneSource),
            ("Two.cs", "B-Two.cs", twoSource)
        ];
        (string Name, string FilePath, string Source)[] additionalProjectSources =
            [("Three.cs", "C-Three.cs", threeSource)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: scope,
            additionalProjectSources: additionalProjectSources).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.InitialAnalyzerDiagnosticCount.Should().Be(4);
        result.AnalyzerDiagnostics.Should().HaveCount(remainingDiagnostics);
        result.Documents.Single(document => document.Name == "One.cs").Source.Should().Be(oneFixed);
        result.Documents.Single(document => document.Name == "Two.cs").Source.Should().Be(
            scope is FixAllScope.Project or FixAllScope.Solution ? twoFixed : twoSource);
        result.Documents.Single(document => document.Name == "Three.cs").Source.Should().Be(
            scope == FixAllScope.Solution ? threeFixed : threeSource);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Format_LinkedDocumentsWithCompatibleIndentation_UpdatesBothDocuments(bool fixAll)
    {
        const string source = "class Sample\n{\n    int Value\n        => 1;\n}\n";
        const string expected = "class Sample\n{\n    int Value =>\n        1;\n}\n";
        (string Name, string FilePath, string Source)[] sources =
            [("Shared.cs", "Shared.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll,
            diagnosticOptions: s_enabled,
            addLinkedProject: true).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == expected);
        result.CodeFixActionOffered.Should().BeTrue();
        if (fixAll)
        {
            result.FixAllActionOffered.Should().BeTrue();
        }
    }

    [TestMethod]
    public async Task Format_LinkedDocumentsWithEquivalentPreprocessorContexts_UpdatesBothDocuments() =>
        await AssertEquivalentPreprocessorContextsAsync(fixAll: false).ConfigureAwait(false);

    [TestMethod]
    public async Task FormatAll_LinkedDocumentsWithEquivalentPreprocessorContexts_UpdatesBothDocuments() =>
        await AssertEquivalentPreprocessorContextsAsync(fixAll: true).ConfigureAwait(false);

    private static async Task AssertEquivalentPreprocessorContextsAsync(bool fixAll)
    {
        const string source = "class Sample\n{\n    int Value\n        => 1;\n}\n";
        const string expected = "class Sample\n{\n    int Value =>\n        1;\n}\n";
        (string Name, string FilePath, string Source)[] sources = [("Shared.cs", "Shared.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll,
            diagnosticOptions: s_enabled,
            addLinkedProject: true,
            parseOptions: new CSharpParseOptions(
                LanguageVersion.Preview,
                preprocessorSymbols: ["NET"]),
            linkedProjectParseOptions: new CSharpParseOptions(
                LanguageVersion.Preview,
                preprocessorSymbols: ["NET", "WINDOWS"])).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == expected);
        result.CodeFixActionOffered.Should().BeTrue();
        if (fixAll)
        {
            result.FixAllActionOffered.Should().BeTrue();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Format_LinkedDocumentsWithConflictingIndentation_OffersNoFix(bool fixAll)
    {
        const string source =
            "class Sample\n{\n    int Method(int left, int right)\n    {\n        return left\n           + right;\n    }\n}\n";
        (string Name, string FilePath, string Source)[] sources =
            [("Shared.cs", "Shared.cs", source)];
        Dictionary<string, string> options = new() { ["indent_size"] = "2" };
        Dictionary<string, string> linkedOptions = new() { ["indent_size"] = "4" };

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll,
            options,
            s_enabled,
            addLinkedProject: true,
            linkedProjectOptions: linkedOptions).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
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
    public async Task Format_LinkedDocumentsWithDivergentPreprocessorContexts_OffersNoFix()
    {
        const string source = """
            class Sample
            {
            #if WRAPPED
                bool Method(bool left, bool right) =>
                    ((
            #else
                bool Method(bool left, bool right) =>
            #endif
                    left
                       && right
            #if WRAPPED
                    ));
            #else
                    ;
            #endif
            }
            """;
        (string Name, string FilePath, string Source)[] sources =
            [("Shared.cs", "Shared.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled,
            addLinkedProject: true,
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            linkedProjectParseOptions: new CSharpParseOptions(
                LanguageVersion.Preview,
                preprocessorSymbols: ["WRAPPED"])).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task FormatAll_LinkedDocumentsWithDistinctActiveBranches_OffersNoFix()
    {
        const string source = """
            class Sample
            {
            #if FIRST
                int First
                    => 1;
            #else
                int Second
                    => 2;
            #endif
            }
            """;
        (string Name, string FilePath, string Source)[] sources = [("Shared.cs", "Shared.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            addLinkedProject: true,
            parseOptions: new CSharpParseOptions(
                LanguageVersion.Preview,
                preprocessorSymbols: ["FIRST"]),
            linkedProjectParseOptions: new CSharpParseOptions(LanguageVersion.Preview)).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
        result.FixAllActionOffered.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Format_LinkedDocumentsWithDifferentLanguageVersions_OffersNoFix(bool fixAll)
    {
        const string source = "class Sample\n{\n    int Value\n        => 1;\n}\n";
        (string Name, string FilePath, string Source)[] sources =
            [("Shared.cs", "Shared.cs", source)];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll,
            diagnosticOptions: s_enabled,
            addLinkedProject: true,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp11),
            linkedProjectParseOptions: new CSharpParseOptions(LanguageVersion.CSharp12)).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
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
    public async Task FormatAll_Canceled_ThrowsOperationCanceledException()
    {
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", "class Sample\n{\n    int Value\n        => 1;\n}\n")];
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Func<Task> action = async () => await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllCancellationToken: cancellation.Token).ConfigureAwait(false);

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }

    private static async Task AssertFixAllAsync(
        string source,
        string expected,
        int expectedInitialDiagnostics)
    {
        (string Name, string FilePath, string Source)[] sources =
            [("Sample.cs", "Sample.cs", source)];
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new StatementBreakFormattingAnalyzer(),
            new FormatStatementBreaksCodeFixProvider(),
            sources,
            StatementBreakFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(expectedInitialDiagnostics);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(expected);
    }

    private static async Task<Diagnostic> GetSingleDiagnosticAsync(string source)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new StatementBreakFormattingAnalyzer(),
            source,
            diagnosticOptions: s_enabled).ConfigureAwait(false);
        return diagnostics.Should().ContainSingle().Subject;
    }

    private static Diagnostic CreateDiagnostic(
        Diagnostic diagnostic,
        ImmutableArray<Location> additionalLocations,
        ImmutableDictionary<string, string?> properties) =>
        Diagnostic.Create(
            diagnostic.Descriptor,
            diagnostic.Location,
            additionalLocations,
            properties,
            "+");

    private static bool TryCreateTextChange(
        Diagnostic diagnostic,
        SourceText source,
        out TextChange change) =>
        TryCreateTextChange(diagnostic, source, CancellationToken.None, out change);

    private static bool TryCreateTextChange(
        Diagnostic diagnostic,
        SourceText source,
        CancellationToken cancellationToken,
        out TextChange change)
    {
        SyntaxTree currentTree = diagnostic.Location.SourceTree!.WithChangedText(source);
        SyntaxNode currentRoot = currentTree.GetRoot(cancellationToken);
        return StatementBreakDiagnosticData.TryCreateTextChange(
            diagnostic,
            currentRoot,
            source,
            currentIndentationUnit: "    ",
            cancellationToken,
            out change);
    }

    private static ImmutableArray<Diagnostic> RepeatDiagnostics(
        ImmutableArray<Diagnostic> diagnostics,
        int count)
    {
        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        ImmutableArray<Diagnostic>.Builder repeated = ImmutableArray.CreateBuilder<Diagnostic>(count);
        for (int index = 0; index < count; index++)
        {
            repeated.Add(diagnostic);
        }

        return repeated.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> RepeatDiagnosticsByDocument(
        ImmutableArray<Diagnostic> diagnostics,
        int firstDocumentRepeats,
        int secondDocumentRepeats)
    {
        Diagnostic[] ordered =
        [
            .. diagnostics.OrderBy(
                static diagnostic => diagnostic.Location.SourceTree!.FilePath,
                StringComparer.Ordinal)
        ];
        ordered.Should().HaveCount(2);
        ImmutableArray<Diagnostic>.Builder repeated = ImmutableArray.CreateBuilder<Diagnostic>(
            firstDocumentRepeats + secondDocumentRepeats);
        for (int index = 0; index < firstDocumentRepeats; index++)
        {
            repeated.Add(ordered[0]);
        }

        for (int index = 0; index < secondDocumentRepeats; index++)
        {
            repeated.Add(ordered[1]);
        }

        return repeated.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> CreateRejectedLinkedBudgetDiagnostics(
        ImmutableArray<Diagnostic> diagnostics,
        int laterDocumentRepeats)
    {
        Diagnostic[][] sharedDiagnostics =
        [
            .. diagnostics
                .Where(diagnostic => diagnostic.Location.SourceTree!.FilePath.EndsWith(
                    "Shared.cs",
                    StringComparison.Ordinal))
                .GroupBy(diagnostic => diagnostic.Location.SourceTree)
                .Select(group => group.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ToArray())
        ];
        sharedDiagnostics.Should().HaveCount(2);
        sharedDiagnostics.Should().OnlyContain(group => group.Length == 2);
        Diagnostic later = diagnostics.Single(diagnostic => diagnostic.Location.SourceTree!.FilePath.EndsWith(
            "Later.cs",
            StringComparison.Ordinal));
        ImmutableArray<Diagnostic>.Builder selected = ImmutableArray.CreateBuilder<Diagnostic>(
            2 + laterDocumentRepeats);
        selected.Add(sharedDiagnostics[0][0]);
        selected.Add(sharedDiagnostics[1][1]);
        for (int index = 0; index < laterDocumentRepeats; index++)
        {
            selected.Add(later);
        }

        return selected.ToImmutable();
    }

    private static string CreateMaximumReplacementSource(string className)
    {
        const int baseIndentationLength = 4081;
        string baseIndentation = new(' ', baseIndentationLength);
        string statementIndentation = new(' ', baseIndentationLength + 8);
        string continuationIndentation = new(' ', baseIndentationLength + 12);
        return $"{baseIndentation}class {className}\n"
            + $"{baseIndentation}{{\n"
            + $"{baseIndentation}    int Method(int left, int right)\n"
            + $"{baseIndentation}    {{\n"
            + $"{statementIndentation}return left +\n"
            + $"{continuationIndentation}right;\n"
            + $"{baseIndentation}    }}\n"
            + $"{baseIndentation}}}\n";
    }

    private static string CreateMaximumReplacementExpected(string className)
    {
        const int baseIndentationLength = 4081;
        string baseIndentation = new(' ', baseIndentationLength);
        string statementIndentation = new(' ', baseIndentationLength + 8);
        string continuationIndentation = new(' ', baseIndentationLength + 12);
        return $"{baseIndentation}class {className}\n"
            + $"{baseIndentation}{{\n"
            + $"{baseIndentation}    int Method(int left, int right)\n"
            + $"{baseIndentation}    {{\n"
            + $"{statementIndentation}return left\n"
            + $"{continuationIndentation}+ right;\n"
            + $"{baseIndentation}    }}\n"
            + $"{baseIndentation}}}\n";
    }
}