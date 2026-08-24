---
compatibility: Uses host-native skill discovery. GitHub CLI 2.90 or later enables provenance-aware cross-host install and update. The bundled user-copy installer requires PowerShell 7 and git; private GitHub sources also require GitHub CLI.
description: Find, build, install, review, update, retire, and share agent skills at repository or user scope. Use when asked to find a skill, build/create one, install/add/vendor one for a project or person, review its routing or workflow, update/sync it, retire/remove it, or reconcile a local change against the commons vs an overlay. Covers host-specific locations, personal-skill privacy, provenance-aware tooling, and the full skill lifecycle. For frontmatter/schema/link diagnostics, use `agent-files-review`.
license: MIT
metadata:
    applicability: universal
    binding: optional-overlay
    github-path: skills/manage-skills
    github-pinned: v0.16.1
    github-ref: refs/tags/v0.16.1
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: b2a3d3dfc1594bc68fddb07b8538a0f64ae8176a
    maturity: canary
    portability: portable
    related: none
    requires: agent-files-review, technical-writing
    risk: remote-write
name: manage-skills
---
# Manage skills

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

The lifecycle skill for project and personal Agent Skills: discover one, choose
its source ownership and runtime scope, install it for the intended hosts, review
whether it guides agents effectively, update or retire it, and keep local changes
in sync with the shared set. It turns "find a skill", "build a skill", "install
this for me", "add this to the repo", "is this skill effective", "update the
skill", and "remove this skill" into actions aligned with the sharing model
instead of ad-hoc copies.

Keep two decisions separate. **Source ownership** says whether a skill is portable
and shared, repository-specific, or personal/private. **Installation scope** says
where a host discovers a runtime copy: project, user, plugin/managed, or remote.
A portable core can be installed at project or user scope; a private personal skill
can remain born-local without entering a repository. The Agent Skills specification
defines package shape, not discovery paths or precedence; each host owns those.

## The six verbs

| Ask | Do | Detail |
| --- | --- | ------ |
| "find a skill for X", "is there a skill that does X" | Tiered search (local -> commons -> public), with an applicability check for this repo. | [find.md](find.md) |
| "build a skill for X", "create a skill" | **Run find first.** Only author new if it exists nowhere; otherwise vendor or tweak the existing one. | [build.md](build.md) |
| "install this skill", "add this for me", "vendor this into the repo" | Choose source ownership, target surfaces, scope, and host path before selecting `gh`, a host CLI, or a script. | [install.md](install.md) |
| "review this skill", "is this skill effective" | Review invocation, workflow closure, progressive disclosure, portability, and lifecycle placement; then hand file validation to `agent-files-review`. | [review.md](review.md) |
| "update the skill", "sync my change", "pull skill updates" | Pull upstream drift; or push a local improvement, classified common (ask before upstreaming) vs deviation (overlay). | [update.md](update.md) |
| "retire this skill", "remove this skill" | Find dependents and replacements first, then deprecate or remove without leaving stale routing, catalog, packaging, or validation state. | [retire.md](retire.md) |

These chain: `build` begins with `find`; an install request runs `find` and the
public-source security gate before `install`; `build`, `install`, and `update`
finish with `review`; and `review` finishes with `agent-files-review` for file-level
validation. A local skill that needs a tweak follows `update` so core/overlay
ownership stays explicit. `retire` inventories every installed scope and host
before removing anything.

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

Use the required `technical-writing` skill after behavior and routing are
settled. It owns human and agent comprehension, grounding, and reader cost;
this skill retains lifecycle placement and semantic correctness, while
`agent-files-review` retains file correctness. A prose pass must preserve
literal trigger phrases, requirement strength, tool names, permission
boundaries, and stop conditions.

For the `SKILL.md` frontmatter check specifically, this skill bundles
[scripts/Validate-Skills.ps1](scripts/Validate-Skills.ps1) - a dependency-free
PowerShell port of the Agent Skills spec validator - so the check runs anywhere
the skill is vendored, without the upstream tool. Run it on the skill directory:
`pwsh scripts/Validate-Skills.ps1 <skill-dir>`. A commons portfolio uses
`-RequirePortfolioMetadata` to enforce its metadata and overlay contract.

For deterministic user-scope copies, this skill also bundles
[scripts/Install-UserSkill.ps1](scripts/Install-UserSkill.ps1). Read
[install.md](install.md) and any local overlay before running it. The skill does
not pre-approve shell access; script execution remains subject to the host's
normal terminal/tool permission flow.

For a new downstream binding, start from
`assets/overlay.md.tmpl`, replace its skill and pin tokens, and keep every local
path and concrete cross-reference in that overlay.

## Sub-pages

- [find.md](find.md) - the tiered search, the applicability check, and the
  recommendation report.
- [build.md](build.md) - the find-first decision tree, the security gate for
  public sources, and canonical source ownership (born-repository,
  born-personal, or born-shared).
- [install.md](install.md) - source ownership vs runtime scope, host locations,
  personal-skill privacy, tool selection, verification, and lifecycle effects.
- [evaluations.md](evaluations.md) - should/should-not install cases for scope,
  host collisions, private skills, tooling fallbacks, updates, and retirement.
- [review.md](review.md) - semantic and lifecycle review: invocation, agent
  execution, progressive disclosure, portability, overlap, and maintenance.
- [update.md](update.md) - the pull (drift) and push (common vs deviation)
  flows and the provenance mechanics behind the golden rule.
- [retire.md](retire.md) - dependency-first deprecation or removal without stale
  routing, catalog, packaging, or validation state.

## Disambiguation

`manage-skills` operates on **skill lifecycle and effectiveness**: discover, add,
review semantic workflow quality, vendor, and sync. It is not
`agent-files-review`, which validates **agent-file correctness**: frontmatter,
schema/conventions, mirror sync, whitespace, links, and diagnostics. A complete
skill review uses both in that order; neither substitutes for the other. For an
overlay, `manage-skills` decides what belongs there, while `agent-files-review`
validates the resulting overlay file.
