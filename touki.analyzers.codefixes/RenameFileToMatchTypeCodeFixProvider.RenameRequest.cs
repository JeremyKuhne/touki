// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis;

namespace Touki.Analyzers;

public sealed partial class RenameFileToMatchTypeCodeFixProvider
{
    /// <summary>
    ///  Describes one document rename requested during Fix All.
    /// </summary>
    private readonly struct RenameRequest
    {
        public RenameRequest(
            DocumentId documentId,
            string originalFilePath,
            string suggestedFileName,
            char detailSeparator)
        {
            DocumentId = documentId;
            OriginalFilePath = originalFilePath;
            SuggestedFileName = suggestedFileName;
            DetailSeparator = detailSeparator;
        }

        public DocumentId DocumentId { get; }

        public string OriginalFilePath { get; }

        public string SuggestedFileName { get; }

        public char DetailSeparator { get; }
    }
}