---
name: manage-skills
description: Find, add, update, and share agent skills in this repo. Use when asked to "find a skill" for a task, "build a skill" or "create a skill" (this skill checks whether one already exists - in the repo, the shared commons, or a public catalog - before authoring a new one), "update a skill", or reconcile a local skill change against the upstream commons vs a repo-local overlay. Covers the find-first build path, the tiered search, and the pull/push update flow. Not for validating one agent file's syntax - that is `agent-files-review`.
metadata:
  portability: semi-portable
---

# Manage skills

The lifecycle skill for the skills under [.agents/skills/](../): discover one,
add one, update one, and keep local changes in sync with the shared set. It turns
"find a skill", "build a skill", and "update the skill" into actions aligned with
this repo's sharing model instead of ad-hoc edits.

The model in one paragraph: skills are authored once as a portable **core** and
shared through a common skills repo (the **commons**); each consuming repo holds a
pinned, provenance-stamped **copy** plus a thin repo-specific **overlay**. The
commons is bidirectional - a generic improvement made in any repo flows back
upstream, while a repo-specific tweak lives only in that repo's overlay.

## Current status

Read this before running commons-dependent steps; it is the one part that changes
as the rollout proceeds.

- **Live now:** local search (this repo's `.agents/skills/`) and authoring a new
  skill. These need no external tooling.
- **Pending:** the shared **commons** repo is not stood up yet, so the commons
  tier of `find`, and the `update` push/pull against upstream, are not yet
  operational. Treat those steps as the target flow, and fall back to local +
  public sources until the commons exists.
- **Tooling:** the `gh skill` commands below are GitHub CLI (>= 2.90, preview) and
  require `gh auth login` first. When `gh` is unavailable or unauthenticated, use
  the manual fallbacks each sub-page notes.

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
unexplained one. The provenance frontmatter (source repo + ref + tree SHA) plus
the `update` drift check is what enforces this: unexplained drift is the signal
that an improvement was written into the wrong layer. See [update.md](update.md).

## Conventions every skill follows

Whatever the verb, the result must satisfy the repo's format rules
([FORMAT.md](../FORMAT.md)): a thin `SKILL.md` core under the size budget with
deep detail in sibling files; `name` matching the directory; a "pushy"
`description` with trigger phrasing; `metadata.portability` set; a row in the
catalog [README.md](../README.md); and a disambiguation entry when the trigger
phrasing competes with an existing skill. After any add or edit, validate with
`pwsh tools/Validate-AgentFiles.ps1` and `pwsh tools/Test-AgentFileLinks.ps1`.

## Sub-pages

- [find.md](find.md) - the tiered search, the applicability check, and the
  recommendation report.
- [build.md](build.md) - the find-first decision tree, the security gate for
  public sources, and authoring a new skill (born-local vs born-shared).
- [update.md](update.md) - the pull (drift) and push (common vs deviation)
  flows and the provenance mechanics behind the golden rule.

## Disambiguation

`manage-skills` operates on the **catalog lifecycle** - discover, add, vendor,
sync. It is not [agent-files-review](../agent-files-review/SKILL.md), which
validates the **syntax and conventions** of one agent file (frontmatter, mirror
sync, whitespace). The normal order is: run `manage-skills` to bring a skill in
or push one out, then `agent-files-review` to validate the file you ended up with.
