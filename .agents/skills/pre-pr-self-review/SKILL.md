---
description: Self-review checklist before opening a PR. Use before invoking `create-pr`, when reviewing your own draft, or when a reviewer flags issues that should have been caught earlier. Codifies recurring mistakes from multi-targeted polyfill work - missing tests for new public surface, unchecked length sums, null-pointer foot-guns from `MemoryMarshal.GetReference` on empty spans, drift from `ArgumentNullException.ThrowIfNull` and `checked()` conventions, TFM phrasing errors, and stale PR descriptions.
license: MIT
metadata:
    github-path: skills/pre-pr-self-review
    github-pinned: v0.4.0
    github-ref: refs/tags/v0.4.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: 67efd7e7fdde472bd96326fdf2cb109b1513f759
    portability: semi-portable
name: pre-pr-self-review
---
# Pre-PR self-review

Run this checklist before invoking the `create-pr` skill. Each item is a question
your code or PR body must answer. Update the skill whenever a reviewer flags
something not yet listed.

This skill pairs with several others a consuming repo wires concretely in its
overlay: a `polyfill-dotnet-api` skill (the source-preference and design rules
this checklist validates), `create-pr` (the workflow this precedes),
`address-pr-feedback` (the follow-up that re-runs this checklist),
`performance-testing` (benchmark authoring required when a perf claim drives a
change), `framework-jit-optimization` (net481 RyuJIT tradeoffs cited in the
polyfill-correctness items), `agent-files-review` (for changes under `.agents/`,
`AGENTS.md`, or `.github/copilot-instructions.md`), and `security-review` (the
security-specific subset - abusive-input handling, length / integer overflow,
allocation and algorithmic DoS, argument validation, and every use of `unsafe` /
`Unsafe.*` / `MemoryMarshal.*` / `Marshal.*` or any BCL API whose docs say
"unsafe" or "caller must"). Invoke `security-review` alongside this checklist for
any change that adds or modifies a member accepting caller-supplied data, or that
touches one of those caller-validated constructs - the common case, not a niche.

## 1. Tests cover every new branch

For each new `public` (or `InternalsVisibleTo`-internal) member:

- Search the test projects for the symbol; no hits = missing test.
- Polyfills in the Framework-only tree: tests run on both TFMs. Wrap
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
  `MethodName_StateUnderTest_ExpectedBehavior` per the repo's test
  conventions. `ReadOnlySpan_Empty_ReturnsEmpty` is *wrong*;
  `SliceAtNull_ReadOnlySpan_Empty_ReturnsEmpty` is right.
- **Every `IDisposable` test local uses `using` or `try`/`finally`.**
  A temp-folder helper, a matcher handle, a pooled-list rental - a bare
  local leaks the resource when an assertion fails. Use the
  `try`/`finally` pattern when the test itself exercises explicit
  `Dispose()`.
- **Don't hard-code `InvariantCulture` for APIs that use
  `CurrentCulture`.** Provider-less formatting helpers generally format
  with `CurrentCulture`. Asserting against `InvariantCulture`-formatted
  strings makes the test locale-dependent.

## 2. Polyfill / framework correctness

For any change in the Framework-only tree (a polyfill or a framework-only fast
path), walk these items (a consuming repo may keep the per-item detail and code
patterns in a `polyfill-correctness` overlay companion):

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

If the change is not in the Framework-only tree, skip to &sect;3.

## 3. PR description matches reality

- TFM phrasing: name the polyfill's *target* TFM (the framework target,
  e.g. `net472`) distinctly from the TFM the tests merely *run* on (e.g.
  `net481`). Do not call a `net472`-targeted polyfill "net481-only".
- File list, test counts, and perf numbers all reflect the *current*
  diff. Re-run after every commit; do not paste numbers from an earlier
  iteration.
- **Walk each bullet of the description against the diff before
  pushing.** If the body says "covers `Foo` with cases A/B/C", search
  the diff for tests named `Foo_…` and confirm A, B, and C are all
  there. Review rounds have been lost to descriptions claiming a case
  (a `Span<char>` "null at end", a double-dispose test) that was not
  actually in the diff.
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
  broken to an offline link check that gates `.agents/**`, `AGENTS.md`,
  and `*.instructions.md` changes. A PR once lost a review round to
  exactly this.
- For changes that touch `.agents/`, `AGENTS.md`, or
  `.github/copilot-instructions.md`, also run the repo's agent-file link
  checker (see the `agent-files-review` skill for options, including
  changed-only and base-ref modes).
- Build both TFMs.
- Run the test suite in **both Debug and Release**. Release-mode RyuJIT
  inlining surfaces bugs Debug doesn't - e.g.
  `[AggressiveInlining]` + `Unsafe.As<T, byte>(ref param)` propagates
  the caller's int-promoted argument into the comparison immediate
  (`cmp ecx, 0xFFFFFFFF` instead of `cmp ecx, 0xFF`) for negative
  signed-primitive inputs on net481, but only in Release. Mask
  explicitly with `& 0xFF` / `& 0xFFFF`. See the `polyfill-dotnet-api`
  and `framework-jit-optimization` skills.
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
review touched `.agents/` files, also update via the `agent-files-review`
workflow so the validator and any CI mirror stay in sync.
