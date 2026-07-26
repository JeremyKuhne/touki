// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

[TestClass]
public class ThreadStaticNamingSuppressorTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string? prefix = null,
        ReportDiagnostic? severity = null) =>
        (await AnalyzeWithCompilationAsync(source, prefix, severity).ConfigureAwait(false)).Diagnostics;

    private static async Task<(ImmutableArray<Diagnostic> Diagnostics, Compilation Compilation)>
        AnalyzeWithCompilationAsync(
            string source,
            string? prefix = null,
            ReportDiagnostic? severity = null)
    {
        Dictionary<string, string> options = new();

        if (prefix is not null)
        {
            options[ThreadStaticNamingAnalyzer.PrefixOption] = prefix;
        }

        return await AnalyzerTestHarness.GetDiagnosticsWithSuppressorAsync(
            new NamingRuleStubAnalyzer(),
            new ThreadStaticNamingSuppressor(),
            source,
            options.Count == 0 ? null : options,
            suppressedDiagnosticId: NamingRuleStubAnalyzer.DiagnosticId,
            severity).ConfigureAwait(false);
    }

    private const string Usings = """
        using System;

        """;

    [TestMethod]
    public async Task ReportSuppressions_ConformingThreadStaticField_SuppressesNamingDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.IsSuppressed.Should().BeTrue();
    }

    [TestMethod]
    public async Task ReportSuppressions_ConformingThreadStaticField_RecordsSuppressionId()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_value;
            }
            """;

        (ImmutableArray<Diagnostic> diagnostics, Compilation compilation) =
            await AnalyzeWithCompilationAsync(source).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;
        SuppressionInfo? suppressionInfo = diagnostic.GetSuppressionInfo(compilation);

        suppressionInfo.Should().NotBeNull();
        suppressionInfo!.ProgrammaticSuppressions.Should().ContainSingle()
            .Which.Descriptor.Id.Should().Be(ThreadStaticNamingSuppressor.SuppressionId);
    }

    [TestMethod]
    public async Task ReportSuppressions_MisnamedThreadStaticField_LeavesDiagnostic()
    {
        // TOUKI0040 owns the message for this field, but the built-in report is left in place so that
        // turning TOUKI0040 off cannot leave a thread-static field unchecked.
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int s_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.IsSuppressed.Should().BeFalse();
    }

    [TestMethod]
    public async Task ReportSuppressions_OrdinaryStaticField_LeavesDiagnostic()
    {
        // A 't_' name without the attribute is not a thread static and gets no help here.
        string source = Usings + """
            class Sample
            {
                private static int t_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.IsSuppressed.Should().BeFalse();
    }

    [TestMethod]
    public async Task ReportSuppressions_ThreadStaticOnInstanceField_LeavesDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private int t_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.IsSuppressed.Should().BeFalse();
    }

    [TestMethod]
    public async Task ReportSuppressions_ConfiguredAsError_StillSuppresses()
    {
        // A diagnostic is suppressible when its *default* severity is below error, which is what lets this
        // work in a repository that raises IDE1006 to 'error' in .editorconfig.
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await AnalyzeAsync(source, severity: ReportDiagnostic.Error).ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Should().ContainSingle().Subject;

        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.IsSuppressed.Should().BeTrue();
    }

    [TestMethod]
    public async Task ReportSuppressions_ConfiguredPrefix_SuppressesConfiguredName()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int tl_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, prefix: "tl_").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.IsSuppressed.Should().BeTrue();
    }

    [TestMethod]
    public async Task ReportSuppressions_ConfiguredPrefix_LeavesDefaultPrefixDiagnostic()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source, prefix: "tl_").ConfigureAwait(false);

        diagnostics.Should().ContainSingle()
            .Which.IsSuppressed.Should().BeFalse();
    }

    [TestMethod]
    public async Task ReportSuppressions_MixedFields_SuppressesOnlyThreadStatics()
    {
        string source = Usings + """
            class Sample
            {
                [ThreadStatic]
                private static int t_value;

                private static int s_value;

                private int _value;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(source).ConfigureAwait(false);

        diagnostics.Should().HaveCount(3);
        diagnostics.Where(diagnostic => diagnostic.IsSuppressed).Should().ContainSingle();
    }
}
