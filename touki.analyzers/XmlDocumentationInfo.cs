// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Touki.Analyzers;

/// <summary>
///  Collects the top-level XML documentation elements associated with source declarations.
/// </summary>
internal struct XmlDocumentationInfo
{
    private HashSet<string>? _parameterNames;

    public int SummaryCount { get; private set; }

    public bool HasInheritdoc { get; private set; }

    public bool HasReturns { get; private set; }

    public readonly bool HasMemberDocumentation => SummaryCount > 0 || HasInheritdoc;

    public readonly bool HasParameter(string name) => _parameterNames?.Contains(name) == true;

    public void AddDeclaration(SyntaxNode declaration)
    {
        TypeDeclarationSyntax? extensionBlock = declaration.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (extensionBlock is not null
            && extensionBlock != declaration
            && extensionBlock.Identifier.IsKind(SyntaxKind.None))
        {
            AddDocumentation(extensionBlock);
        }

        SyntaxNode owner = GetDocumentationOwner(declaration);
        AddDocumentation(owner);
    }

    private void AddDocumentation(SyntaxNode owner)
    {
        SyntaxTriviaList leadingTrivia = owner.GetLeadingTrivia();
        bool foundDocumentation = false;
        bool foundOtherTrivia = false;

        for (int i = leadingTrivia.Count - 1; i >= 0; i--)
        {
            SyntaxTrivia trivia = leadingTrivia[i];
            if (trivia.GetStructure() is DocumentationCommentTriviaSyntax documentation)
            {
                foundDocumentation = true;
                if (foundOtherTrivia || !IsWellFormed(documentation))
                {
                    continue;
                }

                foreach (XmlNodeSyntax content in documentation.Content)
                {
                    AddContent(content);
                }

                continue;
            }

            if (foundDocumentation
                && !trivia.IsKind(SyntaxKind.WhitespaceTrivia)
                && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                foundOtherTrivia = true;
            }
        }
    }

    public static SyntaxNode GetDocumentationOwner(SyntaxNode declaration) => declaration switch
    {
        VariableDeclaratorSyntax variable =>
            (SyntaxNode?)variable.FirstAncestorOrSelf<BaseFieldDeclarationSyntax>() ?? variable,
        _ => declaration
    };

    private void AddContent(XmlNodeSyntax content)
    {
        XmlNameSyntax? name;
        SyntaxList<XmlAttributeSyntax> attributes;

        switch (content)
        {
            case XmlElementSyntax element:
                name = element.StartTag.Name;
                attributes = element.StartTag.Attributes;
                break;
            case XmlEmptyElementSyntax emptyElement:
                name = emptyElement.Name;
                attributes = emptyElement.Attributes;
                break;
            default:
                return;
        }

        if (name.Prefix is not null)
        {
            return;
        }

        switch (name.LocalName.ValueText)
        {
            case "summary":
                SummaryCount++;
                break;
            case "inheritdoc":
                HasInheritdoc = true;
                break;
            case "returns":
                HasReturns = true;
                break;
            case "param":
                AddParameter(attributes);
                break;
        }
    }

    private void AddParameter(SyntaxList<XmlAttributeSyntax> attributes)
    {
        foreach (XmlAttributeSyntax attribute in attributes)
        {
            if (attribute is XmlNameAttributeSyntax nameAttribute
                && nameAttribute.Name.Prefix is null
                && string.Equals(nameAttribute.Name.LocalName.ValueText, "name", StringComparison.Ordinal))
            {
                (_parameterNames ??= new(StringComparer.Ordinal)).Add(nameAttribute.Identifier.Identifier.ValueText);
                return;
            }
        }
    }

    private static bool IsWellFormed(DocumentationCommentTriviaSyntax documentation)
    {
        if (documentation.ContainsDiagnostics)
        {
            return false;
        }

        foreach (SyntaxToken token in documentation.DescendantTokens(descendIntoTrivia: true))
        {
            if (token.IsMissing)
            {
                return false;
            }
        }

        return true;
    }
}