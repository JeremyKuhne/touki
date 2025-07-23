# Reducing String Allocations with Touki

String creation is one of the most frequently executed operations in many .NET programs. Every time a string is built or modified a new instance is allocated and old instances eventually need to be reclaimed by the garbage collector.

On modern .NET platforms (from .NET 6 onward) the compiler rewrites interpolated strings into a lower‑level representation using **interpolated string handlers** — see *String Interpolation in C# 10 and .NET 6* ([.NET Blog](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/)). Benchmarks published in that post show a ~40 % throughput improvement and about a five‑fold reduction in memory allocation compared with `string.Format`.

For developers who need to target .NET Framework 4.8 or earlier, these improvements are not available because the framework lacks the built‑in interpolated‑string handler and many of the supporting APIs. The **Touki** library bridges that gap by providing a default interpolated string handler and polyfills for .NET Framework 4.7.2 and later.

**Touki** also provides additional high‑performance text utilities on **both** .NET 9 **and** .NET Framework 4.7.2 and later so you can enjoy performant, lower allocation string handling while still supporting older frameworks.

## Why reducing allocations matters

Strings in .NET are immutable. Every time you use `string.Concat`, `StringBuilder.Append` or `string.Format`, a new string instance is created. Frequent allocations lead to:

* **Garbage‑collection pressure** – short‑lived strings can quickly accumulate to dramatic weight on the GC.
* **Hidden boxing** – `string.Format` boxes value‑type arguments into an `object[]` array and creates the array itself (see the .NET Blog post above), generating unnecessary heap activity.
* **Parsing costs** – `string.Format` interprets the composite format string at run‑time, so when you don’t know the format string until run‑time you miss out on compile‑time parsing or optimized code paths.

## Touki’s approach

Touki (登器) provides low allocation interpolated‑string support for .NET Framework 4.7.2 and a number of additional helpers for *all* .NET versions. Touki ports portions of the .NET runtime under the MIT license and augments them with extra functionality. On .NET 9 it defers to the built‑in handler; on .NET Framework 4.7.2 it provides its own implementation.

### `ValueStringBuilder`: the core string builder

`ValueStringBuilder` is a `ref struct` that builds strings on the stack when small and rents from `ArrayPool<char>` when they grow (see source code ([ValueStringBuilder.cs](https://github.com/JeremyKuhne/touki/blob/main/touki/Touki/Text/ValueStringBuilder.cs))). It also serves as an **interpolated‑string handler** so helper methods can accept it directly. Based on the `ValueStringBuilder` .NET uses internally, you can now leverage it for your performance critical scenarios.

Touki’s polyfilled `DefaultInterpolatedStringHandler` for .NET 4.7.2 ([source](https://github.com/JeremyKuhne/touki/blob/main/touki/Framework/System/Runtime/CompilerServices/DefaultInterpolatedStringHandler.cs)) simply wraps a `ValueStringBuilder`. On .NET Framework the compiler targets interpolated strings to this handler, giving you the same low‑allocation benefits that newer runtimes provide.

### `Strings`: lower‑cost `Format` methods

The static `Strings` class replaces `string.Format`. Its `Format` overloads accept either **unmanaged generic arguments** or **Touki’s `Value` struct** to avoid boxing (see [Strings.cs](https://github.com/JeremyKuhne/touki/blob/main/touki/Touki/Text/Strings.cs)). Internally it builds the result with a `ValueStringBuilder` and a lightly modified version of the runtime’s `StringBuilder.AppendFormatHelper` ([ValueStringBuilder.Formatting.cs](https://github.com/JeremyKuhne/touki/blob/main/touki/Touki/Text/ValueStringBuilder.Formatting.cs)) that:

1. Uses a small stack‑allocated span for formatting value types,
2. Avoids the internal `ISpanFormattable` interface that doesn’t exist on .NET Framework,
3. Uses Touki’s `Value` struct to skip boxing,
4. Works with `ReadOnlySpan<char>` and `ReadOnlySpan<Value>` so neither the format string nor argument array allocates.

```csharp
using Touki;

string fmt = "{0} – {1:F2}";
double num = 3.14159;

// No boxing for either the string or the double, no intermediate strings
string result = Strings.Format(fmt, 42, num);
```

### `StringSegment`: efficient substring handling

`StringSegment` ([source](https://github.com/JeremyKuhne/touki/blob/main/touki/Touki/Text/StringSegment.cs)) wraps a section of an existing string in a normal (non-ref) struct that can be stored off of the stack:

```csharp
string csv = "apple,banana,cherry";
StringSegment full = new(csv);
int comma = full.IndexOf(',');
StringSegment first = full[..comma]; // "apple"

// or iterate via

StringSegment right = full;
while (right.TrySplit(';', out StringSegment left, out right))
{
    // left will be "apple", "banana", "cherry" in each iteration
}
```

### **`StringSpan`** ([source](https://github.com/JeremyKuhne/touki/blob/main/touki/Touki/Text/StringSpan.cs)) wraps either a `ReadOnlySpan<char>` or a `string` as a "span", so APIs can accept both with the ability to extract the original string.

### `Value` struct: variant values without boxing

Touki’s `Value` struct ([source](https://github.com/JeremyKuhne/touki/blob/main/touki/Touki/Value.cs)) holds primitive, nullable and enum types without boxing. `Strings.Format` overloads take `Value` to avoid boxing even when argument types vary:

```csharp
string fmt = "{0} - {1} - {2}";
string result = Strings.Format(fmt, 1, 2.5, "three"); // "1 - 2.5 - three"
```
For fully supported types there are implicit conversions to `Value`. `Value.Create<T>()` creates for all other types. Note that *all* enums are also supported, but do not have implicit converters.

### `Stream` and `StreamWriter` extensions

`StreamExtensions` adds `WriteFormatted` so you can stream interpolated strings without an intermediate allocation ([StreamExtensions.cs](https://github.com/JeremyKuhne/touki/blob/main/touki/Touki/Io/StreamExtensions.cs)). Unit tests demonstrate the pattern ([StreamExtensionsTests.cs](https://github.com/JeremyKuhne/touki/blob/main/touki.tests/Touki/StreamExtensionsTests.cs)):

```csharp
using MemoryStream stream = new();
stream.WriteFormatted($"Library: {name}, Version: {version}");

textWriter.WriteFormatted($"Library: {name}, Version: {version}")
```

The builder writes directly to the stream buffer, so no extra string is created.

## Bringing modern interpolation to .NET Framework 4.7.2

C# 10 lets you define **custom interpolated‑string handlers**. Touki supplies `DefaultInterpolatedStringHandler` and `AssertInterpolatedStringHandler` ([AssertInterpolatedStringHandler.cs](https://github.com/JeremyKuhne/touki/blob/main/touki/Framework/System/Diagnostics/AssertInterpolatedStringHandler.cs)). The former is the special class C# looks for to implement interpolated strings. The latter is used to provide a low allocation cross compiled assertions in the `Debugging` class:

```csharp
// Works on *both* .NET 9 and .NET Framework 4.7.2
Debugging.Assert(count == 0, $"The count should be 0, but is {count}.");
```

Touki ports span number formatting from .NET 6 to the .NET Framework 4.7.2 build to allow zero allocation number formatting.