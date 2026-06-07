// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Caching;

/// <summary>
///  A thread-safe, bounded least-recently-used cache: it keeps at most a fixed
///  number of entries, evicting the least-recently-accessed one when a new entry
///  would push it over capacity.
/// </summary>
/// <remarks>
///  <para>
///   The trace store caches parsed traces - each potentially large - so an
///   unbounded cache would grow without limit across a long agent session. This
///   bounds that footprint while keeping the hot traces resident.
///  </para>
///  <para>
///   The value factory runs outside the lock so an expensive load does not
///   serialize every other query. Two threads that miss the same key concurrently
///   may both run the factory; the first to re-acquire the lock wins and both
///   callers receive that single stored instance, so identity is preserved.
///  </para>
/// </remarks>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The cached value type.</typeparam>
internal sealed class LruCache<TKey, TValue>
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly Lock _gate = new();

    // Most-recently-used at the head, least-recently-used at the tail. The map
    // indexes into the recency list for O(1) lookup and reordering.
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _map;
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new();

    /// <summary>
    ///  Initializes a new <see cref="LruCache{TKey, TValue}"/>.
    /// </summary>
    /// <param name="capacity">The maximum number of entries to retain. Must be positive.</param>
    /// <param name="comparer">The key comparer, or <see langword="null"/> for the default.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public LruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(comparer);
    }

    /// <summary>
    ///  The number of entries currently cached.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _map.Count;
            }
        }
    }

    /// <summary>
    ///  Returns the cached value for <paramref name="key"/>, producing and caching
    ///  it with <paramref name="factory"/> on a miss. The accessed entry becomes the
    ///  most-recently-used; a miss that grows the cache past its capacity evicts the
    ///  least-recently-used entry.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Produces the value on a miss.</param>
    /// <returns>The cached or newly produced value.</returns>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>>? hit))
            {
                Touch(hit);
                return hit.Value.Value;
            }
        }

        // Produce the value outside the lock: loads are expensive and must not
        // serialize unrelated queries.
        TValue value = factory(key);

        lock (_gate)
        {
            // Another thread may have inserted this key while we were producing the
            // value; honor theirs so all callers share one instance.
            if (_map.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>>? existing))
            {
                Touch(existing);
                return existing.Value.Value;
            }

            LinkedListNode<KeyValuePair<TKey, TValue>> node = _order.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
            _map[key] = node;

            if (_map.Count > _capacity)
            {
                LinkedListNode<KeyValuePair<TKey, TValue>> lru = _order.Last!;
                _order.RemoveLast();
                _map.Remove(lru.Value.Key);
            }

            return value;
        }
    }

    // Moves a node to the head of the recency list. Caller holds the lock.
    private void Touch(LinkedListNode<KeyValuePair<TKey, TValue>> node)
    {
        _order.Remove(node);
        _order.AddFirst(node);
    }
}
