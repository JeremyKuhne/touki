// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;

namespace Touki.Analyzers;

[TestClass]
public class UseTextWriterWriteFormattedAnalyzerTests
{
    private static readonly MetadataReference s_toukiReference =
        MetadataReference.CreateFromFile(typeof(Touki.Io.TextWriterExtensions).Assembly.Location);

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        await AnalyzerTestHarness.GetDiagnosticsAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            source,
            additionalReferences: [s_toukiReference])
            .ConfigureAwait(false);

    [TestMethod]
    public async Task AnalyzeInvocation_TextWriterWriteInterpolatedString_ReportsDiagnostic()
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

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(UseTextWriterWriteFormattedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_DerivedWriterWriteInterpolatedString_ReportsDiagnostic()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(int value)
                {
                    StringWriter writer = new();
                    writer.Write($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(UseTextWriterWriteFormattedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_InterpolatedString_ReportsAtMethodName()
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

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Write");
    }

    [TestMethod]
    public async Task AnalyzeInvocation_ParenthesizedInterpolatedString_ReportsDiagnostic()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.Write(($"Value: {value}"));
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(UseTextWriterWriteFormattedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_WriteLineInterpolatedString_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(TextWriter writer, int value)
                {
                    writer.WriteLine($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_ConditionalAccess_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(TextWriter? writer, int value)
                {
                    writer?.Write($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_UnrelatedWriteMethod_ReportsNothing()
    {
        const string source = """
            class Writer
            {
                public void Write(string value) { }
            }

            class Sample
            {
                void WriteValue(Writer writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_StringArgument_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(TextWriter writer, string value)
                {
                    writer.Write(value);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_ConstantInterpolatedString_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue(TextWriter writer)
                {
                    writer.Write($"Value");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_BaseWriteInterpolatedString_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class Sample : StringWriter
            {
                void WriteValue(int value)
                {
                    base.Write($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_HiddenWriteMethod_ReportsNothing()
    {
        const string source = """
            using System.IO;

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

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_InstanceWriteFormattedMethod_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class CustomWriter : StringWriter
            {
                public void WriteFormatted(string value) { }
            }

            class Sample
            {
                void WriteValue(CustomWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_UnrelatedInstanceWriteFormattedMethod_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class CustomWriter : StringWriter
            {
                public void WriteFormatted() { }
            }

            class Sample
            {
                void WriteValue(CustomWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_RefInstanceWriteFormattedMethod_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class CustomWriter : StringWriter
            {
                public void WriteFormatted(ref string value) { }
            }

            class Sample
            {
                void WriteValue(CustomWriter writer, int value)
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_ExtensionApiUnavailable_ReportsNothing()
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

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_ExtensionHandlerOverloadUnavailable_ReportsNothing()
    {
        const string source = """
            using System.IO;

            namespace Touki.Io
            {
                public static class TextWriterExtensions
                {
                    public static void WriteFormatted(this TextWriter writer, string value) { }
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

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_CSharp9_ReportsNothing()
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

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            source,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp9),
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_CSharp10_ReportsDiagnostic()
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

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new UseTextWriterWriteFormattedAnalyzer(),
            source,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp10),
            additionalReferences: [s_toukiReference]).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(UseTextWriterWriteFormattedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_InsideExpressionTree_ReportsNothing()
    {
        const string source = """
            using System;
            using System.IO;
            using System.Linq.Expressions;

            class Sample
            {
                Expression<Action<TextWriter, int>> Create()
                    => (writer, value) => writer.Write($"Value: {value}");
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_AwaitedInterpolation_ReportsDiagnostic()
    {
        const string source = """
            using System.IO;
            using System.Threading.Tasks;

            class Sample
            {
                async Task WriteValue(TextWriter writer)
                {
                    writer.Write($"Value: {await Task.FromResult(42)}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(UseTextWriterWriteFormattedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_TypeParameterConstrainedToCustomWriter_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class CustomWriter : StringWriter
            {
            }

            class Sample
            {
                void WriteValue<TWriter>(TWriter writer, int value) where TWriter : CustomWriter
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_TypeParameterConstrainedToTextWriter_ReportsDiagnostic()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                void WriteValue<TWriter>(TWriter writer, int value) where TWriter : TextWriter
                {
                    writer.Write($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(UseTextWriterWriteFormattedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_ConditionalWriteFormatted_Compiles()
    {
        const string source = """
            using System.IO;
            using Touki.Io;

            class Sample
            {
                void WriteValue(TextWriter? writer, int value)
                {
                    writer?.WriteFormatted($"Value: {value}");
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}