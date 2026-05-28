---
name: polyfill-dotnet-api
description: Polyfill a modern .NET BCL API for .NET Framework. Use when asked to "polyfill", "backport", "add a span overload for net472/net481", "make API X available downlevel", or any time a member is missing on net472 and present on net10. Picks the right source (Microsoft-shipped package vs PolySharp source-gen vs hand-rolled `touki/Framework/` polyfill) and captures the recurring design gotchas.
---

# Polyfill a .NET API for .NET Framework

`touki` multi-targets `$(DotNetCoreVersion)` (currently `net10.0`) and
`$(DotNetFrameworkVersion)` (currently `net472`). Files under
[`touki/Framework/`](../../../touki/Framework/) compile **only** for the
.NET Framework target (excluded from the modern build by
[touki.csproj](../../../touki/touki.csproj)). Tests run on `net481` but
exercise the `net472` polyfill assembly.

Work the source list below in order; stop at the first hit. Don't add a
hand polyfill for something a Microsoft package or PolySharp already
ships.

**Related skills:**
[`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) (this skill feeds
into &sect;5 / &sect;6 / &sect;7 there),
[`framework-jit-optimization`](../framework-jit-optimization/SKILL.md)
(net481 RyuJIT shape once hand-rolling),
[`performance-testing`](../performance-testing/SKILL.md) (benchmark
required for any perf trade-off).

## Source preference order

### 1. Microsoft-shipped NuGet packages

Probe before polyfilling: write a tiny `#if NETFRAMEWORK` snippet in
`touki.tests/Framework/` that calls the candidate API and try a `net472`
build. If it compiles, a referenced package already supplies the member;
delete the probe.

| Package | Covers | Referenced |
| --- | --- | --- |
| `System.Memory` | `Span<T>` / `ReadOnlySpan<T>` / `Memory<T>`, base `MemoryExtensions`, `MemoryMarshal`, `Unsafe`, `BinaryPrimitives`, `ArrayPool<T>` | yes |
| `Microsoft.Bcl.Memory` | `Range`, `Index`, newer `MemoryExtensions` (`Count`, `CommonPrefixLength`, `ContainsAnyExcept`, `IsWhiteSpace`) | yes |
| `Microsoft.Bcl.HashCode` | `HashCode` | yes |
| `Microsoft.IO.Redist` | .NET 6 `System.IO` (via `global using Microsoft.IO;`) | yes |
| `Microsoft.Bcl.AsyncInterfaces` | `IAsyncEnumerable<T>`, `IAsyncDisposable`, async-returning interfaces | not yet |
| `Microsoft.Bcl.TimeProvider` | `TimeProvider`, `ITimer` | not yet |
| `Microsoft.Bcl.Numerics` | `Half`, `BigInteger` additions | not yet |

When adding a new package, place the `<PackageReference>` inside the
`Condition="'$(TargetFramework)' == '$(DotNetFrameworkVersion)'"`
ItemGroup - never unconditional.

### 2. PolySharp source-generated polyfills

[PolySharp](https://github.com/Sergio0694/PolySharp) (referenced for the
.NET Framework target only) supplies **language / compiler attributes**,
not runtime types. Use it for `IsExternalInit`, `RequiredMember`,
`CompilerFeatureRequired`, `CallerArgumentExpression`,
`ModuleInitializerAttribute`, `SkipLocalsInit`, and the nullable
attributes (`NotNullWhen`, `MaybeNullWhen`, `MemberNotNull`,
`DoesNotReturn`, ...).

The repo enables:

```xml
<PolySharpUsePublicAccessibilityForGeneratedTypes>true</PolySharpUsePublicAccessibilityForGeneratedTypes>
<PolySharpIncludeRuntimeSupportedAttributes>true</PolySharpIncludeRuntimeSupportedAttributes>
```

Don't hand-write attribute polyfills; PolySharp generates them.

### 3. Hand-rolled polyfill in `touki/Framework/Polyfills/<BclNamespace>/`

Last resort, when a runtime member (instance method, static helper,
ctor) isn't supplied by a package and isn't an attribute. Polyfill only
when there's a real caller; "completeness" polyfills bloat the surface.

- **Folder = BCL namespace, dotted.** `System.Convert` &rarr;
  `touki/Framework/Polyfills/System/ConvertExtensions.cs`.
  `System.Text.Encoding` &rarr;
  `touki/Framework/Polyfills/System.Text/EncodingExtensions.cs`.
  `System.Security.Cryptography.CryptographicOperations` &rarr;
  `touki/Framework/Polyfills/System.Security.Cryptography/CryptographicOperations.cs`.
- **`namespace` matches the folder.** `Polyfills/System/Foo.cs` declares
  `namespace System;`, `Polyfills/System.Text/Foo.cs` declares
  `namespace System.Text;`. Callers reach the polyfill through the same
  `using` they already had for the BCL type.
- **Use C# 14 `extension` blocks** (e.g. `extension(Encoding encoding) { ... }`)
  rather than static-class extension methods. Lookup picks the BCL
  member on modern .NET and the polyfill on net472. See
  [ConvertExtensions.cs](../../../touki/Framework/Polyfills/System/ConvertExtensions.cs).
- **Don't `#if NETFRAMEWORK` inside `touki/Framework/`** - the
  whole folder is already framework-only.
- **Touki-specific helpers (not polyfills)** live in
  `touki/Framework/Touki/...` with `Touki.*` namespaces. If your file
  isn't shadowing a public modern .NET BCL member, it doesn't go under
  `Polyfills/`.

## Design rules every hand polyfill must satisfy

### Behavior parity

- Match the BCL exception type **and** message family
  (`ArgumentNullException` vs `NullReferenceException` is an observable
  diff; same for span "destination too short" cases).
- Read modern .NET docs / reference source for edge cases
  (empty / null / zero-length-destination / overflow inputs).
- For overridable members (`Random.NextBytes`, `Encoding.GetBytes`),
  only take a fast path when `typeof(BaseType) == obj.GetType()`;
  subclasses dispatch through the virtual member. See
  [RandomExtensions.NextBytes](../../../touki/Framework/Polyfills/System/RandomExtensions.cs).
- Stateful types (`HashCode`, `Random`): match the documented contract,
  not observable cross-runtime output. `HashCode` is process-local.

### Allocations

The point of a span overload is to skip the array overload's
allocation. Allocating a temp `T[]` to delegate to the BCL throws that
benefit away. **Default allocation-free even at 5-15% throughput
cost**; document the trade-off in `<remarks>`. Use existing helpers
before inventing new ones:

- **`stackalloc` + [`BufferScope<T>`](../../../touki/Touki/Buffers/BufferScope.cs)**
  for small bounded buffers that may grow into `ArrayPool<T>`.
- **`ArrayPool<T>.Shared.Rent` / `Return`** for unbounded buffers
  (clear on return only when `T` contains references); see
  [`ArrayPoolList<T>`](../../../touki/Touki/Collections/ArrayPoolList.cs).
- **Pinned write into `new string('\0', length)`** for `string`-returning
  methods (`Convert.ToHexString`, `string.Concat(ROS<char>...)`).
- **Pinned `unsafe` pass-through to BCL `T*` overloads** when no span
  variant exists; see
  [EncodingExtensions](../../../touki/Framework/Polyfills/System.Text/EncodingExtensions.cs).
  Watch the empty-span null-pinnable foot-gun (Gotchas &sect;1).
- **`MemoryMarshal.AsBytes` / `AsRef` / `GetReference`** for zero-copy
  reinterpretation.

### Reflecting into BCL internals

**Last-resort tool. Never write reflection-based polyfill code without
explicit user approval.** The default answer is "allocate instead."

Consider asking only when both hold:

- Win is **substantial** (order of magnitude, or removes a per-call
  allocation from a documented hot path).
- No supported alternative exists (no Microsoft package, no
  `MemoryMarshal`/`Unsafe` route, no `extension`-blockable surface).

When asking, surface: which BCL member, the perf/alloc delta vs the
supported alternative, the fragility (private members can change in
servicing updates), and any security/trust implications. If approved,
isolate in one internal helper, cache `MethodInfo`/`FieldInfo` once,
and comment which BCL surface is targeted and why.

### Argument validation

- `ArgumentNullException.ThrowIfNull(arg)` - the polyfill at
  [`ArgumentNullExtensions`](../../../touki/Framework/Polyfills/System/ArgumentNullExtensions.cs)
  covers net472.
- `ThrowIfNegative` / `ThrowIfGreaterThan` / etc. when available;
  fall back to `(uint)x > (uint)max`.
- Mirror the BCL exception type. Don't introduce custom exception types.

### Length / overflow

Wrap any sum of input lengths used to size an allocation in `checked()`.
Unchecked overflow allocates the wrong-sized buffer and surfaces failure
later from `CopyTo`.
[`StringExtensions.Concat`](../../../touki/Framework/Polyfills/System/StringExtensions.cs)
is the reference pattern.

### Coexistence with future BCL polyfills

C# 14 `extension` blocks add members to an existing type, not new types.
A future Microsoft `Convert.ToHexString` on net472 wins lookup over the
extension; the polyfill goes silently inert. No `extern alias` needed.

The exception is when the polyfill defines a **new type** in `System.*`
(e.g.
[CryptographicOperations](../../../touki/Framework/Polyfills/System.Security.Cryptography/CryptographicOperations.cs)).
A future Microsoft polyfill for that exact type collides (CS0436/CS0433)
and callers need `extern alias`. The recipe and the list of new-type
polyfills lives in
[docs/polyfill-layout.md](../../../docs/polyfill-layout.md). Prefer
extension members over new `System.*` types when feasible.

Touki's stated commitment is to **defer to Microsoft-shipped polyfills
when they ship**: a future Touki release will reference the official
package and remove (or thin-forward) the duplicate type, so callers
upgrade automatically. See
[docs/polyfill-layout.md](../../../docs/polyfill-layout.md) for the
user-facing version of that policy.

## Gotchas seen in past PRs

The [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) checklist
enforces these; this section explains *why*.

1. **Empty-span pinning yields `null`.**
   `MemoryMarshal.GetReference(default(ROS<T>))` is a null ref, so
   `fixed (T* p = ...)` produces a null pointer, and net481 BCL `T*`
   overloads typically throw `ArgumentNullException` instead of the
   canonical "destination too short". Handle empty source / empty
   destination separately before pinning. See
   [EncodingExtensions.GetBytes](../../../touki/Framework/Polyfills/System.Text/EncodingExtensions.cs).

2. **`[AggressiveInlining]` + `Unsafe.As<T, narrower>(ref param)`
   compares against the wrong constant on net481.** When the caller
   passes a literal of a *signed* primitive narrower than `int`
   (`sbyte`, `short`), C# `int`-promotion sign-extends it (`(sbyte)-1`
   becomes `0xFFFFFFFF`). If the called method is
   `[AggressiveInlining]` and reinterprets the parameter via
   `Unsafe.As<T, byte>(ref oldValue)` to compare against bytes loaded
   with `movzx`, RyuJIT propagates the *int-promoted* constant into
   the compare immediate (`cmp ecx, 0xFFFFFFFF`) instead of the
   requested byte (`0xFF`). The `movzx`-loaded byte is in `[0, 0xFF]`
   so the compare is *unconditionally false* - the loop runs
   silently doing nothing in Release. Debug passes (no inlining).
   Confirmed by disassembly in
   [ReplaceUnsafeAsPerf](../../../touki.perf/ReplaceUnsafeAsPerf.cs).

   **Fix:** explicitly mask in the int domain so RyuJIT must fold the
   high bits to zero. The cast alone is not enough - the JIT's
   constant tracker doesn't model the `conv.u1` IL op as truncating
   to `[0, 0xFF]` here. Use:

   ```csharp
   byte oldByte = (byte)(Unsafe.As<T, byte>(ref oldValue) & 0xFF);
   ushort oldShort = (ushort)(Unsafe.As<T, ushort>(ref oldValue) & 0xFFFF);
   ```

   See [SpanExtensions.Replace.cs](../../../touki/Framework/Polyfills/System/SpanExtensions.Replace.cs)
   for the in-place version, and the
   [ReplaceUnsafeAsPerf](../../../touki.perf/ReplaceUnsafeAsPerf.cs)
   benchmark for the disassembly proof. The unsigned cases (`byte`,
   `ushort`, `char`) are unaffected because their int-promoted form
   already has the upper bits zero. Tests on signed inputs are
   essential - symmetric tests that only use `byte`/`char` will
   not catch this.

3. **`Random.NextBytes(byte[])` is native on net481; the obvious
   managed span loop is slower per byte** (loses to `byte[]` + copy by
   ~1.2&times; on big buffers despite saving the alloc). Use a pinned
   pointer loop and only on the type-exact fast path; subclasses go
   through the array overload. See
   [RandomExtensions](../../../touki/Framework/Polyfills/System/RandomExtensions.cs).

4. **`HashCode` is process-local.** No cross-runtime parity contract;
   only assert within-process determinism.

5. **`net472` vs `net481` phrasing.** Polyfill assembly TFM is `net472`
   (`$(DotNetFrameworkVersion)`); test TFM is `net481` for richer GC
   APIs but consumes the `net472` polyfill. Don't call the polyfill
   "net481-only" in commits or PR descriptions.

## Workflow

1. Probe to confirm the API is missing; pick the highest source from
   the order above that supplies it.
2. If hand-rolling, place under `touki/Framework/<BclNamespace>/` using
   `extension(...)` blocks (only declare a new type when the BCL
   surface itself is a brand-new type).
3. Add tests under [`touki.tests/`](../../../touki.tests/) running on
   both TFMs. Identical observable behavior to modern BCL for matching
   inputs.
4. Consult [`dotnet/runtime`](https://github.com/dotnet/runtime) tests
   for additional edge cases. Location pattern is `src/libraries/<AreaName>/tests/...`.
5. Run [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) and get user approval before
   invoking [`create-pr`](../create-pr/SKILL.md).

## Examples in this repo

| API | Source | File |
| --- | --- | --- |
| `Span<T>.IndexOf` / `Contains` | `System.Memory` | - |
| `HashCode` | `Microsoft.Bcl.HashCode` | - |
| `IsExternalInit`, `CallerArgumentExpression` | PolySharp | - |
| `ROS<T>.StartsWith(T)` (single element) | hand | [SpanExtensions.StartsEndsWith.cs](../../../touki/Framework/Polyfills/System/SpanExtensions.StartsEndsWith.cs) |
| `Convert.ToHexString` / `FromHexString` | hand | [ConvertExtensions.cs](../../../touki/Framework/Polyfills/System/ConvertExtensions.cs) |
| `Random.NextBytes(Span<byte>)` | hand | [RandomExtensions.cs](../../../touki/Framework/Polyfills/System/RandomExtensions.cs) |
| `string.Concat(ROS<char>...)` | hand | [StringExtensions.cs](../../../touki/Framework/Polyfills/System/StringExtensions.cs) |
| `HashCode.AddBytes(ROS<byte>)` | hand | [HashCodeExtensions.cs](../../../touki/Framework/Polyfills/System/HashCodeExtensions.cs) |
| `CryptographicOperations.FixedTimeEquals` | hand | [CryptographicOperations.cs](../../../touki/Framework/Polyfills/System.Security.Cryptography/CryptographicOperations.cs) |
| `Encoding.GetBytes(ROS<char>, Span<byte>)` ... | hand | [EncodingExtensions.cs](../../../touki/Framework/Polyfills/System.Text/EncodingExtensions.cs) |
