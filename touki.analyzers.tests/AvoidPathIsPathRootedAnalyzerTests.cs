// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

[TestClass]
public class AvoidPathIsPathRootedAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        await AnalyzerTestHarness.GetDiagnosticsAsync(new AvoidPathIsPathRootedAnalyzer(), source)
            .ConfigureAwait(false);

    [TestMethod]
    public async Task AnalyzeInvocation_PathIsPathRooted_ReportsDiagnostic()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                bool Check(string path) => Path.IsPathRooted(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(AvoidPathIsPathRootedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_StringAndSpanOverloads_ReportDiagnostics()
    {
        const string source = """
            using System;
            using System.IO;

            class Sample
            {
                bool String(string path) => Path.IsPathRooted(path);
                bool Span(ReadOnlySpan<char> path) => Path.IsPathRooted(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == AvoidPathIsPathRootedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_AllSystemIoPathSpellings_ReportAtMethodName()
    {
        const string source = """
            using System.IO;
            using FilePath = System.IO.Path;
            using static System.IO.Path;

            class Sample
            {
                bool Imported(string path) => Path.IsPathRooted(path);
                bool Qualified(string path) => System.IO.Path.IsPathRooted(path);
                bool Aliased(string path) => FilePath.IsPathRooted(path);
                bool Static(string path) => IsPathRooted(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(4);
        diagnostics.Should().OnlyContain(diagnostic =>
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan) == "IsPathRooted");
    }

    [TestMethod]
    public async Task AnalyzeInvocation_MessageRecommendsQualificationCheck()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                bool Check(string path) => Path.IsPathRooted(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle().Which.GetMessage()
            .Should().Contain("Path.IsPathFullyQualified")
            .And.Contain("working directory");
    }

    [TestMethod]
    public async Task AnalyzeInvocation_PathIsPathFullyQualified_ReportsNothing()
    {
        const string source = """
            using System.IO;

            class Sample
            {
                bool Check(string path) => Path.IsPathFullyQualified(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_UnrelatedPathIsPathRooted_ReportsNothing()
    {
        const string source = """
            static class Path
            {
                public static bool IsPathRooted(string path) => false;
            }

            class Sample
            {
                bool Check(string path) => Path.IsPathRooted(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_SourceDefinedSystemIoPath_ReportsNothing()
    {
        const string source = """
            namespace System.IO
            {
                public static class Path
                {
                    public static bool IsPathRooted(string path) => false;
                }
            }

            class Sample
            {
                bool Check(string path) => System.IO.Path.IsPathRooted(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public async Task AnalyzeInvocation_SourceSystemIoPathWithTrustedRedist_StillReportsRedistCall()
    {
        const string source = """
            namespace System.IO
            {
                public static class Path
                {
                    public static bool IsPathRooted(string path) => false;
                }
            }

            class Sample
            {
                bool Check(string path) => Microsoft.IO.Path.IsPathRooted(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AvoidPathIsPathRootedAnalyzer(),
            source,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be(AvoidPathIsPathRootedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_Net472MicrosoftIoPath_AllSpellingsAndOverloadsReportDiagnostics()
    {
        const string source = """
            using System;
            using Microsoft.IO;
            using RedistPath = Microsoft.IO.Path;
            using static Microsoft.IO.Path;

            class Sample
            {
                bool Imported(string path) => Path.IsPathRooted(path);
                bool Qualified(string path) => Microsoft.IO.Path.IsPathRooted(path);
                bool Aliased(ReadOnlySpan<char> path) => RedistPath.IsPathRooted(path);
                bool Static(string path) => IsPathRooted(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AvoidPathIsPathRootedAnalyzer(),
            source,
            metadataReferences: RoslynTestEnvironment.Net472References).ConfigureAwait(false);

        diagnostics.Should().HaveCount(4);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == AvoidPathIsPathRootedAnalyzer.DiagnosticId);
    }

    [TestMethod]
    public async Task AnalyzeInvocation_Net472SystemIoPathWithoutRedist_RecommendsMicrosoftIoQualificationCheck()
    {
        const string source = """
            class Sample
            {
                bool Check(string path) => System.IO.Path.IsPathRooted(path);
            }
            """;

        ImmutableArray<MetadataReference> references =
        [
            .. RoslynTestEnvironment.Net472References.Where(reference =>
                !string.Equals(
                    Path.GetFileName(reference.Display),
                    "Microsoft.IO.Redist.dll",
                    StringComparison.OrdinalIgnoreCase))
        ];
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            new AvoidPathIsPathRootedAnalyzer(),
            source,
            metadataReferences: references).ConfigureAwait(false);

        diagnostics.Should().ContainSingle().Which.GetMessage()
            .Should().Contain("Microsoft.IO.Path.IsPathFullyQualified");
    }

    [TestMethod]
    public async Task AnalyzeInvocation_SourceDefinedMicrosoftIoPath_ReportsNothing()
    {
        const string source = """
            namespace Microsoft.IO
            {
                public static class Path
                {
                    public static bool IsPathRooted(string path) => false;
                }
            }

            class Sample
            {
                bool Check(string path) => Microsoft.IO.Path.IsPathRooted(path);
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
                bool Check(string path) => Path.IsPathRooted(path);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().BeEmpty();
    }
}