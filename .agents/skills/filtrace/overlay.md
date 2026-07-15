---
core: filtrace
core-pin: a04fdd8cae33af5dd8fb5a25329c8c75eb7dea73
core-repo: https://github.com/JeremyKuhne/filtrace
core-tree-sha: 43e5f226a606a25da9917b68d29c828a9e95aa4c
runtime-pin: 0.6.0
---

# Touki overlay - filtrace

Repository-specific bindings for the tool-shipped [filtrace](SKILL.md) core.
The upstream-owned [skill](SKILL.md), [README](README.md), and [scripts](scripts/)
match standalone
[JeremyKuhne/filtrace](https://github.com/JeremyKuhne/filtrace) source commit
`a04fdd8cae33af5dd8fb5a25329c8c75eb7dea73` (skill tree
`43e5f226a606a25da9917b68d29c828a9e95aa4c`). That unreleased revision enables
consumer overlays; the executable packages remain pinned to release `0.6.0`.

## Bindings

- **MCP server** - registered in [.vscode/mcp.json](../../../.vscode/mcp.json)
  as `dnx KlutzyNinja.Filtrace.Mcp@0.6.0`, exposing the seventeen `trace_*`
  tools an agent calls directly. No clone or build is required.
- **CLI, fresh install** -
  `dotnet tool install -g KlutzyNinja.Filtrace --version 0.6.0`.
- **CLI, existing install** -
  `dotnet tool update -g KlutzyNinja.Filtrace --version 0.6.0`.
- Run `filtrace --version` and require `0.6.0` before passing that executable
  to the capture helpers. Installing does not upgrade an older global tool.
- Touki's [profiling](../performance-testing/profiling.md) and
  [graphical-viewers](../performance-testing/graphical-viewers.md) pages drive
  the vendored capture and viewer scripts.
- [tools/Capture-EtwTrace.ps1](../../../tools/Capture-EtwTrace.ps1) remains
  Touki-specific because it wraps the local net481 benchmark workflow.

## Upstream follow-up

Release 0.6.0 completes
[filtrace#42](https://github.com/JeremyKuhne/filtrace/issues/42): first-class
BenchmarkDotNet scoping and ambiguity diagnostics, query-specific contributing
record counts, ETLX conversion coordination, provider enablement/event state,
source/PDB quality, isolated all-case capture manifests, normalized
manifest-aware diff, and batch analysis.

The implementation supports process and BenchmarkDotNet scoping for
manifest-aware `diff` and `batch`, but the current core's scope inventories do
not list those tools. Its process-scope inventory also omits `export`. Correct
those generic reference lists in filtrace and re-vendor a later source revision;
do not patch the pinned core here.

The 0.6 capture helper also accepts any discovered `filtrace` executable without
checking its version or response schema. Add a helper-side compatibility
preflight upstream; Touki's profiling workflow performs the version check before
invoking the pinned payload.

Touki uses BenchmarkDotNet 0.16.0-preview.1. Its log escapes quotes inside
`--benchmarkName`, writes parameter displays as `[Scenario=...]`, and emits
`// Runtime=...` environment lines. The 0.6 helper's parser does not fully
understand that shape, so verify every manifest case has nonempty `benchmark`
and `parameters` before using manifest-aware `batch` or `diff`. Analyze direct
trace paths when identity is incomplete; never guess case pairing.

The 0.6 helper also caps the on-disk manifest at 20 KiB. Split broad filters
before capture rather than relying on the compact-stdout fallback for a large
case matrix. Treat `activity` as unavailable unless the application EventSource
provider was explicitly enabled; the default BenchmarkDotNet EventPipe capture
does not establish that provider merely because the helper sidecar says enabled.

## Concrete fold-list hit

filtrace folds JIT-helper thunks (`memmove`, write barriers, GC polls) into their
managed caller by default. A pre-filtrace trace of Touki extglob enumeration put
93% inclusive time on `System.Buffer.BulkMoveWithWriteBarrier`, even though the
walked struct contained no GC references. The frame was an attribution artifact;
`callers` identified the engine loops that owned the cost. The full writeup is in
[dotnet-perf-discoveries.md](../../../docs/dotnet-perf-discoveries.md), section 8.

## Updating

Until overlay support is released, copy the complete upstream-owned payload from
an exact filtrace source revision and record that revision in `core-pin`. When a
published release contains the overlay contract, copy the payload from its exact
tag, set `core-pin` to that version, and update the CLI/MCP package pins together.
Never hand-edit the tool-shipped core; fix generic content upstream and re-vendor
it.

After updating, review these bindings and run `tools/Validate-AgentFiles.ps1`,
`tools/Validate-AgentSkills.ps1`, and `tools/Test-AgentFileLinks.ps1`.
