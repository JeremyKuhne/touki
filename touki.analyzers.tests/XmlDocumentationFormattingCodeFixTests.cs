// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

[TestClass]
public class XmlDocumentationFormattingCodeFixTests
{
    private static readonly Dictionary<string, ReportDiagnostic> s_enabled =
        new() { [XmlDocumentationFormattingAnalyzer.DiagnosticId] = ReportDiagnostic.Warn };

    private static async Task<string> ApplyFixAsync(
        string source,
        Dictionary<string, string>? options = null)
        => await CodeFixTestHarness.ApplyFixAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            source,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            options,
            s_enabled).ConfigureAwait(false);

    [TestMethod]
    public void GetFixAllProvider_Default_IsDocumentBased()
    {
        FixAllProvider provider = new FormatXmlDocumentationCodeFixProvider().GetFixAllProvider();

        provider.Should().NotBeSameAs(WellKnownFixAllProviders.BatchFixer);
        provider.GetSupportedFixAllScopes().Should().BeEquivalentTo(
            [
                FixAllScope.Document,
                FixAllScope.Project,
                FixAllScope.Solution,
                FixAllScope.ContainingMember,
                FixAllScope.ContainingType
            ]);
    }

    [TestMethod]
    [DataRow(FixAllScope.Document)]
    [DataRow(FixAllScope.Project)]
    [DataRow(FixAllScope.Solution)]
    [DataRow(FixAllScope.ContainingMember)]
    [DataRow(FixAllScope.ContainingType)]
    public async Task FormatAll_Scope_FixesCommentsWithinScope(FixAllScope scope)
    {
        const string bothOriginal =
            "class Sample\n"
            + "{\n"
            + "    /// <summary>One.</summary>\n"
            + "    int One => 1;\n"
            + "\n"
            + "    /// <summary>Two.</summary>\n"
            + "    int Two => 2;\n"
            + "}\n";
        const string bothMemberFixed =
            "class Sample\n"
            + "{\n"
            + "    /// <summary>\n"
            + "    ///  One.\n"
            + "    /// </summary>\n"
            + "    int One => 1;\n"
            + "\n"
            + "    /// <summary>Two.</summary>\n"
            + "    int Two => 2;\n"
            + "}\n";
        const string bothFixed =
            "class Sample\n"
            + "{\n"
            + "    /// <summary>\n"
            + "    ///  One.\n"
            + "    /// </summary>\n"
            + "    int One => 1;\n"
            + "\n"
            + "    /// <summary>\n"
            + "    ///  Two.\n"
            + "    /// </summary>\n"
            + "    int Two => 2;\n"
            + "}\n";
        const string otherOriginal = "/// <summary>Other.</summary>\nclass Other { }\n";
        const string otherFixed = "/// <summary>\n///  Other.\n/// </summary>\nclass Other { }\n";
        const string additionalOriginal = "/// <summary>Additional.</summary>\nclass Additional { }\n";
        const string additionalFixed =
            "/// <summary>\n///  Additional.\n/// </summary>\nclass Additional { }\n";
        (string Name, string FilePath, string Source)[] sources =
        [
            ("Both.cs", "A-Both.cs", bothOriginal),
            ("Other.cs", "B-Other.cs", otherOriginal)
        ];
        (string Name, string FilePath, string Source)[] additionalProjectSources =
        [
            ("Additional.cs", "Z-Additional.cs", additionalOriginal)
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            sources,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            fixAllScope: scope,
            diagnosticOptions: s_enabled,
            additionalProjectSources: additionalProjectSources).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.InitialAnalyzerDiagnosticCount.Should().Be(4);

        CodeFixTestDocument both = result.Documents.Single(document => document.Name == "Both.cs");
        CodeFixTestDocument other = result.Documents.Single(document => document.Name == "Other.cs");
        CodeFixTestDocument additional = result.Documents.Single(document => document.Name == "Additional.cs");
        switch (scope)
        {
            case FixAllScope.ContainingMember:
                both.Source.Should().Be(bothMemberFixed);
                other.Source.Should().Be(otherOriginal);
                additional.Source.Should().Be(additionalOriginal);
                result.AnalyzerDiagnostics.Should().HaveCount(3);
                break;
            case FixAllScope.ContainingType:
            case FixAllScope.Document:
                both.Source.Should().Be(bothFixed);
                other.Source.Should().Be(otherOriginal);
                additional.Source.Should().Be(additionalOriginal);
                result.AnalyzerDiagnostics.Should().HaveCount(2);
                break;
            case FixAllScope.Project:
                both.Source.Should().Be(bothFixed);
                other.Source.Should().Be(otherFixed);
                additional.Source.Should().Be(additionalOriginal);
                result.AnalyzerDiagnostics.Should().ContainSingle();
                break;
            case FixAllScope.Solution:
                both.Source.Should().Be(bothFixed);
                other.Source.Should().Be(otherFixed);
                additional.Source.Should().Be(additionalFixed);
                result.AnalyzerDiagnostics.Should().BeEmpty();
                break;
        }
    }

    [TestMethod]
    public async Task FormatAll_LinkedDocument_FixesBothProjectCopies()
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            ("Shared.cs", "Shared.cs", "/// <summary>Shared.</summary>\nclass Shared { }\n")
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            sources,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            addLinkedProject: true).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document =>
            document.Source == "/// <summary>\n///  Shared.\n/// </summary>\nclass Shared { }\n");
    }

    [TestMethod]
    public async Task Format_LinkedDocument_FixesBothProjectCopies()
    {
        const string source = "/// <summary>Shared.</summary>\nclass Shared { }\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Shared.cs", "Shared.cs", source)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled,
            addLinkedProject: true).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document =>
            document.Source == "/// <summary>\n///  Shared.\n/// </summary>\nclass Shared { }\n");
    }

    [TestMethod]
    [DataRow(FixAllScope.Document)]
    [DataRow(FixAllScope.Project)]
    [DataRow(FixAllScope.ContainingMember)]
    [DataRow(FixAllScope.ContainingType)]
    public async Task FormatAll_LinkedDocumentNarrowScope_OffersNoFix(FixAllScope scope)
    {
        const string source = "/// <summary>Shared.</summary>\nclass Shared { }\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Shared.cs", "Shared.cs", source)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            fixAllScope: scope,
            diagnosticOptions: s_enabled,
            addLinkedProject: true).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.FixAllActionOffered.Should().BeFalse();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(2);
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Format_LinkedDocumentWithDivergentIndentation_OffersNoFix(bool fixAll)
    {
        const string source = "/// <remarks><para>Text.</para></remarks>\nclass Shared { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.IndentSizeOption] = "1"
        };
        Dictionary<string, string> linkedOptions = new()
        {
            [XmlDocumentationFormattingAnalyzer.IndentSizeOption] = "2"
        };

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Shared.cs", "Shared.cs", source)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll,
            options,
            s_enabled,
            addLinkedProject: true,
            linkedProjectOptions: linkedOptions).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(2);
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
        if (fixAll)
        {
            result.FixAllActionOffered.Should().BeFalse();
        }
        else
        {
            result.CodeFixActionOffered.Should().BeFalse();
        }
    }

    [TestMethod]
    public async Task Format_LinkedDocumentWithDivergentSource_OffersNoFix()
    {
        const string primarySource = "/// <summary>Primary.</summary>\nclass Primary { }\n";
        const string linkedSource = "/// <summary>Linked.</summary>\nclass Linked { }\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled,
            addLinkedProject: true,
            primaryProjectSources: [("Shared.cs", "Shared.cs", primarySource)],
            linkedProjectSources: [("Shared.cs", "Shared.cs", linkedSource)]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeFalse();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(2);
        result.Documents.Single(document => document.Source == primarySource).Source.Should().Be(primarySource);
        result.Documents.Single(document => document.Source == linkedSource).Source.Should().Be(linkedSource);
    }

    [TestMethod]
    public async Task Format_ForeignLanguageDocumentWithSamePath_OffersNoFix()
    {
        const string source = "/// <summary>Shared.</summary>\nclass Shared { }\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Shared.cs", "Shared.cs", source)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled,
            visualBasicProjectSources: [("Shared.cs", "Shared.cs", source)]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().ContainSingle();
        result.Documents.Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
    }

    [TestMethod]
    public async Task FormatAll_ForeignLanguageDocumentWithSamePath_SkipsPathAndFixesEligibleDocument()
    {
        const string blockedSource = "/// <summary>Blocked.</summary>\nclass Blocked { }\n";
        const string eligibleSource = "/// <summary>Eligible.</summary>\nclass Eligible { }\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Blocked.cs", "Z-Blocked.cs", blockedSource)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: FixAllScope.Solution,
            additionalProjectSources: [("Eligible.cs", "A-Eligible.cs", eligibleSource)],
            visualBasicProjectSources: [("Blocked.cs", "Z-Blocked.cs", blockedSource)]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeTrue();
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().ContainSingle();
        result.Documents.Where(document => document.Name == "Blocked.cs")
            .Should().HaveCount(2).And.OnlyContain(document => document.Source == blockedSource);
        result.Documents.Single(document => document.Name == "Eligible.cs").Source.Should().Be(
            "/// <summary>\n///  Eligible.\n/// </summary>\nclass Eligible { }\n");
    }

    [TestMethod]
    public async Task FormatAll_ProjectWithForeignLanguagePath_SkipsPathAndFixesEligibleDocument()
    {
        const string blockedSource = "/// <summary>Blocked.</summary>\nclass Blocked { }\n";
        const string eligibleSource = "/// <summary>Eligible.</summary>\nclass Eligible { }\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [
                ("Eligible.cs", "A-Eligible.cs", eligibleSource),
                ("Blocked.cs", "Z-Blocked.cs", blockedSource)
            ],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: FixAllScope.Project,
            visualBasicProjectSources: [("Blocked.cs", "Z-Blocked.cs", blockedSource)]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeTrue();
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().ContainSingle();
        result.Documents.Where(document => document.Name == "Blocked.cs")
            .Should().HaveCount(2).And.OnlyContain(document => document.Source == blockedSource);
        result.Documents.Single(document => document.Name == "Eligible.cs").Source.Should().Be(
            "/// <summary>\n///  Eligible.\n/// </summary>\nclass Eligible { }\n");
    }

    [TestMethod]
    public async Task FormatAll_MissingLinkedDiagnosticWithKey_OffersNoFix()
    {
        const string linkedSource = "/// <summary>Linked.</summary>\nclass Linked { }\n";
        const string triggerSource = "/// <summary>Trigger.</summary>\nclass Trigger { }\n";
        string? equivalenceKey = null;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Linked.cs", "Z-Linked.cs", linkedSource)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: FixAllScope.Solution,
            addLinkedProject: true,
            additionalProjectSources: [("Trigger.cs", "A-Trigger.cs", triggerSource)],
            transformFixAllDiagnostics: diagnostics =>
                [diagnostics.First(diagnostic => string.Equals(
                    Path.GetFileName(diagnostic.Location.SourceTree?.FilePath),
                    "Z-Linked.cs",
                    StringComparison.OrdinalIgnoreCase))],
            onFixAllEquivalenceKey: key => equivalenceKey = key).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        equivalenceKey.Should().Be(nameof(FormatXmlDocumentationCodeFixProvider));
        result.FixAllActionOffered.Should().BeFalse();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(3);
        result.Documents.Where(document => document.Name == "Linked.cs")
            .Should().HaveCount(2).And.OnlyContain(document => document.Source == linkedSource);
        result.Documents.Single(document => document.Name == "Trigger.cs").Source.Should().Be(triggerSource);
    }

    [TestMethod]
    public async Task FormatAll_DivergentLinkedDiagnosticsWithKey_OffersNoFix()
    {
        const string linkedSource =
            "/// <remarks><para>Linked.</para></remarks>\nclass Linked { }\n";
        const string triggerSource =
            "/// <summary>Trigger.</summary>\nclass Trigger { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.IndentSizeOption] = "1"
        };
        Dictionary<string, string> linkedOptions = new()
        {
            [XmlDocumentationFormattingAnalyzer.IndentSizeOption] = "2"
        };
        string? equivalenceKey = null;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Linked.cs", "Z-Linked.cs", linkedSource)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            options,
            s_enabled,
            fixAllScope: FixAllScope.Solution,
            addLinkedProject: true,
            additionalProjectSources: [("Trigger.cs", "A-Trigger.cs", triggerSource)],
            linkedProjectOptions: linkedOptions,
            transformFixAllDiagnostics: diagnostics =>
                [.. diagnostics.Where(diagnostic => string.Equals(
                    Path.GetFileName(diagnostic.Location.SourceTree?.FilePath),
                    "Z-Linked.cs",
                    StringComparison.OrdinalIgnoreCase))],
            onFixAllEquivalenceKey: key => equivalenceKey = key).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        equivalenceKey.Should().Be(nameof(FormatXmlDocumentationCodeFixProvider));
        result.FixAllActionOffered.Should().BeFalse();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(3);
        result.Documents.Where(document => document.Name == "Linked.cs")
            .Should().HaveCount(2).And.OnlyContain(document => document.Source == linkedSource);
        result.Documents.Single(document => document.Name == "Trigger.cs").Source.Should().Be(triggerSource);
    }

    [TestMethod]
    [DataRow("nested")]
    [DataRow("partial")]
    [DataRow("same-start")]
    public async Task FormatAll_OverlappingReplacementSpans_OffersNoFix(string overlapKind)
    {
        const string source = "/// <summary>Sample.</summary>\nclass Sample { }\n";
        string? equivalenceKey = null;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Sample.cs", "Sample.cs", source)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            transformFixAllDiagnostics: diagnostics => CreateOverlappingDiagnostics(
                diagnostics,
                overlapKind),
            onFixAllEquivalenceKey: key => equivalenceKey = key).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        equivalenceKey.Should().Be(nameof(FormatXmlDocumentationCodeFixProvider));
        result.FixAllActionOffered.Should().BeFalse();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().ContainSingle();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(source);
    }

    [TestMethod]
    [DataRow(FixAllScope.Document)]
    [DataRow(FixAllScope.Project)]
    [DataRow(FixAllScope.Solution)]
    public async Task FormatAll_ReplacementMatchesSource_OffersNoFix(FixAllScope scope)
    {
        const string source = "/// <summary>Sample.</summary>\nclass Sample { }\n";
        string? equivalenceKey = null;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Sample.cs", "Sample.cs", source)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: scope,
            transformFixAllDiagnostics: diagnostics => [CreateNoOpDiagnostic(
                diagnostics.Should().ContainSingle().Subject)],
            onFixAllEquivalenceKey: key => equivalenceKey = key).ConfigureAwait(false);

        equivalenceKey.Should().Be(nameof(FormatXmlDocumentationCodeFixProvider));
        result.FixAllActionOffered.Should().BeFalse();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().ContainSingle();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(source);
    }

    [TestMethod]
    public async Task Format_ReplacementMatchesSource_OffersNoFix()
    {
        const string source = "/// <summary>Sample.</summary>\nclass Sample { }\n";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            [("Sample.cs", "Sample.cs", source)],
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled,
            transformDiagnostics: diagnostics => [CreateNoOpDiagnostic(
                diagnostics.Should().ContainSingle().Subject)]).ConfigureAwait(false);

        result.CodeFixActionOffered.Should().BeFalse();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().ContainSingle();
        result.Documents.Should().ContainSingle().Which.Source.Should().Be(source);
    }

    [TestMethod]
    public async Task FormatAll_Canceled_ThrowsOperationCanceledException()
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            ("Sample.cs", "Sample.cs", "/// <summary>Sample.</summary>\nclass Sample { }\n")
        ];
        using CancellationTokenSource source = new();
        source.Cancel();
        Func<Task> action = async () => await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            sources,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllCancellationToken: source.Token).ConfigureAwait(false);

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Format_SingleLineSummary_ExpandsIt()
    {
        string source =
            "class Sample\n{\n    /// <summary>The name.</summary>\n    string Name => \"name\";\n}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(
            "class Sample\n{\n    /// <summary>\n    ///  The name.\n    /// </summary>\n"
            + "    string Name => \"name\";\n}\n");
    }

    [TestMethod]
    public async Task Format_ExpandedFirstContentLineWithHangingContinuation_PreservesRelativeIndentation()
    {
        string source =
            "/// <summary>First line\n"
            + "///     hanging continuation.\n"
            + "/// </summary>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <summary>\n"
            + "///  First line\n"
            + "///     hanging continuation.\n"
            + "/// </summary>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_FirstProseRunBeforeNestedBlock_DoesNotLeakIntoLaterProse()
    {
        string source =
            "/// <remarks>First line\n"
            + "///     hanging before child.<para>Nested.</para>\n"
            + "/// Trailing block.\n"
            + "/// </remarks>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <remarks>\n"
            + "///  First line\n"
            + "///     hanging before child.\n"
            + "///  <para>\n"
            + "///   Nested.\n"
            + "///  </para>\n"
            + "///  Trailing block.\n"
            + "/// </remarks>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_ProseGeneratedAfterNestedBlock_PreservesHangingContinuation()
    {
        string source =
            "/// <remarks><para>Nested.</para>Trailing first\n"
            + "///      hanging continuation.\n"
            + "/// </remarks>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <remarks>\n"
            + "///  <para>\n"
            + "///   Nested.\n"
            + "///  </para>\n"
            + "///  Trailing first\n"
            + "///      hanging continuation.\n"
            + "/// </remarks>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_ThreeLineTopLevelElement_CompactsIt()
    {
        string source =
            "class Sample\n{\n    /// <returns>\n    ///  The name.\n    /// </returns>\n"
            + "    string Name() => \"name\";\n}\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(
            "class Sample\n{\n    /// <returns>The name.</returns>\n"
            + "    string Name() => \"name\";\n}\n");
    }

    [TestMethod]
    public async Task Format_ConfiguredIndentSize_UsesConfiguredIndentation()
    {
        string source =
            "/// <remarks><para>Text.</para></remarks>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.IndentSizeOption] = "2"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <remarks>\n///   <para>\n///     Text.\n///   </para>\n/// </remarks>\n"
            + "class Sample { }\n");
    }

    [TestMethod]
    public async Task Format_CrlfSource_PreservesLineEndings()
    {
        string source =
            "/// <summary>Text.</summary>\r\nclass Sample { }\r\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <summary>\r\n///  Text.\r\n/// </summary>\r\nclass Sample { }\r\n");
    }

    [TestMethod]
    public async Task Format_CodePayload_PreservesPayloadIndentation()
    {
        string source =
            "/// <example>\n/// <code>\n///       if (ready)\n///       {\n///           Run();\n///       }\n"
            + "/// </code>\n/// </example>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <example>\n///  <code>\n///       if (ready)\n///       {\n///           Run();\n///       }\n"
            + "///  </code>\n/// </example>\nclass Sample { }\n");
    }

    [TestMethod]
    public async Task Format_AlreadyFixedSource_IsIdempotent()
    {
        string source = "/// <summary>Text.</summary>\nclass Sample { }\n";

        string fixedSource = await ApplyFixAsync(source).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource).ConfigureAwait(false);

        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_OverLimitSingleLineWithEdgeWhitespace_IsIdempotent()
    {
        string source = "/// <returns> The name. </returns>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "32"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be("/// <returns>The name.</returns>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_CompactedElementWithExtraPrefixSpaces_IsIdempotent()
    {
        string source = "///    <returns> The name. </returns>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "32"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be("/// <returns>The name.</returns>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_ExactLimitCompactElementWithTrailingSuffix_IsIdempotent()
    {
        string source = "/// <returns> The name. </returns> \nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "32"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be("/// <returns>\n///  The name.\n/// </returns> \nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_NeighboringCompactElementsOnOverLimitLine_IsIdempotent()
    {
        string source =
            "/// <param name=\"value\">The value.</param><returns>The result.</returns>\n"
            + "class Sample { string Method(string value) => value; }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "50"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <param name=\"value\">The value.</param>\n"
            + "/// <returns>The result.</returns>\n"
            + "class Sample { string Method(string value) => value; }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_LastCrowdedElementWithTrailingSuffix_IsIdempotent()
    {
        string source =
            "/// <param name=\"v\">x</param><returns>x</returns>          \n"
            + "class Sample { string Method(string v) => v; }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "30"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <param name=\"v\">x</param>\n"
            + "/// <returns>\n///  x\n/// </returns>          \n"
            + "class Sample { string Method(string v) => v; }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_LaterCrowdedElementRequiringNormalization_IsIdempotent()
    {
        string source =
            "/// <param name=\"v\">x</param><returns> x </returns>\n"
            + "class Sample { string Method(string v) => v; }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "35"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <param name=\"v\">x</param>\n"
            + "/// <returns>x</returns>\n"
            + "class Sample { string Method(string v) => v; }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task Format_PairedElementCrowdedBySelfClosingSibling_IsIdempotent()
    {
        string source = "/// <returns>x</returns><inheritdoc/>\nclass Sample { }\n";
        Dictionary<string, string> options = new()
        {
            [XmlDocumentationFormattingAnalyzer.MaxLineLengthOption] = "30"
        };

        string fixedSource = await ApplyFixAsync(source, options).ConfigureAwait(false);
        string fixedAgain = await ApplyFixAsync(fixedSource, options).ConfigureAwait(false);

        fixedSource.Should().Be(
            "/// <returns>x</returns>\n/// <inheritdoc/>\nclass Sample { }\n");
        fixedAgain.Should().Be(fixedSource);
    }

    [TestMethod]
    public async Task FormatAll_MultipleDocuments_FixesEveryComment()
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            ("One.cs", "One.cs", "/// <summary>One.</summary>\nclass One { }\n"),
            ("Two.cs", "Two.cs", "/// <summary>Two.</summary>\nclass Two { }\n")
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            sources,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().OnlyContain(
            document => document.Source.Contains("///  ", StringComparison.Ordinal)
                && !document.Source.Contains("<summary>One.</summary>", StringComparison.Ordinal)
                && !document.Source.Contains("<summary>Two.</summary>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FormatAll_MultipleCommentsInOneDocument_FixesEveryComment()
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            (
                "Both.cs",
                "Both.cs",
                "/// <summary>One.</summary>\nclass One { }\n\n"
                + "/// <summary>Two.</summary>\nclass Two { }\n")
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            sources,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle()
            .Which.Source.Should().Be(
                "/// <summary>\n///  One.\n/// </summary>\nclass One { }\n\n"
                + "/// <summary>\n///  Two.\n/// </summary>\nclass Two { }\n");
    }

    [TestMethod]
    public async Task FormatAll_LargeDocument_FixesEveryComment()
    {
        const int commentCount = 136;
        const int targetDocumentLength = 80 * 1024;
        (string Name, string FilePath, string Source)[] sources =
        [
            CreateLargeDocument(commentCount, targetDocumentLength)
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new XmlDocumentationFormattingAnalyzer(),
            new FormatXmlDocumentationCodeFixProvider(),
            sources,
            XmlDocumentationFormattingAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.InitialAnalyzerDiagnosticCount.Should().Be(commentCount);
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle()
            .Which.Source.Split(["/// <summary>\n"], StringSplitOptions.None).Length.Should()
            .Be(commentCount + 1);

        static (string Name, string FilePath, string Source) CreateLargeDocument(
            int commentCount,
            int targetDocumentLength)
        {
            string members = string.Join(
                "\n",
                Enumerable.Range(0, commentCount - 1).Select(member =>
                    $"    /// <summary>Member {member}.</summary>\n"
                    + $"    int Value{member} => {member};"));
            string source =
                "/// <summary>Type.</summary>\n"
                + "class Sample\n"
                + "{\n"
                + members
                + "\n}\n";
            source += new string('/', targetDocumentLength - source.Length - 1) + "\n";
            return ("Sample.cs", "Sample.cs", source);
        }
    }

    private static ImmutableArray<Diagnostic> CreateOverlappingDiagnostics(
        ImmutableArray<Diagnostic> diagnostics,
        string overlapKind)
    {
        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        SyntaxTree tree = diagnostic.Location.SourceTree!;
        TextSpan span = diagnostic.Location.SourceSpan;
        TextSpan overlap = overlapKind switch
        {
            "nested" => TextSpan.FromBounds(span.Start + 1, span.End - 1),
            "partial" => TextSpan.FromBounds(span.End - 1, span.End + 1),
            "same-start" => TextSpan.FromBounds(span.Start, span.End - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(overlapKind))
        };
        Diagnostic overlapping = Diagnostic.Create(
            diagnostic.Descriptor,
            Location.Create(tree, overlap),
            [],
            diagnostic.Properties);
        return [diagnostic, overlapping];
    }

    private static Diagnostic CreateNoOpDiagnostic(Diagnostic diagnostic)
    {
        SourceText source = diagnostic.Location.SourceTree!.GetText();
        ImmutableDictionary<string, string?> properties = diagnostic.Properties.SetItem(
            "Replacement",
            source.ToString(diagnostic.Location.SourceSpan));
        return Diagnostic.Create(
            diagnostic.Descriptor,
            diagnostic.Location,
            diagnostic.AdditionalLocations,
            properties);
    }

}
