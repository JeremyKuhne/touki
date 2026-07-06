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
