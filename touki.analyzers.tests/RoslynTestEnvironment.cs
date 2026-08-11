// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

internal static class RoslynTestEnvironment
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> s_references = new(CreateReferences);

    /// <summary>
    ///  Gets metadata references for the trusted platform assemblies of the current test process.
    /// </summary>
    public static ImmutableArray<MetadataReference> References => s_references.Value;

    /// <summary>
    ///  Creates analyzer options backed by the supplied EditorConfig values.
    /// </summary>
    public static AnalyzerOptions CreateAnalyzerOptions(IReadOnlyDictionary<string, string>? options) =>
        new(
            additionalFiles: [],
            optionsProvider: new TestAnalyzerConfigOptionsProvider(
                options is null ? TestAnalyzerConfigOptions.Empty : new TestAnalyzerConfigOptions(options)));

    /// <summary>
    ///  Applies per-diagnostic reporting options to <paramref name="compilation"/>.
    /// </summary>
    /// <returns>
    ///  The original compilation when no options are supplied; otherwise, a compilation with the options applied.
    /// </returns>
    public static Compilation ApplyDiagnosticOptions(
        Compilation compilation,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions) =>
        diagnosticOptions is null
            ? compilation
            : compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(diagnosticOptions));

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