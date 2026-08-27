// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Minimal in-memory harness that compiles a source snippet and returns the diagnostics
///  produced by a single <see cref="DiagnosticAnalyzer"/>.
/// </summary>
internal static class AnalyzerTestHarness
{
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
    /// <param name="diagnosticOptions">
    ///  Optional per-diagnostic severities, standing in for <c>dotnet_diagnostic.&lt;id&gt;.severity</c>
    ///  entries. A rule that ships disabled produces nothing until it is enabled this way.
    /// </param>
    /// <param name="expectedCompilerDiagnosticIds">
    ///  Compiler error identifiers expected from the source. The default requires the source to compile without errors.
    /// </param>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        IReadOnlyDictionary<string, string>? options = null,
        string? fileName = null,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        IReadOnlyCollection<string>? expectedCompilerDiagnosticIds = null,
        CSharpParseOptions? parseOptions = null,
        IReadOnlyCollection<MetadataReference>? additionalReferences = null,
        IReadOnlyCollection<MetadataReference>? metadataReferences = null)
    {
        IReadOnlyCollection<MetadataReference> references =
            metadataReferences ?? RoslynTestEnvironment.GetReferences(additionalReferences);
        CSharpCompilation compilation = parseOptions is null
            ? CreateCompilation(source, fileName, references)
            : CSharpCompilation.Create(
                assemblyName: "Touki.Analyzers.TestCompilation",
                syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions, fileName ?? string.Empty)],
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        return await GetDiagnosticsAsync(
            analyzer,
            compilation,
            options,
            diagnosticOptions,
            expectedCompilerDiagnosticIds,
            optionsByFile: null).ConfigureAwait(false);
    }

    /// <summary>
    ///  Runs <paramref name="analyzer"/> against instrumented <paramref name="source"/>. The callback runs after
    ///  parsing and before analyzer execution, allowing parser activity to be excluded from instrumentation.
    /// </summary>
    /// <param name="expectedCompilerDiagnosticIds">
    ///  Compiler error identifiers expected from the source. The default requires the source to compile without errors.
    /// </param>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        DiagnosticAnalyzer analyzer,
        SourceText source,
        Action beforeAnalysis,
        IReadOnlyDictionary<string, string>? options = null,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        IReadOnlyCollection<string>? expectedCompilerDiagnosticIds = null)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Touki.Analyzers.TestCompilation",
            syntaxTrees: [syntaxTree],
            references: RoslynTestEnvironment.References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        beforeAnalysis();

        return await GetDiagnosticsAsync(
            analyzer,
            compilation,
            options,
            diagnosticOptions,
            expectedCompilerDiagnosticIds,
            optionsByFile: null).ConfigureAwait(false);
    }

    /// <summary>
    ///  Runs <paramref name="analyzer"/> against several named source files in one compilation and returns the
    ///  analyzer-produced diagnostics.
    /// </summary>
    /// <param name="expectedCompilerDiagnosticIds">
    ///  Compiler error identifiers expected from the sources. The default requires the sources to compile without errors.
    /// </param>
    /// <param name="optionsByFile">
    ///  Optional effective <c>.editorconfig</c> values keyed by syntax-tree file path.
    /// </param>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        DiagnosticAnalyzer analyzer,
        IReadOnlyList<(string Source, string FileName)> sources,
        IReadOnlyDictionary<string, string>? options = null,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        IReadOnlyCollection<string>? expectedCompilerDiagnosticIds = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? optionsByFile = null)
    {
        CSharpCompilation compilation = CreateCompilation(sources);

        return await GetDiagnosticsAsync(
            analyzer,
            compilation,
            options,
            diagnosticOptions,
            expectedCompilerDiagnosticIds,
            optionsByFile).ConfigureAwait(false);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        DiagnosticAnalyzer analyzer,
        Compilation compilation,
        IReadOnlyDictionary<string, string>? options,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions,
        IReadOnlyCollection<string>? expectedCompilerDiagnosticIds,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? optionsByFile)
    {
        compilation = RoslynTestEnvironment.ApplyDiagnosticOptions(compilation, diagnosticOptions);

        ImmutableArray<Diagnostic> compilerErrors =
            [.. compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        string[] actualIds = [.. compilerErrors.Select(diagnostic => diagnostic.Id).OrderBy(id => id)];
        string[] expectedIds = [.. (expectedCompilerDiagnosticIds ?? []).OrderBy(id => id)];
        if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Analyzer test source compiler errors did not match. Expected: [{string.Join(", ", expectedIds)}]. "
                + $"Actual:{Environment.NewLine}{string.Join(Environment.NewLine, compilerErrors)}");
        }

        CompilationWithAnalyzers compilationWithAnalyzers =
            compilation.WithAnalyzers(
                [analyzer],
                RoslynTestEnvironment.CreateAnalyzerOptions(options, optionsByFile));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static CSharpCompilation CreateCompilation(
        string source,
        string? fileName,
        IReadOnlyCollection<MetadataReference>? metadataReferences = null)
        => CreateCompilation([(source, fileName ?? string.Empty)], metadataReferences);

    private static CSharpCompilation CreateCompilation(
        IReadOnlyList<(string Source, string FileName)> sources,
        IReadOnlyCollection<MetadataReference>? metadataReferences = null)
    {
        SyntaxTree[] syntaxTrees = new SyntaxTree[sources.Count];

        for (int i = 0; i < sources.Count; i++)
        {
            syntaxTrees[i] = CSharpSyntaxTree.ParseText(sources[i].Source, path: sources[i].FileName);
        }

        return CSharpCompilation.Create(
            assemblyName: "Touki.Analyzers.TestCompilation",
            syntaxTrees: syntaxTrees,
            references: metadataReferences ?? RoslynTestEnvironment.References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }
}
