# Agent customization guide

This repository supports several formats for customizing AI coding agents. Pick the
right one for your needs, place it in the right folder, and CI will validate it.

## Decision matrix

| I want to&hellip;                                              | Use                                                                                  |
| -------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| Set project-wide rules every agent should follow               | [AGENTS.md](../AGENTS.md) (canonical; mirrored to `.github/copilot-instructions.md`) |
| Apply rules only to files matching a glob                      | [.github/instructions/](../.github/instructions/)`<name>.instructions.md`            |
| Provide a reusable slash-command prompt                        | [.github/prompts/](../.github/prompts/)`<name>.prompt.md`                            |
| Define a persona with restricted tools and/or handoffs         | [.github/agents/](../.github/agents/)`<name>.agent.md`                               |
| Package a portable workflow (instructions + scripts/resources) | [.agents/skills/](../.agents/skills/)`<name>/SKILL.md`                               |

## Tool support

| Format                            | Copilot VS Code | Copilot Visual Studio | Copilot CLI | Copilot cloud / github.com | Claude Code   | Codex / Cursor / Aider / Gemini |
| --------------------------------- | --------------- | --------------------- | ----------- | -------------------------- | ------------- | ------------------------------- |
| `AGENTS.md`                       | yes             | yes                   | yes         | yes                        | yes           | yes                             |
| `.github/copilot-instructions.md` | yes             | yes                   | yes         | yes                        | no            | no                              |
| `*.instructions.md` (`applyTo`)   | yes             | yes                   | partial     | yes                        | no            | no                              |
| `*.prompt.md`                     | yes             | yes                   | no          | no                         | no            | no                              |
| `*.agent.md`                      | yes             | yes                   | no          | no                         | via `.claude` | no                              |
| `SKILL.md` (Agent Skills)         | yes             | yes                   | yes         | yes                        | yes           | varies                          |

`AGENTS.md` is the most broadly portable. Prefer it for anything that should apply
universally; reach for the more specialized formats only when you need their extra
features (path scoping, tool restrictions, slash-command UX, packaged scripts).

## Frontmatter requirements

| Format               | Required                              | Notes                                                                  |
| -------------------- | ------------------------------------- | ---------------------------------------------------------------------- |
| `*.instructions.md`  | `applyTo` (non-empty string)          | Comma-separated globs relative to repo root.                           |
| `*.prompt.md`        | none                                  | `description` recommended.                                             |
| `*.agent.md`         | `description`                         | If `tools` is present it must be a YAML list (inline `['a', 'b']` or block syntax). |
| `SKILL.md`           | `name`, `description`                 | `name` must match parent directory name; `^[a-z0-9-]{1,64}$`.          |

## Authoring rules

These come from [AGENTS.md](../AGENTS.md) and apply to every agent file in the repo:

- No trailing whitespace; no whitespace-only lines.
- Use spaces (not tabs) inside Markdown bodies and YAML frontmatter.
- Keep lines under ~120 characters where practical.
- Single blank line between sections; preserve existing whitespace when editing.

## Local validation

Run the validator before pushing:

```pwsh
pwsh tools/Validate-AgentFiles.ps1
```

Use `-Fix` to regenerate `.github/copilot-instructions.md` from `AGENTS.md`:

```pwsh
pwsh tools/Validate-AgentFiles.ps1 -Fix
```

The same checks run in CI via [.github/workflows/agent-files.yml](../.github/workflows/agent-files.yml).

## Verifying instructions are loaded

- **VS Code**: right-click in the Chat view, choose **Diagnostics** &mdash; lists all
  loaded instruction/skill/agent files and any errors.
- **github.com**: open a PR; the Copilot code review check lists the instruction files
  it consulted.
- **Visual Studio**: same Copilot stack as VS Code; instructions load from
  `.github/copilot-instructions.md` and `.github/instructions/`.

## References

- [AGENTS.md standard](https://agents.md/)
- [Agent Skills standard](https://agentskills.io/)
- [GitHub Copilot custom instructions](https://docs.github.com/en/copilot/how-tos/configure-custom-instructions/add-repository-instructions)
- [VS Code custom instructions](https://code.visualstudio.com/docs/copilot/customization/custom-instructions)
- [VS Code Agent Skills](https://code.visualstudio.com/docs/copilot/customization/agent-skills)
- [VS Code custom agents](https://code.visualstudio.com/docs/copilot/customization/custom-agents)
