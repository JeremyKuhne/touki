// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis;

namespace Touki.Analyzers;

public sealed partial class MoveTypeToFileCodeFixProvider
{
    /// <summary>
    ///  Describes one annotated type declaration to move during Fix All.
    /// </summary>
    private readonly struct MoveRequest
    {
        public MoveRequest(
            DocumentId documentId,
            string originalFilePath,
            SyntaxAnnotation annotation,
            int nestingDepth,
            int sourcePosition)
        {
            DocumentId = documentId;
            OriginalFilePath = originalFilePath;
            Annotation = annotation;
            NestingDepth = nestingDepth;
            SourcePosition = sourcePosition;
        }

        public DocumentId DocumentId { get; }

        public string OriginalFilePath { get; }

        public SyntaxAnnotation Annotation { get; }

        public int NestingDepth { get; }

        public int SourcePosition { get; }
    }
}