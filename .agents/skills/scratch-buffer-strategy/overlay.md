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
- The bundled [references/arraypool-performance.md](references/arraypool-performance.md)
  is the single canonical copy of this data. touki no longer keeps a separate
  `docs/` copy; the repo README, AGENTS.md, and the sibling perf docs all point
  at the vendored reference. Do not re-add a `docs/` duplicate - extend this
  overlay (or push changes upstream to the commons core) instead.

## Updating

Pull upstream changes to the core (and its `references/`) with
`gh skill update scratch-buffer-strategy` (review the diff, re-pin). Keep
touki-specific additions in this file, not in the core.
