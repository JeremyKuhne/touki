// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Suppresses the built-in naming rule (IDE1006) for a thread-static field that
///  <see cref="ThreadStaticNamingAnalyzer"/> accepts.
/// </summary>
/// <remarks>
///  <para>
///   A naming symbol group is matched by kind, accessibility, and modifier, and <c>[ThreadStatic]</c> is an
///   attribute rather than a modifier. A thread-static field therefore matches whatever rule covers ordinary
///   statics and is reported for missing that rule's prefix. The gap is tracked by dotnet/roslyn#32955; until
///   it closes, every thread-static field needs a hand-written suppression. This suppressor replaces those.
///  </para>
///  <para>
///   Only a field that already carries the configured thread-static prefix is suppressed. A misnamed one keeps
///   its IDE1006 report alongside <c>TOUKI0040</c>, so turning <c>TOUKI0040</c> off cannot leave thread-static
///   fields with no naming enforcement at all.
///  </para>
///  <para>
///   Suppression is possible here because a diagnostic is suppressible when its <em>default</em> severity is
///   not <see cref="DiagnosticSeverity.Error"/>. IDE1006 defaults below error, so raising it to <c>error</c>
///   in <c>.editorconfig</c> does not put it out of reach.
///  </para>
///  <para>
///   The suppression itself can be turned off, which restores the unsuppressed IDE1006 reports:
///   <code>dotnet_diagnostic.TOUKISUPPRESS0001.severity = none</code>
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ThreadStaticNamingSuppressor : DiagnosticSuppressor
{
    /// <summary>
    ///  The identifier of the suppression this suppressor produces.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   A suppression id is not a rule id, but the two share a namespace: an end user turns either off with
    ///   the same <c>dotnet_diagnostic</c> key or <c>NoWarn</c> entry. The <c>SUPPRESS</c> infix keeps this
    ///   out of reach of any future <c>TOUKI####</c> rule, which is the shape dotnet/runtime uses for
    ///   <c>SYSLIBSUPPRESS0001</c>. Number suppressions from 0001 independently of the rules.
    ///  </para>
    /// </remarks>
    public const string SuppressionId = "TOUKISUPPRESS0001";

    /// <summary>
    ///  The identifier of the diagnostic that is suppressed.
    /// </summary>
    public const string SuppressedDiagnosticId = "IDE1006";

    private static readonly SuppressionDescriptor s_rule = new(
        id: SuppressionId,
        suppressedDiagnosticId: SuppressedDiagnosticId,
        justification: "Thread-static fields carry the thread-static prefix, which the built-in naming rules cannot express.");

    // Cache the supported-suppressions array so the property does not allocate a new array on every access.
    private static readonly ImmutableArray<SuppressionDescriptor> s_supportedSuppressions = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => s_supportedSuppressions;

    /// <inheritdoc/>
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        INamedTypeSymbol? threadStaticAttribute =
            context.Compilation.GetTypeByMetadataName(ThreadStaticNaming.ThreadStaticAttributeMetadataName);

        // Configuration is per tree, so it is read once per tree rather than once per diagnostic. The root is
        // not cached alongside it: GetRoot returns a stored field on a parsed tree, and a lazy tree caches the
        // parse on first call, so repeating it costs a field read.
        Dictionary<SyntaxTree, (AnalyzerConfigOptions Options, string Prefix)>? configurations = null;

        // A semantic model is only built for a field that already looks thread static, and only once per
        // tree. Suppressors run concurrently with each other, but each runs on one thread, so a plain
        // dictionary is enough.
        Dictionary<SyntaxTree, SemanticModel>? semanticModels = null;

        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Creating a suppression for another id throws, and the context is only documented to filter to
            // the supported ids.
            if (diagnostic.Id != SuppressedDiagnosticId || diagnostic.Location.SourceTree is not { } tree)
            {
                continue;
            }

            SyntaxNode root = tree.GetRoot(context.CancellationToken);

            // IDE1006 is reported on the identifier, whose innermost node is the declarator. Anything else -
            // a method, a property, a parameter - is not this suppressor's business.
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                is not VariableDeclaratorSyntax declarator)
            {
                continue;
            }

            configurations ??= [];

            if (!configurations.TryGetValue(tree, out (AnalyzerConfigOptions Options, string Prefix) configuration))
            {
                AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);
                configuration = (options, ThreadStaticNaming.GetPrefix(options));
                configurations.Add(tree, configuration);
            }

            // Check the name before doing any semantic work. A field that is not named as a thread static
            // keeps its report either way.
            if (!ThreadStaticNaming.IsConforming(declarator.Identifier.ValueText, configuration.Prefix))
            {
                continue;
            }

            semanticModels ??= [];

            if (!semanticModels.TryGetValue(tree, out SemanticModel? semanticModel))
            {
                semanticModel = context.GetSemanticModel(tree);
                semanticModels.Add(tree, semanticModel);
            }

            if (semanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not IFieldSymbol field
                || !ThreadStaticNaming.IsThreadStatic(field, threadStaticAttribute, configuration.Options))
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(s_rule, diagnostic));
        }
    }
}
