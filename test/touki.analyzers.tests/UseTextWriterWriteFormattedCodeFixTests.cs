// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

[TestClass]
public class UseTextWriterWriteFormattedCodeFixTests
{
    private static readonly MetadataReference s_toukiReference =
        MetadataReference.CreateFromFile(typeof(Touki.Io.TextWriterExtensions).Assembly.Location);

    [TestMethod]
    public void GetFixAllProvider_Default_IsDocumentBatched()
    {
        FixAllProvider provider = new UseTextWriterWriteFormattedCodeFixProvider().GetFixAllProvider();

        provider.Should().NotBeSameAs(WellKnownFixAllProviders.BatchFixer);
        provider.GetSupportedFixAllScopes().Should().BeEquivalentTo(
            [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution]);
    }

    private static async Task<string> FixAsync(string source) =>
        await CodeFixTestHarness.ApplyFixAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            source,
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

    [TestMethod]
    public async Task UseWriteFormatted_MissingNamespaceImport_AddsImportAndRenamesMethod()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;
        string expected = source
            .Replace(
                "using System.IO;",
                $"using System.IO;{Environment.NewLine}using Touki.Io;")
            .Replace("writer.Write(", "writer.WriteFormatted(");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UseWriteFormatted_NamespaceAlreadyImported_DoesNotDuplicateImport()
    {
        const string source = """
            using System.IO;
            using Touki.Io;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;
        string expected = source.Replace("writer.Write(", "writer.WriteFormatted(");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UseWriteFormatted_NamedArgument_RenamesParameter()
    {
        const string source = """
            using System.IO;
            using Touki.Io;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write(value: $"Value: {value}");
                }
            }
            """;
        string expected = source.Replace(
            "writer.Write(value:",
            "writer.WriteFormatted(builder:");

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(expected);
    }

    [TestMethod]
    public async Task UseWriteFormatted_AlignmentAndFormat_RenamesAndCompiles()
    {
        const string source = """
            using System.IO;
            using Touki.Io;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Value: {value,4:x}");
                }
            }
            """;

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Contain("writer.WriteFormatted($\"Value: {value,4:x}\");");
    }

    [TestMethod]
    public async Task UseWriteFormatted_ConditionalAccess_LeavesSourceUnchanged()
    {
        const string source = """
            using System.IO;
            using Touki.Io;

            class Sample
            {
                void WriteValue(TextWriter? writer, int value)
                {
                    writer?.Write($"Value: {value}");
                }
            }
            """;
        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UseWriteFormatted_FixAll_RewritesEveryCall()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValues(TextWriter writer, int first, int second)
                {
                    writer.Write($"First: {first}");
                    writer.Write($"Second: {second}");
                }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: true,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Subject.Source;
        fixedSource.Should().Contain("writer.WriteFormatted($\"First: {first}\");");
        fixedSource.Should().Contain("writer.WriteFormatted($\"Second: {second}\");");
        fixedSource.Split(["using Touki.Io;"], StringSplitOptions.None).Should().HaveCount(2);
    }

    [TestMethod]
    public async Task UseWriteFormatted_FixAllNestedCalls_RewritesBothCalls()
    {
        const string source = """
            using System;
            using System.IO;
            using Touki.Io;

            class Sample
            {
                void WriteValues(TextWriter writer, int value)
                {
                    writer.Write($"Outer: {Invoke(() => writer.Write($"Inner: {value}"))}");
                }

                static int Invoke(Action action)
                {
                    action();
                    return 0;
                }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: true,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Subject.Source;
        fixedSource.Should().Contain(
            "writer.WriteFormatted($\"Outer: {Invoke(() => writer.WriteFormatted($\"Inner: {value}\"))}\");");
    }

    [TestMethod]
    [DataRow(FixAllScope.Document, 2)]
    [DataRow(FixAllScope.Project, 1)]
    [DataRow(FixAllScope.Solution, 0)]
    public async Task UseWriteFormatted_FixAllScope_RewritesSelectedScope(
        FixAllScope scope,
        int remainingDiagnostics)
    {
        const string firstSource = """
            using System.IO;

            class FirstSample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"First: {value}");
                }
            }
            """;
        const string secondSource = """
            using System.IO;

            class SecondSample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Second: {value}");
                }
            }
            """;
        const string additionalSource = """
            using System.IO;

            class AdditionalSample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Additional: {value}");
                }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [
                ("First.cs", "A-First.cs", firstSource),
                ("Second.cs", "B-Second.cs", secondSource)
            ],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: true,
            additionalReferences: [s_toukiReference],
            fixAllScope: scope,
            additionalProjectSources: [
                ("Additional.cs", "C-Additional.cs", additionalSource)
            ]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(remainingDiagnostics);
        result.Documents.Single(document => document.Name == "First.cs").Source
            .Should().Contain("writer.WriteFormatted($\"First: {value}\");");
        result.Documents.Single(document => document.Name == "Second.cs").Source.Should().Contain(
            scope is FixAllScope.Project or FixAllScope.Solution
                ? "writer.WriteFormatted($\"Second: {value}\");"
                : "writer.Write($\"Second: {value}\");");
        result.Documents.Single(document => document.Name == "Additional.cs").Source.Should().Contain(
            scope == FixAllScope.Solution
                ? "writer.WriteFormatted($\"Additional: {value}\");"
                : "writer.Write($\"Additional: {value}\");");
    }

    [TestMethod]
    public async Task UseWriteFormatted_FixAllManyCalls_RewritesEveryCallAndAddsOneImport()
    {
        const int callCount = 128;
        const int unrelatedBindingCount = 256;
        const string template = """
            using System.IO;

            class Sample
            {
                void WriteValues(TextWriter writer, int value)
                {
            UNRELATED
            CALLS
                }
            }
            """;
        List<string> statements = new(unrelatedBindingCount + callCount);
        for (int index = 0; index < unrelatedBindingCount; index++)
        {
            statements.Add($"        int unrelated{index} = System.Math.Abs(value);");
        }

        string unrelated = string.Join(Environment.NewLine, statements);
        statements.Clear();
        for (int index = 0; index < callCount; index++)
        {
            statements.Add($"        writer.Write($\"Value {index}: {{value}}\");");
        }

        string source = template
            .Replace("UNRELATED", unrelated)
            .Replace("CALLS", string.Join(Environment.NewLine, statements));
        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: true,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(callCount);
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Subject.Source;
        fixedSource.Split(["writer.WriteFormatted("], StringSplitOptions.None)
            .Should().HaveCount(callCount + 1);
        fixedSource.Split(["using Touki.Io;"], StringSplitOptions.None).Should().HaveCount(2);
    }

    [TestMethod]
    public async Task UseWriteFormatted_CompetingExtension_WithholdsFix()
    {
        const string source = """
            global using Other;

            using System.IO;

            namespace Other
            {
                public static class OtherTextWriterExtensions
                {
                    public static void WriteFormatted(
                        this TextWriter writer,
                        ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler builder)
                    {
                    }
                }
            }

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UseWriteFormatted_GlobalUsing_DoesNotAddLocalImport()
    {
        const string globalUsings = "global using Touki.Io;";
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [
                ("GlobalUsings.cs", "GlobalUsings.cs", globalUsings),
                ("Test.cs", "Test.cs", source)
            ],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: false,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        CodeFixTestDocument fixedDocument = result.Documents.Single(document => document.Name == "Test.cs");
        fixedDocument.Source.Should().Contain("writer.WriteFormatted($\"Value: {value}\");");
        fixedDocument.Source.Should().NotContain("using Touki.Io;");
    }

    [TestMethod]
    public async Task UseWriteFormatted_ImportIntroducesAmbiguity_WithholdsFix()
    {
        const string source = """
            using System.IO;
            using Other;

            namespace Other
            {
                public class MSBuildSpecification
                {
                }
            }

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    MSBuildSpecification specification = new();
                    writer.Write($"Value: {value}");
                }
            }
            """;

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UseWriteFormatted_FixAllImportIntroducesAmbiguity_WithholdsDocument()
    {
        const string triggerSource = """
            using System.IO;

            class Trigger
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Trigger: {value}");
                }
            }
            """;
        const string ambiguousSource = """
            using System.IO;
            using Other;

            namespace Other
            {
                public class MSBuildSpecification
                {
                }
            }

            class Sample
            {
                void WriteValues(TextWriter writer, int first, int second)
                {
                    MSBuildSpecification specification = new();
                    writer.Write($"First: {first}");
                    writer.Write($"Second: {second}");
                }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [
                ("Trigger.cs", "A-Trigger.cs", triggerSource),
                ("Ambiguous.cs", "B-Ambiguous.cs", ambiguousSource)
            ],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: true,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.CodeFixActionOffered.Should().BeTrue();
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(2);
        result.Documents.Single(document => document.Name == "Trigger.cs").Source
            .Should().Contain("writer.WriteFormatted($\"Trigger: {value}\");");
        result.Documents.Single(document => document.Name == "Ambiguous.cs").Source.Should().Be(ambiguousSource);
    }

    [TestMethod]
    public async Task UseWriteFormatted_ImportIntroducesInterpolationAmbiguity_WithholdsFix()
    {
        const string source = """
            using System.IO;
            using Other;

            namespace Other
            {
                public class Result
                {
                }
            }

            namespace Touki.Io
            {
                public class Result
                {
                }
            }

            class Sample
            {
                void WriteValue(TextWriter writer)
                {
                    writer.Write($"Result type: {typeof(Result)}");
                }
            }
            """;

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UseWriteFormatted_AppendedImportPreservesCrefBinding_AppliesFix()
    {
        const string globalUsing = """
            global using Other;

            namespace Other
            {
                public class MSBuildSpecification
                {
                }
            }
            """;
        const string source = """
            using System.IO;

            /// <summary>
            ///  Uses <see cref="MSBuildSpecification"/>.
            /// </summary>
            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [
                ("GlobalUsings.cs", "A-GlobalUsings.cs", globalUsing),
                ("Test.cs", "B-Test.cs", source)
            ],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: false,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        result.CodeFixActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Single(document => document.Name == "Test.cs").Source
            .Should().Contain("writer.WriteFormatted($\"Value: {value}\");");
    }

    [TestMethod]
    public async Task UseWriteFormatted_ExpressionTree_WithholdsStaleFix()
    {
        const string source = """
            using System;
            using System.IO;
            using System.Linq.Expressions;
            using Touki.Io;

            class Sample
            {
                Expression<Action<TextWriter, int>> Create()
                    => (writer, value) => writer.Write($"Value: {value}");
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new ForcedWriteAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            source,
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UseWriteFormatted_FixAllWithExpressionTree_RewritesOnlySafeCall()
    {
        const string source = """
            using System;
            using System.IO;
            using System.Linq.Expressions;
            using Touki.Io;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Immediate: {value}");
                }

                Expression<Action<TextWriter, int>> Create()
                    => (writer, value) => writer.Write($"Expression: {value}");
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new ForcedWriteAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: true,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        string fixedSource = result.Documents.Should().ContainSingle().Subject.Source;
        fixedSource.Should().Contain("writer.WriteFormatted($\"Immediate: {value}\");");
        fixedSource.Should().Contain("writer.Write($\"Expression: {value}\");");
    }

    [TestMethod]
    public async Task UseWriteFormatted_AwaitedInterpolation_RenamesAndCompiles()
    {
        const string source = """
            using System.IO;
            using System.Threading.Tasks;
            using Touki.Io;

            class Sample
            {
                async Task WriteValue(TextWriter writer)
                {
                    writer.Write($"Value: {await Task.FromResult(42)}");
                }
            }
            """;

        string fixedSource = await FixAsync(source).ConfigureAwait(false);

        fixedSource.Should().Contain(
            "writer.WriteFormatted($\"Value: {await Task.FromResult(42)}\");");
    }

    [TestMethod]
    public async Task UseWriteFormatted_StaleWriteLineDiagnostic_WithholdsFix()
    {
        const string source = """
            using System.IO;
            using Touki.Io;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.WriteLine($"Value: {value}");
                }
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new ForcedWriteAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            source,
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UseWriteFormatted_StaleCustomWriteDiagnostic_WithholdsFix()
    {
        const string source = """
            using System.IO;
            using Touki.Io;

            class CustomWriter : StringWriter
            {
                public new void Write(string value) { }
            }

            class Sample
            {
                void WriteValue(CustomWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new ForcedWriteAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            source,
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        fixedSource.Should().Be(source);
    }

    [TestMethod]
    public async Task UseWriteFormatted_CSharp10_RenamesMethod()
    {
        const string source = """
            using System.IO;
            using Touki.Io;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        string fixedSource = await CodeFixTestHarness.ApplyFixAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            source,
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            additionalReferences: [s_toukiReference],
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp10)).ConfigureAwait(false);

        fixedSource.Should().Contain("writer.WriteFormatted($\"Value: {value}\");");
    }

    [TestMethod]
    public async Task UseWriteFormatted_MultiplePathlessDocuments_OffersFix()
    {
        const string firstSource = """
            using System.IO;

            class FirstSample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"First: {value}");
                }
            }
            """;
        const string secondSource = """
            class SecondSample
            {
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [
                ("First.cs", "First.cs", firstSource),
                ("Second.cs", "Second.cs", secondSource)
            ],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: false,
            additionalReferences: [s_toukiReference],
            assignSourceFilePaths: false).ConfigureAwait(false);

        result.CodeFixActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().BeEmpty();
        result.Documents.Should().AllSatisfy(document => document.FilePath.Should().BeNull());
        result.Documents.Single(document => document.Name == "First.cs").Source
            .Should().Contain("writer.WriteFormatted($\"First: {value}\");");
    }

    [TestMethod]
    public async Task UseWriteFormatted_LinkedDocument_OffersNoFix()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;
        const string competingExtension = """
            global using Other;

            namespace Other
            {
                public static class OtherTextWriterExtensions
                {
                    public static void WriteFormatted(
                        this System.IO.TextWriter writer,
                        ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler builder)
                    {
                    }
                }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [("Shared.cs", "Shared.cs", source)],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: false,
            additionalReferences: [s_toukiReference],
            addLinkedProject: true,
            linkedProjectSources: [
                ("CompetingExtension.cs", "CompetingExtension.cs", competingExtension)
            ]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(2);
        result.CompilerErrors.Should().BeEmpty();
        result.Documents.Where(document => document.Name == "Shared.cs")
            .Should().HaveCount(2).And.OnlyContain(document => document.Source == source);
        result.CodeFixActionOffered.Should().BeFalse();
    }

    [TestMethod]
    public async Task UseWriteFormatted_FixAllSolution_SkipsLinkedDocumentAndFixesEligibleDocument()
    {
        const string linkedSource = """
            using System.IO;

            class LinkedSample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Linked: {value}");
                }
            }
            """;
        const string eligibleSource = """
            using System.IO;

            class EligibleSample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Eligible: {value}");
                }
            }
            """;

        CodeFixTestResult result = await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [("Linked.cs", "Z-Linked.cs", linkedSource)],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: true,
            additionalReferences: [s_toukiReference],
            fixAllScope: FixAllScope.Solution,
            addLinkedProject: true,
            additionalProjectSources: [
                ("Eligible.cs", "A-Eligible.cs", eligibleSource)
            ]).ConfigureAwait(false);

        result.InitialAnalyzerDiagnosticCount.Should().Be(3);
        result.CodeFixActionOffered.Should().BeTrue();
        result.FixAllActionOffered.Should().BeTrue();
        result.CompilerErrors.Should().BeEmpty();
        result.AnalyzerDiagnostics.Should().HaveCount(2);
        result.Documents.Where(document => document.Name == "Linked.cs")
            .Should().HaveCount(2).And.OnlyContain(document => document.Source == linkedSource);
        result.Documents.Single(document => document.Name == "Eligible.cs").Source
            .Should().Contain("writer.WriteFormatted($\"Eligible: {value}\");");
    }

    [TestMethod]
    public async Task UseWriteFormatted_FixAllCanceled_ThrowsOperationCanceledException()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Func<Task> action = async () => await CodeFixTestHarness.ApplyFixToSolutionAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            new UseTextWriterWriteFormattedCodeFixProvider(),
            [("Test.cs", "Test.cs", source)],
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            fixAll: true,
            additionalReferences: [s_toukiReference],
            fixAllCancellationToken: cancellation.Token).ConfigureAwait(false);

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class ForcedWriteAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor s_rule = new(
            UseTextWriterWriteFormattedAnalyzer.DiagnosticId,
            "Forced Write diagnostic",
            "Forced Write diagnostic",
            "Test",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                static syntaxContext =>
                {
                    if (syntaxContext.Node is InvocationExpressionSyntax
                        {
                            Expression: MemberAccessExpressionSyntax memberAccess
                        }
                        && memberAccess.Name.Identifier.ValueText is "Write" or "WriteLine")
                    {
                        syntaxContext.ReportDiagnostic(
                            Diagnostic.Create(s_rule, memberAccess.Name.GetLocation()));
                    }
                },
                SyntaxKind.InvocationExpression);
        }
    }
}