// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

[TestClass]
public class UseTextWriterWriteFormattedCodeFixTests
{
    private static readonly MetadataReference s_toukiReference =
        MetadataReference.CreateFromFile(typeof(Touki.Io.TextWriterExtensions).Assembly.Location);

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