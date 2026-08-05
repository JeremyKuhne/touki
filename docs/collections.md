# Low-Allocation Collections in Touki

Touki ships a small family of collection types under
[Touki.Collections](../touki/Touki/Collections/) aimed at scenarios where
the caller can avoid backing storage, reuse pooled storage, or use a more
specialized access pattern than the general BCL collections provide. They all
live on .NET 10 and .NET Framework 4.7.2.

## Overview

* [`ListBase<T>`](../touki/Touki/Collections/ListBase.cs) implements `IList<T>`,
    `IReadOnlyList<T>`, and non-generic `IList` for derived list types.
* [`ContiguousList<T>`](../touki/Touki/Collections/ContiguousList.cs) adds
    contiguous, span-style storage access for derived lists.
* [`ArrayBackedList<T>`](../touki/Touki/Collections/ArrayBackedList.cs) is the
    base for `T[]`-backed lists whose subclasses control renting and returning.
* [`ArrayList<T>`](../touki/Touki/Collections/ArrayList.cs) is a non-pooled,
    array-backed list.
* [`ArrayPoolList<T>`](../touki/Touki/Collections/ArrayPoolList.cs) rents its
    backing array from `ArrayPool<T>.Shared` and returns it on `Dispose`.
* [`SingleOptimizedList<TItem, TList>`](../touki/Touki/Collections/SingleOptimizedList.cs)
    stores one item inline and promotes to `TList` when a second item is added.
* [`SinglyLinkedList<T>`](../touki/Touki/Collections/SinglyLinkedList.cs) is a
    minimal singly linked list with inexpensive front and back insertion.
* [`SequenceSet<T>`](../touki/Touki/Collections/SequenceSet.cs) interns unmanaged
    value sequences into one pooled arena. See [sequence-set.md](sequence-set.md).
* [`Cache<T>`](../touki/Touki/Collections/Cache.cs) is a fixed-size, thread-safe
    object pool with a per-thread fast slot.
* [`RefCountedCache<TValue, TCacheEntryData, TKey>`](../touki/Touki/Collections/RefCountedCache.cs)
    hands out scoped, ref-counted access to expensive or constrained resources.
* [`EmptyList<T>`](../touki/Touki/Collections/EmptyList.cs) is a singleton empty
    `IList<T>` / `IReadOnlyList<T>`.

`ListBase<T>` and friends declare `T : notnull`, so nullable analysis warns when
callers use nullable item types. Many mutation methods also reject null at run
time, but the constraint is not a universal runtime check on every mutation
path. Pooled lists such as `ArrayPoolList<T>` (and
`SingleOptimizedList<TItem, TList>` once it has promoted to a pooled
`TList`) return rented buffers to `ArrayPool<T>.Shared` on `Dispose`;
non-pooled lists such as `ArrayList<T>` simply drop their references and
let the GC reclaim the backing array.

## `ArrayPoolList<T>`

An `IList<T>`-compatible collection whose backing array is rented from
`ArrayPool<T>.Shared`. Use it when the caller can own and deterministically
dispose the list:

```csharp
using ArrayPoolList<int> values = new(minimumCapacity: 256);

for (int i = 0; i < 1000; i++)
{
    values.Add(i);
}

int total = 0;
foreach (int value in values)
{
    total += value;
}
```

Disposing the list returns the buffer to the pool. Reference-typed
elements are cleared on `Clear()` so the pool doesn't keep them alive.

## `SingleOptimizedList<TItem, TList>`

Many APIs accept a list but most callers only ever pass one item.
`SingleOptimizedList` keeps that single item inline (no array, no pool
rental) and promotes to a `TList` (typically `ArrayPoolList<T>`) only
when a second item arrives:

```csharp
using SingleOptimizedList<string, ArrayPoolList<string>> matches = new();

matches.Add("first");

// Still inline - no array allocation yet.

if (alsoMatchesSecond)
{
    matches.Add("second");
    // Promoted to ArrayPoolList<string> here.
}
```

On .NET Framework, accessing `Values` or `UnsafeValues` also promotes an inline
item to the backing `TList`, because the downlevel implementation cannot safely
expose a span over the inline field. Modern .NET keeps the item inline for those
accessors.

## `Cache<T>`

`Cache<T>` is a small, fixed-size pool with a `[ThreadStatic]` fast slot,
suitable for reusable worker objects:

```csharp
public sealed class ParserCache : Cache<MyParser>
{
    public ParserCache() : base(cacheSpace: 0) { }
}

ParserCache cache = new();

MyParser parser = cache.Acquire();
try
{
    parser.Parse(input);
}
finally
{
    cache.Release(parser);
}
```

`cacheSpace: 0` (or any value `< 1`) defaults to
`Environment.ProcessorCount * 4`. `T` must be a reference type with a public
parameterless constructor; `Acquire` falls back to `new T()` when the cache is
empty.

When every cache slot is occupied, `Release` disposes the displaced item if it
implements `IDisposable`. Disposing the cache disposes items in its array-backed
slots and the calling thread's fast slot, then rejects subsequent releases. Fast
slots populated on other threads are thread-local and cannot be drained by that
call. The fast slot is static for each closed `Cache<T>` type, so cache instances
on the same thread share it rather than having per-instance isolation.

## `RefCountedCache<TValue, TCacheEntryData, TKey>`

For caching expensive or constrained resources (GDI handles, native
objects, large buffers). Consumers get a `Scope` that ref-counts the
underlying entry and releases it on `Dispose`:

```csharp
using System.IO;
using Touki.Collections;

using StreamCache streams = new();

RefCountedCache<MemoryStream, int, int>.CacheEntry entry = streams.GetEntry(256);
using RefCountedCache<MemoryStream, int, int>.Scope scope = entry.CreateScope();

MemoryStream stream = scope;
stream.WriteByte(0x2A);

public sealed class StreamCache : RefCountedCache<MemoryStream, int, int>
{
    protected override CacheEntry CreateEntry(int capacity, bool cached)
        => new StreamCacheEntry(capacity, cached);

    protected override bool IsMatch(int capacity, CacheEntry entry)
        => capacity == entry.Data;

    private sealed class StreamCacheEntry : CacheEntry
    {
        private readonly MemoryStream _stream;

        public StreamCacheEntry(int capacity, bool cached) : base(capacity, cached)
            => _stream = new MemoryStream(capacity);

        public override MemoryStream Object => _stream;
    }
}
```

When the last `Scope` for an entry is disposed and the entry isn't
cached, the underlying object is released.
