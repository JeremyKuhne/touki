// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public abstract partial class StringSegmentComparer
{
    private sealed class StringSegmentOrdinalIgnoreCaseComparer : StringSegmentComparer
    {
        public override int Compare(StringSegment x, StringSegment y) => x.CompareTo(y, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(StringSegment x, StringSegment y) => x.Equals(y, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode(StringSegment obj)
        {
#if NET
            return string.GetHashCode(obj.AsSpan(), StringComparison.OrdinalIgnoreCase);
#else
            // .NET Framework has no span-based ignore-case hash, and materializing an upper-cased string
            // would allocate. Fold each character with the same simple invariant uppercasing that
            // ordinal-ignore-case comparison uses, so segments that compare equal always hash equally.
            ReadOnlySpan<char> value = obj.AsSpan();
            HashCode hash = new();

            for (int i = 0; i < value.Length; i++)
            {
                hash.Add((int)char.ToUpperInvariant(value[i]));
            }

            return hash.ToHashCode();
#endif
        }
    }
}
