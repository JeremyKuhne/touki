// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public abstract partial class StringSegmentComparer
{
    /// <summary>
    ///  Applies ordinal comparison and matching hash semantics directly to string segments.
    /// </summary>
    private sealed class StringSegmentOrdinalComparer : StringSegmentComparer
    {
        public override int Compare(StringSegment x, StringSegment y) => x.CompareTo(y, StringComparison.Ordinal);
        public override bool Equals(StringSegment x, StringSegment y) => x.Equals(y, StringComparison.Ordinal);
        public override int GetHashCode(StringSegment obj) => obj.GetHashCode();
    }
}
