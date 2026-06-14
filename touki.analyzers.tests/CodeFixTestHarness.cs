// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Minimal in-memory harness that runs an analyzer over a snippet, applies a code fix to the first matching
///  diagnostic, and returns the resulting source text.
/// </summary>
internal static class CodeFixTestHarness
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> s_references = new(CreateReferences);

    /// <summary>
    ///  Runs <paramref name="analyzer"/> against <paramref name="source"/>, applies <paramref name="codeFix"/> to
    ///  the first diagnostic with id <paramref name="diagnosticId"/>, and returns the fixed source. Returns the
    ///  original source unchanged when no such diagnostic or fix is produced.
    /// </summary>
    public static async Task<string> ApplyFixAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider codeFix,
        string source,
        string diagnosticId)
    {
        using AdhocWorkspace workspace = new();
        Project project = workspace
            .AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(s_references.Value)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Document document = project.AddDocument("Test.cs", source);

        Compilation compilation = (await document.Project.GetCompilationAsync().ConfigureAwait(false))!;
        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer]);
        ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);

        Diagnostic? target = diagnostics.FirstOrDefault(diagnostic => diagnostic.Id == diagnosticId);
        if (target is null)
        {
            return source;
        }

        List<CodeAction> actions = [];
        CodeFixContext fixContext = new(
            document,
            target,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await codeFix.RegisterCodeFixesAsync(fixContext).ConfigureAwait(false);

        if (actions.Count == 0)
        {
            return source;
        }

        ImmutableArray<CodeActionOperation> operations =
            await actions[0].GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        ApplyChangesOperation applyChanges = operations.OfType<ApplyChangesOperation>().Single();
        Document changedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
        SourceText text = await changedDocument.GetTextAsync().ConfigureAwait(false);
        return text.ToString();
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
