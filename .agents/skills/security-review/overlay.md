---
core: security-review
core-pin: v0.10.0
---

# Touki overlay - security-review

Repo-specific companion to the vendored [security-review](SKILL.md) skill. The
`SKILL.md` and its siblings (`principles.md`, `checklist.md`, `unsafe-apis.md`,
`reporting.md`) are a **pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`'s frontmatter). Do not hand-edit the
core - `gh skill update` would flag the drift. Everything touki-specific lives
here instead.

## Cross-references (the core's "Related skills")

- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - the broader self-review
  checklist; run security-review alongside it before any publish.
- [`performance-testing`](../performance-testing/SKILL.md) - use when you need to
  *measure* a worst-case input rather than just bound it with a `Stopwatch`.

## Touki examples for the checklist

Concrete touki references for the generic checklist categories:

- **&sect;1 Length / size of inputs** - the `DefaultMaxPatternLength` /
  `maxPatternLength` opt-out shape lives in
  [touki/Touki/Io/Globbing/GlobSpecification.Factory.cs](../../../touki/Touki/Io/Globbing/GlobSpecification.Factory.cs).
- **`stackalloc` row (unsafe-apis)** - when input could exceed the stack budget,
  use [`BufferScope<T>`](../../../touki/Touki/Buffers/BufferScope.cs) (stack with
  `ArrayPool` fallback) rather than a raw `stackalloc`.

## Touki cross-TFM note for the `Unsafe.As` row

The "older-JIT pitfall" in the `Unsafe.As<TFrom, TTo>` row of
[unsafe-apis.md](unsafe-apis.md) is the documented net481 RyuJIT
`[AggressiveInlining]` + `Unsafe.As<T, byte>(ref param)` sign-extension bug. The
polyfill-side detail and the masking fix are in
[`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md); the regression is pinned
by
[touki.tests/Framework/Regressions/UnsafeAsAggressiveInliningRegressionTests.cs](../../../touki.tests/Framework/Regressions/UnsafeAsAggressiveInliningRegressionTests.cs).

## Updating

Pull upstream changes to the core with `gh skill update security-review` (review
the diff, re-pin). Keep touki-specific additions in this file, not in the core.
If something here turns out to be generally useful, promote it upstream to the
commons instead of leaving it as a local deviation (see the `manage-skills`
update flow).
