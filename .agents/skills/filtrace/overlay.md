---
core: filtrace
core-pin: v0.6.1
core-repo: https://github.com/JeremyKuhne/filtrace
core-tree-sha: b9c89b9bd0a698356fe5c81984abf27a5875f87f
runtime-pin: 0.6.1
---

# Touki overlay - filtrace

Repository-specific bindings for the tool-shipped [filtrace](SKILL.md) core.
The upstream-owned [skill](SKILL.md), [README](README.md), and [scripts](scripts/)
match standalone [JeremyKuhne/filtrace](https://github.com/JeremyKuhne/filtrace)
tag `v0.6.1` (commit `e2fb75392576ca4a35bfe7e82dcefb511f1ded1e`,
skill tree `b9c89b9bd0a698356fe5c81984abf27a5875f87f`). The executable
packages and vendored skill are pinned to the same release.

## Bindings

- **MCP server** - registered in [.vscode/mcp.json](../../../.vscode/mcp.json)
  as `dnx KlutzyNinja.Filtrace.Mcp@0.6.1`, exposing the seventeen `trace_*`
  tools an agent calls directly. No clone or build is required.
- **CLI, fresh install** -
  `dotnet tool install -g KlutzyNinja.Filtrace --version 0.6.1`.
- **CLI, existing install** -
  `dotnet tool update -g KlutzyNinja.Filtrace --version 0.6.1`.
- Run `filtrace --version` and require `0.6.1` before passing that executable
  to the capture helpers. Installing does not upgrade an older global tool.
- Touki's [profiling](../performance-testing/profiling.md) and
  [graphical-viewers](../performance-testing/graphical-viewers.md) pages drive
  the vendored capture and viewer scripts.
- [tools/Capture-EtwTrace.ps1](../../../tools/Capture-EtwTrace.ps1) remains
  Touki-specific because it wraps the local net481 benchmark workflow.

## Release 0.6.1

Release 0.6.1 closes
[filtrace#55](https://github.com/JeremyKuhne/filtrace/issues/55), which Touki
reported while exercising BenchmarkDotNet 0.16.0-preview.1. The bundled helper
now verifies hashed parameterized artifacts through their embedded benchmark
identity, preserves runtime metadata, preflights the filtrace version and schema,
and keeps the 20 KiB limit on compact agent output rather than the durable
manifest. Ordinary filenames retain a guarded exact-name and execution-order
fallback only when case counts and distinct capture timestamps align; otherwise
identity remains unavailable. Default EventPipe activity state also remains
unknown without provider evidence, and the skill's scope inventory now matches
the implemented tools.

Touki no longer carries consumer-side workarounds for those issues. Preserve the
helper's fail-closed behavior: when an unidentified case is excluded from
manifest-aware batch or pairing, analyze that trace directly rather than guessing
its benchmark or parameters.

## Concrete fold-list hit

filtrace folds JIT-helper thunks (`memmove`, write barriers, GC polls) into their
managed caller by default. A pre-filtrace trace of Touki extglob enumeration put
93% inclusive time on `System.Buffer.BulkMoveWithWriteBarrier`, even though the
walked struct contained no GC references. The frame was an attribution artifact;
`callers` identified the engine loops that owned the cost. The full writeup is in
[dotnet-perf-discoveries.md](../../../docs/dotnet-perf-discoveries.md), section 8.

## Updating

Copy the complete upstream-owned payload from an exact filtrace tag, record that
tag in `core-pin`, and update the CLI/MCP package pins together. Record the exact
payload tree in `core-tree-sha`; it catches release-copy drift independently of
the human-readable pin. Never hand-edit the tool-shipped core; fix generic
content upstream and re-vendor it.

After updating, review these bindings and run `tools/Validate-AgentFiles.ps1`,
`tools/Validate-AgentSkills.ps1`, and `tools/Test-AgentFileLinks.ps1`.
