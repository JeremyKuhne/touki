// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Minimal in-memory harness that compiles a source snippet and returns the diagnostics
///  produced by a single <see cref="DiagnosticAnalyzer"/>.
/// </summary>
internal static class AnalyzerTestHarness
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> s_references = new(CreateReferences);

    /// <summary>
    ///  Runs <paramref name="analyzer"/> against <paramref name="source"/> and returns the
    ///  analyzer-produced diagnostics.
    /// </summary>
    /// <param name="options">
    ///  Optional <c>.editorconfig</c> values made visible to the analyzer.
    /// </param>
    /// <param name="fileName">
    ///  Optional path for the parsed tree. Analyzers that inspect the file name need one; the default leaves
    ///  the tree pathless, matching an in-memory compilation.
    /// </param>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        IReadOnlyDictionary<string, string>? options = null,
        string? fileName = null)
    {
        CSharpCompilation compilation = CreateCompilation(source, fileName);

        CompilationWithAnalyzers compilationWithAnalyzers =
            compilation.WithAnalyzers([analyzer], CreateAnalyzerOptions(options));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///  Runs <paramref name="analyzer"/> and <paramref name="suppressor"/> together against
    ///  <paramref name="source"/> and returns the diagnostics, including suppressed ones.
    /// </summary>
    /// <param name="severity">
    ///  Optional effective severity to configure for <paramref name="suppressedDiagnosticId"/>, standing in
    ///  for a <c>dotnet_diagnostic.&lt;id&gt;.severity</c> entry.
    /// </param>
    /// <remarks>
    ///  <para>
    ///   Suppressed diagnostics are reported rather than dropped, so a test can tell the difference between
    ///   a diagnostic that was suppressed and one that was never produced.
    ///  </para>
    /// </remarks>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsWithSuppressorAsync(
        DiagnosticAnalyzer analyzer,
        DiagnosticSuppressor suppressor,
        string source,
        IReadOnlyDictionary<string, string>? options = null,
        string? suppressedDiagnosticId = null,
        ReportDiagnostic? severity = null)
    {
        CSharpCompilation compilation = CreateCompilation(source, fileName: null);

        if (severity is { } reportDiagnostic && suppressedDiagnosticId is not null)
        {
            compilation = compilation.WithOptions(
                compilation.Options.WithSpecificDiagnosticOptions(
                    ImmutableDictionary<string, ReportDiagnostic>.Empty.Add(suppressedDiagnosticId, reportDiagnostic)));
        }

        CompilationWithAnalyzersOptions analysisOptions = new(
            options: CreateAnalyzerOptions(options),
            onAnalyzerException: null,
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true);

        CompilationWithAnalyzers compilationWithAnalyzers =
            compilation.WithAnalyzers([analyzer, suppressor], analysisOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static CSharpCompilation CreateCompilation(string source, string? fileName)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, path: fileName ?? string.Empty);

        return CSharpCompilation.Create(
            assemblyName: "Touki.Analyzers.TestCompilation",
            syntaxTrees: [syntaxTree],
            references: s_references.Value,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    private static AnalyzerOptions CreateAnalyzerOptions(IReadOnlyDictionary<string, string>? options) =>
        new(
            additionalFiles: [],
            optionsProvider: new TestAnalyzerConfigOptionsProvider(
                options is null ? TestAnalyzerConfigOptions.Empty : new TestAnalyzerConfigOptions(options)));

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        string trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        return
        [
            .. trustedAssemblies
                .Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }
}
