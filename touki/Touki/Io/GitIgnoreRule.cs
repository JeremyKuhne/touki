// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

internal sealed class GitIgnoreRule(
    GlobSpecification specification,
    string basePath,
    FileSystemMatchAction action)
{
    public GlobSpecification Specification { get; } = specification;

    public string BasePath { get; } = basePath;

    public FileSystemMatchAction Action { get; } = action;

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