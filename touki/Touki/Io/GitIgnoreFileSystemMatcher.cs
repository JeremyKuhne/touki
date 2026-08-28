// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Creates sessions that select either ignored or non-ignored paths from a compiled Git-ignore rule set.
/// </summary>
/// <param name="rules">The compiled Git-ignore rules.</param>
/// <param name="matchIgnored">Whether sessions select ignored paths instead of included paths.</param>
internal sealed class GitIgnoreFileSystemMatcher(
    GitIgnoreRules rules,
    bool matchIgnored) : IFileSystemMatcher
{
    /// <summary>
    ///  Creates a matcher session bound to <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <returns>A new matcher session owned by the caller.</returns>
    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        return new GitIgnoreFileSystemMatcherSession(rules, rootDirectory, matchIgnored);
    }
}