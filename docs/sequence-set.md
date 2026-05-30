# `SequenceSet<T>` - an allocation-free set of value-type sequences

[`SequenceSet<T>`](../touki/Touki/Collections/SequenceSet.cs) is a hash set whose
elements are variable-length sequences of unmanaged values (`ReadOnlySpan<T>`).
It interns each distinct sequence into a single pooled arena and refers to it by
a stable integer handle, so deduplicating or membership-testing a large number
of short sequences costs no per-sequence heap allocation. It targets both
.NET 10 and .NET Framework 4.7.2.

## The gap it fills

The BCL cannot express `HashSet<ReadOnlySpan<T>>`: `ReadOnlySpan<T>` is a
`ref struct` and cannot be used as a generic type argument. The usual
workarounds each cost an allocation per stored element:

| Workaround | Per-element cost |
| --- | --- |
| `HashSet<T[]>` | one array on the heap per distinct sequence |
| `HashSet<string>` (for `char`) | one string on the heap per distinct sequence |
| `HashSet<ReadOnlyMemory<T>>` | a backing array per distinct sequence plus a custom comparer |

`SequenceSet<T>` copies each new sequence's elements into one shared, pooled
arena and records a `(offset, length)` entry, so the only allocations are the
amortized growth of a few pooled arrays - and those buffers come from
`ArrayPool<T>.Shared` and are returned on `Dispose`.

## When to reach for it

- **Memoizing visited states** in a backtracking or graph search where each
  state is serialized to a short run of `int`/`byte`/`char`. This is the
  motivating use case (the extglob failure memo in
  [`CompiledGlobStrategy.ExtGlob.cs`](../touki/Touki/Io/Globbing/CompiledGlobStrategy.ExtGlob.cs)):
  the matcher records every entry state proven not to match so the same subtree
  is never re-explored, converting exponential backtracking into polynomial work.
- **Interning tokens, n-grams, or small keys** where the same short sequence of
  value types recurs many times and you want a single canonical copy plus an
  integer id.
- **Deduplicating** a stream of short value-type sequences without paying for a
  heap object per candidate.

If your elements are reference types, or you need an ordered/indexed list rather
than set semantics, prefer [`ArrayPoolList<T>`](collections.md) or the BCL
collections instead.

## API

```csharp
public sealed class SequenceSet<T> : DisposableBase
    where T : unmanaged, IEquatable<T>
{
    public SequenceSet();
    public SequenceSet(int minimumCapacity);

    public int Count { get; }
    public ReadOnlySpan<T> this[int handle] { get; }

    public bool Add(ReadOnlySpan<T> sequence);
    public bool Add(ReadOnlySpan<T> sequence, out int handle);
    public bool Contains(ReadOnlySpan<T> sequence);
    public void Clear();

    public Enumerator GetEnumerator();   // ref struct, yields ReadOnlySpan<T>
    public void Dispose();
}
```

```csharp
using SequenceSet<int> visited = new();

Span<int> state = stackalloc int[3];
// ... fill state ...

if (visited.Add(state))
{
    // first time we have seen this state - do the expensive work
}
else
{
    // already visited - skip
}
```

The `T : unmanaged` constraint lets the arena be pooled without clearing and
hashed over its raw bytes; the `IEquatable<T>` constraint backs the exact
`SequenceEqual` comparison that resolves hash collisions.

### Interfaces

The natural element type is `ReadOnlySpan<T>`, which cannot be a generic type
argument, so `SequenceSet<T>` deliberately does **not** implement
`ICollection<T>` or `IEnumerable<T>`. It derives from `DisposableBase` (so the
pooled buffers are returned exactly once, even under a racing or double
`Dispose` - returning a rented array to `ArrayPool<T>` twice is corrupting) and
exposes an allocation-free `ref struct` `Enumerator` so it can be used in a
`foreach` that yields each interned sequence as a span:

```csharp
foreach (ReadOnlySpan<int> sequence in visited)
{
    // ...
}
```

## How it works

Storage is split across a few parallel pooled arrays, all rented lazily on the
first `Add` (an unused set never rents anything):

- **Arena** (`T[]`): every interned sequence's elements stored back to back.
- **Entries** (`Entry[]`): for each interned sequence its `Offset` and `Length`
  into the arena, its cached hash code, and the one-based index of the next
  entry in the same bucket chain.
- **Buckets** (`int[]`, power-of-two length): an open hash index mapping
  `hash & mask` to the head of a separately-chained entry list.

`Add` hashes the candidate (FNV-1a over the raw bytes), walks the bucket chain
comparing cached hash codes and then `SequenceEqual` against the arena slice,
and on a miss appends the elements to the arena and links a new entry. The
handle returned is simply the entry index, which is stable until `Clear` or
`Dispose`. The bucket table doubles and rehashes (using the cached hash codes,
so the arena is never reread) when the entry count exceeds the bucket count,
keeping the average chain length near one.

Failure-only correctness note for the memoization use case: because the cached
hash is only a probe and the final comparison is an exact `SequenceEqual`, a
hash collision can never cause two different sequences to be treated as equal.

## Complexity

Let `n` be the number of distinct sequences interned and `k` the length of the
sequence in a given operation.

| Operation | Time | Notes |
| --- | --- | --- |
| `Add` / `Contains` (hit or miss) | O(k) average | O(k) to hash and to `SequenceEqual` the candidate; O(1) expected bucket-chain length |
| `Add` worst case | O(k + n) | only on a pathological all-collisions chain |
| `Add` amortized growth | O(k) | arena, entry, and bucket arrays each double; total copying is O(total elements) and O(n) amortized to O(1) per add |
| `this[handle]` | O(1) | direct entry lookup, returns a span over the arena |
| `Clear` | O(buckets) | zeroes the bucket table; arena and entries are reused |
| `Dispose` | O(1) | returns the three rented arrays to the pool |

## Memory

- **One pooled arena** holds all element data contiguously. For `N` total
  elements across all distinct sequences the arena is `O(N)` elements of `T`,
  rounded up to the next `ArrayPool` bucket and doubled on growth.
- **Two parallel `int`/`Entry` arrays** of length `O(n)` hold the per-entry
  metadata and bucket heads.
- **Zero per-sequence managed allocation**: no `T[]`, `string`, or boxed object
  is created per interned sequence. Adding a sequence that is already present
  allocates nothing at all.
- All backing arrays are rented from `ArrayPool<T>.Shared` /
  `ArrayPool<int>.Shared` / `ArrayPool<Entry>.Shared` and returned on `Dispose`.
  Because `T` is `unmanaged`, the arena is returned without clearing.

## Prior art

The design mirrors internal collections across the .NET ecosystem that intern
variable-length data without per-item allocation:

- **Roslyn [`StringTable`](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/InternalUtilities/StringTable.cs)** -
  interns strings directly from `ReadOnlySpan<char>` through an open-addressing
  table; the closest analogue to this type.
- **Roslyn [`SegmentedHashSet<T>`](https://github.com/dotnet/roslyn/blob/main/src/Dependencies/Collections/Internal/SegmentedHashSet%601.cs) /
  [`SegmentedArray<T>`](https://github.com/dotnet/roslyn/blob/main/src/Dependencies/Collections/SegmentedArray%601.cs)** -
  large hash sets that avoid the Large Object Heap by segmenting their backing
  storage; same bucket/entry chaining layout.
- **[`System.Reflection.Metadata.BlobBuilder`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/src/System/Reflection/Metadata/BlobBuilder.cs)** -
  accumulates variable-length blobs into pooled segments rather than one array
  per blob.
- **[`System.Collections.Generic.HashSet<T>`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/HashSet.cs)** -
  the `buckets[]` + `entries[]` (with cached hash code and `next` chain) layout
  used here is the same shape the BCL set uses internally.
- **[`string.Intern`](https://learn.microsoft.com/dotnet/api/system.string.intern) /
  [`ArrayPool<T>`](https://learn.microsoft.com/dotnet/api/system.buffers.arraypool-1)** -
  the canonical "one canonical copy" and "rent, don't allocate" patterns this
  type combines.
