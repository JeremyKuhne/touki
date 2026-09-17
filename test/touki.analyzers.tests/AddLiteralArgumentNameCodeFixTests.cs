// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Touki.Analyzers;

[TestClass]
public class AddLiteralArgumentNameCodeFixTests
{
    private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_enabled =
        new Dictionary<string, ReportDiagnostic>
        {
            [RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId] = ReportDiagnostic.Warn
        };

    private static async Task<string> FixAsync(string source, string? literals = null)
    {
        Dictionary<string, string>? options = literals is null
            ? null
            : new Dictionary<string, string>
            {
                [RequireNamedArgumentsForLiteralsAnalyzer.LiteralsOption] = literals
            };

        return await CodeFixTestHarness.ApplyFixAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            source,
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            options,
            s_enabled).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task AddArgumentName_PositionalBoolean_AddsParameterName()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled) { }

                void Use() => Target(true);
            }
            """;
        string expected = source.Replace("Target(true)", "Target(enabled: true)");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task AddArgumentName_LeadingTrivia_PreservesTriviaBeforeName()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled) { }

                void Use() => Target(
                    /* keep */ true);
            }
            """;
        string expected = source.Replace("/* keep */ true", "/* keep */ enabled: true");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task AddArgumentName_KeywordParameter_EscapesIdentifier()
    {
        const string source = """
            class Sample
            {
                void Target(bool @event) { }

                void Use() => Target(false);
            }
            """;
        string expected = source.Replace("Target(false)", "Target(@event: false)");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task AddArgumentName_ConfiguredInteger_AddsParameterName()
    {
        const string source = """
            class Sample
            {
                void Target(int count) { }

                void Use() => Target(42);
            }
            """;
        string expected = source.Replace("Target(42)", "Target(count: 42)");

        string fixedSource = await FixAsync(source, "integer").ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task AddArgumentName_AttributeConstructor_AddsParameterName()
    {
        const string source = """
            using System;

            sealed class FlagAttribute : Attribute
            {
                public FlagAttribute(bool enabled) { }
            }

            [Flag(true)]
            class Sample { }
            """;
        string expected = source.Replace("Flag(true)", "Flag(enabled: true)");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task AddArgumentName_CSharp71WithFollowingPositionalArgument_WithholdsFix()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled, int count) { }

                void Use(int count) => Target(true, count);
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp7_1)).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task AddArgumentName_ExpressionTree_WithholdsFix()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;

            class Sample
            {
                void Target(bool enabled) { }

                Expression<Action> Create() => () => Target(true);
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task AddArgumentName_CSharp3_WithholdsFix()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled) { }

                void Use() { Target(true); }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp3)).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(1);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task AddArgumentName_LinkedDocument_WithholdsFix()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled) { }

                void Use() => Target(true);
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            [("Test.cs", "Shared/Test.cs", source)],
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            fixAll: false,
            diagnosticOptions: s_enabled,
            addLinkedProject: true).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task AddArgumentName_FixAll_AddsEveryParameterName()
    {
        const string source = """
            class Sample
            {
                void Target(bool enabled, object value, int count) { }

                void Use() => Target(true, null, default);
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().ContainSingle()
            .Which.Source.Should().Contain("Target(enabled: true, value: null, count: default)");
    }

    [TestMethod]
    public async Task AddArgumentName_FixAllCSharp71_NamesEntirePositionalSuffix()
    {
        const string triggerSource = """
            class Trigger
            {
                void Target(bool enabled) { }

                void Use() => Target(true);
            }
            """;
        const string suffixSource = """
            class Suffix
            {
                void Target(bool enabled, bool visible) { }

                void Use() => Target(true, false);
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            [
                ("Trigger.cs", "A-Trigger.cs", triggerSource),
                ("Suffix.cs", "B-Suffix.cs", suffixSource)
            ],
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: FixAllScope.Project,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp7_1)).ConfigureAwait(false);

        result.CompilerErrors.Should().BeEmpty();
        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.Documents.Single(document => document.Name == "Trigger.cs").Source
            .Should().Contain("Target(enabled: true)");
        result.Documents.Single(document => document.Name == "Suffix.cs").Source
            .Should().Contain("Target(enabled: true, visible: false)");
        result.AnalyzerDiagnostics.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(FixAllScope.Document)]
    [DataRow(FixAllScope.Project)]
    [DataRow(FixAllScope.Solution)]
    public async Task AddArgumentName_FixAllScope_ChangesOnlySelectedScope(FixAllScope scope)
    {
        (string Name, string FilePath, string Source)[] sources =
        [
            ("First.cs", "First.cs", "partial class Sample { void First(bool enabled) { } void A() => First(true); }"),
            ("Second.cs", "Second.cs", "partial class Sample { void Second(bool enabled) { } void B() => Second(false); }")
        ];
        (string Name, string FilePath, string Source)[] additionalProjectSources =
        [
            ("Additional.cs", "Z-Additional.cs", "class Other { void Target(bool enabled) { } void Use() => Target(true); }")
        ];

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            sources,
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: scope,
            additionalProjectSources: additionalProjectSources).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.Documents.Single(document => document.Name == "First.cs").Source.Should().Contain("First(enabled: true)");

        switch (scope)
        {
            case FixAllScope.Document:
                result.AnalyzerDiagnostics.Should().HaveCount(2);
                break;
            case FixAllScope.Project:
                result.Documents.Single(document => document.Name == "Second.cs").Source
                    .Should().Contain("Second(enabled: false)");
                result.AnalyzerDiagnostics.Should().ContainSingle();
                break;
            case FixAllScope.Solution:
                result.Documents.Single(document => document.Name == "Second.cs").Source
                    .Should().Contain("Second(enabled: false)");
                result.Documents.Single(document => document.Name == "Additional.cs").Source
                    .Should().Contain("Target(enabled: true)");
                result.AnalyzerDiagnostics.Should().BeEmpty();
                break;
        }
    }

    [TestMethod]
    public async Task AddArgumentName_FixAllCanceled_ThrowsOperationCanceledException()
    {
        const string source = "class Sample { void Target(bool enabled) { } void Use() => Target(true); }";
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Func<Task> action = async () => await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllCancellationToken: cancellation.Token).ConfigureAwait(false);

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task AddArgumentName_FixAllSolution_SkipsLinkedDocumentsAndFixesOrdinaryDocument()
    {
        const string linkedSource = "class Shared { void Target(bool enabled) { } void Use() => Target(true); }";
        const string ordinarySource = "class Ordinary { void Target(bool enabled) { } void Use() => Target(false); }";

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new RequireNamedArgumentsForLiteralsAnalyzer(),
            new AddLiteralArgumentNameCodeFixProvider(),
            [("Shared.cs", "Z-Shared.cs", linkedSource)],
            RequireNamedArgumentsForLiteralsAnalyzer.DiagnosticId,
            fixAll: true,
            diagnosticOptions: s_enabled,
            fixAllScope: FixAllScope.Solution,
            addLinkedProject: true,
            additionalProjectSources: [("Ordinary.cs", "A-Ordinary.cs", ordinarySource)]).ConfigureAwait(false);

        result.CompilerErrors.Should().BeEmpty();
        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.Documents.Where(document => document.Name == "Shared.cs")
            .Should().OnlyContain(document => document.Source == linkedSource);
        result.Documents.Single(document => document.Name == "Ordinary.cs").Source
            .Should().Contain("Target(enabled: false)");
        result.AnalyzerDiagnostics.Should().HaveCount(2);
    }
}
