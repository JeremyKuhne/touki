// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

/// <summary>
///  Comparer class for <see cref="StringSegment"/>.
/// </summary>
public abstract partial class StringSegmentComparer : IEqualityComparer<StringSegment>, IComparer<StringSegment>
{
    /// <summary>
    ///  Returns the default <see cref="StringSegmentComparer"/> that compares segments using ordinal comparison.
    /// </summary>
    public static StringSegmentComparer Ordinal { get; } = new StringSegmentOrdinalComparer();

    /// <summary>
    ///  Returns the default <see cref="StringSegmentComparer"/> that compares segments using ordinal ignore case comparison.
    /// </summary>
    public static StringSegmentComparer OrdinalIgnoreCase { get; } = new StringSegmentOrdinalIgnoreCaseComparer();

    /// <inheritdoc/>
    public abstract int Compare(StringSegment x, StringSegment y);

    /// <inheritdoc/>
    public abstract bool Equals(StringSegment x, StringSegment y);

    /// <inheritdoc/>
    public abstract int GetHashCode(StringSegment obj);
}
