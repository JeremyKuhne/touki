// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Reports a thread-static field whose name does not carry the thread-static prefix, which defaults to
///  <see cref="DefaultPrefix"/>.
/// </summary>
/// <remarks>
///  <para>
///   A <c>[ThreadStatic]</c> field holds one value per thread rather than one per process. Code that reads
///   it on a thread that never wrote it sees a different slot, so the distinction belongs in the name where
///   every use site can see it, not only at the declaration where the attribute sits.
///  </para>
///  <para>
///   The built-in naming rules cannot express this. A symbol group is matched by kind, accessibility, and
///   modifier, and <c>[ThreadStatic]</c> is an attribute rather than a modifier, so a thread-static field
///   matches the ordinary <c>static</c> group and IDE1006 asks for the static prefix instead. That gap is
///   tracked by dotnet/roslyn#32955. <see cref="ThreadStaticNamingSuppressor"/> silences IDE1006 for any
///   field this rule accepts, so the two rules never ask for different names.
///  </para>
///  <para>
///   The prefix is configured per path in <c>.editorconfig</c>:
///   <code>dotnet_code_quality.TOUKI0040.thread_static_prefix = t_</code>
///  </para>
///  <para>
///   Attributes beyond <c>System.ThreadStaticAttribute</c> can be treated as marking a field thread static,
///   as a comma-separated list of type names written with or without the <c>Attribute</c> suffix. The
///   namespace is not considered:
///   <code>dotnet_code_quality.TOUKI0040.additional_thread_static_attributes = MyThreadLocal</code>
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ThreadStaticNamingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0040";

    /// <summary>
    ///  The <c>.editorconfig</c> key that overrides the prefix a thread-static field must carry.
    /// </summary>
    public const string PrefixOption = "dotnet_code_quality.TOUKI0040.thread_static_prefix";

    /// <summary>
    ///  The <c>.editorconfig</c> key that names further attributes marking a field thread static, separated
    ///  by commas.
    /// </summary>
    public const string AdditionalAttributesOption =
        "dotnet_code_quality.TOUKI0040.additional_thread_static_attributes";

    /// <summary>
    ///  The prefix required when <see cref="PrefixOption"/> is not configured.
    /// </summary>
    public const string DefaultPrefix = "t_";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Thread-static field should carry the thread-static prefix",
        messageFormat: "Thread-static field '{0}' should be named '{1}'",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A thread-static field holds one value per thread, so its name should say so at every use site rather than only at the declaration. The built-in naming rules match on modifiers and cannot see the attribute.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    // Cache the supported-diagnostics array so the property does not allocate a new array on every access.
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
            // Resolve the attribute once per compilation so each field is a symbol comparison rather than a
            // string comparison, and so a compilation without the type does no work at all.
            INamedTypeSymbol? threadStaticAttribute =
                start.Compilation.GetTypeByMetadataName(ThreadStaticNaming.ThreadStaticAttributeMetadataName);

            start.RegisterSymbolAction(
                context => AnalyzeField(context, threadStaticAttribute),
                SymbolKind.Field);
        });
    }

    private static void AnalyzeField(SymbolAnalysisContext context, INamedTypeSymbol? threadStaticAttribute)
    {
        IFieldSymbol field = (IFieldSymbol)context.Symbol;

        // A compiler-generated field, such as a property's backing field, is not the author's to name.
        if (field.IsImplicitlyDeclared || field.Locations.Length == 0)
        {
            return;
        }

        // Filter on the symbol before reading configuration, so an ordinary field costs nothing.
        if (!ThreadStaticNaming.CouldBeThreadStatic(field))
        {
            return;
        }

        Location location = field.Locations[0];

        if (location.SourceTree is not { } tree)
        {
            return;
        }

        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);

        if (!ThreadStaticNaming.IsThreadStatic(field, threadStaticAttribute, options))
        {
            return;
        }

        string prefix = ThreadStaticNaming.GetPrefix(options);

        if (ThreadStaticNaming.IsConforming(field.Name, prefix))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(s_rule, location, field.Name, ThreadStaticNaming.SuggestedName(field.Name, prefix)));
    }
}
