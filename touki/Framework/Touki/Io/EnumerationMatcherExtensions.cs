// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Convenience methods for working with <see cref="IEnumerationMatcher"/>.
/// </summary>
public static class EnumerationMatcherExtensions
{
    /// <inheritdoc cref="IEnumerationMatcher.MatchesDirectory(ReadOnlySpan{char}, ReadOnlySpan{char})"/>
    public static bool MatchesDirectory(
        this IEnumerationMatcher matcher,
        string currentDirectory,
        string directoryName) => matcher.MatchesDirectory(currentDirectory.AsSpan(), directoryName.AsSpan());

    /// <inheritdoc cref="IEnumerationMatcher.MatchesFile(ReadOnlySpan{char}, ReadOnlySpan{char})"/>
    public static bool MatchesFile(
        this IEnumerationMatcher matcher,
        string currentDirectory,
        string fileName) => matcher.MatchesFile(currentDirectory.AsSpan(), fileName.AsSpan());
}
