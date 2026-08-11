// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Touki.Analyzers;

/// <summary>
///  Offers a collision-aware file rename for <c>TOUKI0021</c>.
/// </summary>
/// <remarks>
///  <para>
///   Workspaces that support document-info changes receive a normal rename. Structural actions are not offered in
///   <c>MSBuildWorkspace</c>, used by <c>dotnet format</c>: that workspace cannot rename document info, and a
///   remove/add replacement writes explicit compile items that collide with SDK default globs.
///  </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RenameFileToMatchTypeCodeFixProvider))]
[Shared]
public sealed class RenameFileToMatchTypeCodeFixProvider : CodeFixProvider
{
    private const string FileNameMatchesTypeId = "TOUKI0021";
    private const string SuggestedFileNameProperty = "SuggestedFileName";
    private const string SuggestedDetailSeparatorProperty = "SuggestedDetailSeparator";
    private const string EquivalenceKey = nameof(RenameFileToMatchTypeCodeFixProvider);

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [FileNameMatchesTypeId];
    private static readonly FixAllProvider s_fixAllProvider = new RenameFileFixAllProvider();

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!TryGetSuggestion(diagnostic, out string suggestedFileName, out char detailSeparator)
                || !CanRename(context.Document))
            {
                continue;
            }

            string availableFileName = GetAvailableFileName(
                context.Document.Project.Solution,
                context.Document,
                suggestedFileName,
                detailSeparator);

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Rename file to '{availableFileName}'",
                    cancellationToken => RenameDocumentAsync(
                        context.Document.Project.Solution,
                        context.Document,
                        availableFileName,
                        cancellationToken),
                    EquivalenceKey),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    private static bool TryGetSuggestion(
        Diagnostic diagnostic,
        out string suggestedFileName,
        out char detailSeparator)
    {
        if (diagnostic.Properties.TryGetValue(SuggestedFileNameProperty, out string? value)
            && !string.IsNullOrWhiteSpace(value)
            && string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal)
            && diagnostic.Properties.TryGetValue(SuggestedDetailSeparatorProperty, out string? separatorValue)
            && separatorValue is { Length: 1 })
        {
            suggestedFileName = value!;
            detailSeparator = separatorValue[0];
            return true;
        }

        suggestedFileName = string.Empty;
        detailSeparator = default;
        return false;
    }

    private static bool CanRename(Document document) =>
        document.FilePath is not null
        && document.Project.Solution.Workspace.Kind != WorkspaceKind.MSBuild
        && document.Project.Solution.Workspace.CanApplyChange(ApplyChangesKind.ChangeDocumentInfo)
        && !DocumentFileUtilities.HasSharedFilePath(document.Project.Solution, document);

    private static string GetAvailableFileName(
        Solution solution,
        Document document,
        string suggestedFileName,
        char detailSeparator)
    {
        if (IsDestinationAvailable(solution, document, suggestedFileName))
        {
            return suggestedFileName;
        }

        string extension = Path.GetExtension(suggestedFileName);
        string stem = Path.GetFileNameWithoutExtension(suggestedFileName);

        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{stem}{detailSeparator}{suffix}{extension}";
            if (IsDestinationAvailable(solution, document, candidate))
            {
                return candidate;
            }
        }
    }

    private static bool IsDestinationAvailable(Solution solution, Document document, string fileName)
    {
        string targetFilePath = DocumentFileUtilities.GetTargetFilePath(document, fileName)!;
        string currentFilePath = document.FilePath!;
        return !DocumentFileUtilities.HasDocumentWithFilePath(solution, targetFilePath, document.Id)
            && DocumentFileUtilities.IsFileSystemDestinationAvailable(currentFilePath, targetFilePath);
    }

    private static Task<Solution> RenameDocumentAsync(
        Solution solution,
        Document document,
        string fileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? targetFilePath = DocumentFileUtilities.GetTargetFilePath(document, fileName);
        if (targetFilePath is null)
        {
            return Task.FromResult(solution);
        }

        Solution renamedSolution = solution
                .WithDocumentName(document.Id, fileName)
                .WithDocumentFilePath(document.Id, targetFilePath);
        return Task.FromResult(renamedSolution);
    }

    private sealed class RenameFileFixAllProvider : FixAllProvider
    {
        public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            if (fixAllContext.Solution.Workspace.Kind == WorkspaceKind.MSBuild)
            {
                return Task.FromResult<CodeAction?>(null);
            }

            CodeAction action = CodeAction.Create(
                "Rename files to match their types",
                cancellationToken => RenameAllAsync(fixAllContext, cancellationToken),
                EquivalenceKey);
            return Task.FromResult<CodeAction?>(action);
        }

        private static async Task<Solution> RenameAllAsync(
            FixAllContext context,
            CancellationToken cancellationToken)
        {
            ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            switch (context.Scope)
            {
                case FixAllScope.Document when context.Document is not null:
                    diagnostics.AddRange(await context.GetDocumentDiagnosticsAsync(context.Document).ConfigureAwait(false));
                    break;
                case FixAllScope.Project:
                    diagnostics.AddRange(await context.GetAllDiagnosticsAsync(context.Project).ConfigureAwait(false));
                    break;
                case FixAllScope.Solution:
                    foreach (Project project in context.Solution.Projects)
                    {
                        diagnostics.AddRange(await context.GetAllDiagnosticsAsync(project).ConfigureAwait(false));
                    }

                    break;
            }

            List<RenameRequest> requests = [];
            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Location.SourceTree is null
                    || !TryGetSuggestion(
                        diagnostic,
                        out string suggestedFileName,
                        out char detailSeparator))
                {
                    continue;
                }

                Document? document = context.Solution.GetDocument(diagnostic.Location.SourceTree);
                if (document?.FilePath is null)
                {
                    continue;
                }

                requests.Add(new(document.Id, document.FilePath, suggestedFileName, detailSeparator));
            }

            requests.Sort(static (left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.OriginalFilePath, right.OriginalFilePath));

            Solution solution = context.Solution;
            foreach (RenameRequest request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Document? document = solution.GetDocument(request.DocumentId);
                if (document is null || !CanRename(document))
                {
                    continue;
                }

                string availableFileName = GetAvailableFileName(
                    solution,
                    document,
                    request.SuggestedFileName,
                    request.DetailSeparator);
                solution = await RenameDocumentAsync(
                    solution,
                    document,
                    availableFileName,
                    cancellationToken).ConfigureAwait(false);
            }

            return solution;
        }
    }

    private readonly struct RenameRequest
    {
        public RenameRequest(
            DocumentId documentId,
            string originalFilePath,
            string suggestedFileName,
            char detailSeparator)
        {
            DocumentId = documentId;
            OriginalFilePath = originalFilePath;
            SuggestedFileName = suggestedFileName;
            DetailSeparator = detailSeparator;
        }

        public DocumentId DocumentId { get; }

        public string OriginalFilePath { get; }

        public string SuggestedFileName { get; }

        public char DetailSeparator { get; }
    }
}