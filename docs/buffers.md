# Buffers, Span Readers, and Span Writers

Touki's buffer-related types in [`touki/Touki/Buffers/`](../touki/Touki/Buffers/)
and reader/writer types in [`touki/Touki/Io/`](../touki/Touki/Io/) provide a
small set of stack-friendly APIs for working with `Span<T>` and
`ReadOnlySpan<T>`. They're available on .NET 10 and .NET Framework 4.7.2.

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

[`SpanReader<T>`](../touki/Touki/Io/SpanReader.cs) is a
`ref struct` over a `ReadOnlySpan<T>` modeled on `SequenceReader<T>`. It
constrains `T : unmanaged, IEquatable<T>`, which lets it read primitives,
chars, bytes, and small structs:

```csharp
using Touki.Io;

ReadOnlySpan<byte> payload = [0x01, 0x03, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0xCC];
SpanReader<byte> reader = new(payload);

if (reader.TryRead(out byte tag)
    && reader.TryRead<int>(out int length)
    && length >= 0)
{
    if (reader.TryRead(length, out ReadOnlySpan<byte> body))
    {
        Console.WriteLine($"Tag {tag}: {body.Length} bytes");
    }
}
```

Highlights:

* `TryRead`, `TryReadTo`, `TryPeek`, `Advance`,
  `AdvancePast` - the standard reader surface.
* `Position`, `Length`, `Unread`, `End` for inspection.
* `TryRead<TValue>(out TValue)` reads any other unmanaged value type
  out of a `byte` reader via a checked reinterpret.
* [`SpanReaderExtensions`](../touki/Touki/Io/SpanReaderExtensions.cs)
  adds higher-level helpers (e.g. `TryReadPositiveInteger` on a
  `SpanReader<char>`).

## `SpanWriter<T>`

[`SpanWriter<T>`](../touki/Touki/Io/SpanWriter.cs) is the symmetric
type for writing into a `Span<T>` with `T : unmanaged`:

```csharp
using Touki.Io;

ReadOnlySpan<byte> payload = [0x02, 0x03];
Span<byte> destination = stackalloc byte[64];
SpanWriter<byte> writer = new(destination);

if (writer.TryWrite((byte)0x01)
    && writer.TryWrite(payload))
{
    ReadOnlySpan<byte> written = destination[..writer.Position];
    Console.WriteLine($"Wrote {written.Length} bytes");
}
```

`TryWrite` returns `false` instead of throwing when there isn't room, so
callers can fall back to a larger buffer (often a `BufferScope<T>`)
without exception overhead.

## `SpanExtensions`

[`SpanExtensions`](../touki/Touki/Buffers/SpanExtensions.cs) adds
helpers for searching, ordinal comparison, replacement, sorting, line
enumeration, null-terminated slicing, and other common operations on
`Span<T>` and `ReadOnlySpan<T>`. For downlevel targets, the `Split(...)` /
`SpanSplitEnumerator<T>` polyfill lives in
[`System.SpanExtensions`](../touki/Framework/Polyfills/System/SpanExtensions.SpanSplitEnumerator.cs),
so the same `foreach (Range range in span.Split(...))` code compiles on
.NET 10 and .NET Framework 4.7.2.

## `RunLengthEncoder`

[`RunLengthEncoder`](../touki/Touki/Buffers/RunLengthEncoder.cs) provides a
simple span-based byte run-length codec. The format stores each run as a
one-byte count followed by the repeated byte; runs longer than 255 bytes are
split across pairs.

Use `GetEncodedLength` or `GetDecodedLength` to size a destination, then call
`TryEncode` or `TryDecode`. The `Try` methods return `false` when the destination
is too small, and `TryDecode` also rejects an odd-length count/value stream.
