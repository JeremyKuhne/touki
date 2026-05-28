# Buffers, Span Readers, and Span Writers

Touki's buffer-related types in [`touki/Touki/Buffers/`](../touki/Touki/Buffers/)
are declared in the `Touki` namespace and provide a small set of
stack-friendly APIs for working with `Span<T>` and `ReadOnlySpan<T>`.
They're available on .NET 10 and .NET Framework 4.7.2.

## `BufferScope<T>`

[`BufferScope<T>`](../touki/Touki/Buffers/BufferScope.cs) is a
`ref struct` that pairs a possibly-stack-allocated initial buffer with
an `ArrayPool<T>.Shared` rental fallback. It's the buffer equivalent of
`ValueStringBuilder`: small workloads stay on the stack, larger ones
spill to the pool, and `Dispose` returns whatever was rented.

```csharp
using BufferScope<char> buffer = new(stackalloc char[64], minimumLength: 64);

// Use as if it were a Span<char>.
buffer[0] = 'H';
buffer[1] = 'i';
ReadOnlySpan<char> view = buffer[..2];
```

Constructors:

| Constructor | Behavior |
| --- | --- |
| `new BufferScope<T>(int minimumLength)` | Always rents from `ArrayPool<T>.Shared`. |
| `new BufferScope<T>(Span<T> initialBuffer)` | Wraps a caller-supplied (typically `stackalloc`) buffer. |
| `new BufferScope<T>(Span<T> initialBuffer, int minimumLength)` | Uses the initial buffer if it's large enough, otherwise rents. |

Call `EnsureCapacity(int, bool copy)` to grow the buffer; it switches to
`ArrayPool<T>` automatically and (optionally) copies existing contents.

## `SpanReader<T>`

[`SpanReader<T>`](../touki/Touki/Buffers/SpanReader.cs) is a
`ref struct` over a `ReadOnlySpan<T>` modeled on `SequenceReader<T>`. It
constrains `T : unmanaged, IEquatable<T>`, which lets it read primitives,
chars, bytes, and small structs:

```csharp
SpanReader<byte> reader = new(payload);

if (reader.TryRead(out byte tag) && reader.TryRead<int>(out int length))
{
    if (reader.TryReadCount(length, out ReadOnlySpan<byte> body))
    {
        Process(tag, body);
    }
}
```

Highlights:

* `TryRead`, `TryReadCount`, `TryReadTo`, `TryPeek`, `Advance`,
  `AdvancePast` - the standard reader surface.
* `Position`, `Length`, `Unread`, `End` for inspection.
* `TryRead<TValue>(out TValue)` reads any other unmanaged value type
  out of a `byte` reader via a checked reinterpret.
* [`SpanReaderExtensions`](../touki/Touki/Buffers/SpanReaderExtensions.cs)
  adds higher-level helpers (e.g. `TryReadPositiveInteger` on a
  `SpanReader<char>`).

## `SpanWriter<T>`

[`SpanWriter<T>`](../touki/Touki/Buffers/SpanWriter.cs) is the symmetric
type for writing into a `Span<T>` with `T : unmanaged`:

```csharp
Span<byte> destination = stackalloc byte[64];
SpanWriter<byte> writer = new(destination);

if (writer.TryWrite((byte)0x01)
    && writer.TryWrite<int>(payload.Length)
    && writer.TryWrite(payload))
{
    Send(destination[..writer.Position]);
}
```

`TryWrite` returns `false` instead of throwing when there isn't room, so
callers can fall back to a larger buffer (often a `BufferScope<T>`)
without exception overhead.

## `SpanExtensions`

[`SpanExtensions`](../touki/Touki/Buffers/SpanExtensions.cs) adds
allocation-free helpers on top of `Span<T>` and `ReadOnlySpan<T>`. For
downlevel targets, the `Split(...)` / `SpanSplitEnumerator<T>` polyfill
lives in
[`System.SpanExtensions`](../touki/Framework/Polyfills/System/SpanExtensions.SpanSplitEnumerator.cs),
so the same `foreach (Range range in span.Split(...))` code compiles on
.NET 10 and .NET Framework 4.7.2.
