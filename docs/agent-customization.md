# Agent customization guide

This repository supports several formats for customizing AI coding agents. Pick the
right one for your needs, place it in the right folder, and CI will validate it.

## Decision matrix

| I want to...                                                   | Use                                                                                  |
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
| `*.instructions.md` (`applyTo`)   | yes             | yes                   | yes         | yes                        | no            | no                              |
| `*.prompt.md`                     | yes             | yes                   | no          | no                         | no            | no                              |
| `*.agent.md`                      | yes             | yes                   | yes         | yes                        | via `.claude` | varies                          |
| `SKILL.md` (Agent Skills)         | yes             | yes                   | yes         | yes                        | yes           | varies                          |

`AGENTS.md` is the most broadly portable. Prefer it for anything that should apply
universally; reach for the more specialized formats only when you need their extra
features (path scoping, tool restrictions, slash-command UX, packaged scripts).

## MCP servers

Workspace-scoped MCP server config lives in [.vscode/mcp.json](../.vscode/mcp.json).
Add a server here only when the agent genuinely cannot reach the data otherwise -
prefer instructions and skills first (see the practitioner guide rule "instructions
-> skills -> MCP"). This file configures the VS Code workspace only. Copilot cloud
agent and code review use the repository's MCP settings on GitHub.com, which are a
separate configuration and tool allowlist.

Currently wired up:

| Server | Purpose |
| ------ | ------- |
| `microsoft-learn` (`https://learn.microsoft.com/api/mcp`) | Current official .NET BCL docs and reference content. Used by the [`polyfill-dotnet-api`](../.agents/skills/polyfill-dotnet-api/SKILL.md) workflow when verifying modern API shapes before deciding whether to polyfill. |
| `filtrace` (`KlutzyNinja.Filtrace.Mcp@0.6.3`) | Published .NET trace-analysis runtime used by the [`filtrace`](../.agents/skills/filtrace/SKILL.md) workflow. The [overlay](../.agents/skills/filtrace/overlay.md) pins the vendored skill and executable packages to the same release. |

After changing a workspace MCP package pin, run **MCP: Restart Server** and choose
the server (or reload the VS Code window) before relying on newly added tools or
parameters. Editing `mcp.json` does not replace the schema already attached to an
active chat session.

## Frontmatter requirements

| Format               | Required                              | Notes                                                                  |
| -------------------- | ------------------------------------- | ---------------------------------------------------------------------- |
| `*.instructions.md`  | `applyTo` (non-empty string)          | Comma-separated globs relative to repo root.                           |
| `*.prompt.md`        | none                                  | `description` recommended.                                             |
| `*.agent.md`         | `description`                         | If `tools` is present it must be a YAML list (inline `['a', 'b']` or block syntax). |
| `SKILL.md`           | `name`, `description`                 | Touki names use ASCII `^[a-z0-9-]{1,64}$` and match the parent directory. See [FORMAT.md](../.agents/skills/FORMAT.md) for portfolio metadata, provenance, and overlays. |

Prompt and custom-agent bodies reference tools with `#tool:<tool-name>`. Their
`tools` frontmatter uses tool-set aliases such as `read`, `search`, `edit`, and
`web`, or namespaced tools such as `web/fetch`. Unrecognized tools are ignored,
so validate the available set in the target host.

## Authoring rules

These come from [AGENTS.md](../AGENTS.md) and apply to every agent file in the repo:

- No trailing whitespace; no whitespace-only lines.
- Use spaces (not tabs) inside Markdown bodies and YAML frontmatter.
- Prefer plain ASCII punctuation in Touki-authored files. Exact vendored cores
  may retain upstream punctuation; correct it upstream and re-vendor.
- Keep lines under ~120 characters where practical.
- Single blank line between sections; preserve existing whitespace when editing.

## Local validation

Run the validator before pushing:

```pwsh
pwsh tools/Validate-AgentFiles.ps1
pwsh tools/Validate-AgentSkills.ps1
pwsh tools/Test-AgentFileLinks.ps1
```

Use `-Fix` after every `AGENTS.md` change to regenerate
`.github/copilot-instructions.md`, then stage both files:

```pwsh
pwsh tools/Validate-AgentFiles.ps1 -Fix
```

The same checks run in CI via [.github/workflows/agent-files.yml](../.github/workflows/agent-files.yml).
The skill validator checks the mixed catalog, the strict commons subset,
overlay pins, relationship targets/cycles, and catalog category labels.

## Improvement loop

The customization layer is source code: it is iterated, not set once. Every
correction you give an agent is a signal that something is missing.

### Triage matrix

When the agent does something wrong, classify the failure and place the fix
at the right layer.

| Failure class                                 | Right layer                                                    |
| --------------------------------------------- | -------------------------------------------------------------- |
| Factual: wrong about *what the code is*       | [AGENTS.md](../AGENTS.md) or path-specific `*.instructions.md` |
| Procedural: wrong *workflow*, repeated        | New skill in [.agents/skills/](../.agents/skills/)             |
| Capability: agent literally cannot reach data | MCP server (or a script the agent can run)                     |
| Enforcement: agent rationalizes around a rule | Hook, CI check, or `chat.tools.terminal.autoApprove` denylist  |

The order matters. Try instructions before skills, skills before MCP, and
prefer deterministic enforcement (CI, denylist, branch protection) over more
prose anywhere a rule has been violated before.

### Size budget

Attention to instructions degrades as files grow. Targets:

- [AGENTS.md](../AGENTS.md): <= 20 KB. If it grows past this, split rules into
  a path-specific `*.instructions.md` and leave a one-line summary pointer.
- Each `*.instructions.md`: <= 12 KB. If a single area needs more, split it
  further by glob.
- Each `SKILL.md`: <= 15 KB body. Move long examples or scripts into sibling
  files within the skill directory.

### Periodic maintenance

Roughly monthly:

1. Read [AGENTS.md](../AGENTS.md) end to end. Anything stale, contradictory,
   or vague? Tighten.
2. Skim each `*.instructions.md`. Same.
3. Check [.agents/skills/README.md](../.agents/skills/README.md): every
   cross-reference should resolve, every disambiguation should still match
   the skills it names. The CI freshness warning (90 days, derived from git
   history) flags individual skills that have not been touched.
4. Scan the recent git log for repeated CI fixes to the same rule, repeated
   "fix the comments" cycles on PRs, or repeated permission requests for
   the same command. These are gap signals; address them at the right layer
   from the matrix above.

### Hygiene rules

- The phrasing-bank lists in
  [AGENTS.md](../AGENTS.md) "Working with the user on changes" (verbs that
  are / are not approval) are **append-only**. Add new misreads when they
  occur. Do not rewrite past entries except to deduplicate.
- When changing a skill, also update any cross-reference in
  [.agents/skills/README.md](../.agents/skills/README.md) within the same
  change set.
- When a rule moves from instructions to a CI check (or vice versa), keep
  the prose in instructions explaining *why*. The mechanical gate is the
  *what*; instructions explain the *why*.

## Verifying instructions are loaded

- **VS Code**: right-click in the Chat view, choose **Diagnostics** - lists all
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
