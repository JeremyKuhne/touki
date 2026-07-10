---
core: pre-pr-self-review
core-pin: v0.10.0
---

# Touki overlay - pre-pr-self-review

Repo-specific companion to the vendored [pre-pr-self-review](SKILL.md) skill. The
`SKILL.md` is a **pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in its frontmatter). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here (and
in `polyfill-correctness.md`, which is a touki overlay sibling, not part of the
vendored payload).

## Cross-references (the core names these skills generically)

- [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) - the source-preference
  and design rules for adding a new polyfill that this checklist validates.
- [`create-pr`](../create-pr/SKILL.md) - the workflow this checklist precedes.
- [`address-pr-feedback`](../address-pr-feedback/SKILL.md) - the follow-up workflow
  that re-runs this checklist after review comments.
- [`performance-testing`](../performance-testing/SKILL.md) - benchmark authoring
  required when a perf claim drives a code change.
- [`framework-jit-optimization`](../framework-jit-optimization/SKILL.md) - net481
  RyuJIT tradeoffs cited in the polyfill-correctness items.
- [`agent-files-review`](../agent-files-review/SKILL.md) - for any changes under
  `.agents/`, `AGENTS.md`, or `.github/copilot-instructions.md`.
- [`security-review`](../security-review/SKILL.md) - the security-specific subset;
  invoke alongside this checklist for any change accepting caller-supplied data or
  touching `unsafe` / `Unsafe.*` / `MemoryMarshal.*` / `Marshal.*`.

## Touki specifics the core refers to generically

- The Framework-only tree the core names generically is `touki/Framework/`; the
  polyfill's target TFM is `$(DotNetFrameworkVersion)` = `net472` (the test
  project's `net481` is just where it runs - do not call a polyfill "net481-only").
- Test conventions (the `MethodName_StateUnderTest_ExpectedBehavior` naming, the
  disposables-in-test-bodies pattern, the culture-sensitive-assertions rule) are in
  [tests.instructions.md](../../../.github/instructions/tests.instructions.md).
- The agent-file link checker the core's &sect;4 names generically is
  [tools/Test-AgentFileLinks.ps1](../../../tools/Test-AgentFileLinks.ps1) (see
  [`agent-files-review`](../agent-files-review/SKILL.md) &sect;7 for `-ChangedOnly`
  and `-Base`).
- Touki utility types referenced in the checklist's test-hygiene items:
  `Touki.Text.ValueStringBuilder`, `Touki.Io.TempFolder`,
  `Touki.Collections.ArrayPoolList<T>`, and the MSBuild matcher handles returned by
  `MSBuildMatchBuilder.FromSpecification`.
- The war-stories the core anonymized were touki review rounds: PR #141 (a PR body
  claiming a `Span<char>` "null at end" case and a `MatchAnyDirectory` double-dispose
  test, neither in the diff) and PR #110 (links broken by a stale base point).

## Polyfill correctness detail (touki overlay sibling)

The deep per-item detail for the core's &sect;2 - empty-span pinning, `checked()`
sums, throw helpers, the allocation-free strategy catalog, BCL parity, JIT-naming -
lives in [polyfill-correctness.md](polyfill-correctness.md). It is **not** part of
the vendored core (it links touki source files under `touki/Framework/`,
`touki/Touki/`, and `touki.tests/`), so it stays a touki overlay sibling that
`gh skill update` leaves untouched. A repo adopting this skill writes its own
equivalent rather than inheriting touki's source links.

## Updating

Pull upstream changes to the core with `gh skill update pre-pr-self-review`
(review the diff, re-pin). Keep touki-specific additions in this file and in
`polyfill-correctness.md`, not in the core.
