// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Offers an "Add argument name" fix for <c>TOUKI0029</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddLiteralArgumentNameCodeFixProvider))]
[Shared]
public sealed partial class AddLiteralArgumentNameCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; these are stable public contracts.
    private const string RequireNamedArgumentsForLiteralsId = "TOUKI0029";
    private const string ParameterNameProperty = "ParameterName";

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [RequireNamedArgumentsForLiteralsId];
    private static readonly FixAllProvider s_fixAllProvider = new AddArgumentNamesFixAllProvider();

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SemanticModel? semanticModel =
            await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        SourceText text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null
            || semanticModel is null
            || context.Document.FilePath is not null
                && DocumentFileUtilities.HasSharedFilePath(context.Document.Project.Solution, context.Document))
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!TryCreateTextChange(
                diagnostic,
                root,
                semanticModel,
                context.CancellationToken,
                out TextChange change,
                out string escapedParameterName))
            {
                continue;
            }

            Document changedDocument = context.Document.WithText(text.WithChanges(change));

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Add argument name '{escapedParameterName}'",
                    createChangedDocument: _ => Task.FromResult(changedDocument),
                    equivalenceKey: nameof(AddLiteralArgumentNameCodeFixProvider)),
                diagnostic);
        }
    }

    private static bool TryCreateTextChange(
        Diagnostic diagnostic,
        SyntaxNode root,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out TextChange change,
        out string escapedParameterName)
    {
        if (!TryCreateCandidate(
            diagnostic,
            root,
            semanticModel,
            cancellationToken,
            out ArgumentNameCandidate candidate)
            || !HasValidArgumentOrder(candidate.Argument, namedArguments: null))
        {
            change = default;
            escapedParameterName = string.Empty;
            return false;
        }

        change = candidate.Change;
        escapedParameterName = candidate.EscapedParameterName;
        return true;
    }

    private static bool TryCreateCandidate(
        Diagnostic diagnostic,
        SyntaxNode root,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ArgumentNameCandidate candidate)
    {
        candidate = default;
        if (diagnostic.Id != RequireNamedArgumentsForLiteralsId
            || diagnostic.Location.SourceTree != root.SyntaxTree
            || !diagnostic.Properties.TryGetValue(ParameterNameProperty, out string? parameterName)
            || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        SyntaxNode? argument = FindArgument(node, diagnostic.Location.SourceSpan);
        ExpressionSyntax? expression = GetUnnamedExpression(argument);
        if (argument is null || expression is null)
        {
            return false;
        }

        IArgumentOperation? operation = GetArgumentOperation(
            semanticModel,
            argument,
            expression,
            cancellationToken);
        if (operation?.ArgumentKind != ArgumentKind.Explicit
            || operation.Parameter is not { CanBeReferencedByName: true } parameter
            || parameter.Name != parameterName
            || !CanAddName(argument, semanticModel, operation))
        {
            return false;
        }

        string escapedParameterName = SyntaxFacts.GetKeywordKind(parameterName) == SyntaxKind.None
            ? parameterName
            : $"@{parameterName}";
        TextChange change = new(new(expression.SpanStart, 0), $"{escapedParameterName}: ");
        candidate = new(argument, change, escapedParameterName);
        return true;
    }

    private static bool CanAddName(
        SyntaxNode argument,
        SemanticModel semanticModel,
        IArgumentOperation operation)
    {
        if (IsInsideExpressionTree(operation, semanticModel.Compilation))
        {
            return false;
        }

        return argument.SyntaxTree.Options is not CSharpParseOptions parseOptions
            || parseOptions.LanguageVersion >= LanguageVersion.CSharp4;
    }

    private static bool HasValidArgumentOrder(SyntaxNode argument, ISet<int>? namedArguments)
    {
        if (argument.SyntaxTree.Options is not CSharpParseOptions parseOptions
            || parseOptions.LanguageVersion >= LanguageVersion.CSharp7_2)
        {
            return true;
        }

        return !HasFollowingPositionalArgument(argument, namedArguments);
    }

    private static bool HasFollowingPositionalArgument(SyntaxNode argument, ISet<int>? namedArguments)
    {
        return argument switch
        {
            ArgumentSyntax ordinary => ordinary.Parent is BaseArgumentListSyntax argumentList
                && argumentList.Arguments
                    .SkipWhile(candidate => !ReferenceEquals(candidate, ordinary))
                    .Skip(1)
                    .Any(candidate => candidate.NameColon is null
                        && (namedArguments is null || !namedArguments.Contains(candidate.Expression.SpanStart))),
            AttributeArgumentSyntax attribute => attribute.Parent is AttributeArgumentListSyntax argumentList
                && argumentList.Arguments
                    .SkipWhile(candidate => !ReferenceEquals(candidate, attribute))
                    .Skip(1)
                    .Any(candidate => candidate.NameColon is null
                        && candidate.NameEquals is null
                        && (namedArguments is null || !namedArguments.Contains(candidate.Expression.SpanStart))),
            _ => true
        };
    }

    private static bool IsInsideExpressionTree(IArgumentOperation operation, Compilation compilation)
    {
        INamedTypeSymbol? expression = compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");
        if (expression is null)
        {
            return false;
        }

        for (IOperation? current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IConversionOperation { Type: INamedTypeSymbol convertedType }
                && SymbolEqualityComparer.Default.Equals(convertedType.OriginalDefinition, expression))
            {
                return true;
            }
        }

        return false;
    }

    private static SyntaxNode? FindArgument(SyntaxNode node, Microsoft.CodeAnalysis.Text.TextSpan diagnosticSpan)
    {
        for (SyntaxNode? current = node; current is not null; current = current.Parent)
        {
            ExpressionSyntax? expression = GetUnnamedExpression(current);
            if (expression is not null && expression.Span == diagnosticSpan)
            {
                return current;
            }

            if (current is ArgumentListSyntax or AttributeArgumentListSyntax or BracketedArgumentListSyntax)
            {
                return null;
            }
        }

        return null;
    }

    private static ExpressionSyntax? GetUnnamedExpression(SyntaxNode? argument) => argument switch
    {
        ArgumentSyntax { NameColon: null } ordinary => ordinary.Expression,
        AttributeArgumentSyntax { NameColon: null } attribute => attribute.Expression,
        _ => null
    };

    private static IArgumentOperation? GetArgumentOperation(
        SemanticModel semanticModel,
        SyntaxNode argument,
        ExpressionSyntax expression,
        CancellationToken cancellationToken)
    {
        IOperation? operation = semanticModel.GetOperation(argument, cancellationToken)
            ?? semanticModel.GetOperation(expression, cancellationToken);

        while (operation is not (null or IArgumentOperation))
        {
            operation = operation.Parent;
        }

        return operation as IArgumentOperation;
    }

}
