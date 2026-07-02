# Design rules every hand polyfill must satisfy

Detail for the [polyfill-dotnet-api](SKILL.md) skill. Applies once the
[source-selection.md](source-selection.md) order has landed you on a
hand-rolled polyfill under `touki/Framework/Polyfills/`.

## Behavior parity

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

## Allocations

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
  Watch the empty-span null-pinnable foot-gun ([gotchas.md](gotchas.md) &sect;1).
- **`MemoryMarshal.AsBytes` / `AsRef` / `GetReference`** for zero-copy
  reinterpretation.

## Reflecting into BCL internals

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

## Argument validation

- `ArgumentNullException.ThrowIfNull(arg)` - the polyfill at
  [`ArgumentNullExtensions`](../../../touki/Framework/Polyfills/System/ArgumentNullExtensions.cs)
  covers net472.
- `ThrowIfNegative` / `ThrowIfGreaterThan` / etc. when available;
  fall back to `(uint)x > (uint)max`.
- Mirror the BCL exception type. Don't introduce custom exception types.

## Length / overflow

Wrap any sum of input lengths used to size an allocation in `checked()`.
Unchecked overflow allocates the wrong-sized buffer and surfaces failure
later from `CopyTo`.
[`StringExtensions.Concat`](../../../touki/Framework/Polyfills/System/StringExtensions.cs)
is the reference pattern.

## Coexistence with future BCL polyfills

C# 14 `extension` blocks add members to an existing type, not new types.
A future Microsoft `Convert.ToHexString` on net472 wins lookup over the
extension; the polyfill goes silently inert. No `extern alias` needed.

The exception is when the polyfill defines a **new type** in `System.*`
(e.g.
[CryptographicOperations](../../../touki/Framework/Polyfills/System.Security.Cryptography/CryptographicOperations.cs)).
A future Microsoft polyfill for that exact type collides (CS0436/CS0433)
and callers need `extern alias`. The recipe and the list of new-type
polyfills lives in
[references/polyfill-layout.md](references/polyfill-layout.md). Prefer
extension members over new `System.*` types when feasible.

Touki's stated commitment is to **defer to Microsoft-shipped polyfills
when they ship**: a future Touki release will reference the official
package and remove (or thin-forward) the duplicate type, so callers
upgrade automatically. See
[references/polyfill-layout.md](references/polyfill-layout.md) for the
user-facing version of that policy.
