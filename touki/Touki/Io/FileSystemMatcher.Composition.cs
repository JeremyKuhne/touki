// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public static partial class FileSystemMatcher
{
    /// <summary>
    ///  Creates an immutable matcher in which any matching exclude takes precedence over includes.
    /// </summary>
    /// <param name="includes">The matchers that include paths.</param>
    /// <param name="excludes">The optional matchers that exclude paths.</param>
    /// <returns>The immutable matcher definition.</returns>
    public static IFileSystemMatcher CreateExclusionWins(
        IReadOnlyList<IFileSystemMatcher> includes,
        IReadOnlyList<IFileSystemMatcher>? excludes = null) =>
        new ExclusionWinsFileSystemMatcher(
            SnapshotMatchers(includes, requireNonEmpty: true, nameof(includes)),
            excludes is null
                ? []
                : SnapshotMatchers(excludes, requireNonEmpty: false, nameof(excludes)));

    /// <summary>
    ///  Creates an immutable ordered matcher in which the last matching rule determines the result.
    /// </summary>
    /// <param name="rules">The ordered match rules.</param>
    /// <param name="includeUnmatched">Whether to include paths that match no rule.</param>
    /// <returns>The immutable matcher definition.</returns>
    public static IFileSystemMatcher CreateOrdered(
        IReadOnlyList<FileSystemMatchRule> rules,
        bool includeUnmatched = false)
    {
        ArgumentNullException.ThrowIfNull(rules);
        FileSystemMatchRule[] snapshot = new FileSystemMatchRule[rules.Count];
        for (int index = 0; index < rules.Count; index++)
        {
            FileSystemMatchRule rule = rules[index];
            if (rule.Matcher is null)
            {
                throw new ArgumentException("Rules cannot contain a default value.", nameof(rules));
            }

            snapshot[index] = rule;
        }

        return new OrderedFileSystemMatcher(snapshot, includeUnmatched);
    }

    private static IFileSystemMatcher[] SnapshotMatchers(
        IReadOnlyList<IFileSystemMatcher> matchers,
        bool requireNonEmpty,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(matchers);
        if (requireNonEmpty && matchers.Count == 0)
        {
            throw new ArgumentException("At least one matcher is required.", parameterName);
        }

        IFileSystemMatcher[] snapshot = new IFileSystemMatcher[matchers.Count];
        for (int index = 0; index < matchers.Count; index++)
        {
            snapshot[index] = matchers[index]
                ?? throw new ArgumentException("Matchers cannot contain null.", parameterName);
        }

        return snapshot;
    }
}