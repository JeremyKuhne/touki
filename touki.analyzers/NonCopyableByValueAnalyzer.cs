// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Touki.Analyzers;

/// <summary>
///  Reports copies of a <c>[NonCopyable]</c> value: passing it by value, returning it by value, assigning it from
///  an existing location, boxing it, or storing it as a field of a copyable struct. Such a type owns a resource
///  that must not be duplicated, so it should be passed by <see langword="ref"/> / <see langword="in"/> instead.
/// </summary>
/// <remarks>
///  <para>
///   <b>Move vs copy heuristic.</b> C# has no concept of a "move", so the analyzer distinguishes a copy from a
///   move structurally via <see cref="CopyAnalysis.IsCopyOfExistingLocation"/>: a value <em>read from an existing
///   storage location</em> (a local, parameter, field, or array element) is treated as a copy, while a value
///   produced fresh (a <see langword="new"/> expression, a factory/method result, or <see langword="default"/>)
///   is treated as a move and is not reported. This is an approximation; it can miss a copy whose source is an
///   unusual expression shape, and a caller can suppress a deliberate transfer with
///   <c>[SuppressMessage]</c>.
///  </para>
///  <para>
///   <b>Reported copy mechanisms:</b> by-value argument (parameter <see cref="RefKind.None"/>), by-value return
///   (method does not return by <see langword="ref"/>), simple assignment and variable initialization from a
///   location, boxing conversion to a reference type, and a <c>[NonCopyable]</c>-typed field declared in a
///   <em>copyable</em> struct (copying the outer struct shallow-copies the field).
///  </para>
///  <para>
///   <b>Constraints and limitations.</b> Like its companion <see cref="DefensiveCopyAnalyzer"/>, this is a
///   conservative source-level approximation over the bound <see cref="IOperation"/> tree, not an exact IL copy
///   accounting. Known gaps, each a deliberate false negative:
///  </para>
///  <para>
///   - <b>Not covered:</b> auto-property backing copies, <see cref="System.Nullable{T}"/> wrapping,
///   deconstruction / tuple-element copies, <c>foreach</c> over a by-value struct enumerable, and copies the
///   compiler synthesizes during lowering (async / iterator hoisting, closures) which have no source operation.
///  </para>
///  <para>
///   - <b>User-defined conversions are skipped</b> (they invoke an operator rather than copying the value),
///   so an implicit operator such as <c>BufferScope&lt;T&gt;</c>'s conversion to <see cref="System.Span{T}"/> is
///   intentionally not flagged.
///  </para>
///  <para>
///   - <b>Most paths are unreachable for a <see langword="ref"/> struct.</b> A <see langword="ref"/> struct
///   (the common <c>[NonCopyable]</c> case, e.g. a pooled buffer) cannot be boxed, stored in a non-ref-struct
///   field, or hoisted into an async state machine by language rule, so those reports never arise for it; the
///   live cases for a ref struct are by-value argument, return, assignment, and local initialization.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonCopyableByValueAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id reported by this analyzer.</summary>
    public const string DiagnosticId = "TOUKI0004";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "By-value copy of a non-copyable struct",
        messageFormat: "'{0}' is marked [NonCopyable] and is copied by value ({1})",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A type marked [NonCopyable] owns a resource that must not be duplicated. Where feasible pass or alias it by reference ('ref'/'in'/'ref readonly'); otherwise redesign so the value is not copied by value.",
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
            // Every diagnostic from this analyzer requires the [NonCopyable] attribute, so if the compilation does
            // not reference it there is nothing to analyze - register no actions and the analyzer costs ~nothing.
            // Capturing the symbol in the closure (not a static field) avoids rooting the Compilation across edits.
            if (start.Compilation.GetTypeByMetadataName(CopyAnalysis.NonCopyableAttributeMetadataName) is not { } nonCopyable)
            {
                return;
            }

            start.RegisterOperationAction(c => AnalyzeArgument(c, nonCopyable), OperationKind.Argument);
            start.RegisterOperationAction(c => AnalyzeReturn(c, nonCopyable), OperationKind.Return);
            start.RegisterOperationAction(c => AnalyzeAssignment(c, nonCopyable), OperationKind.SimpleAssignment);
            start.RegisterOperationAction(c => AnalyzeVariableDeclarator(c, nonCopyable), OperationKind.VariableDeclarator);
            start.RegisterOperationAction(c => AnalyzeConversion(c, nonCopyable), OperationKind.Conversion);
            start.RegisterSymbolAction(c => AnalyzeField(c, nonCopyable), SymbolKind.Field);
        });
    }

    private static void AnalyzeArgument(OperationAnalysisContext context, INamedTypeSymbol nonCopyable)
    {
        IArgumentOperation argument = (IArgumentOperation)context.Operation;

        // Only by-value passing copies; ref / in / out pass the original location.
        if (argument.Parameter is not { RefKind: RefKind.None } parameter)
        {
            return;
        }

        if (!CopyAnalysis.IsNonCopyable(argument.Value.Type, nonCopyable) || !CopyAnalysis.IsCopyOfExistingLocation(argument.Value))
        {
            return;
        }

        Report(context, argument.Value, argument.Value.Type!, $"as an argument to parameter '{parameter.Name}'");
    }

    private static void AnalyzeReturn(OperationAnalysisContext context, INamedTypeSymbol nonCopyable)
    {
        IReturnOperation operation = (IReturnOperation)context.Operation;
        if (operation.ReturnedValue is not { } value)
        {
            return;
        }

        // A by-ref (or ref readonly) return hands back the original location, not a copy.
        if (context.ContainingSymbol is IMethodSymbol { ReturnsByRef: true } or IMethodSymbol { ReturnsByRefReadonly: true })
        {
            return;
        }

        if (!CopyAnalysis.IsNonCopyable(value.Type, nonCopyable) || !CopyAnalysis.IsCopyOfExistingLocation(value))
        {
            return;
        }

        Report(context, value, value.Type!, "as a return value");
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context, INamedTypeSymbol nonCopyable)
    {
        ISimpleAssignmentOperation assignment = (ISimpleAssignmentOperation)context.Operation;

        // A ref assignment (x = ref y) rebinds an alias and does not copy.
        if (assignment.IsRef)
        {
            return;
        }

        if (!CopyAnalysis.IsNonCopyable(assignment.Value.Type, nonCopyable) || !CopyAnalysis.IsCopyOfExistingLocation(assignment.Value))
        {
            return;
        }

        Report(context, assignment.Value, assignment.Value.Type!, "via assignment");
    }

    private static void AnalyzeVariableDeclarator(OperationAnalysisContext context, INamedTypeSymbol nonCopyable)
    {
        IVariableDeclaratorOperation declarator = (IVariableDeclaratorOperation)context.Operation;

        // A ref local (ref var x = ref y) aliases the original and does not copy.
        if (declarator.Symbol.RefKind != RefKind.None)
        {
            return;
        }

        if (declarator.Initializer?.Value is not { } value)
        {
            return;
        }

        if (!CopyAnalysis.IsNonCopyable(value.Type, nonCopyable) || !CopyAnalysis.IsCopyOfExistingLocation(value))
        {
            return;
        }

        Report(context, value, value.Type!, $"into local '{declarator.Symbol.Name}'");
    }

    private static void AnalyzeConversion(OperationAnalysisContext context, INamedTypeSymbol nonCopyable)
    {
        IConversionOperation conversion = (IConversionOperation)context.Operation;

        // User-defined conversions call an operator; they are not a boxing copy of the value.
        if (conversion.OperatorMethod is not null)
        {
            return;
        }

        if (!CopyAnalysis.IsNonCopyable(conversion.Operand.Type, nonCopyable))
        {
            return;
        }

        // Boxing converts a value type to a reference type (object, an interface, or dynamic).
        if (conversion.Type is { IsReferenceType: true })
        {
            Report(context, conversion, conversion.Operand.Type!, "via a boxing conversion");
        }
    }

    private static void AnalyzeField(SymbolAnalysisContext context, INamedTypeSymbol nonCopyable)
    {
        IFieldSymbol field = (IFieldSymbol)context.Symbol;
        if (field.IsImplicitlyDeclared || !CopyAnalysis.IsNonCopyable(field.Type, nonCopyable))
        {
            return;
        }

        // Copying a struct shallow-copies its fields. Flag a non-copyable field of a copyable struct; if the
        // containing type is itself non-copyable, its own copies are reported elsewhere.
        INamedTypeSymbol container = field.ContainingType;
        if (container.TypeKind != TypeKind.Struct || CopyAnalysis.IsNonCopyable(container, nonCopyable))
        {
            return;
        }

        foreach (Location location in field.Locations)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                s_rule,
                location,
                field.Type.Name,
                $"field '{field.Name}'; mark the containing type [NonCopyable] or avoid storing it by value"));
        }
    }

    private static void Report(OperationAnalysisContext context, IOperation location, ITypeSymbol type, string mechanism) =>
        context.ReportDiagnostic(Diagnostic.Create(s_rule, location.Syntax.GetLocation(), type.Name, mechanism));
}
