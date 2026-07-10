---
compatibility: Requires gh 2.90 or later for install and update operations; manual file comparison remains available without gh.
description: Find, add, update, and share agent skills in this repo. Use when asked to "find a skill" for a task, "build a skill" or "create a skill" (this skill checks whether one already exists - in the repo, the shared commons, or a public catalog - before authoring a new one), "update a skill", or reconcile a local skill change against the upstream commons vs a repo-local overlay. Covers the find-first build path, the tiered search, and the pull/push update flow. Not for validating one agent file's syntax - that is `agent-files-review`.
license: MIT
metadata:
    applicability: universal
    binding: optional-overlay
    github-path: skills/manage-skills
    github-pinned: v0.10.0
    github-ref: refs/tags/v0.10.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: bb78d07296156a84ee9f54023145532a5275607a
    maturity: canary
    portability: portable
    related: agent-files-review
    requires: none
    risk: remote-write
name: manage-skills
---
# Manage skills

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

The lifecycle skill for the skills under a repo's `.agents/skills/`: discover one,
add one, update one, and keep local changes in sync with the shared set. It turns
"find a skill", "build a skill", and "update the skill" into actions aligned with
the sharing model instead of ad-hoc edits.

The model in one paragraph: skills are authored once as a portable **core** and
shared through a common skills repo (the **commons**); each consuming repo holds a
pinned, provenance-stamped **copy** plus a thin repo-specific **overlay**. The
commons is bidirectional - a generic improvement made in any repo flows back
upstream, while a repo-specific tweak lives only in that repo's overlay.

## The three verbs

| Ask | Do | Detail |
| --- | --- | ------ |
| "find a skill for X", "is there a skill that does X" | Tiered search (local -> commons -> public), with an applicability check for this repo. | [find.md](find.md) |
| "build a skill for X", "create a skill" | **Run find first.** Only author new if it exists nowhere; otherwise vendor or tweak the existing one. | [build.md](build.md) |
| "update the skill", "sync my change", "pull skill updates" | Pull upstream drift; or push a local improvement, classified common (ask before upstreaming) vs deviation (overlay). | [update.md](update.md) |

These chain: `build` always begins by running `find`, and a `find` that turns up a
local skill needing a tweak hands off to the overlay path in `build`.

## The golden rule

When you change a skill that was vendored from the commons: **never let a vendored
core diverge silently.** A vendored core is a mirror of upstream. Classify the
edit, then place it - and **nothing about upstreaming is automatic**:

- **Local deviation** (the change is specific to this repo) -> move it into the
  repo's **overlay**, and restore the vendored core to match upstream.
- **Common** (the change helps every consumer) -> it *should* go upstream, but
  upstreaming is not always plausible. **Ask** before attempting it; never open a
  commons PR unprompted. If upstreamed, re-pin to the new version; if not yet,
  keep it as a recorded *pending-upstream divergence* so it is intentional, not
  silent.

So a vendored-core edit ends in one of three **recorded** states - upstreamed,
moved to the overlay, or a tracked pending-upstream divergence - never an
unexplained one. The provenance frontmatter (source repo, ref, and tree SHA) plus
the `update` drift check is what enforces this: unexplained drift is the signal
that an improvement was written into the wrong layer. See [update.md](update.md).

## Conventions every skill follows

Whatever the verb, the result must satisfy the repo's authoring rules
(`FORMAT.md`) and then pass `agent-files-review`, which owns the file-level
checks - frontmatter, mirror sync, whitespace, and the validator and link
checker. Don't restate those rules here.

For the `SKILL.md` frontmatter check specifically, this skill bundles
[scripts/Validate-Skills.ps1](scripts/Validate-Skills.ps1) - a dependency-free
PowerShell port of the Agent Skills spec validator - so the check runs anywhere
the skill is vendored, without the upstream tool. Run it on the skill directory:
`pwsh scripts/Validate-Skills.ps1 <skill-dir>`. A commons portfolio uses
`-RequirePortfolioMetadata` to enforce its metadata and overlay contract.

For a new downstream binding, start from
`assets/overlay.md.tmpl`, replace its skill and pin tokens, and keep every local
path and concrete cross-reference in that overlay.

## Sub-pages

- [find.md](find.md) - the tiered search, the applicability check, and the
  recommendation report.
- [build.md](build.md) - the find-first decision tree, the security gate for
  public sources, and authoring a new skill (born-local vs born-shared).
- [update.md](update.md) - the pull (drift) and push (common vs deviation)
  flows and the provenance mechanics behind the golden rule.

## Disambiguation

`manage-skills` operates on the **catalog lifecycle** - discover, add, vendor,
sync. It is not `agent-files-review`, which
validates the **syntax and conventions** of one agent file (frontmatter, mirror
sync, whitespace). The normal order is: run `manage-skills` to bring a skill in
or push one out, then `agent-files-review` to validate the file you ended up with.
