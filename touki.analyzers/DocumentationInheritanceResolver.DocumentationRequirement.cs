// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Touki.Analyzers;

internal static partial class DocumentationInheritanceResolver
{
    /// <summary>
    ///  Matches inherited signature documentation only when it describes the corresponding member contract.
    /// </summary>
    private readonly struct DocumentationRequirement
    {
        private readonly DocumentationElement _element;
        private readonly int _parameterOrdinal;
        private readonly string? _parameterName;
        private readonly ITypeSymbol? _type;
        private readonly RefKind _refKind;
        private readonly bool _isParams;
        private readonly bool _sourceHasExtensionReceiver;
        private readonly bool _returnsByRef;
        private readonly bool _returnsByRefReadonly;

        public DocumentationRequirement(ISymbol source, IParameterSymbol parameter, string parameterName)
        {
            _element = DocumentationElement.Parameter;
            _parameterOrdinal = parameter.Ordinal;
            _parameterName = parameterName;
            _type = parameter.Type;
            _refKind = parameter.RefKind;
            _isParams = parameter.IsParams;
            _sourceHasExtensionReceiver = source is IMethodSymbol { IsExtensionMethod: true }
                or INamedTypeSymbol { IsExtension: true, ExtensionParameter: not null };
        }

        public DocumentationRequirement(IMethodSymbol method)
        {
            _element = DocumentationElement.Returns;
            _type = method.ReturnType;
            _returnsByRef = method.ReturnsByRef;
            _returnsByRefReadonly = method.ReturnsByRefReadonly;
        }

        public bool IsSignatureRequirement => _element is
            DocumentationElement.Parameter or DocumentationElement.Returns;

        public bool Matches(ISymbol symbol, CancellationToken cancellationToken) => _element switch
        {
            DocumentationElement.Parameter => MatchesParameter(symbol, cancellationToken),
            DocumentationElement.Returns => MatchesReturn(symbol),
            _ => true
        };

        public bool HasDocumentation(XmlDocumentationInfo documentation) => _element switch
        {
            DocumentationElement.Parameter => _parameterName is { } name && documentation.HasParameter(name),
            DocumentationElement.Returns => documentation.HasReturns,
            _ => documentation.SummaryCount > 0
        };

        public bool HasDocumentation(MetadataDocumentationInfo documentation) => _element switch
        {
            DocumentationElement.Parameter =>
                _parameterName is { } name && documentation.ParameterNames?.Contains(name) == true,
            DocumentationElement.Returns => documentation.HasReturns,
            _ => documentation.HasSummary
        };

        private bool MatchesParameter(ISymbol symbol, CancellationToken cancellationToken)
        {
            int ordinal = _parameterOrdinal;
            if (_sourceHasExtensionReceiver && symbol is IMethodSymbol { IsStatic: false })
            {
                ordinal--;
            }

            IParameterSymbol? target = GetParameter(symbol, ordinal, cancellationToken);
            return target is not null
                && string.Equals(target.Name, _parameterName, StringComparison.Ordinal)
                && HasMatchingType(_type, target.Type, symbol)
                && target.RefKind == _refKind
                && target.IsParams == _isParams;
        }

        private bool MatchesReturn(ISymbol symbol)
        {
            IMethodSymbol? method = symbol switch
            {
                IMethodSymbol target => target,
                INamedTypeSymbol { TypeKind: TypeKind.Delegate } type => type.DelegateInvokeMethod,
                _ => null
            };

            if (method is null)
            {
                return false;
            }

            return !method.ReturnsVoid
                && HasMatchingType(_type, method.ReturnType, method)
                && method.ReturnsByRef == _returnsByRef
                && method.ReturnsByRefReadonly == _returnsByRefReadonly;
        }

        private static bool HasMatchingType(ITypeSymbol? source, ITypeSymbol target, ISymbol targetMember)
        {
            if (source is null)
            {
                return false;
            }

            if (SymbolEqualityComparer.IncludeNullability.Equals(source, target))
            {
                return true;
            }

            // Cref placeholders omit the nonnullable annotation of constrained type parameters.
            if (source is ITypeParameterSymbol nonNullableSource
                && target is ITypeParameterSymbol crefTarget
                && nonNullableSource.NullableAnnotation == NullableAnnotation.NotAnnotated
                && crefTarget.NullableAnnotation == NullableAnnotation.None
                && crefTarget.TypeParameterKind == TypeParameterKind.Cref
                && CrefParametersMatch(nonNullableSource, crefTarget, targetMember))
            {
                return true;
            }

            if (source.NullableAnnotation != target.NullableAnnotation)
            {
                return false;
            }

            if (source is ITypeParameterSymbol sourceParameter
                && target is ITypeParameterSymbol targetParameter)
            {
                if (sourceParameter.TypeParameterKind != targetParameter.TypeParameterKind
                    || sourceParameter.Ordinal != targetParameter.Ordinal)
                {
                    return targetParameter.TypeParameterKind == TypeParameterKind.Cref
                        && CrefParametersMatch(sourceParameter, targetParameter, targetMember);
                }

                if (sourceParameter.ContainingSymbol is IMethodSymbol sourceMethod)
                {
                    return targetParameter.ContainingSymbol is IMethodSymbol targetMethod
                        && sourceMethod.Arity == targetMethod.Arity;
                }

                return sourceParameter.ContainingSymbol is INamedTypeSymbol sourceOwner
                    && targetParameter.ContainingSymbol is INamedTypeSymbol targetOwner
                    && sourceOwner.Arity == targetOwner.Arity
                    && GetTypeNestingDepth(sourceOwner) == GetTypeNestingDepth(targetOwner);
            }

            if (source is IArrayTypeSymbol sourceArray && target is IArrayTypeSymbol targetArray)
            {
                return sourceArray.Rank == targetArray.Rank
                    && sourceArray.IsSZArray == targetArray.IsSZArray
                    && HasMatchingType(sourceArray.ElementType, targetArray.ElementType, targetMember);
            }

            if (source is IPointerTypeSymbol sourcePointer && target is IPointerTypeSymbol targetPointer)
            {
                return HasMatchingType(sourcePointer.PointedAtType, targetPointer.PointedAtType, targetMember);
            }

            if (source is IFunctionPointerTypeSymbol sourceFunction
                && target is IFunctionPointerTypeSymbol targetFunction)
            {
                return FunctionPointersMatch(sourceFunction, targetFunction, targetMember);
            }

            if (source is INamedTypeSymbol sourceNamed && target is INamedTypeSymbol targetNamed)
            {
                if (!SymbolEqualityComparer.Default.Equals(sourceNamed.OriginalDefinition, targetNamed.OriginalDefinition)
                    || sourceNamed.TypeArguments.Length != targetNamed.TypeArguments.Length)
                {
                    return false;
                }

                if (sourceNamed.ContainingType is { } sourceContaining)
                {
                    if (targetNamed.ContainingType is not { } targetContaining
                        || !HasMatchingType(sourceContaining, targetContaining, targetMember))
                    {
                        return false;
                    }
                }
                else if (targetNamed.ContainingType is not null)
                {
                    return false;
                }

                for (int i = 0; i < sourceNamed.TypeArguments.Length; i++)
                {
                    if (!HasMatchingType(sourceNamed.TypeArguments[i], targetNamed.TypeArguments[i], targetMember))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        private static bool CrefParametersMatch(
            ITypeParameterSymbol source,
            ITypeParameterSymbol target,
            ISymbol targetMember)
        {
            if (source.Ordinal != target.Ordinal || targetMember is not IMethodSymbol method)
            {
                return false;
            }

            bool methodArgument = source.Ordinal < method.TypeArguments.Length
                && SymbolEqualityComparer.Default.Equals(method.TypeArguments[source.Ordinal], target);
            bool containingArgument = IsContainingTypeArgument(method.ContainingType, target);
            if (methodArgument == containingArgument)
            {
                return false;
            }

            if (methodArgument)
            {
                return source.TypeParameterKind == TypeParameterKind.Method
                    && source.ContainingSymbol is IMethodSymbol sourceMethod
                    && sourceMethod.Arity == method.Arity;
            }

            if (source.TypeParameterKind != TypeParameterKind.Type
                || source.ContainingSymbol is not INamedTypeSymbol sourceOwner)
            {
                return false;
            }

            for (INamedTypeSymbol? owner = method.ContainingType;
                owner is not null;
                owner = owner.ContainingType)
            {
                if (owner.Arity == sourceOwner.Arity
                    && GetTypeNestingDepth(owner) == GetTypeNestingDepth(sourceOwner)
                    && source.Ordinal < owner.TypeArguments.Length
                    && SymbolEqualityComparer.Default.Equals(owner.TypeArguments[source.Ordinal], target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsContainingTypeArgument(INamedTypeSymbol? owner, ITypeParameterSymbol target)
        {
            for (; owner is not null; owner = owner.ContainingType)
            {
                foreach (ITypeSymbol argument in owner.TypeArguments)
                {
                    if (SymbolEqualityComparer.Default.Equals(argument, target))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool FunctionPointersMatch(
            IFunctionPointerTypeSymbol source,
            IFunctionPointerTypeSymbol target,
            ISymbol targetMember)
        {
            IMethodSymbol sourceSignature = source.Signature;
            IMethodSymbol targetSignature = target.Signature;
            if (sourceSignature.CallingConvention != targetSignature.CallingConvention
                || sourceSignature.UnmanagedCallingConventionTypes.Length
                    != targetSignature.UnmanagedCallingConventionTypes.Length
                || sourceSignature.Parameters.Length != targetSignature.Parameters.Length
                || sourceSignature.ReturnsByRef != targetSignature.ReturnsByRef
                || sourceSignature.ReturnsByRefReadonly != targetSignature.ReturnsByRefReadonly
                || !HasMatchingType(sourceSignature.ReturnType, targetSignature.ReturnType, targetMember))
            {
                return false;
            }

            for (int i = 0; i < sourceSignature.UnmanagedCallingConventionTypes.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(
                    sourceSignature.UnmanagedCallingConventionTypes[i],
                    targetSignature.UnmanagedCallingConventionTypes[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < sourceSignature.Parameters.Length; i++)
            {
                IParameterSymbol sourceParameter = sourceSignature.Parameters[i];
                IParameterSymbol targetParameter = targetSignature.Parameters[i];
                if (sourceParameter.RefKind != targetParameter.RefKind
                    || !HasMatchingType(sourceParameter.Type, targetParameter.Type, targetMember))
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetTypeNestingDepth(INamedTypeSymbol type)
        {
            int depth = 0;
            for (INamedTypeSymbol? containing = type.ContainingType;
                containing is not null;
                containing = containing.ContainingType)
            {
                depth++;
            }

            return depth;
        }

        private static IParameterSymbol? GetParameter(
            ISymbol symbol,
            int ordinal,
            CancellationToken cancellationToken)
        {
            if (ordinal < 0)
            {
                return null;
            }

            switch (symbol)
            {
                case IMethodSymbol method when ordinal < method.Parameters.Length:
                    return method.Parameters[ordinal];
                case IPropertySymbol property when ordinal < property.Parameters.Length:
                    return property.Parameters[ordinal];
                case INamedTypeSymbol { TypeKind: TypeKind.Delegate, DelegateInvokeMethod: { } invoke }
                    when ordinal < invoke.Parameters.Length:
                    return invoke.Parameters[ordinal];
                case INamedTypeSymbol { IsExtension: true, ExtensionParameter: { } receiver }
                    when ordinal == 0:
                    return receiver;
                case INamedTypeSymbol type:
                    foreach (IMethodSymbol constructor in type.InstanceConstructors)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (IsPrimaryConstructor(constructor, cancellationToken)
                            && ordinal < constructor.Parameters.Length)
                        {
                            return constructor.Parameters[ordinal];
                        }
                    }

                    break;
            }

            return null;
        }
    }
}
