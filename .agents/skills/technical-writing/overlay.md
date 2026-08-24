---
core: technical-writing
core-pin: v0.16.1
---

# Touki overlay - technical-writing

Repo-specific companion to the vendored [technical-writing](SKILL.md) skill. The
core and its sibling pages are a **pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills). Do not
hand-edit them; keep Touki-specific authority and workflow links here.

## Touki bindings

- [AGENTS.md](../../../AGENTS.md) is the authority for repository terminology,
  ASCII punctuation, approval boundaries, and contributor-facing style.
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) first establishes that PR
  claims match the current diff and observed validation. This skill then reviews
  the reader-facing candidate; [`create-pr`](../create-pr/SKILL.md) or
  [`address-pr-feedback`](../address-pr-feedback/SKILL.md) retains publication.
- [`agent-files-review`](../agent-files-review/SKILL.md) owns customization
  behavior and file correctness. Use this skill only after those facts are
  settled when agent-facing prose also needs a comprehension pass.
- [`code-comprehension`](../code-comprehension/SKILL.md) owns source-code naming
  and structure rather than human-facing prose.
- Personal voice profiles remain private and user-scoped. Do not add personal
  writing evidence or a voice-profile skill to this public repository.

## Updating

Pull upstream changes with `gh skill update technical-writing` (review the diff,
re-pin). Keep Touki-specific additions in this file, not in the core.
