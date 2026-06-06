# Touki overlay - scratch-buffer-strategy

Repo-specific companion to the vendored [scratch-buffer-strategy](SKILL.md) skill.
The `SKILL.md` and its bundled
[references/arraypool-performance.md](references/arraypool-performance.md) are a
**pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`'s frontmatter). Do not hand-edit the
core or its `references/` - `gh skill update` would flag the drift. Everything
touki-specific lives here instead.

## Cross-references (the core's "Related" section)

- [`performance-testing`](../performance-testing/SKILL.md) - author/run the
  BenchmarkDotNet benchmarks (`StackZeroInitPerf`, `ArrayPoolSeedRentPerf`,
  `ArrayPoolCrossoverPerf`, `BufferScopeOverheadPerf`) that produced the numbers
  in the bundled reference.
- [`framework-jit-optimization`](../framework-jit-optimization/SKILL.md) - net481
  loop tuning once a buffer choice is made.

## Touki types and docs

- The stack-with-pool-fallback wrapper the core names generically is
  [`Touki.Buffers.BufferScope<T>`](../../../touki/Touki/Buffers/BufferScope.cs).
- The benchmarks backing the reference's numbers live in `touki.perf/`
  (`StackZeroInitPerf.cs`, `ArrayPoolSeedRentPerf.cs`, `ArrayPoolCrossoverPerf.cs`,
  `BufferScopeOverheadPerf.cs`).
- To confirm zeroing / pool overhead is actually the hot cost before acting, see
  [docs/performance-investigation.md](../../../docs/performance-investigation.md).

## Note: the touki copy of the reference data

touki also keeps
[docs/arraypool-performance.md](../../../docs/arraypool-performance.md) - the
original, touki-flavored version of the bundled reference, linked from the repo
[README](../../../README.md) and `docs/performance-investigation.md`. The vendored
`references/` copy is the generic (portable) version that travels to other repos;
the `docs/` copy is the touki-facing one. They are near-identical (the doc is
~95 % portable); whether to collapse them into a single canonical copy is a
tracked follow-up, not done in the vendoring change.

## Updating

Pull upstream changes to the core (and its `references/`) with
`gh skill update scratch-buffer-strategy` (review the diff, re-pin). Keep
touki-specific additions in this file, not in the core.
