# Touki overlay - filtrace

Repo-specific companion to the vendored [filtrace](SKILL.md) skill. The `SKILL.md`
is a **pinned copy** of the agent skill shipped by the standalone
[JeremyKuhne/filtrace](https://github.com/JeremyKuhne/filtrace) trace analyzer (see
the `metadata.github-*` provenance in `SKILL.md`'s frontmatter). filtrace is a
*tool-shipped skill*: its canonical home is the tool's own repo (single-sourced
from filtrace `docs/`), not the
[agent-skills commons](https://github.com/JeremyKuhne/agent-skills). Do not
hand-edit the core - re-vendor it from filtrace instead. Everything
touki-specific lives here.

## How touki consumes filtrace

filtrace ships as published NuGet packages; touki uses both heads:

- **MCP server** - registered in [.vscode/mcp.json](../../../.vscode/mcp.json) as
  `dnx KlutzyNinja.Filtrace.Mcp`, exposing the fifteen `trace_*` tools an agent
  calls directly. No clone or build required.
- **CLI** - `dotnet tool install -g KlutzyNinja.Filtrace`, then `filtrace <verb>`.

The skill body's "full reference" links point at filtrace's `docs/workflow.md`
and `docs/traps.md` as absolute `https://github.com/JeremyKuhne/filtrace` URLs:
the load-bearing verb and trap catalogs are embedded in the skill body, so those
links are supplementary and resolve from anywhere.

The skill body also links four bundled scripts by *relative* path
(`scripts/Capture-BenchmarkTrace.ps1`, `scripts/Capture-ProjectTrace.ps1`,
`scripts/Open-SpeedscopeTrace.ps1`, `scripts/Open-PerfettoTrace.ps1`), so
[scripts/](scripts/) is vendored here verbatim alongside `SKILL.md` (and
`README.md`) - these are filtrace's own generic capture-then-analyze and
viewer-opener wrappers (any BenchmarkDotNet project or executable project),
distinct from touki's own
[tools/Capture-EtwTrace.ps1](../../../tools/Capture-EtwTrace.ps1) (a touki-specific
net481 ETW wrapper, kept separately below) and touki's own
[tools/Open-SpeedscopeTrace.ps1](../../../tools/Open-SpeedscopeTrace.ps1) /
[tools/Open-PerfettoTrace.ps1](../../../tools/Open-PerfettoTrace.ps1) (touki-specific
viewer launchers wired into its BenchmarkDotNet artifact layout).

## Cross-references (touki side)

- [`performance-testing`](../performance-testing/SKILL.md) - the touki skill that
  *delegates* trace-driving to filtrace. It owns the touki-specific half: how to
  capture a benchmark trace (`-p EP --keepFiles`, where the symbols build is),
  the EventPipe-vs-ETW attribution divergence, and reading the line ranking. For
  the filtrace verb/tool reference and trap catalog it points here.
- [profiling.md](../performance-testing/profiling.md) and
  [graphical-viewers.md](../performance-testing/graphical-viewers.md) - the touki
  capture recipes and viewer launchers that drive filtrace.
- [tools/Capture-EtwTrace.ps1](../../../tools/Capture-EtwTrace.ps1) - the touki
  net481 ETW capture wrapper that prints scoped `filtrace` next-step commands.

## Touki note - a concrete Trap #8 hit

The core's **Trap #8** already covers this: filtrace folds JIT-helper thunks
(`memmove`, write-barriers, GC-poll) into their managed caller **by default**, so
its ranking does not surface the artifact below - only a *raw / unfolded*
EventPipe view (or a third-party viewer) does. Recorded here as the touki data
point behind that trap: a pre-filtrace `CpuSampling` trace of touki's extglob
enumeration read `System.Buffer.BulkMoveWithWriteBarrier` at 93% inclusive - a
sampling artifact that really belonged to the two engine loop bodies calling it.
The sharp tell: a write-barrier variant over a **ref-free** struct is impossible
(it needs `RuntimeHelpers.IsReferenceOrContainsReferences<T>()`), so it cannot be
a real call; attribute it to the caller with `callers`. Full writeup:
[docs/dotnet-perf-discoveries.md](../../../docs/dotnet-perf-discoveries.md)
section 8.

## Updating

Re-vendor from filtrace when its skill changes: copy both
`.agents/skills/filtrace/SKILL.md` **and** its `scripts/` subfolder from the
filtrace repo (the relative links in the body only resolve if `scripts/` is
present here too), re-add this provenance block (bump `github-pinned` /
`github-tree-sha` to the new commit or tag), and keep touki-specific notes here,
not in the core. When filtrace cuts a release whose package carries the updated
skill, pin to that tag (`github-ref: refs/tags/vX.Y.Z`, `github-pinned: vX.Y.Z` -
a tag pin uses the tag name, not a commit SHA) rather than a branch/commit. After
re-vendoring, run `tools/Validate-AgentFiles.ps1` and
`tools/Test-AgentFileLinks.ps1` - the latter catches a missed `scripts/` copy
immediately (dangling relative links).
