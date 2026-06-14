// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Touki.Analyzers;

/// <summary>
///  Reports defensive copies of structs: accessing a non-readonly instance member through a read-only location
///  (an <see langword="in"/> parameter, a <see langword="readonly"/> field, a <see langword="ref"/>
///  <see langword="readonly"/> local, etc.) silently copies the struct, runs the member against the copy, and
///  discards it.
/// </summary>
/// <remarks>
///  <para>
///   Two diagnostics are produced. <c>TOUKI0002</c> fires on any struct and defaults to
///   <see cref="DiagnosticSeverity.Hidden"/> because it is high volume. <c>TOUKI0003</c> fires only on types marked
///   <c>[NonCopyable]</c> and defaults to <see cref="DiagnosticSeverity.Warning"/>. A given copy is reported by
///   exactly one of them so the two never overlap on the same location.
///  </para>
///  <para>
///   <b>Constraints and limitations.</b> This analyzer is a deliberately conservative source-level approximation,
///   not an exact accounting of the copies the compiler emits. It runs over the bound-but-not-lowered
///   <see cref="IOperation"/> tree, which is <em>before</em> the C# compiler lowers a defensive copy into its
///   <c>ldobj</c>/<c>stloc</c>/<c>ldloca</c> IL sequence. It therefore <em>predicts</em> a copy from the language
///   rules (a read-only receiver plus a non-<see langword="readonly"/>, non-static member) rather than observing
///   the emitted copy. The consequences:
///  </para>
///  <para>
///   - <b>Invisible / synthesized copies are not seen.</b> Copies the compiler introduces during lowering with no
///   corresponding source operation - async / iterator state-machine field hoisting, captured-variable closures,
///   and compiler thunks - are unreachable here. Detecting those requires inspecting emitted IL.
///  </para>
///  <para>
///   - <b>Read-only-location detection is partial.</b> The receiver is classified by
///   <see cref="CopyAnalysis.TryGetReadOnlyReason"/>, which recognizes <see langword="in"/> /
///   <see langword="ref"/> <see langword="readonly"/> parameters, <see langword="readonly"/> fields (outside their
///   declaring constructor), <see langword="ref"/> <see langword="readonly"/> locals, and members that return by
///   <see langword="ref"/> <see langword="readonly"/>. Long receiver chains through value-returning members are not
///   fully tracked, so some real defensive copies are missed (a false negative is preferred over a false positive).
///  </para>
///  <para>
///   - <b>Concrete structs only.</b> A generic type parameter that is later substituted with a struct is not
///   analyzed here; the rule fires on concrete struct receivers.
///  </para>
///  <para>
///   When in doubt the analyzer stays silent. A complementary source-IL inspection workflow (the
///   <c>il-copy-inspection</c> agent skill) reads emitted IL to cover the synthesized-copy class this analyzer
///   cannot, and to confirm a prediction made here.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DefensiveCopyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id for a defensive copy of any struct.</summary>
    public const string DefensiveCopyId = "TOUKI0002";

    /// <summary>The diagnostic id for a defensive copy of a <c>[NonCopyable]</c> struct.</summary>
    public const string NonCopyableDefensiveCopyId = "TOUKI0003";

    private const string HelpLinkUri = "https://github.com/JeremyKuhne/touki";

    private static readonly DiagnosticDescriptor s_defensiveCopy = new(
        id: DefensiveCopyId,
        title: "Defensive copy of a struct",
        messageFormat: "A defensive copy of '{0}' is created because non-readonly member '{1}' is accessed on a read-only {2}; mutations are discarded",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: true,
        description: "Accessing a non-readonly instance member through a read-only location copies the struct, runs the member against the copy, then discards it.",
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor s_nonCopyableDefensiveCopy = new(
        id: NonCopyableDefensiveCopyId,
        title: "Defensive copy of a non-copyable struct",
        messageFormat: "'{0}' is marked [NonCopyable]; accessing non-readonly member '{1}' on a read-only {2} creates a defensive copy",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A type marked [NonCopyable] must not be copied. Accessing a non-readonly member through a read-only location creates a discarded defensive copy.",
        helpLinkUri: HelpLinkUri);

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics =
        ImmutableArray.Create(s_defensiveCopy, s_nonCopyableDefensiveCopy);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            // Resolve the marker attribute once per compilation by its fully qualified metadata name. Capturing it
            // in the closure (rather than a static/instance field) keeps the symbol scoped to this compilation, so
            // it cannot root the Compilation across edits. A null result means the attribute is not referenced, so
            // no type is [NonCopyable] and every defensive copy is reported as the generic TOUKI0002.
            INamedTypeSymbol? nonCopyable =
                start.Compilation.GetTypeByMetadataName(CopyAnalysis.NonCopyableAttributeMetadataName);
            start.RegisterOperationAction(c => AnalyzeInvocation(c, nonCopyable), OperationKind.Invocation);
            start.RegisterOperationAction(c => AnalyzePropertyReference(c, nonCopyable), OperationKind.PropertyReference);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol? nonCopyable)
    {
        IInvocationOperation invocation = (IInvocationOperation)context.Operation;
        AnalyzeAccess(context, nonCopyable, invocation.Instance, invocation.TargetMethod);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context, INamedTypeSymbol? nonCopyable)
    {
        IPropertyReferenceOperation reference = (IPropertyReferenceOperation)context.Operation;

        // A write through the setter on a read-only location is a compile error, not a defensive copy.
        if (IsAssignmentTarget(reference))
        {
            return;
        }

        AnalyzeAccess(context, nonCopyable, reference.Instance, reference.Property);
    }

    private static void AnalyzeAccess(
        OperationAnalysisContext context,
        INamedTypeSymbol? nonCopyable,
        IOperation? instance,
        ISymbol member)
    {
        if (instance is null || !CopyAnalysis.IsCopyableStruct(instance.Type))
        {
            return;
        }

        if (!CopyAnalysis.MemberForcesDefensiveCopy(member))
        {
            return;
        }

        if (!CopyAnalysis.TryGetReadOnlyReason(instance, context.ContainingSymbol, out ReadOnlyReason reason))
        {
            return;
        }

        ITypeSymbol receiverType = instance.Type!;
        bool isNonCopyable = nonCopyable is not null && CopyAnalysis.IsNonCopyable(receiverType, nonCopyable);
        DiagnosticDescriptor rule = isNonCopyable ? s_nonCopyableDefensiveCopy : s_defensiveCopy;

        context.ReportDiagnostic(Diagnostic.Create(
            rule,
            instance.Syntax.GetLocation(),
            receiverType.Name,
            member.Name,
            CopyAnalysis.Describe(reason)));
    }

    private static bool IsAssignmentTarget(IOperation operation) => operation.Parent switch
    {
        ISimpleAssignmentOperation assignment => assignment.Target == operation,
        ICompoundAssignmentOperation compound => compound.Target == operation,
        ICoalesceAssignmentOperation coalesce => coalesce.Target == operation,
        IIncrementOrDecrementOperation => true,
        _ => false,
    };
}
