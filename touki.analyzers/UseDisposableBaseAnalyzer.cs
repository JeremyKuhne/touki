// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Reports classes that directly implement <see cref="System.IDisposable"/> without deriving from
///  <c>Touki.DisposableBase</c>.
/// </summary>
/// <remarks>
///  <para>
///   The rule reports only a class that declares <see cref="System.IDisposable"/> in its own base list. A class
///   that merely inherits the interface is left alone because its base type owns the disposal implementation.
///   Structs are excluded because they cannot derive from <c>Touki.DisposableBase</c>.
///  </para>
///  <para>
///   Analysis is disabled when the compilation does not reference <c>Touki.DisposableBase</c>.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseDisposableBaseAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0012";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Derive disposable classes from DisposableBase",
        messageFormat: "Class '{0}' implements IDisposable directly and should derive from Touki.DisposableBase",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Classes that directly implement IDisposable should derive from Touki.DisposableBase for thread-safe, idempotent disposal.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId),
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            INamedTypeSymbol? disposable = compilationContext.Compilation.GetTypeByMetadataName("System.IDisposable");
            INamedTypeSymbol? disposableBase = compilationContext.Compilation.GetTypeByMetadataName("Touki.DisposableBase");
            if (disposable is null || disposableBase is null)
            {
                return;
            }

            ConcurrentDictionary<INamedTypeSymbol, TypeDeclarationSyntax> candidates =
                new(SymbolEqualityComparer.Default);
            compilationContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeTypeDeclaration(
                    syntaxContext,
                    disposable,
                    disposableBase,
                    candidates),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.RecordDeclaration);
            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (KeyValuePair<INamedTypeSymbol, TypeDeclarationSyntax> candidate in candidates)
                {
                    endContext.ReportDiagnostic(
                        Diagnostic.Create(
                            s_rule,
                            candidate.Value.Identifier.GetLocation(),
                            candidate.Key.Name));
                }
            });
        });
    }

    private static void AnalyzeTypeDeclaration(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol disposable,
        INamedTypeSymbol disposableBase,
        ConcurrentDictionary<INamedTypeSymbol, TypeDeclarationSyntax> candidates)
    {
        TypeDeclarationSyntax declaration = (TypeDeclarationSyntax)context.Node;
        if (declaration.BaseList is null
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type
            || type.TypeKind != TypeKind.Class
            || DerivesFrom(type, disposableBase))
        {
            return;
        }

        foreach (BaseTypeSyntax baseType in declaration.BaseList.Types)
        {
            ITypeSymbol? baseTypeSymbol = context.SemanticModel.GetTypeInfo(baseType.Type, context.CancellationToken).Type;
            if (SymbolEqualityComparer.Default.Equals(baseTypeSymbol, disposable))
            {
                candidates.AddOrUpdate(
                    type,
                    declaration,
                    (_, current) => IsEarlier(declaration, current) ? declaration : current);
                return;
            }
        }
    }

    private static bool IsEarlier(TypeDeclarationSyntax candidate, TypeDeclarationSyntax current)
    {
        int pathComparison = string.Compare(
            candidate.SyntaxTree.FilePath,
            current.SyntaxTree.FilePath,
            StringComparison.Ordinal);

        return pathComparison < 0
            || (pathComparison == 0 && candidate.SpanStart < current.SpanStart);
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol disposableBase)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, disposableBase))
            {
                return true;
            }
        }

        return false;
    }
}
