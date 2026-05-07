---
name: pre-pr-self-review
description: Self-review checklist before opening a PR. Use before invoking `create-pr`, when reviewing your own draft, or when a reviewer flags issues that should have been caught earlier. Codifies recurring mistakes from this repo's polyfill work: missing tests for new public surface, unchecked length sums, null-pointer foot-guns from `MemoryMarshal.GetReference` on empty spans, drift from `ArgumentNullException.ThrowIfNull` and `checked()` conventions, TFM phrasing errors, and stale PR descriptions.
---

# Pre-PR self-review

Run this checklist before invoking the [`create-pr`](../create-pr/SKILL.md)
skill. Each item is a question your code or PR body must answer. Update
the skill whenever a reviewer flags something not yet listed.

**Related skills:**

- [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) &mdash; the
  source-preference and design rules for adding a new polyfill that
  this checklist then validates.
- [`create-pr`](../create-pr/SKILL.md) &mdash; the workflow this checklist
  precedes.
- [`address-pr-feedback`](../address-pr-feedback/SKILL.md) &mdash; the
  follow-up workflow that re-runs this checklist after review comments.
- [`performance-testing`](../performance-testing/SKILL.md) &mdash; benchmark
  authoring required by &sect;7 when a perf claim drives a code change.
- [`framework-jit-optimization`](../framework-jit-optimization/SKILL.md)
  &mdash; net481 RyuJIT tradeoffs cited in &sect;5 and &sect;7.
- [`agent-files-review`](../agent-files-review/SKILL.md) &mdash; for any
  changes under `.agents/`, `AGENTS.md`, or `.github/copilot-instructions.md`.

## 1. Tests cover every new branch

For each new `public` (or `InternalsVisibleTo`-internal) member:

- Search the test projects for the symbol; no hits = missing test.
- Polyfills under `touki/Framework/`: tests run on both TFMs. Wrap
  polyfill-only paths (subclass fallbacks, null-receiver guards) in
  `#if NETFRAMEWORK`.
- Runtime type-check fast paths (`typeof(T) == obj.GetType()`): test
  the fast path *and* a subclass override.
- Generic primitive specializations: every specialized branch needs a
  test. Don't rely on `byte`/`int` covering `bool`/`sbyte`/`short`/
  `ushort`/`uint`/`long`/`ulong` &mdash; each has independent ref
  reinterpretation.
- Security-sensitive APIs (`FixedTimeEquals`, hex decode): cover equal,
  differing-content, length mismatch, both empty, one empty, and a long
  span where only the last byte differs.
- Allocating APIs (`Concat`, `ToHexString`): include an
  `OverflowException` test on the length sum (see &sect;3).

## 2. Empty / null spans before `unsafe` interop

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

## 3. Multi-input length sums are `checked()`

Any public API summing lengths before allocating wraps the sum in
`checked()`. Unchecked overflow allocates the wrong-sized buffer and
fails later from `CopyTo`. See
[touki.tests/System/StringExtensionsConcatTests.cs](../../../touki.tests/System/StringExtensionsConcatTests.cs)
for the canonical `OverflowException` test pattern.

## 4. Throw helpers

- Null guards use `ArgumentNullException.ThrowIfNull(arg)` &mdash; the
  polyfill at
  [touki/Framework/Polyfills/System/ArgumentNullExtensions.cs](../../../touki/Framework/Polyfills/System/ArgumentNullExtensions.cs)
  covers net472.
- Range checks use `ThrowIfNegative` / `ThrowIfGreaterThan` / etc. when
  available; fall back to `(uint)x > (uint)max`.
- Match the BCL exception type for parity. New custom exception types
  are almost never the right answer in a polyfill.

## 5. Span overloads prefer fewer allocations over raw speed

The whole reason callers reach for a span overload is to avoid the
allocation the array overload makes. A polyfill that allocates a temp
`T[]` to delegate to the BCL has thrown that benefit away.

**Default to allocation-free, even if 5&ndash;15% slower.** Document
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
allocate &mdash; do not reflect into the BCL.

## 6. Behavior parity with the modern BCL

- Read modern .NET docs / reference source for edge cases (empty / null
  inputs, length-zero destination, exception types and message family).
- Mirror the BCL exception type and message family for observable cases.
- For stateful types (`HashCode`, `Random`), document any deviation in
  `<remarks>`. `HashCode` is process-local in the BCL too &mdash;
  within-process determinism is the only contract.
- For overridable members (`Random.NextBytes`, `Encoding.GetBytes`),
  the polyfill's fast path applies only when
  `typeof(T) == obj.GetType()`; subclasses must dispatch through the
  virtual member.

## 7. Performance claims name the JIT and are measured

State which JIT &mdash; **net481 RyuJIT** (no tiered JIT, no PGO, no
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

## 8. PR description matches reality

- TFM phrasing: the polyfill TFM is `$(DotNetFrameworkVersion)` =
  `net472`. The test project's `net481` TFM is just where it runs. Do
  not call the polyfill "net481-only".
- File list, test counts, and perf numbers all reflect the *current*
  diff. Re-run after every commit; do not paste numbers from an earlier
  iteration.
- "Deliberately deferred" entries match what's actually absent from
  the working tree.

## 9. Final audit before staging

- `git status --short` &mdash; delete leftover probe / scratch files;
  confirm every listed file belongs in the change set.
- `git diff --check` &mdash; whitespace.
- **Rebase onto the canonical `main` if the branch trails it.** Use
  `upstream/main` when working from a fork (the canonical repo lives at
  `upstream`), `origin/main` when cloning the canonical repo directly.
  Recently-merged sister PRs may have introduced files this PR
  cross-references; running off a stale base point makes those links look
  broken to the offline lychee check that gates `.agents/**`, `AGENTS.md`,
  and `*.instructions.md` changes. PR #110 lost a review round to exactly
  this.
- For changes that touch `.agents/`, `AGENTS.md`, or `.github/copilot-instructions.md`,
  also run `pwsh tools/Test-AgentFileLinks.ps1` (see
  [`agent-files-review`](../agent-files-review/SKILL.md) &sect;7 for
  options including `-ChangedOnly` and `-Base`).
- Build both TFMs.
- Run `dotnet test` in **both Debug and Release**. Release-mode RyuJIT
  inlining surfaces bugs Debug doesn't &mdash; e.g.
  `[AggressiveInlining]` + `Unsafe.As<T, byte>(ref param)` propagates
  the caller's int-promoted argument into the comparison immediate
  (`cmp ecx, 0xFFFFFFFF` instead of `cmp ecx, 0xFF`) for negative
  signed-primitive inputs on net481, but only in Release. Mask
  explicitly with `& 0xFF` / `& 0xFFFF`. See
  [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) &sect;Gotchas
  and [`framework-jit-optimization/specialization.md`](../framework-jit-optimization/specialization.md).
- Stage **by path**, never `git add -A` / `git add .` when the working
  tree spans more than one logical change. If topics are intermingled,
  ask before staging.

## 10. Failing CI is a stop, not a sprint

When a build / test / CI run fails on a PR:

1. Diagnose.
2. Prepare the fix in the working tree.
3. Describe what changed, why, and any risks.
4. **Stop and wait for explicit approval before commit/push.**

Stacked rapid-fire fix commits are how perf regressions and unrelated
sweep-ups get into history.

## 11. Update this skill

If a reviewer flags something not in this checklist, add it. If the
review touched `.agents/` files, also update via the
[`agent-files-review`](../agent-files-review/SKILL.md) workflow so the
validator and CI mirror stay in sync.
