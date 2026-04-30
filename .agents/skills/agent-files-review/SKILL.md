---
name: agent-files-review
description: Review changes to AI-agent customization files in this repo (AGENTS.md, .github/copilot-instructions.md, *.instructions.md, *.prompt.md, *.agent.md, SKILL.md, validator, CI workflow). Use when asked to review or validate agent-file changes, fix CI failures from agent-files.yml, or audit a draft of any of these files.
---

# Agent customization files — review checklist

Run through every applicable item below before approving a change to an agent
customization file. The PR review history showed each of these caught a real bug.

## 1. AGENTS.md / `.github/copilot-instructions.md`

- The mirror is generated. **Never edit `.github/copilot-instructions.md` by hand.**
  Edits go in [AGENTS.md](../../../AGENTS.md), then run
  `pwsh tools/Validate-AgentFiles.ps1 -Fix` to regenerate.
- Run `pwsh tools/Validate-AgentFiles.ps1` after any AGENTS.md edit. The
  workflow at [.github/workflows/agent-files.yml](../../../.github/workflows/agent-files.yml)
  enforces this; out-of-sync mirrors fail CI.
- **Relative Markdown links must work in both AGENTS.md and the mirror.** The
  generator rewrites them automatically (`.github/x` → `x`, other paths get
  `../` prepended). Don't pre-rewrite links by hand. Don't add absolute
  filesystem paths or `./` prefixes that would defeat the regex.
- Don't use end-of-line comments in AGENTS.md examples or anywhere else; the
  file itself bans the pattern. Same applies to YAML examples in the README
  files for `.github/agents/`, `.github/prompts/`, etc. — put `# comment`
  lines above the field, not after it.
- Watch for placeholder syntax in code examples (e.g.
  `<see langword=".."/>`). Use a recognizable ellipsis (`"..."`) or a real
  example value.
- Run a grammar pass: singular vs. plural ("extension methods"),
  "for your needs" not "for your need", etc.

## 2. `*.instructions.md` (path-specific instructions)

- Frontmatter must include a non-empty `applyTo` glob.
- Glob is comma-separated, relative to repo root. Quote the value for safety.
- The validator only checks `applyTo`'s presence and emptiness; it does not
  verify that the glob actually matches anything. Sanity-check by eye.

## 3. `*.agent.md` (custom agents)

- Frontmatter must include `description`.
- If `tools` is present, it must be a YAML list. Either form is accepted by
  the validator:

  ```yaml
  tools: ['search', 'edit']
  ```

  ```yaml
  tools:
    - search
    - edit
  ```

- The repo's authoring rules forbid end-of-line comments; document optional
  fields with comment lines *above* them in any examples.

## 4. `SKILL.md` (`.agents/skills/<name>/SKILL.md`)

- `name` is **required** and must:
  - match `^[a-z0-9-]{1,64}$`
  - equal the parent directory name exactly
- `description` is required; make it specific enough that another agent
  can decide when to load it.
- A name/dir mismatch causes the skill to silently fail to load. Always
  verify by running the validator.

## 5. `*.prompt.md` (reusable prompts)

- No required frontmatter, but `description` is recommended for the slash
  menu UX.

## 6. Validator and workflow

- The frontmatter parser in [tools/Validate-AgentFiles.ps1](../../../tools/Validate-AgentFiles.ps1)
  is hand-rolled. It supports flat scalars, inline lists, and block lists.
  It does **not** handle nested mappings, multi-line strings, anchors, or
  flow mappings. If a contributor needs those, switch to the
  `powershell-yaml` module rather than extending the regex.
- The mirror generator preserves AGENTS.md's on-disk line endings (LF or
  CRLF) so `core.autocrlf=true` doesn't cause spurious diffs on Windows
  checkouts.
- [.markdownlint.jsonc](../../../.markdownlint.jsonc) intentionally disables
  MD013 (line-length), MD022/MD032 (blanks-around), MD033 (inline HTML),
  MD041 (first-line heading). It keeps MD040 (fenced-code-language),
  no-trailing-spaces, no-hard-tabs. Don't disable MD040 — adding a `text`
  language tag is trivial and prevents drift.
- The CI job runs on `ubuntu-latest`. PowerShell is preinstalled; no `pip`
  step is needed.

## 7. Whitespace (applies to every file in scope)

- No trailing whitespace.
- No whitespace-only lines (a "blank" line must be truly empty).
- Tabs are forbidden in Markdown bodies.
- These rules are enforced both by the validator and by markdownlint.

## How to run the checks locally

```pwsh
# Validate everything (frontmatter, mirror sync, dir-name match, whitespace).
pwsh tools/Validate-AgentFiles.ps1

# Regenerate the mirror after editing AGENTS.md.
pwsh tools/Validate-AgentFiles.ps1 -Fix
```

If the validator passes locally but CI fails, the failure is almost always
markdownlint (open the failing job's annotations) or the lychee link check
(broken relative link &mdash; remember the mirror rewrites them, so test the
form in AGENTS.md, not the rewritten copy).
