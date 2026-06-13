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
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(DiagnosticAnalyzer analyzer, string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Touki.Analyzers.TestCompilation",
            syntaxTrees: [syntaxTree],
            references: s_references.Value,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers([analyzer]);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

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
