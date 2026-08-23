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
///  Reports calls to <c>System.IO.Path.Combine</c> and <c>Microsoft.IO.Path.Combine</c>, whose rooted-segment
///  behavior is easy to misuse, and recommends <c>Path.Join</c>, which joins every segment without replacing the
///  preceding path.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UsePathJoinAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0032";

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
        title: "Use Path.Join instead of Path.Combine",
        messageFormat: "Use 'Path.Join' instead of 'Path.Combine'",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Path.Combine replaces the preceding path when a later segment is rooted. Path.Join treats "
            + "every argument as a segment, which avoids unexpected path replacement across Windows, Unix, and WSL.",
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
            if (start.Compilation.GetTypeByMetadataName(PathMetadataName) is not { } path)
            {
                return;
            }

            INamedTypeSymbol? redistPath = start.Compilation.GetTypeByMetadataName(RedistPathMetadataName);
            if (redistPath is not null && !IsRedistPath(redistPath))
            {
                redistPath = null;
            }

            start.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeInvocation(syntaxContext, path, redistPath),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol path,
        INamedTypeSymbol? redistPath)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;
        if (GetMethodName(invocation.Expression) is not { Identifier.ValueText: "Combine" } methodName
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol { Name: "Combine", IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, path)
                && !SymbolEqualityComparer.Default.Equals(method.ContainingType, redistPath))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(s_rule, methodName.GetLocation()));
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
}