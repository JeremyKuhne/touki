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
        private readonly bool _sourceIsExtensionMethod;
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
            _sourceIsExtensionMethod = source is IMethodSymbol { IsExtensionMethod: true };
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
            if (_sourceIsExtensionMethod && symbol is IMethodSymbol { IsStatic: false })
            {
                ordinal--;
            }

            IParameterSymbol? target = GetParameter(symbol, ordinal, cancellationToken);
            return target is not null
                && string.Equals(target.Name, _parameterName, StringComparison.Ordinal)
                && SymbolEqualityComparer.IncludeNullability.Equals(target.Type, _type)
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

            return method is not null
                && !method.ReturnsVoid
                && SymbolEqualityComparer.IncludeNullability.Equals(method.ReturnType, _type)
                && method.ReturnsByRef == _returnsByRef
                && method.ReturnsByRefReadonly == _returnsByRefReadonly;
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
