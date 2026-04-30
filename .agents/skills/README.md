# Skills (`.agents/skills/`)

Vendor-neutral location for [Agent Skills](https://agentskills.io/) &mdash; portable
folders of instructions, scripts, and resources that an agent can load on demand.
Discovered by GitHub Copilot (VS Code, CLI, cloud agent) and Claude Code.

## Layout

```
.agents/skills/
  <skill-name>/
    SKILL.md           # required
    <other resources>  # scripts, templates, examples (optional)
```

The directory name **must** match the `name` field in `SKILL.md` exactly, or the skill
silently fails to load.

## `SKILL.md` format

```markdown
---
name: skill-name                 # lowercase, digits, hyphens; max 64 chars; matches dir name
description: One-sentence summary of what the skill does and when to use it.
---

# Body

Detailed instructions, procedures, examples. Reference sibling files with relative
Markdown links, e.g. `[template](./template.cs)`.
```

Optional frontmatter fields: `argument-hint`, `user-invocable`,
`disable-model-invocation`, `context` (`inline` or `fork`).

See [docs/agent-customization.md](../../docs/agent-customization.md) for the full
decision matrix and tooling support.
