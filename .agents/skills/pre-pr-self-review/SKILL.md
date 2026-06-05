---
name: pre-pr-self-review
description: Self-review checklist before opening a PR. Use before invoking `create-pr`, when reviewing your own draft, or when a reviewer flags issues that should have been caught earlier. Codifies recurring mistakes from this repo's polyfill work: missing tests for new public surface, unchecked length sums, null-pointer foot-guns from `MemoryMarshal.GetReference` on empty spans, drift from `ArgumentNullException.ThrowIfNull` and `checked()` conventions, TFM phrasing errors, and stale PR descriptions.
metadata:
  portability: semi-portable
---

# Pre-PR self-review

Run this checklist before invoking the [`create-pr`](../create-pr/SKILL.md)
skill. Each item is a question your code or PR body must answer. Update
the skill whenever a reviewer flags something not yet listed.

**Related skills:**

- [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) - the
  source-preference and design rules for adding a new polyfill that
  this checklist then validates.
- [`create-pr`](../create-pr/SKILL.md) - the workflow this checklist
  precedes.
- [`address-pr-feedback`](../address-pr-feedback/SKILL.md) - the
  follow-up workflow that re-runs this checklist after review comments.
- [`performance-testing`](../performance-testing/SKILL.md) - benchmark
  authoring required when a perf claim drives a code change.
- [`framework-jit-optimization`](../framework-jit-optimization/SKILL.md)
  - net481 RyuJIT tradeoffs cited in the polyfill-correctness items.
- [`agent-files-review`](../agent-files-review/SKILL.md) - for any
  changes under `.agents/`, `AGENTS.md`, or `.github/copilot-instructions.md`.
- [`security-review`](../security-review/SKILL.md) - the
  security-specific subset (abusive-input handling, length / integer
  overflow, allocation and algorithmic DoS, argument validation, and
  every use of `unsafe` / `Unsafe.*` / `MemoryMarshal.*` /
  `Marshal.*` or any BCL API whose docs say "unsafe" or "caller
  must"). Invoke alongside this checklist for any change that adds
  or modifies a member accepting caller-supplied data, or any change
  that touches one of those caller-validated constructs - the
  common case, not a niche.

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
  `ushort`/`uint`/`long`/`ulong` - each has independent ref
  reinterpretation.
- Security-sensitive APIs (`FixedTimeEquals`, hex decode): cover equal,
  differing-content, length mismatch, both empty, one empty, and a long
  span where only the last byte differs.
- Allocating APIs (`Concat`, `ToHexString`): include an
  `OverflowException` test on the length sum.

Test hygiene for the tests themselves - the recurring miss list
that costs the most review rounds on coverage-only PRs:

- **Test method names start with the method under test.**
  `MethodName_StateUnderTest_ExpectedBehavior` (see
  [tests.instructions.md](../../../.github/instructions/tests.instructions.md)).
  `ReadOnlySpan_Empty_ReturnsEmpty` is *wrong*;
  `SliceAtNull_ReadOnlySpan_Empty_ReturnsEmpty` is right.
- **Every `IDisposable` test local uses `using` or `try`/`finally`.**
  `TempFolder`, `IEnumerationMatcher`, anything returned by
  `MSBuildMatchBuilder.FromSpecification`, `ArrayPoolList<T>` -
  a bare local leaks the resource when an assertion fails. See
  the "Disposables in test bodies" section in `tests.instructions.md`
  for the pattern when the test itself exercises explicit `Dispose()`.
- **Don't hard-code `InvariantCulture` for APIs that use
  `CurrentCulture`.** Touki's provider-less formatting helpers
  (`string.FormatValue`, `ValueStringBuilder.AppendFormat` without a
  provider, etc.) format with `CurrentCulture`. Asserting against
  `InvariantCulture`-formatted strings makes the test locale-dependent.
  See the "Culture-sensitive assertions" section in
  `tests.instructions.md`.

## 2. Polyfill / framework correctness

For any change under `touki/Framework/`, walk the
[polyfill-correctness.md](polyfill-correctness.md) items:

- Empty / null spans handled before `unsafe` interop (empty source,
  empty destination, both empty; exception type cross-checked).
- Multi-input length sums wrapped in `checked()`.
- Throw helpers use the standard BCL exceptions, not custom types.
- Span overloads stay allocation-free by default (document any
  trade-off in `<remarks>`).
- Behavior parity with the modern BCL (exception type and message
  family, edge cases, type-exact fast paths).
- Performance claims name the JIT (net481 RyuJIT vs modern .NET RyuJIT)
  and are measured or explicitly marked unmeasured.

If the change is not under `touki/Framework/`, skip to &sect;3.

## 3. PR description matches reality

- TFM phrasing: the polyfill TFM is `$(DotNetFrameworkVersion)` =
  `net472`. The test project's `net481` TFM is just where it runs. Do
  not call the polyfill "net481-only".
- File list, test counts, and perf numbers all reflect the *current*
  diff. Re-run after every commit; do not paste numbers from an earlier
  iteration.
- **Walk each bullet of the description against the diff before
  pushing.** If the body says "covers `Foo` with cases A/B/C", search
  the diff for tests named `Foo_…` and confirm A, B, and C are all
  there. PR #141 lost a round to a description that claimed a
  `Span<char>` "null at end" case and a `MatchAnyDirectory` double-
  dispose test, neither of which was actually in the diff.
- "Deliberately deferred" entries match what's actually absent from
  the working tree.

## 4. Final audit before staging

- `git status --short` - delete leftover probe / scratch files;
  confirm every listed file belongs in the change set.
- `git diff --check` - whitespace.
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
  inlining surfaces bugs Debug doesn't - e.g.
  `[AggressiveInlining]` + `Unsafe.As<T, byte>(ref param)` propagates
  the caller's int-promoted argument into the comparison immediate
  (`cmp ecx, 0xFFFFFFFF` instead of `cmp ecx, 0xFF`) for negative
  signed-primitive inputs on net481, but only in Release. Mask
  explicitly with `& 0xFF` / `& 0xFFFF`. See
  [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) and
  [`polyfill-correctness.md`](polyfill-correctness.md).
- Stage **by path**, never `git add -A` / `git add .` when the working
  tree spans more than one logical change. If topics are intermingled,
  ask before staging.

## 5. Failing CI is a stop, not a sprint

When a build / test / CI run fails on a PR:

1. Diagnose.
2. Prepare the fix in the working tree.
3. Describe what changed, why, and any risks.
4. **Stop and wait for explicit approval before commit/push.**

Stacked rapid-fire fix commits are how perf regressions and unrelated
sweep-ups get into history.

## 6. Update this skill

If a reviewer flags something not in this checklist, add it. If the
review touched `.agents/` files, also update via the
[`agent-files-review`](../agent-files-review/SKILL.md) workflow so the
validator and CI mirror stay in sync.

## Sub-pages

- [polyfill-correctness.md](polyfill-correctness.md) - the deep
  per-item detail for &sect;2 (empty-span pinning, `checked()` sums,
  throw helpers, allocation-free strategies, BCL parity, JIT-naming),
  with the code patterns and reference files.
