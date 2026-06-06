# traceq

A small, agent-shaped CLI and MCP server for analyzing .NET CPU/memory/wall-clock
traces - the productized successor to `touki.mcp`. Built on the
`Microsoft.Diagnostics.Tracing.TraceEvent` library; reads EventPipe
(`.nettrace` / `.speedscope.json`) and ETW (`.etl`) captures from both .NET and
.NET Framework runs.

> **Incubation status.** This is the M0 scaffold of a self-contained subtree
> inside the `touki` repository. It is promoted to its own repository at the
> M3.5 gate. The full plan, surface area, and milestones live in
> [docs/traceq-implementation-plan.md](../docs/traceq-implementation-plan.md)
> (in the parent repo during incubation).

## Layout

| Path | Purpose |
|---|---|
| `src/TraceQ.Core/` | Analysis core: trace readers, stack-source providers, the provider-agnostic question-service engine. The only place logic lives. |
| `src/TraceQ/` | CLI host (`traceq`); the `traceq mcp` verb hosts the server. |
| `src/TraceQ.Mcp/` | Thin shim package over the same core assembly. |
| `tests/TraceQ.Core.Tests/` | Unit + golden-file contract tests. |
| `tests/TraceQ.Parity.Tests/` | Numeric parity against the frozen legacy oracles. |
| `eval/` | Headless-agent eval harness, tasks, baselines (M5). |
| `docs/` | Single-source workflow text for the skill / README / help (M4). |
| `skills/traceq/` | The shipped agent skill. |

## Self-containment

The subtree carries its own `Directory.Build.props`, `Directory.Build.targets`,
`Directory.Packages.props`, `global.json`, and `.editorconfig` (`root = true`),
none of which inherit from the parent `touki` repo. Nothing outside `traceq/`
references in, and nothing inside references a `touki` project. Promotion is a
plain file copy.

## Build and test (standalone)

```pwsh
cd traceq
dotnet build traceq.slnx
dotnet test traceq.slnx
```
