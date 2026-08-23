// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Reports calls to <c>System.IO.Path.IsPathRooted</c> and <c>Microsoft.IO.Path.IsPathRooted</c>, which are
///  commonly mistaken for checks that a path resolves independently of a working directory.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidPathIsPathRootedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0033";

    /// <summary>
    ///  The CLR metadata name of <c>System.IO.Path</c>.
    /// </summary>
    public const string PathMetadataName = "System.IO.Path";

    /// <summary>
    ///  The CLR metadata name of the downlevel path implementation from <c>Microsoft.IO.Redist</c>.
    /// </summary>
    public const string RedistPathMetadataName = "Microsoft.IO.Path";

    private const string RedistAssemblyName = "Microsoft.IO.Redist";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Avoid Path.IsPathRooted",
        messageFormat: "Review 'Path.IsPathRooted'; use '{0}' when resolution must not depend on a working directory",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Path.IsPathRooted returns true for paths such as Windows drive-relative paths that still "
            + "depend on a working directory. Path.IsPathFullyQualified determines whether path resolution is "
            + "independent of the current or per-drive working directory.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            INamedTypeSymbol? path = start.Compilation.GetTypeByMetadataName(PathMetadataName);
            if (path is not null
                && SymbolEqualityComparer.Default.Equals(path.ContainingAssembly, start.Compilation.Assembly))
            {
                path = null;
            }

            INamedTypeSymbol? redistPath = start.Compilation.GetTypeByMetadataName(RedistPathMetadataName);
            if (redistPath is not null && !IsRedistPath(redistPath))
            {
                redistPath = null;
            }

            if (path is null && redistPath is null)
            {
                return;
            }

            bool systemHasFullyQualified = path is not null && HasPublicFullyQualified(path);
            bool redistHasFullyQualified = redistPath is not null && HasPublicFullyQualified(redistPath);

            start.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeInvocation(
                    syntaxContext,
                    path,
                    redistPath,
                    systemHasFullyQualified,
                    redistHasFullyQualified),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? path,
        INamedTypeSymbol? redistPath,
        bool systemHasFullyQualified,
        bool redistHasFullyQualified)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;
        if (GetMethodName(invocation.Expression) is not { Identifier.ValueText: "IsPathRooted" } methodName
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol { Name: "IsPathRooted", IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, path)
                && !SymbolEqualityComparer.Default.Equals(method.ContainingType, redistPath))
        {
            return;
        }

        string recommendation =
            SymbolEqualityComparer.Default.Equals(method.ContainingType, redistPath)
                || !systemHasFullyQualified && redistHasFullyQualified
                ? "Microsoft.IO.Path.IsPathFullyQualified"
                : "Path.IsPathFullyQualified";
        context.ReportDiagnostic(Diagnostic.Create(s_rule, methodName.GetLocation(), recommendation));
    }

    private static SimpleNameSyntax? GetMethodName(ExpressionSyntax expression) => expression switch
    {
        SimpleNameSyntax simpleName => simpleName,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
        _ => null
    };

    private static bool IsRedistPath(INamedTypeSymbol path)
    {
        AssemblyIdentity identity = path.ContainingAssembly.Identity;
        return identity.Name == RedistAssemblyName
            && identity.PublicKeyToken.Length == 8
            && identity.PublicKeyToken[0] == 0xcc
            && identity.PublicKeyToken[1] == 0x7b
            && identity.PublicKeyToken[2] == 0x13
            && identity.PublicKeyToken[3] == 0xff
            && identity.PublicKeyToken[4] == 0xcd
            && identity.PublicKeyToken[5] == 0x2d
            && identity.PublicKeyToken[6] == 0xdd
            && identity.PublicKeyToken[7] == 0x51;
    }

    private static bool HasPublicFullyQualified(INamedTypeSymbol path)
    {
        foreach (ISymbol member in path.GetMembers("IsPathFullyQualified"))
        {
            if (member is IMethodSymbol
                {
                    IsStatic: true,
                    DeclaredAccessibility: Accessibility.Public
                })
            {
                return true;
            }
        }

        return false;
    }
}