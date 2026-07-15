# Agent Skills format reference

Format details for files under `.agents/skills/`. For the catalog of skills
currently present in this repo, see [README.md](./README.md).

## Layout

```text
.agents/skills/
  <skill-name>/
    SKILL.md           # required
    overlay.md         # repository-specific bindings for a vendored core (optional)
    <other resources>  # scripts, templates, examples (optional)
```

The directory name **must** match the `name` field in `SKILL.md` exactly, or the
skill silently fails to load.

## `SKILL.md` format

```markdown
---
name: skill-name                 # ASCII lowercase, digits, hyphens; max 64 chars; matches dir
description: One-sentence summary of what the skill does and when to use it.
metadata:
  applicability: repo-local
  binding: none
  maturity: stable
  portability: repo-specific
  related: none
  requires: none
  risk: local-write
---

# Body

Detailed instructions, procedures, examples. Reference sibling files with
relative Markdown links, e.g. `[template](./template.cs)`.
```

Optional frontmatter fields: `argument-hint`, `user-invocable`,
`disable-model-invocation`, `context` (`inline` or `fork`).

This repo also uses:

- `compatibility` - free-text environment requirements (the Agent Skills spec
  field). Used to declare an MCP-server dependency, e.g. "Uses the
  microsoft-learn MCP server when available; falls back to web docs otherwise".
- Portfolio metadata - string-valued `portability`, `applicability`, `binding`,
  `risk`, `maturity`, `requires`, and `related`. The vocabularies are enforced by
  the bundled strict validator; relationship values are `none` or comma-separated
  skill names.

Touki uses the ASCII skill-name subset `^[a-z0-9-]{1,64}$`, matching current
VS Code discovery. Exact vendored payloads can retain upstream punctuation and
format wording; fix generic issues upstream and re-vendor rather than silently
editing a pinned core.

## Portfolio and provenance

The catalog contains three categories:

- **Commons core** - `metadata.portability: portable`, complete portfolio
  metadata, and `metadata.github-*` provenance pinned to an immutable release.
- **Tool-shipped core** - copied from the tool's own repository (currently
  `filtrace`); its source owns the core metadata. When the core is overlay-capable,
  Touki records package wiring, local paths, and update provenance in `overlay.md`.
- **Touki-owned skill** - `metadata.portability: repo-specific` and complete
  local portfolio metadata, with no `metadata.github-*` provenance.

Never hand-edit a vendored core. `github-pinned` is the tag name for a tag pin;
`github-ref` is the full ref; `github-tree-sha` identifies the upstream skill
directory. Touki-specific paths, examples, and relationships belong in
`overlay.md` or another documented overlay sibling.

For a vendored core with an overlay, the core uses `metadata.binding` of
`optional-overlay` or `required-overlay` and contains the standard overlay loader
sentence. The overlay starts with:

```yaml
---
core: skill-name
core-pin: v0.10.0
---
```

`core` matches the directory. For a commons core, `core-pin` matches the core's
`github-pinned` value. For a tool-shipped core, use the installed package version
or an exact source revision as `core-pin`; review the bindings whenever that pin
changes. A tool-shipped source-revision overlay also records `core-repo` and
`core-tree-sha` so the local payload can be verified, and may record a separate
`runtime-pin` when the published executable version differs from the skill source.

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

## Validation

Run both local gates after changing a skill:

```pwsh
pwsh tools/Validate-AgentFiles.ps1
pwsh tools/Validate-AgentSkills.ps1
pwsh tools/Test-AgentFileLinks.ps1
```

`Validate-AgentSkills.ps1` runs the bundled validator over the mixed 18-skill
catalog, runs strict portfolio validation over the commons cores selected by
provenance, and checks overlays, relationship targets/cycles, and catalog labels.

## Discovery

Vendor-neutral location for [Agent Skills](https://agentskills.io/). Discovered
by GitHub Copilot (VS Code, CLI, cloud agent) and Claude Code.

See [docs/agent-customization.md](../../docs/agent-customization.md) for the
full decision matrix and tooling support.
