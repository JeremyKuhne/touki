// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class UsePathJoinAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        await AnalyzerTestHarness.GetDiagnosticsAsync(new UsePathJoinAnalyzer(), source).ConfigureAwait(false);

    [TestMethod]
    public async Task AnalyzeInvocation_PathCombine_ReportsDiagnostic()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(UsePathJoinAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_AllPathCombineOverloads_ReportDiagnostics()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Two(string first, string second) => Path.Combine(first, second);
                string Three(string first, string second, string third) => Path.Combine(first, second, third);
                string Four(string first, string second, string third, string fourth)
                    => Path.Combine(first, second, third, fourth);
                string Array(string[] paths) => Path.Combine(paths);
                string Span(System.ReadOnlySpan<string> paths) => Path.Combine(paths);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(5);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == UsePathJoinAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_FullyQualifiedPathCombine_ReportsAtMethodName()
    {
        const string source = """
            class Sample
            {
                string Build(string first, string second) => System.IO.Path.Combine(first, second);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        Location location = diagnostics.Should().ContainSingle().Subject.Location;
        location.SourceTree!.GetText().ToString(location.SourceSpan).Should().Be("Combine");
    }

    [TestMethod]
    public async Task AnalyzeInvocation_AliasedPathCombine_ReportsDiagnostic()
    {
        const string source = """
            using FilePath = System.IO.Path;

            class Sample
            {
                string Build(string first, string second) => FilePath.Combine(first, second);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(UsePathJoinAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_UsingStaticCombine_ReportsDiagnostic()
    {
        const string source = """
            using static System.IO.Path;

            class Sample
            {
                string Build(string first, string second) => Combine(first, second);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(UsePathJoinAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_PathJoin_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                string Build(string first, string second) => Path.Join(first, second);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_UnrelatedPathCombine_ReportsNothing()
    {
        const string source = """
            static class Path
            {
                public static string Combine(string first, string second) => first + second;
            }

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_Net472MicrosoftIoPathCombine_AllSpellingsReportDiagnostics()
    {
        const string source = """
            using Microsoft.IO;
            using RedistPath = Microsoft.IO.Path;
            using static Microsoft.IO.Path;

            class Sample
            {
                string Imported(string first, string second) => Path.Combine(first, second);
                string Qualified(string first, string second) => Microsoft.IO.Path.Combine(first, second);
                string Aliased(string first, string second) => RedistPath.Combine(first, second);
                string Static(string first, string second) => Combine(first, second);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new UsePathJoinAnalyzer(),
            source,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        diagnostics.Should().HaveCount(4);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == UsePathJoinAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_SourceDefinedMicrosoftIoPathCombine_ReportsNothing()
    {
        const string source = """
            namespace Microsoft.IO
            {
                public static class Path
                {
                    public static string Combine(string first, string second) => first + second;
                }
            }

            class Sample
            {
                string Build(string first, string second) => Microsoft.IO.Path.Combine(first, second);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_GeneratedCode_ReportsNothing()
    {
        const string source = """
            // <auto-generated/>
            using System.IO;

            class Sample
            {
                string Build(string first, string second) => Path.Combine(first, second);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}