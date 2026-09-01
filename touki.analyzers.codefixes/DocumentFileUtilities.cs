// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Touki.Analyzers;

internal static class DocumentFileUtilities
{
    /// <summary>
    ///  Gets the path comparer for the current platform: ordinal-ignore-case on Windows and ordinal elsewhere.
    /// </summary>
    public static StringComparer PathComparer => FilePathIdentity.PathComparer;

    /// <summary>
    ///  Determines whether another document in the solution has the same file path as
    ///  <paramref name="document"/>, using the current platform's path identity.
    /// </summary>
    public static bool HasSharedFilePath(
        Solution solution,
        Document document,
        CancellationToken cancellationToken = default)
    {
        if (document.FilePath is not { } filePath)
        {
            return false;
        }

        foreach (Project project in solution.Projects)
        {
            foreach (Document candidate in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidate.Id != document.Id
                    && PathComparer.Equals(candidate.FilePath, filePath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///  Gets every document in the solution that represents the same physical source file as
    ///  <paramref name="document"/>.
    /// </summary>
    public static ImmutableArray<DocumentId> GetRelatedDocumentIds(
        Document document,
        CancellationToken cancellationToken)
    {
        if (document.FilePath is null)
        {
            return [document.Id];
        }

        ImmutableArray<DocumentId>.Builder documentIds = ImmutableArray.CreateBuilder<DocumentId>();
        foreach (Project project in document.Project.Solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (Document candidate in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidate.Id == document.Id
                    || PathComparer.Equals(candidate.FilePath, document.FilePath))
                {
                    if (project.Language != document.Project.Language)
                    {
                        return default;
                    }

                    documentIds.Add(candidate.Id);
                }
            }
        }

        return documentIds.ToImmutable();
    }

    /// <summary>
    ///  Indexes each document in <paramref name="language"/> by every document that represents the same physical
    ///  source file.
    /// </summary>
    public static Dictionary<DocumentId, ImmutableArray<DocumentId>> IndexRelatedDocuments(
        Solution solution,
        string language,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<DocumentId>> documentsByPath = new(PathComparer);
        HashSet<string> pathsInOtherLanguages = new(PathComparer);
        Dictionary<DocumentId, ImmutableArray<DocumentId>> relatedDocuments = [];
        foreach (Project project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (Document document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (document.FilePath is null)
                {
                    if (project.Language == language)
                    {
                        relatedDocuments.Add(document.Id, [document.Id]);
                    }

                    continue;
                }

                if (project.Language != language)
                {
                    pathsInOtherLanguages.Add(document.FilePath);
                    continue;
                }

                if (!documentsByPath.TryGetValue(document.FilePath, out List<DocumentId>? documentIds))
                {
                    documentIds = [];
                    documentsByPath.Add(document.FilePath, documentIds);
                }

                documentIds.Add(document.Id);
            }
        }

        foreach (KeyValuePair<string, List<DocumentId>> pair in documentsByPath)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImmutableArray<DocumentId> group = pathsInOtherLanguages.Contains(pair.Key)
                ? default
                : [.. pair.Value];
            foreach (DocumentId documentId in pair.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                relatedDocuments.Add(documentId, group);
            }
        }

        return relatedDocuments;
    }

    /// <summary>
    ///  Gets every document whose physical source path is shared by another document in the solution.
    /// </summary>
    public static HashSet<DocumentId> IndexSharedDocuments(
        Solution solution,
        CancellationToken cancellationToken)
    {
        Dictionary<string, DocumentId> documentsByPath = new(PathComparer);
        HashSet<DocumentId> sharedDocuments = [];
        foreach (Project project in solution.Projects)
        {
            foreach (Document document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (document.FilePath is null)
                {
                    continue;
                }

                if (documentsByPath.TryGetValue(document.FilePath, out DocumentId? relatedDocumentId))
                {
                    sharedDocuments.Add(relatedDocumentId);
                    sharedDocuments.Add(document.Id);
                }
                else
                {
                    documentsByPath.Add(document.FilePath, document.Id);
                }
            }
        }

        return sharedDocuments;
    }

    /// <summary>
    ///  Determines whether a document in <paramref name="solution"/> has <paramref name="filePath"/>, using an
    ///  ordinal comparison appropriate for the current platform and optionally excluding one document.
    /// </summary>
    public static bool HasDocumentWithFilePath(
        Solution solution,
        string filePath,
        DocumentId? excludedDocumentId = null)
    {
        foreach (Project project in solution.Projects)
        {
            foreach (Document candidate in project.Documents)
            {
                if (candidate.Id != excludedDocumentId
                    && PathComparer.Equals(candidate.FilePath, filePath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether <paramref name="targetFilePath"/> is available on the file system, treating the
    ///  current document path as available and path-inspection failures as collisions.
    /// </summary>
    public static bool IsFileSystemDestinationAvailable(string currentFilePath, string targetFilePath)
    {
        string fullCurrentPath = Path.GetFullPath(currentFilePath);
        string fullTargetPath = Path.GetFullPath(targetFilePath);
        if (string.Equals(fullCurrentPath, fullTargetPath, StringComparison.Ordinal))
        {
            return true;
        }

        string? directory = Path.GetDirectoryName(fullTargetPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return true;
        }

        string targetName = Path.GetFileName(fullTargetPath);
        bool targetExists = File.Exists(fullTargetPath) || Directory.Exists(fullTargetPath);
        bool targetResolvesToCurrentEntry = false;

        try
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                string fullEntryPath = Path.GetFullPath(entry);
                bool isCurrentEntry = string.Equals(fullEntryPath, fullCurrentPath, StringComparison.Ordinal);
                string entryName = Path.GetFileName(fullEntryPath);
                if (string.Equals(entryName, targetName, StringComparison.Ordinal))
                {
                    return isCurrentEntry;
                }

                if (targetExists
                    && isCurrentEntry
                    && string.Equals(entryName, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    targetResolvesToCurrentEntry = true;
                }
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return !targetExists || targetResolvesToCurrentEntry;
    }

    /// <summary>
    ///  Gets the path for <paramref name="fileName"/> in the directory containing
    ///  <paramref name="document"/>.
    /// </summary>
    /// <param name="document">The document whose directory contains the target file.</param>
    /// <param name="fileName">The target file name.</param>
    /// <returns>The target path, or <see langword="null"/> when the document has no file path.</returns>
    public static string? GetTargetFilePath(Document document, string fileName)
    {
        if (document.FilePath is null)
        {
            return null;
        }

        string? directory = Path.GetDirectoryName(document.FilePath);
#pragma warning disable TOUKI0032 // Path.Join is unavailable on netstandard2.0.
        return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
#pragma warning restore TOUKI0032
    }
}
