// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Touki.Analyzers;

/// <summary>
///  Reports a freshly produced value of a <c>[MustDispose]</c> type that is bound to a local and then not
///  deterministically disposed - neither consumed by a <see langword="using"/>, disposed explicitly, nor handed
///  off to another owner. Such a value owns a resource that must be released on every path, so it should be
///  declared with <see langword="using"/> or disposed in a <see langword="try"/>/<see langword="finally"/>.
/// </summary>
/// <remarks>
///  <para>
///   <b>Ownership model.</b> Like the companion <see cref="NonCopyableByValueAnalyzer"/>, the obligation attaches
///   only to a <em>freshly produced</em> value (a <see langword="new"/> expression, a factory/method result, or
///   <see langword="default"/>) bound to a local, reusing <see cref="CopyAnalysis.IsCopyOfExistingLocation"/> to
///   tell a fresh owner from an alias of an existing one. A value read from an existing location is an alias and
///   carries no new obligation; the original owner is what must be disposed.
///  </para>
///  <para>
///   <b>What discharges the obligation.</b> A local is considered satisfied if anywhere in the same member it is:
///   the subject of a <see langword="using"/> declaration or statement; the receiver of a <c>Dispose</c> /
///   <c>DisposeAsync</c> call (including <c>?.Dispose()</c>); returned (directly or through a ternary, switch
///   expression, or null-coalesce); assigned to a field, property, or out/ref parameter; aliased into another
///   local; or passed as a by-value / <see langword="in"/> / <see langword="ref"/> argument. Passing as one of
///   those argument kinds is treated as an ownership transfer - a deliberate conservative false-negative, since
///   this pass cannot tell whether the callee takes ownership.
///  </para>
///  <para>
///   <b>Receiving by <see langword="out"/>.</b> An <see langword="out"/> argument is the mirror image: the callee
///   produces the value and the <em>caller</em> receives ownership, so the receiving local (<c>Foo(out var s)</c>
///   / <c>Foo(out Scope s)</c> / a pre-declared <c>Foo(out s)</c>) is treated as a fresh obligation that must
///   itself be disposed. Returning a value or writing it through an <see langword="out"/> / <see langword="ref"/>
///   parameter therefore satisfies the producer while creating the obligation on whoever receives it.
///  </para>
///  <para>
///   <b>Use is not disposal.</b> A user-defined implicit conversion (e.g. a scope's conversion to
///   <see cref="System.Span{T}"/> or to its wrapped value) is a <em>use</em>, not a transfer, so a value consumed
///   only through such a conversion is still reported. This is what catches the classic "implicit conversion in a
///   <see langword="using"/> leaks the scope" mistake.
///  </para>
///  <para>
///   <b>Conservative first pass.</b> This is a presence-based approximation over the bound
///   <see cref="IOperation"/> tree, not control-flow dominance: a single <c>Dispose</c> on any path silences the
///   diagnostic even if other paths leak. Known, deliberate false negatives: a value assigned to a local that is
///   declared without an initializer and only later set; a value reached only by a return-value-attributed
///   factory (the attribute is recognized on <em>types</em>, not return values, in this pass); and copies the
///   compiler synthesizes during lowering. The cardinal rule is that a missed leak is preferable to a false
///   warning that trains users to disable the rule.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MustDisposeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id reported by this analyzer.</summary>
    public const string DiagnosticId = "TOUKI0010";

    /// <summary>
    ///  The canonical CLR metadata name of the attribute that marks a type as must-dispose. Resolved once per
    ///  compilation and compared against candidate types' attributes by identity.
    /// </summary>
    public const string MustDisposeAttributeMetadataName = "Touki.MustDisposeAttribute";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Dispose a [MustDispose] value deterministically",
        messageFormat: "'{0}' is marked [MustDispose] but is not deterministically disposed; declare it with 'using' or dispose it in a 'finally'",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A type marked [MustDispose] owns a resource that must be released on every path. Consume the value with a 'using' declaration or statement, or dispose it in a 'try'/'finally'.",
        helpLinkUri: "https://github.com/JeremyKuhne/touki");

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
            // Every diagnostic requires the [MustDispose] attribute, so if the compilation does not reference it
            // there is nothing to analyze - register no actions and the analyzer costs ~nothing. Capturing the
            // symbol in the closure (not a static field) avoids rooting the Compilation across edits.
            if (start.Compilation.GetTypeByMetadataName(MustDisposeAttributeMetadataName) is not { } mustDispose)
            {
                return;
            }

            // Disposal is a member-global property, so analyze a whole operation block at once rather than a single
            // operation kind.
            start.RegisterOperationBlockAction(c => AnalyzeOperationBlock(c, mustDispose));
        });
    }

    private static void AnalyzeOperationBlock(OperationBlockAnalysisContext context, INamedTypeSymbol mustDispose)
    {
        // Owned, freshly produced [MustDispose] locals keyed by symbol -> the operation to report on (a declarator
        // or the receiving reference of an out argument).
        Dictionary<ISymbol, IOperation> owned = new(SymbolEqualityComparer.Default);

        // Locals whose disposal obligation is discharged (disposed, returned, stored, aliased, or passed on).
        HashSet<ISymbol> discharged = new(SymbolEqualityComparer.Default);

        foreach (IOperation root in context.OperationBlocks)
        {
            foreach (IOperation operation in Descend(root))
            {
                switch (operation)
                {
                    case IVariableDeclaratorOperation declarator:
                        AnalyzeDeclarator(declarator, mustDispose, owned, discharged);
                        break;
                    case IInvocationOperation invocation:
                        MarkDispose(invocation, discharged);
                        break;
                    case IUsingOperation usingOperation:
                        MarkTransfer(usingOperation.Resources, discharged);
                        break;
                    case IReturnOperation { ReturnedValue: { } returned }:
                        MarkTransfer(returned, discharged);
                        break;
                    case ISimpleAssignmentOperation assignment:
                        // Storing into a field/property (escape), aliasing into another local, or writing through
                        // an out/ref parameter (transfer back to the caller) all hand the obligation elsewhere.
                        if (assignment.Target is IFieldReferenceOperation or IPropertyReferenceOperation
                            or ILocalReferenceOperation or IParameterReferenceOperation)
                        {
                            MarkTransfer(assignment.Value, discharged);
                        }

                        break;
                    case IArgumentOperation argument:
                        AnalyzeArgument(argument, mustDispose, owned, discharged);
                        break;
                }
            }
        }

        foreach (KeyValuePair<ISymbol, IOperation> candidate in owned)
        {
            if (discharged.Contains(candidate.Key))
            {
                continue;
            }

            Location location = candidate.Key.Locations.Length > 0
                ? candidate.Key.Locations[0]
                : candidate.Value.Syntax.GetLocation();

            context.ReportDiagnostic(Diagnostic.Create(s_rule, location, candidate.Key.Name));
        }
    }

    private static void AnalyzeDeclarator(
        IVariableDeclaratorOperation declarator,
        INamedTypeSymbol mustDispose,
        Dictionary<ISymbol, IOperation> owned,
        HashSet<ISymbol> discharged)
    {
        if (declarator.Initializer?.Value is not { } initializer)
        {
            return;
        }

        // 'Scope y = x;' (or 'ref Scope y = ref x;') aliases an existing owner: record the source as transferred
        // and do not treat the new local as a fresh obligation.
        if (CopyAnalysis.IsCopyOfExistingLocation(initializer))
        {
            MarkTransfer(initializer, discharged);
            return;
        }

        // A ref local aliases another location rather than owning a value.
        if (declarator.Symbol.RefKind != RefKind.None)
        {
            return;
        }

        // A 'using' declaration (or 'using (T x = ...)') already guarantees disposal.
        if (IsUsingDeclared(declarator))
        {
            return;
        }

        if (!IsMustDispose(declarator.Symbol.Type, mustDispose))
        {
            return;
        }

        owned[declarator.Symbol] = declarator;
    }

    private static void AnalyzeArgument(
        IArgumentOperation argument,
        INamedTypeSymbol mustDispose,
        Dictionary<ISymbol, IOperation> owned,
        HashSet<ISymbol> discharged)
    {
        // An out argument hands a freshly produced value back to the caller, so the receiving local becomes a new
        // disposal obligation rather than a transfer away. Every other argument kind (by value, 'in', 'ref') is
        // treated as an ownership transfer - a deliberate conservative assumption, since this pass cannot tell
        // whether the callee takes ownership.
        if (argument.Parameter is { RefKind: RefKind.Out })
        {
            // 'Foo(out var s)' / 'Foo(out Scope s)' wrap the receiver in a declaration expression; a pre-declared
            // 'Foo(out s)' passes the local reference directly.
            ILocalReferenceOperation? receiver = argument.Value switch
            {
                IDeclarationExpressionOperation { Expression: ILocalReferenceOperation declared } => declared,
                ILocalReferenceOperation local => local,
                _ => null,
            };

            if (receiver is not null && IsMustDispose(receiver.Local.Type, mustDispose))
            {
                owned[receiver.Local] = receiver;
            }

            return;
        }

        MarkTransfer(argument.Value, discharged);
    }

    private static void MarkDispose(IInvocationOperation invocation, HashSet<ISymbol> discharged)
    {
        if (invocation.TargetMethod.Name is not ("Dispose" or "DisposeAsync") || invocation.Instance is not { } instance)
        {
            return;
        }

        // 'x.Dispose()'.
        MarkTransfer(instance, discharged);

        // 'x?.Dispose()': the invocation instance is the conditional-access placeholder, so resolve the real
        // receiver from the enclosing conditional access.
        if (instance is IInstanceReferenceOperation or IConditionalAccessInstanceOperation)
        {
            for (IOperation? parent = invocation.Parent; parent is not null; parent = parent.Parent)
            {
                if (parent is IConditionalAccessOperation conditional)
                {
                    MarkTransfer(conditional.Operation, discharged);
                    break;
                }
            }
        }
    }

    /// <summary>
    ///  Records the local referenced by <paramref name="value"/> as discharged, following only the shapes that
    ///  genuinely hand off the same value: implicit (non-user) conversions, parentheses, and the branches of a
    ///  ternary, switch expression, or null-coalesce. A user-defined conversion is intentionally <em>not</em>
    ///  followed - it is a use of the value, not a transfer of ownership.
    /// </summary>
    private static void MarkTransfer(IOperation? value, HashSet<ISymbol> discharged)
    {
        switch (Unwrap(value))
        {
            case ILocalReferenceOperation local:
                discharged.Add(local.Local);
                break;
            case IConditionalOperation conditional:
                MarkTransfer(conditional.WhenTrue, discharged);
                MarkTransfer(conditional.WhenFalse, discharged);
                break;
            case ISwitchExpressionOperation switchExpression:
                foreach (ISwitchExpressionArmOperation arm in switchExpression.Arms)
                {
                    MarkTransfer(arm.Value, discharged);
                }

                break;
            case ICoalesceOperation coalesce:
                MarkTransfer(coalesce.Value, discharged);
                MarkTransfer(coalesce.WhenNull, discharged);
                break;
        }
    }

    private static IOperation? Unwrap(IOperation? operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion:
                    operation = conversion.Operand;
                    break;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    break;
                default:
                    return operation;
            }
        }
    }

    private static bool IsUsingDeclared(IVariableDeclaratorOperation declarator)
    {
        IOperation? parent = declarator.Parent;
        while (parent is IVariableDeclarationOperation or IVariableDeclarationGroupOperation)
        {
            parent = parent.Parent;
        }

        return parent is IUsingDeclarationOperation or IUsingOperation;
    }

    private static bool IsMustDispose(ITypeSymbol? type, INamedTypeSymbol mustDispose)
    {
        if (type is null)
        {
            return false;
        }

        // GetAttributes() returns the already-materialized attribute list; comparing AttributeClass by symbol
        // identity is allocation-free, unlike building and comparing ToDisplayString().
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, mustDispose))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<IOperation> Descend(IOperation root)
    {
        Stack<IOperation> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            IOperation operation = stack.Pop();
            yield return operation;
            foreach (IOperation child in operation.ChildOperations)
            {
                stack.Push(child);
            }
        }
    }
}
