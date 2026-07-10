---
compatibility: Requires the repository's validator and relative-link check; exact commands may be supplied by an overlay.
description: Review changes to AI-agent customization files (AGENTS.md, .github/copilot-instructions.md, *.instructions.md, *.prompt.md, *.agent.md, SKILL.md, the validator, the CI workflow). Use when asked to review or validate agent-file changes, fix CI failures from the agent-files workflow, or audit a draft of any of these files.
license: MIT
metadata:
    applicability: agent-customization
    binding: optional-overlay
    github-path: skills/agent-files-review
    github-pinned: v0.10.0
    github-ref: refs/tags/v0.10.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: 64a00d548909d8f84fb8ac6f6e3db77deceab270
    maturity: canary
    portability: portable
    related: manage-skills
    requires: none
    risk: local-write
name: agent-files-review
---
# Agent customization files - review checklist

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

Run through every applicable item below before approving a change to an agent
customization file. Each item below caught a real bug in PR review history.

This skill assumes the repository has adopted the agent-file scaffold: an
`AGENTS.md` single-source with a generated `.github/copilot-instructions.md`
mirror, a validator script (conventionally `tools/Validate-AgentFiles.ps1`), a
relative-link checker (conventionally `tools/Test-AgentFileLinks.ps1`), and a CI
workflow (conventionally `.github/workflows/agent-files.yml`) running markdownlint
plus an offline lychee link check. A consuming repo wires the exact paths in its
overlay.

## 1. AGENTS.md / `.github/copilot-instructions.md`

- The mirror is generated. **Never edit `.github/copilot-instructions.md` by hand.**
  Edits go in `AGENTS.md`, then regenerate the mirror with the validator's
  fix mode.
- Run the validator after any `AGENTS.md` edit. The agent-files CI workflow
  enforces this; out-of-sync mirrors fail CI.
- **Relative Markdown links must work in both `AGENTS.md` and the mirror.** The
  generator rewrites them automatically (`.github/x` → `x`, other paths get
  `../` prepended). Don't pre-rewrite links by hand. Don't add absolute
  filesystem paths or `./` prefixes that would defeat the regex.
- Don't use end-of-line comments in `AGENTS.md` examples or anywhere else; the
  file itself bans the pattern. Same applies to YAML examples in the README
  files for `.github/agents/`, `.github/prompts/`, etc. - put `# comment`
  lines above the field, not after it.
- Watch for placeholder syntax in code examples (e.g.
  `<see langword=".."/>`). Use a recognizable ellipsis (`"..."`) or a real
  example value.
- Run a grammar pass: singular vs. plural ("extension methods"),
  "for your needs" not "for your need", etc.

## 2. Per-file frontmatter and naming

The frontmatter and naming rules for each file type - `*.instructions.md`,
`*.agent.md`, `SKILL.md`, and `*.prompt.md` - live in
[frontmatter.md](frontmatter.md). Check the entry for the file type you touched.

## 3. Validator and workflow

- The frontmatter parser in the validator script is typically hand-rolled. A
  hand-rolled parser supports flat scalars, inline lists, and block lists, but
  **not** nested mappings, multi-line strings, anchors, or flow mappings. If a
  contributor needs those, switch to a real YAML module (e.g.
  `powershell-yaml`) rather than extending the regex.
- The mirror generator preserves `AGENTS.md`'s on-disk line endings (LF or
  CRLF) so `core.autocrlf=true` doesn't cause spurious diffs on Windows
  checkouts.
- The markdownlint config typically disables MD013 (line-length), MD022/MD032
  (blanks-around), MD033 (inline HTML), MD041 (first-line heading), and keeps
  MD040 (fenced-code-language), no-trailing-spaces, no-hard-tabs. Don't disable
  MD040 - adding a `text` language tag is trivial and prevents drift.
- The CI job typically runs on `ubuntu-latest`. PowerShell is preinstalled
  there; no `pip` step is needed.
- A commons repository should validate source `skills/*/SKILL.md`, not only a
  consumer's `.agents/skills/` directory. Treat a conditional that silently
  skips a populated source tree as a broken gate.
- Validate agent frontmatter, plugin/marketplace/MCP manifests, relationship
  names, and generated catalogs in addition to skill frontmatter. Then install
  each core alone (plus declared `requires`) and resolve links inside that
  artifact; a repo-wide link check cannot catch undeclared sibling dependencies.

## 4. Relative Markdown links must resolve in this branch

The CI link check is **offline lychee** - it only follows links that
resolve to files in the current working tree. A link to a file that exists
on the canonical repo's `main` but not in your branch will fail.

- **Before opening a PR with agent-file changes, run the link checker.**
  Without arguments, it scans every Markdown file in CI's lychee scope and
  reports any relative link whose target doesn't exist on disk. It typically
  auto-detects the PR base ref: `upstream/main` if a remote literally named
  `upstream` exists (the fork-PR workflow, where `origin` is your fork and
  `upstream` is the canonical repo), `origin/main` otherwise (working directly
  on a clone of the canonical repo). A changed-only mode and an explicit
  base-ref override are the usual options.
- **If you cite files added by a different in-flight or just-merged PR,
  rebase your branch on the canonical `main`** (`upstream/main` from a
  fork, or `origin/main` from a direct clone) so those files exist
  locally. A recurring review-round loss comes from exactly this scenario:
  the branch predated a sister PR that added the referenced source files,
  so every cross-reference to them failed lychee even though those files
  existed on the canonical `main`.
- The lychee check runs against the merged tree on `main` only after a
  push, so a stale branch can still ship broken links into your PR. Treat
  rebase-then-link-audit as a single step before pushing any agent-file
  change that references code added elsewhere.
- Don't downgrade a real link to backticks just to silence the checker.
  Either fix the link or rebase. Backtick references work as a last resort
  when the file truly does not exist in any branch yet.

## 5. Whitespace (applies to every file in scope)

- No trailing whitespace.
- No whitespace-only lines (a "blank" line must be truly empty).
- Tabs are forbidden in Markdown bodies.
- **Files must end with a single newline character** (markdownlint MD047).
  The validator flags a *missing* trailing newline (it checks the file ends
  with `\n`, not that there is exactly one); markdownlint enforces the full
  single-newline rule in CI. New files
  created via the standard editor tooling get this for free, but
  hand-edited or copy-pasted content sometimes loses the final `\n`.
- These rules are enforced both by the validator and by markdownlint.

## How to run the checks locally

**Always run the validator before declaring a review complete or pushing
agent-file changes.** It catches mirror drift, missing/invalid frontmatter,
`SKILL.md` naming mistakes, missing trailing newlines, and trailing/empty-line
whitespace - the same rules CI enforces. Run it in plain mode to validate, and
in fix mode (`-Fix`) to regenerate the mirror after editing `AGENTS.md`.

The validator does **not** reproduce markdownlint's full rule set. After it
passes, sanity-check that your Markdown:

- ends with exactly one newline (MD047);
- has a language tag on every fenced code block, e.g. \`\`\`text or \`\`\`yaml
  (MD040);
- doesn't use tabs (no-hard-tabs).

If CI fails after a local validator pass, the failure is almost always
markdownlint (open the failing job's annotations) or the lychee link check
(broken relative link - remember the mirror rewrites links, so test
the form in `AGENTS.md`, not the rewritten copy).

For a commons portfolio, use strict mode and check generated collateral:

```pwsh
./skills/manage-skills/scripts/Validate-Skills.ps1 ./skills -RequirePortfolioMetadata
./tools/Update-SkillCatalog.ps1
Invoke-Pester ./tests
```
