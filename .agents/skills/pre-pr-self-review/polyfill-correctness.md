# Polyfill correctness items

Detail for the [pre-pr-self-review](SKILL.md) checklist. These items apply to
any change under `touki/Framework/` (a polyfill or a framework-only fast path).
The [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) skill sets the
design rules; this page is the pre-PR verification of them.

## Empty / null spans before `unsafe` interop

`MemoryMarshal.GetReference(default(ReadOnlySpan<T>))` is a null ref.
`fixed (T* p = &nullRef)` produces a null pointer, and BCL `T*` overloads
on net481 typically throw `ArgumentNullException` instead of the canonical
"destination too short" / "source empty" exception the modern span
overloads produce.

Before pinning:

- Both empty &rarr; return the zero-length result without pinning.
- Source non-empty, destination empty &rarr; pass a stack-allocated
  non-null pointer with length 0
  (`byte stack = 0; return Foo(src, &stack, 0);`).
- Source empty &rarr; return the empty result without pinning.
- Cross-check the resulting exception type against the modern BCL.

## Multi-input length sums are `checked()`

Any public API summing lengths before allocating wraps the sum in
`checked()`. Unchecked overflow allocates the wrong-sized buffer and
fails later from `CopyTo`. See
[touki.tests/System/StringExtensionsConcatTests.cs](../../../touki.tests/System/StringExtensionsConcatTests.cs)
for the canonical `OverflowException` test pattern.

## Throw helpers

- Null guards use `ArgumentNullException.ThrowIfNull(arg)` - the
  polyfill at
  [touki/Framework/Polyfills/System/ArgumentNullExtensions.cs](../../../touki/Framework/Polyfills/System/ArgumentNullExtensions.cs)
  covers net472.
- Range checks use `ThrowIfNegative` / `ThrowIfGreaterThan` / etc. when
  available; fall back to `(uint)x > (uint)max`.
- Match the BCL exception type for parity. New custom exception types
  are almost never the right answer in a polyfill.

## Span overloads prefer fewer allocations over raw speed

The whole reason callers reach for a span overload is to avoid the
allocation the array overload makes. A polyfill that allocates a temp
`T[]` to delegate to the BCL has thrown that benefit away.

**Default to allocation-free, even if 5-15% slower.** Document
the trade-off in `<remarks>` so callers understand the choice.

Strategies used elsewhere in this repo (look here before inventing a
new one):

- **`stackalloc` for small bounded buffers.** Always pair with
  [`Touki.Buffers.BufferScope<T>`](../../../touki/Touki/Buffers/BufferScope.cs)
  so the buffer transparently grows into an `ArrayPool<T>` rental if it
  doesn't fit. Usage: `using BufferScope<char> buffer = new(stackalloc char[64]);`
- **`ArrayPool<T>.Shared.Rent` / `Return`** for unbounded buffers. See
  [`Touki.Collections.ArrayPoolList<T>`](../../../touki/Touki/Collections/ArrayPoolList.cs)
  for the rental + clear-on-return pattern (clear only when `T` contains
  references).
- **Pinned write into `new string('\0', length)`.** Allocates one final
  string with no temp `char[]`; see
  [`StringExtensions.Concat`](../../../touki/Framework/Polyfills/System/StringExtensions.cs)
  and `Convert.ToHexString` for the `fixed (char* p = result)` pattern.
- **Pinned `unsafe` pass-through to BCL `T*` overloads** when the BCL
  doesn't expose a span variant. See
  [`EncodingExtensions`](../../../touki/Framework/Polyfills/System.Text/EncodingExtensions.cs).
- **`MemoryMarshal.AsBytes` / `AsRef` / `GetArrayDataReference`** to
  reinterpret without copying.
- **Type-exact runtime check + inline path for the base type**, with
  fallback through the virtual member for subclasses (see
  [`RandomExtensions.NextBytes`](../../../touki/Framework/Polyfills/System/RandomExtensions.cs)).
- **`InternalsVisibleTo` to the test project** so you can test
  internal helpers directly without exposing them in the public API.

If the only way to be allocation-free is to call an `internal` BCL API,
allocate - do not reflect into the BCL.

## Behavior parity with the modern BCL

- Read modern .NET docs / reference source for edge cases (empty / null
  inputs, length-zero destination, exception types and message family).
- Mirror the BCL exception type and message family for observable cases.
- For stateful types (`HashCode`, `Random`), document any deviation in
  `<remarks>`. `HashCode` is process-local in the BCL too -
  within-process determinism is the only contract.
- For overridable members (`Random.NextBytes`, `Encoding.GetBytes`),
  the polyfill's fast path applies only when
  `typeof(T) == obj.GetType()`; subclasses must dispatch through the
  virtual member.

## Performance claims name the JIT and are measured

State which JIT - **net481 RyuJIT** (no tiered JIT, no PGO, no
`EqualityComparer<T>.Default` intrinsic, weaker inlining) vs **modern
.NET RyuJIT** (.NET 6+, tiered, PGO, devirtualizes
`EqualityComparer<T>.Default`). Unqualified "RyuJIT does X" claims are
wrong about half the time on this repo. The
[`framework-jit-optimization`](../framework-jit-optimization/SKILL.md)
skill catalogues which optimizations actually win on net481.

For code changes in `touki/Framework/` driven by a perf claim:

- Add a benchmark in `touki.perf/` per the
  [`performance-testing`](../performance-testing/SKILL.md) skill, *or*
- Include a statement in the commit message, the PR description, or the
  method's `<remarks>` explicitly indicating that no performance
  measurements were conducted.

If a polyfill is slower than the array-taking BCL it shadows, quantify
the overhead in `<remarks>` and keep the benchmark file in `touki.perf/`.
