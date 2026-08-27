// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public sealed partial class SequenceSet<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    ///  Stores the arena location, cached hash, and bucket-chain link for one sequence.
    /// </summary>
    private struct Entry
    {
        // Offset of this sequence's first element in the arena.
        public int Offset;

        // Number of elements this sequence occupies in the arena.
        public int Length;

        // Cached hash of the sequence, kept so rehashing never rereads the arena.
        public int HashCode;

        // One-based index of the next entry in the same bucket chain; 0 marks the end.
        public int Next;
    }
}
