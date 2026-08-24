---
core: manage-skills
core-pin: v0.16.1
---

# Touki overlay - manage-skills

Repo-specific companion to the vendored [manage-skills](SKILL.md) skill. The
core and its sibling pages and scripts are a **pinned copy of the portable core**
from [JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills). Do
not hand-edit them; keep Touki-specific policy and temporary release guards here.

## Private-install safety override

Do not run
[`Install-UserSkill.ps1`](scripts/Install-UserSkill.ps1) with `-Private` from the
v0.16.1 core. Stop and report that private installation is unavailable at this
pin. The released script does not:

- create destination directories and files with owner-only Unix modes;
- reject a local-only source beneath a known synchronization root; or
- identify network-backed paths on POSIX systems.

These are privacy-boundary failures, not optional hardening. Do not work around
the guard with a manual copy or a different destination. Non-private installation
still follows the core workflow, subject to the replacement guard below. Reassess
and remove this override only after a later immutable upstream release addresses
all three cases and includes tests.

## Replacement safety override

Do not run `Install-UserSkill.ps1` with `-Force` at v0.16.1. The preflight checks
the destination's existing ancestor for a Git worktree but not an existing
destination itself, and it rejects a destination inside the source without
rejecting a source inside the destination. Replacement can therefore move a Git
repository or canonical source into the temporary backup and recursively delete
it after installation. Stop and report that replacement is unavailable at this
pin; do not substitute a manual delete or copy. Remove this guard only after a
later immutable upstream release checks both containment directions and the
destination itself, with regression tests for cleanup.

## Touki bindings

- [README.md](../README.md) is the project-scope catalog and disambiguation
  authority for the local portfolio.
- [`agent-files-review`](../agent-files-review/SKILL.md) owns
  [Validate-AgentSkills.ps1](../../../tools/Validate-AgentSkills.ps1),
  [Validate-AgentFiles.ps1](../../../tools/Validate-AgentFiles.ps1), and
  [Test-AgentFileLinks.ps1](../../../tools/Test-AgentFileLinks.ps1).
- [`technical-writing`](../technical-writing/SKILL.md) reviews publishable skill
  prose only after lifecycle placement and routing are settled.

## Updating

Pull upstream changes with `gh skill update manage-skills` (review the diff,
re-pin). Keep Touki-specific additions and temporary release guards in this file,
not in the core.
