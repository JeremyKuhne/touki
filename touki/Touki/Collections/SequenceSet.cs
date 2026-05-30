// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

/// <summary>
///  A set of variable-length sequences of unmanaged values backed by a single pooled arena and a
///  bucket-chained hash index. Adding a sequence interns it: identical sequences collapse to the same
///  stable integer handle and the element data is stored once, contiguously, with no per-sequence heap
///  allocation.
/// </summary>
/// <typeparam name="T">
///  The element type. Must be <see langword="unmanaged"/> so the backing arena can be pooled without
///  clearing and hashed over its raw bytes, and <see cref="IEquatable{T}"/> so stored sequences can be
///  compared exactly.
/// </typeparam>
/// <remarks>
///  <para>
///   This type fills a gap the BCL cannot express: a hash set whose element is a <see cref="ReadOnlySpan{T}"/>.
///   <see cref="ReadOnlySpan{T}"/> is a <see langword="ref struct"/> and therefore cannot be used as the
///   generic argument of <see cref="HashSet{T}"/> or <see cref="Dictionary{TKey, TValue}"/>. The usual
///   workarounds - keying on <c>T[]</c>, <see cref="string"/>, or <c>ReadOnlyMemory&lt;T&gt;</c> - allocate one
///   heap object per stored sequence. <see cref="SequenceSet{T}"/> instead copies each new sequence's
///   elements into one shared, pooled arena and refers to them by a <c>(offset, length)</c> entry, so the
///   only allocations are the amortized growth of a handful of pooled arrays.
///  </para>
///  <para>
///   <b>When to use it.</b> Reach for <see cref="SequenceSet{T}"/> when you need to deduplicate, intern, or
///   membership-test a large or unbounded number of short, variable-length sequences of value types on a hot
///   path - for example memoizing visited states in a backtracking search (each state serialized to a run of
///   <see cref="int"/>), interning tokens or n-grams, or canonicalizing small byte/char keys - and you want to
///   avoid the per-element garbage that a <c>HashSet&lt;byte[]&gt;</c> or <c>HashSet&lt;string&gt;</c> would
///   produce.
///  </para>
///  <para>
///   <b>Allocation.</b> Backing storage is rented lazily from <see cref="ArrayPool{T}"/> on the first
///   <see cref="Add(ReadOnlySpan{T})"/>; an unused set never rents. All rented buffers are returned by
///   <see cref="DisposableBase.Dispose()"/>. Because <typeparamref name="T"/> is <see langword="unmanaged"/>
///   the arena is returned without clearing. Disposal derives from <see cref="DisposableBase"/> so the pooled
///   arrays are returned exactly once even under a racing or double <see cref="DisposableBase.Dispose()"/>;
///   returning a rented array to <see cref="ArrayPool{T}"/> twice is corrupting, so the single-return guarantee
///   matters.
///  </para>
///  <para>
///   <b>Interfaces.</b> The natural element type is <see cref="ReadOnlySpan{T}"/>, which cannot appear as a
///   generic type argument, so this type intentionally does not implement <see cref="ICollection{T}"/> or
///   <see cref="IEnumerable{T}"/>. It implements <see cref="IDisposable"/> and exposes an allocation-free
///   <see cref="GetEnumerator"/> (a <see langword="ref struct"/> enumerator) so it can still be used in a
///   <c>foreach</c> that yields each interned sequence as a <see cref="ReadOnlySpan{T}"/>.
///  </para>
///  <para>
///   <b>Thread safety.</b> Instances are not thread safe. Concurrent <see cref="Add(ReadOnlySpan{T})"/> calls
///   require external synchronization.
///  </para>
///  <para>
///   <b>Prior art.</b> The design mirrors internal collections in the .NET ecosystem that intern
///   variable-length data without per-item allocation: Roslyn's <c>StringTable</c>
///   (<c>Microsoft.CodeAnalysis</c>) interns strings from <c>ReadOnlySpan&lt;char&gt;</c> through an
///   open-addressing table, <c>System.Reflection.Metadata.BlobBuilder</c> accumulates variable-length blobs
///   into pooled segments, and the bucket/entry chaining here is the same layout used by
///   <see cref="HashSet{T}"/> and Roslyn's <c>SegmentedHashSet&lt;T&gt;</c>.
///  </para>
/// </remarks>
public sealed class SequenceSet<T> : DisposableBase where T : unmanaged, IEquatable<T>
{
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

    private const int DefaultMinimumCapacity = 16;
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    private readonly int _minimumCapacity;

    // Element arena. All interned sequences are stored back to back here.
    private T[]? _arena;
    private int _arenaUsed;

    // Per-entry metadata, parallel to the bucket chain.
    private Entry[]? _entries;
    private int _count;

    // One-based entry indices; index is hash & _bucketMask.
    private int[]? _buckets;
    private int _bucketCount;
    private int _bucketMask;

    /// <summary>
    ///  Initializes a new instance of the <see cref="SequenceSet{T}"/> class.
    /// </summary>
    public SequenceSet() : this(DefaultMinimumCapacity)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="SequenceSet{T}"/> class with the given minimum capacity.
    /// </summary>
    /// <param name="minimumCapacity">
    ///  The number of sequences the set should be able to hold before its first internal growth. Backing
    ///  arrays are still rented lazily on the first <see cref="Add(ReadOnlySpan{T})"/>.
    /// </param>
    public SequenceSet(int minimumCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumCapacity);
        _minimumCapacity = Math.Max(minimumCapacity, 1);
    }

    /// <summary>
    ///  The number of distinct sequences currently interned in the set.
    /// </summary>
    public int Count => _count;

    /// <summary>
    ///  Returns the interned sequence for the given <paramref name="handle"/> as a view over the arena.
    /// </summary>
    /// <param name="handle">A handle previously returned by <see cref="Add(ReadOnlySpan{T}, out int)"/>.</param>
    /// <remarks>
    ///  <para>
    ///   The returned span is valid until the next mutation of the set (<see cref="Add(ReadOnlySpan{T})"/> may
    ///   reallocate the arena) and until <see cref="DisposableBase.Dispose()"/> or <see cref="Clear"/>. Do not retain it across
    ///   those operations.
    ///  </para>
    /// </remarks>
    public ReadOnlySpan<T> this[int handle]
    {
        get
        {
            if ((uint)handle >= (uint)_count)
            {
                ThrowHandleOutOfRange(handle);
            }

            ref Entry entry = ref _entries![handle];
            return _arena.AsSpan(entry.Offset, entry.Length);
        }
    }

    /// <summary>
    ///  Adds <paramref name="sequence"/> to the set if it is not already present.
    /// </summary>
    /// <param name="sequence">The sequence to intern.</param>
    /// <returns>
    ///  <see langword="true"/> if the sequence was newly added; <see langword="false"/> if an equal sequence
    ///  was already present.
    /// </returns>
    public bool Add(ReadOnlySpan<T> sequence) => Add(sequence, out _);

    /// <summary>
    ///  Adds <paramref name="sequence"/> to the set if it is not already present and returns its stable handle.
    /// </summary>
    /// <param name="sequence">The sequence to intern.</param>
    /// <param name="handle">
    ///  On return, the stable handle of the interned sequence, whether it was newly added or already present.
    ///  Handles remain valid until <see cref="Clear"/> or <see cref="DisposableBase.Dispose()"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the sequence was newly added; <see langword="false"/> if an equal sequence
    ///  was already present.
    /// </returns>
    public bool Add(ReadOnlySpan<T> sequence, out int handle)
    {
        if (_buckets is null)
        {
            Initialize();
        }

        int hash = Hash(sequence);
        int bucket = hash & _bucketMask;
        for (int entry = _buckets![bucket] - 1; entry >= 0; entry = _entries![entry].Next - 1)
        {
            ref Entry candidate = ref _entries![entry];
            if (candidate.HashCode == hash
                && _arena.AsSpan(candidate.Offset, candidate.Length).SequenceEqual(sequence))
            {
                handle = entry;
                return false;
            }
        }

        handle = AddNew(sequence, hash);
        return true;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if an equal sequence is already interned in the set.
    /// </summary>
    /// <param name="sequence">The sequence to look for.</param>
    public bool Contains(ReadOnlySpan<T> sequence)
    {
        if (_buckets is null)
        {
            return false;
        }

        int hash = Hash(sequence);
        int bucket = hash & _bucketMask;
        for (int entry = _buckets[bucket] - 1; entry >= 0; entry = _entries![entry].Next - 1)
        {
            ref Entry candidate = ref _entries![entry];
            if (candidate.HashCode == hash
                && _arena.AsSpan(candidate.Offset, candidate.Length).SequenceEqual(sequence))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Removes all interned sequences while retaining the rented backing storage for reuse.
    /// </summary>
    public void Clear()
    {
        if (_buckets is not null)
        {
            Array.Clear(_buckets, 0, _bucketCount);
        }

        _count = 0;
        _arenaUsed = 0;
    }

    private int AddNew(ReadOnlySpan<T> sequence, int hash)
    {
        if (_count == _entries!.Length)
        {
            GrowEntries();
        }

        int length = sequence.Length;
        if (_arenaUsed + length > _arena!.Length)
        {
            GrowArena(length);
        }

        sequence.CopyTo(_arena.AsSpan(_arenaUsed, length));

        int index = _count;
        ref Entry entry = ref _entries[index];
        entry.Offset = _arenaUsed;
        entry.Length = length;
        entry.HashCode = hash;

        int bucket = hash & _bucketMask;
        entry.Next = _buckets![bucket];
        _buckets[bucket] = index + 1;

        _arenaUsed += length;
        _count = index + 1;

        if (_count > _bucketCount)
        {
            GrowBuckets();
        }

        return index;
    }

    private void Initialize()
    {
        _bucketCount = NextPowerOfTwo(_minimumCapacity);
        _bucketMask = _bucketCount - 1;
        _buckets = ArrayPool<int>.Shared.Rent(_bucketCount);
        Array.Clear(_buckets, 0, _bucketCount);
        _entries = ArrayPool<Entry>.Shared.Rent(_minimumCapacity);
        _arena = ArrayPool<T>.Shared.Rent(checked(_minimumCapacity * 4));
        _count = 0;
        _arenaUsed = 0;
    }

    private void GrowEntries()
    {
        Entry[] grown = ArrayPool<Entry>.Shared.Rent(_entries!.Length * 2);
        Array.Copy(_entries, grown, _count);
        ArrayPool<Entry>.Shared.Return(_entries);
        _entries = grown;
    }

    private void GrowArena(int additional)
    {
        int required = checked(_arenaUsed + additional);
        int newSize = Math.Max(_arena!.Length * 2, required);
        T[] grown = ArrayPool<T>.Shared.Rent(newSize);
        Array.Copy(_arena, grown, _arenaUsed);
        ArrayPool<T>.Shared.Return(_arena);
        _arena = grown;
    }

    private void GrowBuckets()
    {
        int newCount = _bucketCount * 2;
        int[] grown = ArrayPool<int>.Shared.Rent(newCount);
        Array.Clear(grown, 0, newCount);

        int mask = newCount - 1;
        for (int i = 0; i < _count; i++)
        {
            int bucket = _entries![i].HashCode & mask;
            _entries[i].Next = grown[bucket];
            grown[bucket] = i + 1;
        }

        ArrayPool<int>.Shared.Return(_buckets!);
        _buckets = grown;
        _bucketCount = newCount;
        _bucketMask = mask;
    }

    private static int Hash(ReadOnlySpan<T> sequence)
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(sequence);
        uint hash = FnvOffsetBasis;
        ref byte start = ref MemoryMarshal.GetReference(bytes);
        for (int i = 0; i < bytes.Length; i++)
        {
            hash = (hash ^ Unsafe.Add(ref start, i)) * FnvPrime;
        }

        return (int)hash;
    }

    private static int NextPowerOfTwo(int value)
    {
        int result = 1;
        while (result < value)
        {
            result <<= 1;
        }

        return result;
    }

    private static void ThrowHandleOutOfRange(int handle) =>
        throw new ArgumentOutOfRangeException(nameof(handle), handle, "Handle does not refer to an interned sequence.");

    /// <summary>
    ///  Returns an allocation-free enumerator over the interned sequences in insertion order.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_arena is not null)
        {
            ArrayPool<T>.Shared.Return(_arena);
            _arena = null;
        }

        if (_entries is not null)
        {
            ArrayPool<Entry>.Shared.Return(_entries);
            _entries = null;
        }

        if (_buckets is not null)
        {
            ArrayPool<int>.Shared.Return(_buckets);
            _buckets = null;
        }

        _count = 0;
        _arenaUsed = 0;
        _bucketCount = 0;
        _bucketMask = 0;
    }

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
