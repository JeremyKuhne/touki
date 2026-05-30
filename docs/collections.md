# Low-Allocation Collections in Touki

Touki ships a small family of collection types under
[Touki.Collections](../touki/Touki/Collections/) aimed at scenarios where
the BCL `List<T>`, `LinkedList<T>` or `ConcurrentBag<T>` either allocate
more than necessary or don't fit the access pattern. They all live on
.NET 10 and .NET Framework 4.7.2.

## Overview

| Type | When to reach for it |
| --- | --- |
| [`ListBase<T>`](../touki/Touki/Collections/ListBase.cs) | Abstract base that implements `IList<T>`, `IReadOnlyList<T>`, and non-generic `IList` once so concrete lists only implement the interesting members. |
| [`ContiguousList<T>`](../touki/Touki/Collections/ContiguousList.cs) | `ListBase<T>` for lists backed by contiguous storage (exposes `AsSpan`-style access for derived types). |
| [`ArrayBackedList<T>`](../touki/Touki/Collections/ArrayBackedList.cs) | Concrete base for lists backed by a `T[]`; subclasses override how the array is rented and returned. |
| [`ArrayList<T>`](../touki/Touki/Collections/ArrayList.cs) | Plain `T[]`-backed list (no pooling). |
| [`ArrayPoolList<T>`](../touki/Touki/Collections/ArrayPoolList.cs) | `T[]`-backed list that rents from `ArrayPool<T>.Shared` and returns the buffer on `Dispose`. |
| [`SingleOptimizedList<TItem, TList>`](../touki/Touki/Collections/SingleOptimizedList.cs) | Stores a single item inline; promotes to `TList` (e.g. `ArrayPoolList<T>`) only when a second item is added. |
| [`SinglyLinkedList<T>`](../touki/Touki/Collections/SinglyLinkedList.cs) | Minimal singly-linked list used internally by `RefCountedCache<,,>`; useful when you need cheap front/back inserts without a doubly-linked layout. |
| [`SequenceSet<T>`](../touki/Touki/Collections/SequenceSet.cs) | Hash set of variable-length `ReadOnlySpan<T>` sequences of unmanaged values, interned into one pooled arena with no per-sequence allocation. For deduplicating or memoizing short value-type sequences. See [sequence-set.md](sequence-set.md). |
| [`Cache<T>`](../touki/Touki/Collections/Cache.cs) | Fixed-size, thread-safe object pool with a per-thread fast slot. For pooling reusable workers, parsers, builders, etc. |
| [`RefCountedCache<TValue, TCacheEntryData, TKey>`](../touki/Touki/Collections/RefCountedCache.cs) | Cache that hands out scoped, ref-counted handles to expensive resources (GDI objects, native handles, `Pen`/`Brush`/`Font`-style objects). |
| [`EmptyList<T>`](../touki/Touki/Collections/EmptyList.cs) | Singleton empty `IList<T>`/`IReadOnlyList<T>`. |

`ListBase<T>` and friends require `T : notnull`; nulls are rejected at the
boundary. Pooled lists such as `ArrayPoolList<T>` (and
`SingleOptimizedList<TItem, TList>` once it has promoted to a pooled
`TList`) return rented buffers to `ArrayPool<T>.Shared` on `Dispose`;
non-pooled lists such as `ArrayList<T>` simply drop their references and
let the GC reclaim the backing array.

## `ArrayPoolList<T>`

A drop-in replacement for `List<T>` whose backing array is rented from
`ArrayPool<T>.Shared`:

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
`Environment.ProcessorCount * 4`. `T` must have a public parameterless
constructor; `Acquire` falls back to `new T()` when the cache is empty.

## `RefCountedCache<TValue, TCacheEntryData, TKey>`

For caching expensive or constrained resources (GDI handles, native
objects, large buffers). Consumers get a `Scope` that ref-counts the
underlying entry and releases it on `Dispose`:

```csharp
public sealed class PenCache : RefCountedCache<Pen, Color, Color>
{
    protected override CacheEntry CreateEntry(Color key, bool cached)
        => new PenCacheEntry(key, cached);

    protected override bool IsMatch(Color key, CacheEntry entry)
        => key == entry.Data;

    private sealed class PenCacheEntry : CacheEntry
    {
        private readonly Pen _pen;
        public PenCacheEntry(Color color, bool cached) : base(color, cached)
            => _pen = new Pen(color);
        public override Pen Object => _pen;
    }
}

PenCache pens = new();

using (RefCountedCache<Pen, Color, Color>.Scope scope = pens.GetEntry(Color.Red))
{
    Pen pen = scope; // implicit conversion
    DrawWith(pen);
}
```

When the last `Scope` for an entry is disposed and the entry isn't
cached, the underlying object is released.
