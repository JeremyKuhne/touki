---
name: pre-pr-self-review
description: Self-review checklist for changes before opening a pull request. Use when about to call the `create-pr` skill, when reviewing your own draft, or when a reviewer flags issues that should have been caught earlier. Catches the recurring mistakes seen in this repo's polyfill work: missing tests for newly added public surface, unchecked length arithmetic, null-pointer foot-guns from `MemoryMarshal.GetReference` on empty spans, drift from project-wide conventions (`ArgumentNullException.ThrowIfNull`, `checked()`, etc.), TFM phrasing errors, and stale claims in the PR description.
---

# Pre-PR self-review

Run this checklist on every set of changes before invoking the `create-pr`
skill. Treat each item as a question you must answer in code or in the PR body
&mdash; do not invoke `create-pr` until you can. Update this skill whenever
review feedback exposes a new recurring mistake.

The goal is to catch the mistakes that have already cost a review round-trip
in this repo, listed below in roughly the order they tend to appear.

## 1. Public surface has tests, and they exercise every branch

For every new `public` (or `internal` with `InternalsVisibleTo`) member added
in this change set:

- Search the test projects for the symbol name. If there are no hits, the test
  is missing.
- For polyfills (anything under `touki/Framework/`), the test must run on both
  TFMs: tests that only make sense for the polyfill implementation (e.g.
  subclass-fallback paths, null-receiver guards) belong inside
  `#if NETFRAMEWORK`. The cross-runtime cases must run on both.
- For methods with a runtime type-check fast path
  (`if (typeof(T) == obj.GetType())` and similar), there must be at least one
  test for the fast path *and* one test for the slow path / subclass override.
- For generic primitive specializations (`typeof(T) == typeof(byte)` chains),
  every specialized branch needs a test. Asserting only on `byte`, `char`, and
  `int` is not enough &mdash; `bool`, `sbyte`, `short`, `ushort`, `uint`,
  `long`, `ulong` each have their own ref reinterpret + `Unsafe.Add` scaling
  that can break independently.
- For security-sensitive APIs (`FixedTimeEquals`, hashing, hex decoding),
  cover: equal inputs, differing-content inputs, length mismatch, both empty,
  one empty, and a long-span case where a single-byte difference at the end
  is detected.
- For methods that allocate (`Concat`, `ToHexString`, etc.), include an
  `OverflowException` test on the length arithmetic where applicable
  (see &sect;3).

## 2. Empty / null inputs to `unsafe` interop are explicitly handled

`MemoryMarshal.GetReference(default(ReadOnlySpan<T>))` returns a null
reference, and `fixed (T* p = &nullRef)` produces a null pointer. When that
pointer is forwarded to a BCL `T*` overload, the BCL frequently throws
`ArgumentNullException` or `NullReferenceException` instead of the canonical
"destination too short" / "source empty" exception that the span-based BCL
overloads on modern .NET produce.

Before pinning a span:

- If both source and destination are empty, return early with the documented
  zero-length result.
- If only the destination is empty but the source is not, pass a non-null
  stack pointer with length 0 (`byte stack = 0; return Foo(src, &stack, 0);`)
  so the BCL emits the canonical "too short" exception.
- If only the source is empty, return the documented empty result without
  pinning either span.
- Cross-check the resulting exception type against the modern .NET BCL span
  overload. Different exception types are an observable behavior diff
  reviewers will flag.

## 3. Multi-input length arithmetic is `checked()`

Any time a public API sums lengths (`a.Length + b.Length + c.Length`) before
allocating, the addition must be `checked()`. An unchecked `int` overflow
silently wraps to a small positive value, the allocation succeeds at the
wrong size, and the failure surfaces from a downstream `CopyTo` instead of
from the length calculation itself. Pre-existing `Touki.Text.StringExtensions`
already uses `checked` &mdash; new code should match.

A unit test that constructs bogus-length spans over a single pinned element
(never read) and asserts `OverflowException` is the canonical pattern; see
[touki.tests/System/StringExtensionsConcatTests.cs](../../../touki.tests/System/StringExtensionsConcatTests.cs).

## 4. Throw helpers and exception throws

- All argument-null guards use `ArgumentNullException.ThrowIfNull(arg)`. Do
  not write `if (x is null) throw new ArgumentNullException(nameof(x))`.
  This applies to both `net10.0` and `net472` &mdash; the polyfill in
  [touki/Framework/Touki/Exceptions/ArgumentNullExtensions.cs](../../../touki/Framework/Touki/Exceptions/ArgumentNullExtensions.cs)
  covers the older target.
- Range checks should use the corresponding `ThrowIfNegative` /
  `ThrowIfGreaterThan` / etc. helpers when they exist, falling back to
  `(uint)x > (uint)max` patterns only if a polyfill doesn't.
- New custom exception types are almost never the right answer in a polyfill;
  prefer the BCL exception types the modern API documents.

## 5. PR description matches reality

Before generating the PR body, re-read it against the actual diff:

- TFM phrasing: the polyfill assembly's TFM is `$(DotNetFrameworkVersion)`,
  currently `net472`. The test project happens to set its TFM to `net481`,
  so test output mentions `net481`. Do not call the polyfill "net481-only"
  in the PR description.
- File list: only mention files that are actually in the commit. Run
  `git diff --name-only origin/main..HEAD` and reconcile.
- Test counts: re-run `dotnet test` after the final commit and use those
  numbers. Stale counts from an earlier iteration are misleading.
- Performance claims: only assert numbers that were actually measured in
  the current state of the code. Avoid copy-pasting numbers from a previous
  run after the implementation changed.
- "Deliberately deferred" entries should match the absence of those types
  in the working tree &mdash; if a previous draft contained a `GuidExtensions`
  file that was reverted, the description should not list a Guid table row.

## 6. Behavior parity with the modern BCL

For every polyfilled API:

- Read the modern .NET reference source / docs for edge cases (empty input,
  null input, length-zero destination, max-length input, exception types).
- Mirror the BCL's exception type *and* exception message family for
  observable cases.
- For `HashCode`, `Random`, and other stateful types, document any
  intentional deviation in `<remarks>` (`HashCode`'s output is process-local
  in the BCL too, so within-process determinism is the only contract that
  matters; cross-runtime equality is not).
- For methods with overrides on derived types (`Random.NextBytes`,
  `Encoding.GetBytes`, etc.), the polyfill must dispatch through the
  overridable instance member when the runtime type is not exactly the
  base type. The fast path applies only when `typeof(T) == obj.GetType()`.

## 7. Performance trade-offs are documented

If a polyfill is slower than the BCL it shadows (e.g. a managed loop vs a
native sampler), say so in `<remarks>` and quantify the overhead. Reviewers
should not have to re-discover that an "optimization" trades CPU for
allocation reduction. If both shapes were benchmarked, keep the benchmark
file under `touki.perf/` so the trade-off can be reproduced.

## 8. File audit

Last sanity passes before staging:

- `git status --short` &mdash; are there leftover probe / scratch files
  (e.g. an empty `*Probe.cs` from a TFM compile experiment)? Delete them.
- `git diff --check` &mdash; whitespace errors.
- Build both TFMs at least once: `dotnet build -f net10.0` and
  `dotnet build -f net472` (or whatever `$(DotNetFrameworkVersion)` is).
- Run `dotnet test` on both TFMs and capture the totals for the PR body.

## 9. Update this skill when reviewers find something new

If a reviewer flags a class of issue that is not already in this checklist,
add it. The point of this skill is to absorb feedback that would otherwise
have to be relearned per PR.
