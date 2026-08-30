// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Touki.Analyzers;

internal static class DocumentFileUtilities
{
    /// <summary>
    ///  Determines whether another document in the solution has the same file path as
    ///  <paramref name="document"/>, using an ordinal case-insensitive comparison.
    /// </summary>
    public static bool HasSharedFilePath(Solution solution, Document document)
    {
        string filePath = document.FilePath!;

        foreach (Project project in solution.Projects)
        {
            foreach (Document candidate in project.Documents)
            {
                if (candidate.Id != document.Id
                    && string.Equals(candidate.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether a document in <paramref name="solution"/> has <paramref name="filePath"/>, using an
    ///  ordinal case-insensitive comparison and optionally excluding one document.
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
                    && string.Equals(candidate.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
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

        try
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                string fullEntryPath = Path.GetFullPath(entry);
                if (string.Equals(fullEntryPath, fullCurrentPath, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(Path.GetFileName(fullEntryPath), targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
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

        return true;
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