// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;

namespace Touki.Io;

/// <summary>
///  Configures glob compilation and file-system traversal for <see cref="GlobEnumerator"/>.
/// </summary>
public sealed class GlobEnumerationOptions
{
    /// <summary>
    ///  Gets the exclude patterns applied after the include pattern.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    ///  Gets the pattern dialect.
    /// </summary>
    public GlobDialect Dialect { get; init; } = GlobDialect.PosixPath;

    /// <summary>
    ///  Gets the glob compilation options.
    /// </summary>
    public GlobOptions GlobOptions { get; init; }

    /// <summary>
    ///  Gets optional file-system enumeration options. <see langword="null"/> selects the defaults.
    /// </summary>
    public EnumerationOptions? EnumerationOptions { get; init; }
}
