// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Tracks a matcher definition and whether its children have been expanded while building a flattened graph.
/// </summary>
internal readonly struct CompositeMatcherWorkItem(
    IFileSystemMatcher matcher,
    bool expanded)
{
    public IFileSystemMatcher Matcher { get; } = matcher;

    public bool Expanded { get; } = expanded;
}
