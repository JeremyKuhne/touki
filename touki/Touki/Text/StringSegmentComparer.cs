// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

/// <summary>
///  Comparer class for <see cref="StringSegment"/>.
/// </summary>
public abstract class StringSegmentComparer : IEqualityComparer<StringSegment>, IComparer<StringSegment>
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

    private sealed class StringSegmentOrdinalComparer : StringSegmentComparer
    {
        public override int Compare(StringSegment x, StringSegment y) => x.CompareTo(y, StringComparison.Ordinal);
        public override bool Equals(StringSegment x, StringSegment y) => x.Equals(y, StringComparison.Ordinal);
        public override int GetHashCode(StringSegment obj) => obj.GetHashCode();
    }

    private sealed class StringSegmentOrdinalIgnoreCaseComparer : StringSegmentComparer
    {
        public override int Compare(StringSegment x, StringSegment y) => x.CompareTo(y, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(StringSegment x, StringSegment y) => x.Equals(y, StringComparison.OrdinalIgnoreCase);
        public override int GetHashCode(StringSegment obj) => obj.GetHashCode();
    }
}
