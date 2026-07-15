# filtrace skill

The shipped agent skill is [SKILL.md](SKILL.md). Its verb and trap blocks are
embedded from the single-source workflow text in
[docs/](https://github.com/JeremyKuhne/filtrace/tree/main/docs)
([workflow.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/workflow.md),
[traps.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/traps.md)) and kept
in sync by
[tools/Test-Docs.ps1](https://github.com/JeremyKuhne/filtrace/blob/main/tools/Test-Docs.ps1).

This skill lives at the vendor-neutral `.agents/skills/` location read by GitHub
Copilot (VS Code, CLI, cloud agent) and Claude Code, and it ships inside the
`KlutzyNinja.Filtrace.Mcp` NuGet package (under `skills/`). See
[docs/implementation-plan.md](https://github.com/JeremyKuhne/filtrace/blob/main/docs/implementation-plan.md)
(M4) for the knowledge-layer milestone.

## Consumer overlays

The skill is complete without local configuration. A consuming repository may add
an `overlay.md` beside `SKILL.md` to bind the workflow to project-specific paths,
capture defaults, symbol directories, process/root conventions, or safety policy.
The overlay is consumer-owned and is not part of the packaged filtrace skill.

Use the installed package version or source revision as `core-pin`:

```markdown
---
core: filtrace
core-pin: vX.Y.Z
---

# Filtrace overlay

## Bindings

- Build outputs and symbols are under `artifacts/bin`.
- Prefer BenchmarkDotNet workload scope for captures under `BenchmarkDotNet.Artifacts`.
```

Review the bindings whenever the filtrace skill is updated, then update `core-pin`.
