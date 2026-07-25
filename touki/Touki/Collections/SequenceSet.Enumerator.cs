// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public sealed partial class SequenceSet<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    ///  Enumerates the interned sequences of a <see cref="SequenceSet{T}"/> as <see cref="ReadOnlySpan{T}"/>
    ///  views, in insertion order, without allocating.
    /// </summary>
    public ref struct Enumerator
    {
        private readonly SequenceSet<T> _set;
        private int _index;

        internal Enumerator(SequenceSet<T> set)
        {
            _set = set;
            _index = -1;
        }

        /// <summary>
        ///  The sequence at the current position as a view over the set's arena.
        /// </summary>
        public readonly ReadOnlySpan<T> Current => _set[_index];

        /// <summary>
        ///  Advances to the next interned sequence.
        /// </summary>
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _set._count)
            {
                _index = next;
                return true;
            }

            return false;
        }
    }
}
