// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Associates a reusable matcher definition with an ordered match action.
/// </summary>
public readonly struct FileSystemMatchRule
{
    /// <summary>
    ///  Constructs a matcher rule.
    /// </summary>
    public FileSystemMatchRule(
        IFileSystemMatcher matcher,
        FileSystemMatchAction action)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        if (action is not FileSystemMatchAction.Include and not FileSystemMatchAction.Exclude)
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        Matcher = matcher;
        Action = action;
    }

    /// <summary>
    ///  Gets the matcher definition.
    /// </summary>
    public IFileSystemMatcher Matcher { get; }

    /// <summary>
    ///  Gets the action applied when the matcher matches.
    /// </summary>
    public FileSystemMatchAction Action { get; }
}