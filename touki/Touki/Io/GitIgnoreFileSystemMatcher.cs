// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Creates sessions that select either ignored or non-ignored paths from a compiled Git-ignore rule set.
/// </summary>
internal sealed class GitIgnoreFileSystemMatcher(
    GitIgnoreRules rules,
    bool matchIgnored) : IFileSystemMatcher
{
    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        return new GitIgnoreFileSystemMatcherSession(rules, rootDirectory, matchIgnored);
    }
}