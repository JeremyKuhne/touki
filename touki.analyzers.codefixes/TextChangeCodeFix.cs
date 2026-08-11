// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

internal static class TextChangeCodeFix
{
    /// <summary>
    ///  Registers a code action that replaces the diagnostic's source span with <paramref name="replacement"/>.
    /// </summary>
    public static void Register(
        CodeFixContext context,
        Diagnostic diagnostic,
        string title,
        string replacement,
        string equivalenceKey)
    {
        TextSpan span = diagnostic.Location.SourceSpan;
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                cancellationToken => ReplaceAsync(context.Document, span, replacement, cancellationToken),
                equivalenceKey),
            diagnostic);
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        TextSpan span,
        string replacement,
        CancellationToken cancellationToken)
    {
        SourceText text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(new TextChange(span, replacement)));
    }
}