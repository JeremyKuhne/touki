---
name: polyfill-dotnet-api
description: Polyfill a modern .NET BCL API for .NET Framework by hand-rolling it under `touki/Framework/`. Use when asked to "polyfill", "backport", "add a span overload for net472/net481", "make API X available downlevel", or any time a member is missing on net472 and present on net10. The authoring counterpart to the vendored `dotnet-polyfills` skill (which surveys *which* Microsoft package or PolySharp generator supplies a member); this skill covers the source-preference order, the behavior-parity design rules, and the recurring net472/net481 gotchas.
compatibility: Uses the microsoft-learn MCP server to confirm modern BCL API shapes and edge cases when available; falls back to fetching learn.microsoft.com and the dotnet/runtime reference source otherwise.
metadata:
  portability: repo-specific
---

# Polyfill a .NET API for .NET Framework

`touki` multi-targets `$(DotNetCoreVersion)` (currently `net10.0`) and
`$(DotNetFrameworkVersion)` (currently `net472`). Files under
[`touki/Framework/`](../../../touki/Framework/) compile **only** for the
.NET Framework target (excluded from the modern build by
[touki.csproj](../../../touki/touki.csproj)). Tests run on `net481` but
exercise the `net472` polyfill assembly.

**Related skills:**
[`dotnet-polyfills`](../dotnet-polyfills/SKILL.md) (the vendored survey of
*which* Microsoft package or PolySharp generator supplies a member - consult it
for steps 1-2 of the source order below),
[`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) (this skill feeds
into its tests / allocation / parity items),
[`framework-jit-optimization`](../framework-jit-optimization/SKILL.md)
(net481 RyuJIT shape once hand-rolling),
[`performance-testing`](../performance-testing/SKILL.md) (benchmark
required for any perf trade-off).

## Source preference order (stop at the first hit)

Don't add a hand polyfill for something a Microsoft package or PolySharp
already ships. The vendored [`dotnet-polyfills`](../dotnet-polyfills/SKILL.md)
skill is the full survey of steps 1-2 (which package or generator supplies a
member); [source-selection.md](source-selection.md) keeps the touki-specific
bindings and the hand-rolled folder/namespace rules.

1. **Microsoft-shipped NuGet package.** Probe first: write a tiny
   `#if NETFRAMEWORK` snippet in `touki.tests/Framework/`, build `net472`,
   and if it compiles a referenced package already supplies the member.
   `System.Memory`, `Microsoft.Bcl.Memory`, `Microsoft.Bcl.HashCode`, and
   `Microsoft.IO.Redist` are already referenced.
2. **PolySharp source-gen** for compiler / language *attributes*
   (`IsExternalInit`, `CallerArgumentExpression`, `SkipLocalsInit`, the
   nullable attributes). Never hand-write these.
3. **Hand-rolled polyfill** under
   `touki/Framework/Polyfills/<BclNamespace>/`, only when a real caller
   needs a runtime member no package or attribute supplies. Folder = BCL
   namespace dotted; `namespace` matches the folder; use C# 14 `extension`
   blocks; no `#if NETFRAMEWORK` inside the already-framework-only tree.

## Design rules for a hand polyfill

When step 3 applies, the polyfill must satisfy the rules in
[design-rules.md](design-rules.md): behavior parity (exception type *and*
message family, edge cases, type-exact fast paths), **allocation-free by
default even at a 5-15% throughput cost**, `checked()` on any length sum
that sizes an allocation, standard throw helpers (no custom exception
types), no reflection into BCL internals without explicit user approval,
and coexistence with a future Microsoft polyfill (prefer `extension`
members over new `System.*` types). The recurring net481 / empty-span /
TFM-phrasing foot-guns are in [gotchas.md](gotchas.md).

## Workflow

1. **Confirm the API is missing and learn its exact shape.** When the
   microsoft-learn MCP server is available, query it for the modern API
   signature, exception contract, and edge-case behavior; otherwise fetch
   the learn.microsoft.com page and the
   [`dotnet/runtime`](https://github.com/dotnet/runtime) reference source
   (`src/libraries/<AreaName>/...`). Pick the highest source from the
   order above that supplies it.
2. If hand-rolling, place under `touki/Framework/Polyfills/<BclNamespace>/`
   using `extension(...)` blocks (only declare a new type when the BCL
   surface itself is a brand-new type). Apply [design-rules.md](design-rules.md).
3. Add tests under [`touki.tests/`](../../../touki.tests/) running on both
   TFMs. Identical observable behavior to the modern BCL for matching
   inputs. Consult `dotnet/runtime` tests (`src/libraries/<AreaName>/tests/...`)
   for additional edge cases - via the microsoft-learn MCP server when
   available, otherwise the GitHub source.
4. Run [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) and get user
   approval before invoking [`create-pr`](../create-pr/SKILL.md).

## Sub-pages

- [source-selection.md](source-selection.md) - the package table, the
  PolySharp attribute list, and the hand-rolled folder / namespace rules.
- [design-rules.md](design-rules.md) - behavior parity, allocation
  strategies, argument validation, `checked()` sums, reflection policy,
  and coexistence with future BCL polyfills.
- [gotchas.md](gotchas.md) - empty-span pinning, the net481 `Unsafe.As`
  sign-extension bug, native `Random.NextBytes`, process-local `HashCode`,
  and net472-vs-net481 phrasing.

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
