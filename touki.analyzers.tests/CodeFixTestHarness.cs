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
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        IReadOnlyCollection<MetadataReference>? additionalReferences = null,
        CSharpParseOptions? parseOptions = null,
        IReadOnlyCollection<MetadataReference>? metadataReferences = null)
    {
        using AdhocWorkspace workspace = new();
        Project project = workspace
            .AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(
                metadataReferences ?? RoslynTestEnvironment.GetReferences(additionalReferences))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(parseOptions ?? new CSharpParseOptions(LanguageVersion.Preview));
        Document document = project.AddDocument("Test.cs", source);

        Compilation compilation = (await document.Project.GetCompilationAsync().ConfigureAwait(false))!;

        compilation = RoslynTestEnvironment.ApplyDiagnosticOptions(compilation, diagnosticOptions);
        ThrowIfCompilerErrors(compilation, "Code-fix test source");
        AnalyzerOptions analyzerOptions = RoslynTestEnvironment.CreateAnalyzerOptions(options);

        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer], analyzerOptions);
        ImmutableArray<Diagnostic> diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);

        Diagnostic? target = GetFirstDiagnostic(diagnostics, diagnosticId);
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
        Compilation changedCompilation = (await changedDocument.Project.GetCompilationAsync().ConfigureAwait(false))!;
        ThrowIfCompilerErrors(changedCompilation, "Code-fix result");
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
        string? workspaceKind = null,
        IReadOnlyCollection<MetadataReference>? additionalReferences = null,
        IReadOnlyCollection<MetadataReference>? metadataReferences = null,
        FixAllScope fixAllScope = FixAllScope.Solution,
        bool addLinkedProject = false,
        IReadOnlyList<(string Name, string FilePath, string Source)>? additionalProjectSources = null,
        CancellationToken fixAllCancellationToken = default,
        CSharpParseOptions? parseOptions = null,
        CSharpParseOptions? linkedProjectParseOptions = null,
        IReadOnlyDictionary<string, string>? linkedProjectOptions = null,
        bool referenceAdditionalProject = false,
        IReadOnlyList<(string Name, string FilePath, string Source)>? primaryProjectSources = null,
        IReadOnlyList<(string Name, string FilePath, string Source)>? linkedProjectSources = null,
        IReadOnlyCollection<AnalyzerReference>? analyzerReferences = null,
        Func<ImmutableArray<Diagnostic>, ImmutableArray<Diagnostic>>? transformDiagnostics = null,
        Func<ImmutableArray<Diagnostic>, ImmutableArray<Diagnostic>>? transformFixAllDiagnostics = null,
        Action<DocumentId>? onFixAllDocumentDiagnosticsRequested = null)
    {
        if (linkedProjectOptions is not null && !addLinkedProject)
        {
            throw new ArgumentException(
                $"{nameof(linkedProjectOptions)} requires {nameof(addLinkedProject)} to be true.",
                nameof(linkedProjectOptions));
        }

        if (linkedProjectSources is not null && !addLinkedProject)
        {
            throw new ArgumentException(
                $"{nameof(linkedProjectSources)} requires {nameof(addLinkedProject)}.",
                nameof(linkedProjectSources));
        }

        if (referenceAdditionalProject && additionalProjectSources is null)
        {
            throw new ArgumentException(
                $"{nameof(referenceAdditionalProject)} requires {nameof(additionalProjectSources)}.",
                nameof(referenceAdditionalProject));
        }

        using AdhocWorkspace workspace = workspaceKind is null
            ? new AdhocWorkspace()
            : new AdhocWorkspace(MefHostServices.DefaultHost, workspaceKind);
        IReadOnlyCollection<MetadataReference> references =
            metadataReferences ?? RoslynTestEnvironment.GetReferences(additionalReferences);
        Project project = workspace
            .AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(references)
            .WithCompilationOptions(new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));
        ProjectId projectId = project.Id;
        if (parseOptions is not null)
        {
            project = project.WithParseOptions(parseOptions);
        }

        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"touki-code-fix-{Guid.NewGuid():N}");

        foreach ((string name, string filePath, string source) in sources)
        {
            Document document = project.AddDocument(
                name,
                source,
                filePath: GetAbsoluteTestPath(filePath, temporaryRoot));
            project = document.Project;
        }

        if (primaryProjectSources is not null)
        {
            foreach ((string name, string filePath, string source) in primaryProjectSources)
            {
                Document document = project.AddDocument(
                    name,
                    source,
                    filePath: GetAbsoluteTestPath(filePath, temporaryRoot));
                project = document.Project;
            }
        }

        if (analyzerReferences is not null)
        {
            foreach (AnalyzerReference analyzerReference in analyzerReferences)
            {
                project = project.AddAnalyzerReference(analyzerReference);
            }
        }

        Solution solution = project.Solution;
        ProjectId? linkedProjectId = null;
        if (addLinkedProject)
        {
            linkedProjectId = ProjectId.CreateNewId();
            solution = solution
                .AddProject(linkedProjectId, "LinkedProject", "LinkedProject", LanguageNames.CSharp)
                .AddMetadataReferences(linkedProjectId, references)
                .WithProjectCompilationOptions(
                    linkedProjectId,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            if (linkedProjectParseOptions is not null)
            {
                solution = solution.WithProjectParseOptions(linkedProjectId, linkedProjectParseOptions);
            }

            foreach ((string name, string filePath, string source) in sources)
            {
                solution = solution.AddDocument(
                    DocumentId.CreateNewId(linkedProjectId),
                    name,
                    SourceText.From(source),
                    filePath: GetAbsoluteTestPath(filePath, temporaryRoot));
            }

            if (linkedProjectSources is not null)
            {
                foreach ((string name, string filePath, string source) in linkedProjectSources)
                {
                    solution = solution.AddDocument(
                        DocumentId.CreateNewId(linkedProjectId),
                        name,
                        SourceText.From(source),
                        filePath: GetAbsoluteTestPath(filePath, temporaryRoot));
                }
            }
        }

        if (linkedProjectOptions is not null)
        {
            solution = AddGlobalAnalyzerConfig(
                solution,
                projectId,
                options ?? new Dictionary<string, string>(),
                temporaryRoot,
                "TestProject");
            solution = AddGlobalAnalyzerConfig(
                solution,
                linkedProjectId!,
                linkedProjectOptions!,
                temporaryRoot,
                "LinkedProject");
        }

        if (additionalProjectSources is not null)
        {
            ProjectId additionalProjectId = ProjectId.CreateNewId();
            solution = solution
                .AddProject(additionalProjectId, "AdditionalProject", "AdditionalProject", LanguageNames.CSharp)
                .AddMetadataReferences(additionalProjectId, references)
                .WithProjectCompilationOptions(
                    additionalProjectId,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            foreach ((string name, string filePath, string source) in additionalProjectSources)
            {
                solution = solution.AddDocument(
                    DocumentId.CreateNewId(additionalProjectId),
                    name,
                    SourceText.From(source),
                    filePath: GetAbsoluteTestPath(filePath, temporaryRoot));
            }

            if (referenceAdditionalProject)
            {
                solution = solution.AddProjectReference(projectId, new ProjectReference(additionalProjectId));
            }
        }

        AnalyzerOptions analyzerOptions = RoslynTestEnvironment.CreateAnalyzerOptions(options);
        ImmutableArray<Diagnostic>.Builder diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();
        Dictionary<Diagnostic, DocumentId> diagnosticDocuments = [];
        foreach (Project currentProject in solution.Projects)
        {
            Compilation compilation =
                (await currentProject.GetCompilationAsync(CancellationToken.None).ConfigureAwait(false))!;
            compilation = RoslynTestEnvironment.ApplyDiagnosticOptions(compilation, diagnosticOptions);
            AnalyzerOptions currentAnalyzerOptions = currentProject.AnalyzerConfigDocuments.Any()
                ? currentProject.AnalyzerOptions
                : analyzerOptions;
            CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer], currentAnalyzerOptions);
            ImmutableArray<Diagnostic> projectDiagnostics =
                await withAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None).ConfigureAwait(false);
            foreach (Diagnostic diagnostic in projectDiagnostics)
            {
                diagnosticsBuilder.Add(diagnostic);
                if (diagnostic.Location.SourceTree is { } tree
                    && currentProject.GetDocument(tree) is { } diagnosticDocument)
                {
                    diagnosticDocuments[diagnostic] = diagnosticDocument.Id;
                }
            }
        }

        ImmutableArray<Diagnostic> analyzerDiagnostics = diagnosticsBuilder.ToImmutable();
        ImmutableArray<Diagnostic> diagnostics = analyzerDiagnostics;
        if (transformDiagnostics is not null)
        {
            diagnostics = transformDiagnostics(diagnostics);
        }

        Diagnostic? target = GetFirstDiagnostic(diagnostics, diagnosticId);
        if (target is null || target.Location.SourceTree is null)
        {
            return await CreateResultAsync(
                solution,
                analyzer,
                analyzerOptions,
                diagnosticOptions,
                initialAnalyzerDiagnosticCount: diagnostics.Length).ConfigureAwait(false);
        }

        Document? triggerDocument = GetDiagnosticDocument(solution, target, diagnosticDocuments);
        if (triggerDocument is null)
        {
            return await CreateResultAsync(
                solution,
                analyzer,
                analyzerOptions,
                diagnosticOptions,
                initialAnalyzerDiagnosticCount: diagnostics.Length).ConfigureAwait(false);
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
                solution,
                analyzer,
                analyzerOptions,
                diagnosticOptions,
                codeFixActionOffered: false,
                initialAnalyzerDiagnosticCount: diagnostics.Length).ConfigureAwait(false);
        }

        CodeAction? actionToApply = actions.FirstOrDefault();
        if (fixAll)
        {
            FixAllProvider? fixAllProvider = codeFix.GetFixAllProvider();
            if (fixAllProvider is null)
            {
                return await CreateResultAsync(
                    solution,
                    analyzer,
                    analyzerOptions,
                    diagnosticOptions,
                    fixAllActionOffered: false,
                    initialAnalyzerDiagnosticCount: diagnostics.Length).ConfigureAwait(false);
            }

            ImmutableArray<Diagnostic> fixAllDiagnostics = transformFixAllDiagnostics is null
                ? diagnostics
                : transformFixAllDiagnostics(analyzerDiagnostics);
            TestDiagnosticProvider diagnosticProvider = new(
                solution,
                fixAllDiagnostics,
                diagnosticDocuments,
                onFixAllDocumentDiagnosticsRequested);
            FixAllContext fixAllContext = fixAllScope is FixAllScope.ContainingMember or FixAllScope.ContainingType
                ? new(
                    triggerDocument,
                    target.Location.SourceSpan,
                    codeFix,
                    fixAllScope,
                    actionToApply?.EquivalenceKey,
                    [diagnosticId],
                    diagnosticProvider,
                    fixAllCancellationToken)
                : new(
                    triggerDocument,
                    codeFix,
                    fixAllScope,
                    actionToApply?.EquivalenceKey,
                    [diagnosticId],
                    diagnosticProvider,
                    fixAllCancellationToken);
            CodeAction? fixAllAction = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);
            if (fixAllAction is null)
            {
                return await CreateResultAsync(
                    solution,
                    analyzer,
                    analyzerOptions,
                    diagnosticOptions,
                    fixAllActionOffered: false,
                    initialAnalyzerDiagnosticCount: diagnostics.Length).ConfigureAwait(false);
            }

            actionToApply = fixAllAction;
        }

        if (actionToApply is null)
        {
            return await CreateResultAsync(
                solution,
                analyzer,
                analyzerOptions,
                diagnosticOptions,
                initialAnalyzerDiagnosticCount: diagnostics.Length).ConfigureAwait(false);
        }

        ImmutableArray<CodeActionOperation> operations =
            await actionToApply.GetOperationsAsync(fixAllCancellationToken).ConfigureAwait(false);
        ApplyChangesOperation? applyChanges = operations.OfType<ApplyChangesOperation>().SingleOrDefault();
        Solution changedSolution = applyChanges?.ChangedSolution ?? solution;
        return await CreateResultAsync(
            changedSolution,
            analyzer,
            analyzerOptions,
            diagnosticOptions,
            codeFixActionOffered: actions.Count > 0,
            fixAllActionOffered: fixAll ? true : null,
            initialAnalyzerDiagnosticCount: diagnostics.Length).ConfigureAwait(false);
    }

    private static async Task<CodeFixTestResult> CreateResultAsync(
        Solution solution,
        DiagnosticAnalyzer analyzer,
        AnalyzerOptions analyzerOptions,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions,
        bool? codeFixActionOffered = null,
        bool? fixAllActionOffered = null,
        int initialAnalyzerDiagnosticCount = 0)
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
            AnalyzerOptions currentAnalyzerOptions = project.AnalyzerConfigDocuments.Any()
                ? project.AnalyzerOptions
                : analyzerOptions;
            CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([analyzer], currentAnalyzerOptions);
            analyzerDiagnostics.AddRange(
                await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false));
        }

        return new(
            documents.ToImmutable(),
            compilerErrors.ToImmutable(),
            analyzerDiagnostics.ToImmutable(),
            codeFixActionOffered,
            fixAllActionOffered,
            initialAnalyzerDiagnosticCount);
    }

    private static Diagnostic? GetFirstDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId) =>
        diagnostics
            .Where(diagnostic => diagnostic.Id == diagnosticId)
            .OrderBy(diagnostic => diagnostic.Location.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .FirstOrDefault();

    private static Document? GetDiagnosticDocument(
        Solution solution,
        Diagnostic diagnostic,
        Dictionary<Diagnostic, DocumentId> diagnosticDocuments)
    {
        if (diagnosticDocuments.TryGetValue(diagnostic, out DocumentId? documentId))
        {
            return solution.GetDocument(documentId);
        }

        if (diagnostic.Location.SourceTree is not { } tree)
        {
            return null;
        }

        foreach (Project project in solution.Projects)
        {
            if (project.GetDocument(tree) is { } document)
            {
                return document;
            }
        }

        return null;
    }

    private static void ThrowIfCompilerErrors(Compilation compilation, string context)
    {
        ImmutableArray<Diagnostic> compilerErrors =
            [.. compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        if (!compilerErrors.IsEmpty)
        {
            throw new InvalidOperationException(
                $"{context} contains compiler errors:{Environment.NewLine}{string.Join(Environment.NewLine, compilerErrors)}");
        }
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

    private static Solution AddGlobalAnalyzerConfig(
        Solution solution,
        ProjectId projectId,
        IReadOnlyDictionary<string, string> options,
        string temporaryRoot,
        string projectName)
    {
        List<string> lines = ["is_global = true"];
        foreach (KeyValuePair<string, string> option in options.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            lines.Add($"{option.Key} = {option.Value}");
        }

        string source = $"{string.Join("\n", lines)}\n";
        return solution.AddAnalyzerConfigDocument(
            DocumentId.CreateNewId(projectId),
            $"{projectName}.globalconfig",
            SourceText.From(source),
            filePath: Path.Combine(temporaryRoot, $"{projectName}.globalconfig"));
    }

    private sealed class TestDiagnosticProvider(
        Solution solution,
        ImmutableArray<Diagnostic> diagnostics,
        Dictionary<Diagnostic, DocumentId> diagnosticDocuments,
        Action<DocumentId>? onDocumentDiagnosticsRequested)
        : FixAllContext.DiagnosticProvider
    {
        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            onDocumentDiagnosticsRequested?.Invoke(document.Id);
            SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            return diagnostics.Where(diagnostic =>
                diagnosticDocuments.TryGetValue(diagnostic, out DocumentId? documentId)
                    ? documentId == document.Id
                    : diagnostic.Location.SourceTree == syntaxTree);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken) =>
            Task.FromResult(diagnostics.Where(diagnostic =>
                diagnostic.Location == Location.None && solution.ProjectIds.Count == 1));

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken) =>
            Task.FromResult(diagnostics.Where(diagnostic =>
                diagnosticDocuments.TryGetValue(diagnostic, out DocumentId? documentId)
                    ? solution.GetDocument(documentId)?.Project.Id == project.Id
                    : diagnostic.Location.SourceTree is { } tree
                        ? project.GetDocument(tree) is not null
                        : solution.ProjectIds.Count == 1));
    }

}

internal sealed record CodeFixTestResult(
    ImmutableArray<CodeFixTestDocument> Documents,
    ImmutableArray<Diagnostic> CompilerErrors,
    ImmutableArray<Diagnostic> AnalyzerDiagnostics,
    bool? CodeFixActionOffered,
    bool? FixAllActionOffered,
    int InitialAnalyzerDiagnosticCount);

internal sealed record CodeFixTestDocument(string Name, string? FilePath, string Source);
