// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Touki.Analyzers;

/// <summary>
///  Shared, language-agnostic helpers for reasoning about struct value copies over <see cref="IOperation"/> trees.
/// </summary>
/// <remarks>
///  <para>
///   These helpers encode the <em>conservative approximation</em> that both <see cref="DefensiveCopyAnalyzer"/>
///   and <see cref="NonCopyableByValueAnalyzer"/> share. The fundamental constraint is timing: an analyzer runs on
///   the bound-but-not-lowered operation tree, whereas the compiler emits the actual struct copies (the
///   <c>ldobj</c> / <c>stloc</c> / <c>ldloca</c> defensive-copy sequence, <c>box</c>, <c>cpobj</c>) only during
///   later lowering. There is no public Roslyn signal for "a copy was emitted here", so these helpers reconstruct
///   the copy from C# language rules instead of observing it.
///  </para>
///  <para>
///   The practical rule throughout: when a construct cannot be classified with confidence, return the
///   non-reporting answer. A missed copy (false negative) is preferable to a spurious warning (false positive)
///   that trains users to disable the rule. The synthesized copies that never appear in the operation tree
///   (async / iterator hoisting, closures, thunks) are out of scope by construction and are the domain of a
///   complementary IL-inspection pass rather than these helpers.
///  </para>
/// </remarks>
internal static class CopyAnalysis
{
    /// <summary>
    ///  The canonical CLR metadata name of the attribute that marks a value type as non-copyable. Resolve it once
    ///  per compilation with <see cref="Compilation.GetTypeByMetadataName(string)"/> and compare candidate types'
    ///  attributes against the resulting symbol by identity.
    /// </summary>
    public const string NonCopyableAttributeMetadataName = "Touki.NonCopyableAttribute";

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="type"/> is a value type annotated with the
    ///  <paramref name="nonCopyableAttribute"/> symbol (matched by identity, i.e. the fully qualified
    ///  <c>Touki.NonCopyableAttribute</c>).
    /// </summary>
    public static bool IsNonCopyable(ITypeSymbol? type, INamedTypeSymbol nonCopyableAttribute)
    {
        if (type is not { IsValueType: true })
        {
            return false;
        }

        // GetAttributes() returns the symbol's already-materialized attribute list; iterating it and comparing the
        // AttributeClass by symbol identity is allocation-free, unlike building and comparing ToDisplayString().
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, nonCopyableAttribute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Returns <see langword="true"/> for a struct type where accessing a non-readonly instance member can force a
    ///  defensive copy. <see langword="readonly"/> structs never need a defensive copy, and non-structs (enums,
    ///  type parameters, classes) are excluded.
    /// </summary>
    public static bool IsCopyableStruct(ITypeSymbol? type) =>
        type is { IsValueType: true, IsReadOnly: false, TypeKind: TypeKind.Struct };

    /// <summary>
    ///  Returns <see langword="true"/> if accessing <paramref name="member"/> on a read-only receiver forces a
    ///  defensive copy, i.e. the member is a non-static instance member that is not <see langword="readonly"/>.
    /// </summary>
    public static bool MemberForcesDefensiveCopy(ISymbol member)
    {
        if (member.IsStatic)
        {
            return false;
        }

        return member switch
        {
            IMethodSymbol method => !method.IsReadOnly,
            IPropertySymbol property => property.GetMethod is { IsReadOnly: false },
            _ => false,
        };
    }

    /// <summary>
    ///  Determines whether <paramref name="receiver"/> is a read-only location, returning the reason if so.
    /// </summary>
    public static bool TryGetReadOnlyReason(IOperation? receiver, ISymbol containingSymbol, out ReadOnlyReason reason)
    {
        reason = default;
        switch (receiver)
        {
            case IParameterReferenceOperation parameter:
                switch (parameter.Parameter.RefKind)
                {
                    case RefKind.In:
                        reason = ReadOnlyReason.InParameter;
                        return true;
                    case RefKind.RefReadOnlyParameter:
                        reason = ReadOnlyReason.RefReadOnlyParameter;
                        return true;
                    default:
                        return false;
                }

            case IFieldReferenceOperation field:
                if (field.Field.IsReadOnly && !IsInsideInitializingConstructor(field.Field, containingSymbol))
                {
                    reason = field.Field.IsStatic ? ReadOnlyReason.StaticReadOnlyField : ReadOnlyReason.ReadOnlyField;
                    return true;
                }

                // A non-readonly field is itself read-only only when reached through a read-only location (e.g. a
                // field of an 'in' parameter). This recursion handles one level of receiver chain; it does not
                // model a value-returning property in the middle of the chain (that return is its own copy), which
                // is one of the documented partial-coverage cases - a missed copy rather than a false positive.
                return field.Instance is not null && TryGetReadOnlyReason(field.Instance, containingSymbol, out reason);

            case ILocalReferenceOperation local when local.Local.RefKind == RefKind.RefReadOnly:
                reason = ReadOnlyReason.RefReadOnlyLocal;
                return true;

            case IInvocationOperation invocation when invocation.TargetMethod.ReturnsByRefReadonly:
                reason = ReadOnlyReason.RefReadOnlyReturn;
                return true;

            case IPropertyReferenceOperation property when property.Property.ReturnsByRefReadonly:
                reason = ReadOnlyReason.RefReadOnlyReturn;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    ///  Describes a <see cref="ReadOnlyReason"/> for a diagnostic message.
    /// </summary>
    public static string Describe(ReadOnlyReason reason) => reason switch
    {
        ReadOnlyReason.InParameter => "'in' parameter",
        ReadOnlyReason.RefReadOnlyParameter => "'ref readonly' parameter",
        ReadOnlyReason.ReadOnlyField => "readonly field",
        ReadOnlyReason.StaticReadOnlyField => "static readonly field",
        ReadOnlyReason.RefReadOnlyLocal => "'ref readonly' local",
        ReadOnlyReason.RefReadOnlyReturn => "'ref readonly' return value",
        _ => "read-only location",
    };

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="value"/> reads an existing storage location, so using it
    ///  by value duplicates that location (a copy). A value produced fresh (a <see langword="new"/> expression, a
    ///  factory call, <see langword="default"/>) is treated as a move and returns <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is the move-vs-copy heuristic. C# has no move semantics, so "is this a copy?" is decided by the shape
    ///   of the source expression: a reference to a local, parameter, field, or array element names a storage
    ///   location that survives the operation, hence reading it by value duplicates it. Anything else - a
    ///   constructor or factory result, <see langword="default"/>, a value already on the evaluation stack - is a
    ///   freshly produced value with no other owner, so consuming it is a move, not a copy. The list of "location"
    ///   operations is intentionally narrow; an unrecognized shape is treated as a move (false negative) rather than
    ///   risking a false positive on a deliberate transfer.
    ///  </para>
    /// </remarks>
    public static bool IsCopyOfExistingLocation(IOperation value) => Unwrap(value) switch
    {
        ILocalReferenceOperation => true,
        IParameterReferenceOperation => true,
        IFieldReferenceOperation => true,
        IArrayElementReferenceOperation => true,
        _ => false,
    };

    private static IOperation Unwrap(IOperation operation)
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

    private static bool IsInsideInitializingConstructor(IFieldSymbol field, ISymbol containingSymbol)
    {
        if (containingSymbol is not IMethodSymbol method)
        {
            return false;
        }

        bool isMatchingConstructor = field.IsStatic
            ? method.MethodKind == MethodKind.StaticConstructor
            : method.MethodKind == MethodKind.Constructor;

        return isMatchingConstructor
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, field.ContainingType);
    }
}
