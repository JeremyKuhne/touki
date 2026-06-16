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
  `dnx KlutzyNinja.Filtrace.Mcp`, exposing the thirteen `trace_*` tools an agent
  calls directly. No clone or build required.
- **CLI** - `dotnet tool install -g KlutzyNinja.Filtrace`, then `filtrace <verb>`.

The skill body's "full reference" links point at filtrace's `docs/workflow.md`
and `docs/traps.md` as absolute `https://github.com/JeremyKuhne/filtrace` URLs:
the load-bearing verb and trap catalogs are embedded in the skill body, so those
links are supplementary and resolve from anywhere.

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

## Updating

Re-vendor from filtrace when its skill changes: copy
`.agents/skills/filtrace/SKILL.md` from the filtrace repo, re-add this provenance
block (bump `github-pinned` / `github-tree-sha` to the new commit), and keep
touki-specific notes here, not in the core. When filtrace cuts a release whose
package carries the updated skill, prefer pinning to that tag.
