// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;
using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Immutable ordered gitignore rules with explicit included and ignored matcher projections.
/// </summary>
public sealed class GitIgnoreRules
{
    private readonly GitIgnoreRule[] _rules;

    private GitIgnoreRules(GitIgnoreRule[] rules)
    {
        _rules = rules;
    }

    /// <summary>
    ///  Gets the number of compiled rules.
    /// </summary>
    public int Count => _rules.Length;

    /// <summary>
    ///  Parses one root gitignore file.
    /// </summary>
    /// <param name="content">The gitignore file content.</param>
    /// <returns>The compiled rules.</returns>
    public static GitIgnoreRules Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Compile([new(content)]);
    }

    /// <summary>
    ///  Compiles gitignore sources in parent-to-child source order.
    /// </summary>
    /// <param name="sources">The gitignore sources to compile.</param>
    /// <returns>The compiled rules.</returns>
    public static GitIgnoreRules Compile(IReadOnlyList<GitIgnoreRuleSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        using SingleOptimizedList<GitIgnoreRule, ArrayPoolList<GitIgnoreRule>> rules = [];
        for (int index = 0; index < sources.Count; index++)
        {
            GitIgnoreRuleSource source = sources[index];
            if (source.Content is null || source.BasePath is null)
            {
                throw new ArgumentException("Sources cannot contain a default value.", nameof(sources));
            }

            AddRules(rules, source.Content.AsSpan(), source.BasePath);
        }

        GitIgnoreRule[] snapshot = new GitIgnoreRule[rules.Count];
        rules.CopyTo(snapshot, 0);
        return new(snapshot);
    }

    /// <summary>
    ///  Returns whether a canonical root-relative file path is ignored.
    /// </summary>
    /// <param name="rootRelativeFilePath">The canonical root-relative file path.</param>
    /// <returns><see langword="true"/> if the file is ignored; otherwise <see langword="false"/>.</returns>
    public bool IsIgnoredFile(ReadOnlySpan<char> rootRelativeFilePath)
    {
        ValidateCanonicalPath(rootRelativeFilePath, allowEmpty: false, nameof(rootRelativeFilePath));
        return IsIgnoredFileCore(rootRelativeFilePath);
    }

    /// <summary>
    ///  Creates a reusable matcher whose successful file results mean included.
    /// </summary>
    /// <returns>The reusable matcher definition.</returns>
    public IFileSystemMatcher CreateIncludedMatcher() => new GitIgnoreFileSystemMatcher(this, matchIgnored: false);

    /// <summary>
    ///  Creates a reusable matcher whose successful file results mean ignored.
    /// </summary>
    /// <returns>The reusable matcher definition.</returns>
    public IFileSystemMatcher CreateIgnoredMatcher() => new GitIgnoreFileSystemMatcher(this, matchIgnored: true);

    /// <summary>
    ///  Determines whether a canonical file path or one of its ancestor directories is ignored.
    /// </summary>
    /// <param name="rootRelativeFilePath">The canonical root-relative file path.</param>
    /// <returns><see langword="true"/> if the file is ignored; otherwise <see langword="false"/>.</returns>
    internal bool IsIgnoredFileCore(ReadOnlySpan<char> rootRelativeFilePath)
    {
        int separatorIndex = rootRelativeFilePath.IndexOf('/');
        while (separatorIndex >= 0)
        {
            if (Evaluate(rootRelativeFilePath[..separatorIndex], isDirectory: true))
            {
                return true;
            }

            int nextSeparator = rootRelativeFilePath[(separatorIndex + 1)..].IndexOf('/');
            separatorIndex = nextSeparator < 0
                ? -1
                : separatorIndex + 1 + nextSeparator;
        }

        return Evaluate(rootRelativeFilePath, isDirectory: false);
    }

    /// <summary>
    ///  Determines whether a canonical directory path or one of its ancestor directories is ignored.
    /// </summary>
    /// <param name="rootRelativeDirectoryPath">The canonical root-relative directory path.</param>
    /// <returns><see langword="true"/> if the directory is ignored; otherwise <see langword="false"/>.</returns>
    internal bool IsIgnoredDirectoryCore(ReadOnlySpan<char> rootRelativeDirectoryPath)
    {
        int separatorIndex = rootRelativeDirectoryPath.IndexOf('/');
        while (separatorIndex >= 0)
        {
            if (Evaluate(rootRelativeDirectoryPath[..separatorIndex], isDirectory: true))
            {
                return true;
            }

            int nextSeparator = rootRelativeDirectoryPath[(separatorIndex + 1)..].IndexOf('/');
            separatorIndex = nextSeparator < 0
                ? -1
                : separatorIndex + 1 + nextSeparator;
        }

        return Evaluate(rootRelativeDirectoryPath, isDirectory: true);
    }

    /// <summary>
    ///  Validates a canonical root-relative path.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="allowEmpty">Whether an empty path is valid.</param>
    /// <param name="parameterName">The parameter name to use for validation exceptions.</param>
    internal static void ValidateCanonicalPath(
        ReadOnlySpan<char> path,
        bool allowEmpty,
        string parameterName)
    {
        if (path.IsEmpty)
        {
            if (allowEmpty)
            {
                return;
            }

            throw new ArgumentException("The path cannot be empty.", parameterName);
        }

        if (path[0] == '/' || path[^1] == '/')
        {
            throw new ArgumentException("The path cannot start or end with '/'.", parameterName);
        }

        ReadOnlySpan<char> remaining = path;
        while (true)
        {
            int separatorIndex = remaining.IndexOf('/');
            ReadOnlySpan<char> segment = separatorIndex < 0 ? remaining : remaining[..separatorIndex];
            if (segment.IsEmpty || segment.SequenceEqual(".") || segment.SequenceEqual(".."))
            {
                throw new ArgumentException("The path must contain canonical segments.", parameterName);
            }

            if (separatorIndex < 0)
            {
                return;
            }

            remaining = remaining[(separatorIndex + 1)..];
        }
    }

    private bool Evaluate(ReadOnlySpan<char> rootRelativePath, bool isDirectory)
    {
        bool ignored = false;
        for (int index = 0; index < _rules.Length; index++)
        {
            GitIgnoreRule rule = _rules[index];
            if (rule.Matches(rootRelativePath, isDirectory))
            {
                ignored = rule.Action == FileSystemMatchAction.Exclude;
            }
        }

        return ignored;
    }

    private static void AddRules(
        SingleOptimizedList<GitIgnoreRule, ArrayPoolList<GitIgnoreRule>> rules,
        ReadOnlySpan<char> content,
        string basePath)
    {
        while (!content.IsEmpty)
        {
            ReadOnlySpan<char> line;
            int newlineIndex = content.IndexOfAny('\r', '\n');
            if (newlineIndex < 0)
            {
                line = content;
                content = default;
            }
            else
            {
                line = content[..newlineIndex];
                content = content[newlineIndex..];
                content = content.Length >= 2 && content[0] == '\r' && content[1] == '\n'
                    ? content[2..]
                    : content[1..];
            }

            while (line.Length > 0 && line[^1] is ' ' or '\t')
            {
                line = line[..^1];
            }

            if (line.IsEmpty || line[0] == '#')
            {
                continue;
            }

            FileSystemMatchAction action = FileSystemMatchAction.Exclude;
            if (line[0] == '!')
            {
                action = FileSystemMatchAction.Include;
                line = line[1..];
                if (line.IsEmpty)
                {
                    continue;
                }
            }

            string pattern = line.ToString();
            if (action == FileSystemMatchAction.Include && pattern[0] == '!')
            {
                pattern = $@"\{pattern}";
            }

            GlobSpecification specification = GlobSpecification.Compile(
                pattern,
                GlobDialect.Git);
            rules.Add(new(specification, basePath, action));
        }
    }
}