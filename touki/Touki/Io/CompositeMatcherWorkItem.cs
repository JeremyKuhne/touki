// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Tracks a matcher definition and whether its children have been expanded while building a flattened graph.
/// </summary>
/// <param name="matcher">The matcher definition.</param>
/// <param name="expanded">Whether the matcher's children have been expanded.</param>
internal readonly struct CompositeMatcherWorkItem(
    IFileSystemMatcher matcher,
    bool expanded)
{
    /// <summary>
    ///  Gets the matcher definition.
    /// </summary>
    public IFileSystemMatcher Matcher { get; } = matcher;

    /// <summary>
    ///  Gets whether the matcher's children have been expanded.
    /// </summary>
    public bool Expanded { get; } = expanded;
}
