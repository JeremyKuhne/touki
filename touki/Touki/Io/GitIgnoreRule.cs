// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Matches a compiled Git-ignore pattern relative to its source base path and carries the rule's ordered action.
/// </summary>
/// <param name="specification">The compiled glob specification.</param>
/// <param name="basePath">The canonical root-relative directory containing the rule source.</param>
/// <param name="action">The action applied when the rule matches.</param>
internal sealed class GitIgnoreRule(
    GlobSpecification specification,
    string basePath,
    FileSystemMatchAction action)
{
    /// <summary>
    ///  Gets the compiled glob specification.
    /// </summary>
    public GlobSpecification Specification { get; } = specification;

    /// <summary>
    ///  Gets the canonical root-relative directory containing the rule source.
    /// </summary>
    public string BasePath { get; } = basePath;

    /// <summary>
    ///  Gets the action applied when the rule matches.
    /// </summary>
    public FileSystemMatchAction Action { get; } = action;

    /// <summary>
    ///  Determines whether the rule matches a canonical root-relative path.
    /// </summary>
    /// <param name="rootRelativePath">The canonical root-relative path.</param>
    /// <param name="isDirectory">Whether the path identifies a directory.</param>
    /// <returns><see langword="true"/> if the rule matches; otherwise <see langword="false"/>.</returns>
    public bool Matches(ReadOnlySpan<char> rootRelativePath, bool isDirectory)
    {
        if (BasePath.Length > 0)
        {
            if (rootRelativePath.Length <= BasePath.Length
                || !rootRelativePath.StartsWith(BasePath, StringComparison.Ordinal)
                || rootRelativePath[BasePath.Length] != '/')
            {
                return false;
            }

            rootRelativePath = rootRelativePath[(BasePath.Length + 1)..];
        }

        return (isDirectory || !Specification.DirectoryOnly)
            && Specification.IsMatch(rootRelativePath);
    }
}