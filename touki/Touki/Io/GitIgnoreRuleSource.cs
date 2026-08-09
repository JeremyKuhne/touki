// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Supplies one gitignore file and its canonical root-relative directory.
/// </summary>
public readonly struct GitIgnoreRuleSource
{
    /// <summary>
    ///  Constructs a rule source.
    /// </summary>
    /// <param name="content">The full gitignore file content.</param>
    /// <param name="basePath">
    ///  The canonical <c>/</c>-separated root-relative directory containing the file, or empty for
    ///  the enumeration root.
    /// </param>
    public GitIgnoreRuleSource(string content, string basePath = "")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(basePath);
        GitIgnoreRules.ValidateCanonicalPath(basePath, allowEmpty: true, nameof(basePath));
        Content = content;
        BasePath = basePath;
    }

    /// <summary>
    ///  Gets the full gitignore file content.
    /// </summary>
    public string Content { get; }

    /// <summary>
    ///  Gets the canonical root-relative directory containing the gitignore file.
    /// </summary>
    public string BasePath { get; }
}