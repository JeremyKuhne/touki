# Agent Skills format reference

Format details for files under `.agents/skills/`. For the catalog of skills
currently present in this repo, see [README.md](./README.md).

## Layout

```text
.agents/skills/
  <skill-name>/
    SKILL.md           # required
    <other resources>  # scripts, templates, examples (optional)
```

The directory name **must** match the `name` field in `SKILL.md` exactly, or the
skill silently fails to load.

## `SKILL.md` format

```markdown
---
name: skill-name                 # lowercase, digits, hyphens; max 64 chars; matches dir name
description: One-sentence summary of what the skill does and when to use it.
---

# Body

Detailed instructions, procedures, examples. Reference sibling files with
relative Markdown links, e.g. `[template](./template.cs)`.
```

Optional frontmatter fields: `argument-hint`, `user-invocable`,
`disable-model-invocation`, `context` (`inline` or `fork`).

This repo also uses two optional fields:

- `compatibility` - free-text environment requirements (the Agent Skills spec
  field). Used to declare an MCP-server dependency, e.g. "Uses the
  microsoft-learn MCP server when available; falls back to web docs otherwise".
- `metadata.portability` - one of `portable`, `semi-portable`, or
  `repo-specific`, mirrored in the [README.md](./README.md) inventory. Records
  how much a skill's content would need to change to be reused in another repo.
  The validator ignores nested `metadata` keys, so this is additive.

## Thin core plus sibling files

Keep each `SKILL.md` body small (the whole body loads on every trigger; sibling
files load only when referenced). When a skill grows past roughly 150 lines,
split the deep detail into sibling `*.md` files in the same directory and leave
the core as an overview that links to them. See
[framework-jit-optimization](./framework-jit-optimization/SKILL.md),
[performance-testing](./performance-testing/SKILL.md), and
[security-review](./security-review/SKILL.md) for the pattern. Sibling links are
plain relative Markdown (`[authoring.md](authoring.md)`); the directory depth is
unchanged, so links to repo files keep the same `../../../` prefix as the core.

## Discovery

Vendor-neutral location for [Agent Skills](https://agentskills.io/). Discovered
by GitHub Copilot (VS Code, CLI, cloud agent) and Claude Code.

See [docs/agent-customization.md](../../docs/agent-customization.md) for the
full decision matrix and tooling support.
