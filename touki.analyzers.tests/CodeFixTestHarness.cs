// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Minimal in-memory harness that runs an analyzer over a snippet, applies a code fix to the first matching
///  diagnostic, and returns the resulting source text.
/// </summary>
internal static class CodeFixTestHarness
{
    /// <summary>
    ///  Runs <paramref name="analyzer"/> against <paramref name="source"/>, applies <paramref name="codeFix"/> to
    ///  the first diagnostic with id <paramref name="diagnosticId"/>, and returns the fixed source. Returns the
    ///  original source unchanged when no such diagnostic or fix is produced.
    /// </summary>
    /// <param name="options">
    ///  Optional <c>.editorconfig</c> values made visible to the analyzer.
    /// </param>
    /// <param name="diagnosticOptions">
    ///  Optional per-diagnostic severities. A rule that ships disabled produces nothing until it is enabled
    ///  this way.
    /// </param>
    public static async Task<string> ApplyFixAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider codeFix,
        string source,
        string diagnosticId,
        IReadOnlyDictionary<string, string>? options = null,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null)
    {
        using AdhocWorkspace workspace = new();
        Project project = workspace
            .AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(RoslynTestEnvironment.References)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Document document = project.AddDocument("Test.cs", source);

        Compilation compilation = (await document.Project.GetCompilationAsync().ConfigureAwait(false))!;

        compilation = RoslynTestEnvironment.ApplyDiagnosticOptions(compilation, diagnosticOptions);
        AnalyzerOptions analyzerOptions = RoslynTestEnvironment.CreateAnalyzerOptions(options);

        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer], analyzerOptions);
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

    /// <summary>
    ///  Runs a code fix against a named multi-document project and returns all documents from the changed solution.
    /// </summary>
    public static async Task<CodeFixTestResult> ApplyFixToSolutionAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider codeFix,
        IReadOnlyList<(string Name, string FilePath, string Source)> sources,
        string diagnosticId,
        bool fixAll,
        IReadOnlyDictionary<string, string>? options = null,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        string? workspaceKind = null)
    {
        using AdhocWorkspace workspace = workspaceKind is null
            ? new AdhocWorkspace()
            : new AdhocWorkspace(MefHostServices.DefaultHost, workspaceKind);
        Project project = workspace
            .AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(RoslynTestEnvironment.References)
            .WithCompilationOptions(new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"touki-code-fix-{Guid.NewGuid():N}");

        foreach ((string name, string filePath, string source) in sources)
        {
            Document document = project.AddDocument(
                name,
                source,
                filePath: GetAbsoluteTestPath(filePath, temporaryRoot));
            project = document.Project;
        }

        Compilation compilation = (await project.GetCompilationAsync().ConfigureAwait(false))!;

        compilation = RoslynTestEnvironment.ApplyDiagnosticOptions(compilation, diagnosticOptions);
        AnalyzerOptions analyzerOptions = RoslynTestEnvironment.CreateAnalyzerOptions(options);

        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer], analyzerOptions);
        ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        Diagnostic? target = diagnostics.FirstOrDefault(diagnostic => diagnostic.Id == diagnosticId);
        if (target is null || target.Location.SourceTree is null)
        {
            return await CreateResultAsync(
                project.Solution,
                analyzer,
                analyzerOptions,
                diagnosticOptions).ConfigureAwait(false);
        }

        Document? triggerDocument = project.Solution.GetDocument(target.Location.SourceTree);
        if (triggerDocument is null)
        {
            return await CreateResultAsync(
                project.Solution,
                analyzer,
                analyzerOptions,
                diagnosticOptions).ConfigureAwait(false);
        }

        List<CodeAction> actions = [];
        CodeFixContext fixContext = new(
            triggerDocument,
            target,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await codeFix.RegisterCodeFixesAsync(fixContext).ConfigureAwait(false);

        if (actions.Count == 0 && !fixAll)
        {
            return await CreateResultAsync(
                project.Solution,
                analyzer,
                analyzerOptions,
                diagnosticOptions).ConfigureAwait(false);
        }

        CodeAction? actionToApply = actions.FirstOrDefault();
        if (fixAll)
        {
            FixAllProvider? fixAllProvider = codeFix.GetFixAllProvider();
            if (fixAllProvider is null)
            {
                return await CreateResultAsync(
                    project.Solution,
                    analyzer,
                    analyzerOptions,
                    diagnosticOptions,
                    fixAllActionOffered: false).ConfigureAwait(false);
            }

            FixAllContext fixAllContext = new(
                triggerDocument,
                codeFix,
                FixAllScope.Solution,
                actionToApply?.EquivalenceKey,
                [diagnosticId],
                new TestDiagnosticProvider(diagnostics),
                CancellationToken.None);
            CodeAction? fixAllAction = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);
            if (fixAllAction is null)
            {
                return await CreateResultAsync(
                    project.Solution,
                    analyzer,
                    analyzerOptions,
                    diagnosticOptions,
                    fixAllActionOffered: false).ConfigureAwait(false);
            }

            actionToApply = fixAllAction;
        }

        if (actionToApply is null)
        {
            return await CreateResultAsync(
                project.Solution,
                analyzer,
                analyzerOptions,
                diagnosticOptions).ConfigureAwait(false);
        }

        ImmutableArray<CodeActionOperation> operations =
            await actionToApply.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        ApplyChangesOperation? applyChanges = operations.OfType<ApplyChangesOperation>().SingleOrDefault();
        Solution changedSolution = applyChanges?.ChangedSolution ?? project.Solution;
        return await CreateResultAsync(
            changedSolution,
            analyzer,
            analyzerOptions,
            diagnosticOptions,
            fixAllActionOffered: fixAll ? true : null).ConfigureAwait(false);
    }

    private static async Task<CodeFixTestResult> CreateResultAsync(
        Solution solution,
        DiagnosticAnalyzer analyzer,
        AnalyzerOptions analyzerOptions,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions,
        bool? fixAllActionOffered = null)
    {
        ImmutableArray<CodeFixTestDocument>.Builder documents = ImmutableArray.CreateBuilder<CodeFixTestDocument>();
        ImmutableArray<Diagnostic>.Builder compilerErrors = ImmutableArray.CreateBuilder<Diagnostic>();
        ImmutableArray<Diagnostic>.Builder analyzerDiagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (Project project in solution.Projects)
        {
            foreach (Document document in project.Documents)
            {
                SourceText text = await document.GetTextAsync().ConfigureAwait(false);
                documents.Add(new(document.Name, document.FilePath, text.ToString()));
            }

            Compilation compilation = (await project.GetCompilationAsync().ConfigureAwait(false))!;
            compilation = RoslynTestEnvironment.ApplyDiagnosticOptions(compilation, diagnosticOptions);

            compilerErrors.AddRange(
                compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer], analyzerOptions);
            analyzerDiagnostics.AddRange(
                await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false));
        }

        return new(
            documents.ToImmutable(),
            compilerErrors.ToImmutable(),
            analyzerDiagnostics.ToImmutable(),
            fixAllActionOffered);
    }

    private static string GetAbsoluteTestPath(string filePath, string temporaryRoot)
    {
        if (Path.IsPathFullyQualified(filePath))
        {
            return filePath;
        }

        string relativePath = filePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace(':', '_');
        return Path.GetFullPath(Path.Combine(temporaryRoot, relativePath));
    }

    private sealed class TestDiagnosticProvider(ImmutableArray<Diagnostic> diagnostics)
        : FixAllContext.DiagnosticProvider
    {
        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            return diagnostics.Where(diagnostic => diagnostic.Location.SourceTree == syntaxTree);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken) =>
            Task.FromResult(diagnostics.Where(diagnostic => diagnostic.Location == Location.None));

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }

}

internal sealed record CodeFixTestResult(
    ImmutableArray<CodeFixTestDocument> Documents,
    ImmutableArray<Diagnostic> CompilerErrors,
    ImmutableArray<Diagnostic> AnalyzerDiagnostics,
    bool? FixAllActionOffered);

internal sealed record CodeFixTestDocument(string Name, string? FilePath, string Source);
