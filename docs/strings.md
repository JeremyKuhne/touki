# Reducing String Allocations with Touki

String creation is one of the most frequently executed operations in many .NET
programs. Every time a string is built or modified, a new instance is allocated
and old instances eventually need to be reclaimed by the garbage collector.

On modern .NET platforms (from .NET 6 onward) the compiler rewrites interpolated
strings into a lower-level representation using **interpolated string handlers** -
see *String Interpolation in C# 10 and .NET 6*
([.NET Blog](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/)).
Benchmarks published in that post show a ~40% throughput improvement and about a
five-fold reduction in memory allocation compared with `string.Format`.

For developers who need to target .NET Framework 4.8 or earlier, these
improvements are not available because the framework lacks the built-in
interpolated-string handler and many of the supporting APIs. The **Touki** library
bridges that gap by providing a default interpolated string handler and polyfills
for .NET Framework 4.7.2 and later.

**Touki** also provides additional high-performance text utilities on **both**
.NET 10 **and** .NET Framework 4.7.2 and later, so applications can use
lower-allocation string handling while still supporting older frameworks.

## Why reducing allocations matters

Strings in .NET are immutable. Operations such as `string.Concat` and
`string.Format` materialize result strings. `StringBuilder` mutates its own
buffer, but growing that buffer can allocate additional arrays and `ToString()`
materializes the final string. Frequent allocations lead to:

* **Garbage-collection pressure** - short-lived strings can quickly accumulate to
  significant weight on the GC.
* **Hidden boxing** - `string.Format` boxes value-type arguments into an
  `object[]` array and creates the array itself (see the .NET Blog post above),
  generating unnecessary heap activity.
* **Parsing costs** - `string.Format` interprets the composite format string at
  run time, so when the format is not known until run time, compile-time parsing
  and optimized paths are unavailable.

## Touki's approach

Touki (登器) provides low-allocation interpolated-string support for .NET
Framework 4.7.2 and additional helpers for all supported targets. Touki ports
portions of the .NET runtime under the MIT license and augments them with extra
functionality. On .NET 10 it defers to the built-in handler; on .NET Framework
4.7.2 it provides its own implementation.

### `ValueStringBuilder`: the core string builder

`ValueStringBuilder` is a `ref struct` that starts with caller-supplied storage,
typically a small stack buffer, and rents pooled byte-backed storage when it
grows (see [`ValueStringBuilder.cs`](../touki/Touki/Text/ValueStringBuilder.cs)).
It also serves as an **interpolated-string handler** so helper methods can accept
it directly. Based on the `ValueStringBuilder` .NET uses internally, you can now
leverage it for performance-critical scenarios.

Touki's polyfilled `DefaultInterpolatedStringHandler` for .NET 4.7.2
([source](../touki/Framework/Polyfills/System.Runtime.CompilerServices/DefaultInterpolatedStringHandler.cs))
wraps a `ValueStringBuilder`. On .NET Framework the compiler targets interpolated
strings to this handler, providing low-allocation formatting similar to newer
runtimes.

### `StringExtensions`: lower-cost `Format` methods

The static `StringExtensions` class augments `string.Format`-style formatting.
`FormatValue<T>` accepts one unmanaged argument, while `FormatValues` accepts
Touki `Value` arguments to avoid boxing heterogeneous primitive values (see
[`StringExtensions.cs`](../touki/Touki/Text/StringExtensions.cs)). Internally it
builds the result with a `ValueStringBuilder` and a modified version of the
runtime's `StringBuilder.AppendFormatHelper`
([`ValueStringBuilder.Formatting.cs`](../touki/Touki/Text/ValueStringBuilder.Formatting.cs))
that:

1. Uses a small stack-allocated span for formatting value types,
2. Avoids the internal `ISpanFormattable` interface that doesn't exist on .NET Framework,
3. Uses Touki's `Value` struct to skip boxing,
4. Works with `ReadOnlySpan<char>` and `ReadOnlySpan<Value>` so neither the format string nor argument array allocates.

```csharp
using Touki.Text;

// ...

string fmt = "{0} - {1:F2}";
double num = 3.14159;

// No boxing for the int or double; only the result string is materialized.
string result = string.FormatValues(fmt, 42, num);
```

### `StringSegment`: efficient substring handling

[`StringSegment`](../touki/Touki/Text/StringSegment.cs) wraps a section of an
existing string in a normal (non-ref) struct that can be stored off the stack:

```csharp
string csv = "apple,banana,cherry";
StringSegment full = new(csv);
int comma = full.IndexOf(',');
StringSegment first = full[..comma]; // "apple"

// or iterate via

StringSegment right = full;
while (right.TrySplit(',', out StringSegment left, out right))
{
    // left will be "apple", "banana", "cherry" in each iteration
}
```

### `Value` struct: variant values without boxing

Touki's [`Value`](../touki/Touki/Value.cs) struct
holds primitive, nullable, and enum types without boxing. `string.FormatValues`
overloads take `Value` to avoid boxing even when argument types vary:

```csharp
string fmt = "{0} - {1} - {2}";
string result = string.FormatValues(fmt, 1, 2.5, "three"); // "1 - 2.5 - three"
```
For fully supported types there are implicit conversions to `Value`.
`Value.Create<T>()` creates values for all other types. All enums are supported,
but do not have implicit conversions.

### `Stream` and `TextWriter` extensions

`StreamExtensions` and `TextWriterExtensions` add handler-backed `WriteFormatted`
overloads that format an interpolated value through `ValueStringBuilder`-backed
storage ([`StreamExtensions.cs`](../touki/Touki/Io/StreamExtensions.cs) and
[`TextWriterExtensions.cs`](../touki/Touki/Io/TextWriterExtensions.cs)). Unit tests
demonstrate the pattern
([`StreamExtensionsTests.cs`](../touki.tests/Touki/StreamExtensionsTests.cs)):

```csharp
using Touki.Io;

string name = "Touki";
Version version = new(1, 0);
using MemoryStream stream = new();
using StringWriter textWriter = new();

stream.WriteFormatted($"Library: {name}, Version: {version}");

textWriter.WriteFormatted($"Library: {name}, Version: {version}");
```

The handler writes directly to a `Stream`, an exact `StreamWriter`, or an exact
`StringWriter`, so those paths do not create an intermediate result string. A
custom `TextWriter` instead receives a string through its virtual `Write(string)`
method, preserving overrides and their side effects. On .NET, a separate
optimization overload accepts an existing `string`; that overload is not available
on .NET Framework.

The `Stream` overload writes raw UTF-16 code-unit bytes without applying a text
encoding or byte-order mark. Use a `TextWriter` such as `StreamWriter` when the
target is an encoded text file or protocol.

### Related text views and comparers

[`StringSpan`](../touki/Touki/Text/StringSpan.cs) provides a stack-only view over
string data when the slice does not need to be stored, while `StringSegment` is a
normal struct that can live in fields and collections.
[`StringSegmentComparer`](../touki/Touki/Text/StringSegmentComparer.cs) provides
ordinal and ordinal-ignore-case equality and ordering for `StringSegment` values.

## Bringing modern interpolation to .NET Framework 4.7.2

C# 10 lets you define **custom interpolated-string handlers**. Touki supplies
`DefaultInterpolatedStringHandler` and `AssertInterpolatedStringHandler`
([AssertInterpolatedStringHandler.cs](../touki/Framework/Polyfills/System.Diagnostics/AssertInterpolatedStringHandler.cs)).
The former is the special class C# looks for to implement interpolated strings.
The latter provides low-allocation cross-compiled assertions in the `Debugging`
class:

```csharp
// Works on *both* .NET 10 and .NET Framework 4.7.2
Debugging.Assert(count == 0, $"The count should be 0, but is {count}.");
```

Touki ports span number formatting from modern .NET to the .NET Framework 4.7.2 build to allow zero allocation number formatting.

## See also

- [Low-Allocation Collections](collections.md) for `ArrayPoolList<T>` and friends.
- [Buffers, Span Readers, and Span Writers](buffers.md) for `BufferScope<T>`, `SpanReader<T>`, and `SpanWriter<T>`.
- [IO Helpers](io.md) for `WriteFormatted` on `Stream` and `TextWriter`.
