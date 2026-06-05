---
name: agent-files-review
description: Review changes to AI-agent customization files in this repo (AGENTS.md, .github/copilot-instructions.md, *.instructions.md, *.prompt.md, *.agent.md, SKILL.md, validator, CI workflow). Use when asked to review or validate agent-file changes, fix CI failures from agent-files.yml, or audit a draft of any of these files.
metadata:
  portability: semi-portable
---

# Agent customization files - review checklist

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
  files for `.github/agents/`, `.github/prompts/`, etc. - put `# comment`
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
  no-trailing-spaces, no-hard-tabs. Don't disable MD040 - adding a `text`
  language tag is trivial and prevents drift.
- The CI job runs on `ubuntu-latest`. PowerShell is preinstalled; no `pip`
  step is needed.

## 7. Relative Markdown links must resolve in this branch

The CI link check is **offline lychee** - it only follows links that
resolve to files in the current working tree. A link to a file that exists
on the canonical repo's `main` but not in your branch will fail.

- **Before opening a PR with agent-file changes, run
  [`tools/Test-AgentFileLinks.ps1`](../../../tools/Test-AgentFileLinks.ps1).**
  Without arguments, it scans every Markdown file in CI's lychee scope and
  reports any relative link whose target doesn't exist on disk:

  ```pwsh
  pwsh tools/Test-AgentFileLinks.ps1
  ```

  The script auto-detects the PR base ref when used with `-ChangedOnly`:
  it picks `upstream/main` if a remote literally named `upstream` exists
  (the fork-PR workflow, where `origin` is your fork and `upstream` is the
  canonical repo) and `origin/main` otherwise (working directly on a clone
  of the canonical repo). Override with `-Base <ref>` if needed:

  ```pwsh
  # Audit only files changed on this branch vs the auto-detected base.
  pwsh tools/Test-AgentFileLinks.ps1 -ChangedOnly

  # Override the base explicitly.
  pwsh tools/Test-AgentFileLinks.ps1 -Base origin/feature
  ```

- **If you cite files added by a different in-flight or just-merged PR,
  rebase your branch on the canonical `main`** (`upstream/main` from a
  fork, or `origin/main` from a direct clone) so those files exist
  locally. PR #110's review feedback was triggered by exactly this
  scenario: the branch predated PR #109 (which added the polyfill files),
  so every cross-reference to `ConvertExtensions.cs`, `RandomExtensions.cs`,
  `StringExtensions.cs`, `EncodingExtensions.cs`, `HashCodeExtensions.cs`,
  `CryptographicOperations.cs`, `SpanExtensions.StartsEndsWith.cs`,
  `StartsWithPerf.cs`, and `StringExtensionsConcatTests.cs` failed lychee
  even though those files existed on the canonical `main`.
- The lychee check runs against the merged tree on `main` only after a
  push, so a stale branch can still ship broken links into your PR. Treat
  rebase-then-link-audit as a single step before pushing any agent-file
  change that references code added elsewhere.
- Don't downgrade a real link to backticks just to silence the checker.
  Either fix the link or rebase. Backtick references work as a last resort
  when the file truly does not exist in any branch yet.

## 8. Whitespace (applies to every file in scope)

- No trailing whitespace.
- No whitespace-only lines (a "blank" line must be truly empty).
- Tabs are forbidden in Markdown bodies.
- **Files must end with a single newline character** (markdownlint MD047).
  The validator now flags this; markdownlint enforces it in CI. New files
  created via the standard editor tooling get this for free, but
  hand-edited or copy-pasted content sometimes loses the final `\n`.
- These rules are enforced both by the validator and by markdownlint.

## How to run the checks locally

**Always run the validator before declaring a review complete or pushing
agent-file changes.** It catches mirror drift, missing/invalid frontmatter,
SKILL.md naming mistakes, missing trailing newlines, and trailing/empty-line
whitespace - the same rules CI enforces.

```pwsh
# Validate everything (frontmatter, mirror sync, dir-name match, whitespace,
# trailing newline).
pwsh tools/Validate-AgentFiles.ps1

# Regenerate the mirror after editing AGENTS.md.
pwsh tools/Validate-AgentFiles.ps1 -Fix
```

The validator does **not** reproduce markdownlint's full rule set. After it
passes, sanity-check that your Markdown:

- ends with exactly one newline (MD047);
- has a language tag on every fenced code block, e.g. \`\`\`text or \`\`\`yaml
  (MD040);
- doesn't use tabs (no-hard-tabs).

If CI fails after a local validator pass, the failure is almost always
markdownlint (open the failing job's annotations) or the lychee link check
(broken relative link - remember the mirror rewrites links, so test
the form in AGENTS.md, not the rewritten copy).
