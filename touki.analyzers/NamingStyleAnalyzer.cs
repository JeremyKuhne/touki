// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Touki.Analyzers.NamingStyles;

namespace Touki.Analyzers;

/// <summary>
///  Reports symbols whose names do not follow the configured naming rules.
/// </summary>
/// <remarks>
///  <para>
///   A replacement for IDE1006 that keeps its built-in conventions in force when a project adds a rule of its
///   own, understands attributes and negated modifiers when deciding which symbols a rule covers, and does not
///   treat <c>const</c> as <c>static</c>.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamingStyleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic id reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0041";

    /// <summary>
    ///  Diagnostic property holding the suggested replacement name.
    /// </summary>
    internal const string SuggestedNameProperty = "SuggestedName";

    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        title: "Naming rule violation",
        messageFormat: "Naming rule violation: {0}",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Warning,

        // Off by default. The rules this enforces are a house style, so a project has to ask for them by
        // raising dotnet_diagnostic.TOUKI0041.severity, the same way TOUKI0002 ships hidden.
        isEnabledByDefault: false,
        description: "Names are checked against the touki_naming_rule entries in .editorconfig, plus the "
            + "built-in conventions for types, non-field members, interfaces and type parameters.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [s_rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        // Parsing the naming configuration walks every .editorconfig key, so do it once per set of options
        // rather than once per symbol. The compiler's provider hands out one instance per syntax tree, so this
        // is one parse per source file, not one per distinct configuration.
        ConcurrentDictionary<AnalyzerConfigOptions, NamingStyleRules> cache = new();

        context.RegisterSymbolAction(
            symbolContext => AnalyzeSymbol(symbolContext, cache),
            SymbolKind.Namespace,
            SymbolKind.NamedType,
            SymbolKind.Method,
            SymbolKind.Property,
            SymbolKind.Field,
            SymbolKind.Event);

        context.RegisterOperationAction(
            operationContext => AnalyzeOperation(operationContext, cache),
            OperationKind.VariableDeclarator,
            OperationKind.LocalFunction);
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        ConcurrentDictionary<AnalyzerConfigOptions, NamingStyleRules> cache)
    {
        ISymbol symbol = context.Symbol;
        Report(symbol, context.Options, cache, context.ReportDiagnostic);

        // Type parameters and parameters have no symbol action of their own, so they are visited through the
        // symbol that declares them.
        switch (symbol)
        {
            case INamedTypeSymbol namedType:
                ReportAll(namedType.TypeParameters, context, cache);
                break;
            case IMethodSymbol method:
                ReportAll(method.TypeParameters, context, cache);
                ReportAll(method.Parameters, context, cache);
                break;
            case IPropertySymbol property:
                ReportAll(property.Parameters, context, cache);
                break;
        }
    }

    private static void ReportAll<TSymbol>(
        ImmutableArray<TSymbol> symbols,
        SymbolAnalysisContext context,
        ConcurrentDictionary<AnalyzerConfigOptions, NamingStyleRules> cache)
        where TSymbol : ISymbol
    {
        foreach (TSymbol symbol in symbols)
        {
            Report(symbol, context.Options, cache, context.ReportDiagnostic);
        }
    }

    private static void AnalyzeOperation(
        OperationAnalysisContext context,
        ConcurrentDictionary<AnalyzerConfigOptions, NamingStyleRules> cache)
    {
        ISymbol? symbol = context.Operation switch
        {
            IVariableDeclaratorOperation declarator => declarator.Symbol,
            ILocalFunctionOperation localFunction => localFunction.Symbol,
            _ => null
        };

        if (symbol is not null)
        {
            Report(symbol, context.Options, cache, context.ReportDiagnostic);
        }
    }

    private static void Report(
        ISymbol symbol,
        AnalyzerOptions options,
        ConcurrentDictionary<AnalyzerConfigOptions, NamingStyleRules> cache,
        Action<Diagnostic> reportDiagnostic)
    {
        if (!IsCandidate(symbol))
        {
            return;
        }

        Location location = symbol.Locations[0];
        SyntaxTree? tree = location.SourceTree;
        if (tree is null)
        {
            return;
        }

        AnalyzerConfigOptions configOptions = options.AnalyzerConfigOptionsProvider.GetOptions(tree);
        NamingStyleRules rules = cache.GetOrAdd(configOptions, NamingStyleRules.Create);

        if (!rules.TryGetApplicableRule(symbol, out NamingRule rule)
            || rule.Severity == ReportDiagnostic.Suppress
            || rule.NamingStyle.IsNameCompliant(symbol.Name, out string? failureReason))
        {
            return;
        }

        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty;
        string? suggestedName = SuggestName(rule.NamingStyle, symbol.Name);
        if (suggestedName is not null)
        {
            properties = properties.Add(SuggestedNameProperty, suggestedName);
        }

        reportDiagnostic(TryGetSeverity(rule.Severity, out DiagnosticSeverity severity)
            ? Diagnostic.Create(
                s_rule,
                location,
                severity,
                additionalLocations: null,
                properties,
                failureReason)
            : Diagnostic.Create(s_rule, location, properties, failureReason));
    }

    /// <summary>
    ///  Returns a name that is different from <paramref name="name"/> and satisfies <paramref name="style"/>,
    ///  or <see langword="null"/> when no candidate does.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   A suggestion the rule would turn around and report again is worse than no suggestion: the code fix
    ///   would offer a rename that does not clear the diagnostic. Degenerate names such as <c>_</c> have no
    ///   compliant form under some styles, so the candidates are verified rather than assumed.
    ///  </para>
    /// </remarks>
    private static string? SuggestName(NamingStyle style, string name)
    {
        foreach (string candidate in style.MakeCompliant(name))
        {
            if (candidate.Length > 0 && candidate != name && style.IsNameCompliant(candidate, out _))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///  Returns the severity the rule pins the diagnostic to, or <see langword="false"/> when the rule leaves
    ///  it to the descriptor and to <c>dotnet_diagnostic.TOUKI0041.severity</c>.
    /// </summary>
    private static bool TryGetSeverity(ReportDiagnostic report, out DiagnosticSeverity severity)
    {
        switch (report)
        {
            case ReportDiagnostic.Hidden:
                severity = DiagnosticSeverity.Hidden;
                return true;
            case ReportDiagnostic.Info:
                severity = DiagnosticSeverity.Info;
                return true;
            case ReportDiagnostic.Warn:
                severity = DiagnosticSeverity.Warning;
                return true;
            case ReportDiagnostic.Error:
                severity = DiagnosticSeverity.Error;
                return true;
            default:
                severity = default;
                return false;
        }
    }

    /// <summary>
    ///  Filters out symbols that either have no name of their own or cannot be renamed in isolation.
    /// </summary>
    private static bool IsCandidate(ISymbol symbol)
    {
        if (symbol.IsImplicitlyDeclared || symbol.Locations.Length == 0 || symbol.Name.Length == 0)
        {
            return false;
        }

        // An override has to keep the name of the member it overrides, and an explicit implementation has to
        // keep the name of the interface member. Neither can be fixed where it is reported.
        if (symbol.IsOverride || IsExplicitInterfaceImplementation(symbol))
        {
            return false;
        }

        // An indexer's name is the synthetic "this[]", not something written in source.
        if (symbol is IPropertySymbol { IsIndexer: true })
        {
            return false;
        }

        return symbol is not IMethodSymbol method
            || method.MethodKind is MethodKind.Ordinary or MethodKind.LocalFunction;
    }

    private static bool IsExplicitInterfaceImplementation(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => !method.ExplicitInterfaceImplementations.IsEmpty,
        IPropertySymbol property => !property.ExplicitInterfaceImplementations.IsEmpty,
        IEventSymbol @event => !@event.ExplicitInterfaceImplementations.IsEmpty,
        _ => false
    };
}
