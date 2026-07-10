---
core: address-pr-feedback
core-pin: v0.10.0
---

# Touki overlay - address-pr-feedback

Repo-specific companion to the vendored [address-pr-feedback](SKILL.md) skill. The
`SKILL.md` is a **pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in its frontmatter). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here.

## Cross-references (the core names these skills generically)

- [`create-pr`](../create-pr/SKILL.md) - opening the initial PR (the same publish
  gate, different edit scope).
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - the validation checklist
  that applies to both initial and follow-up rounds.

## Touki specifics

- The "Working with the user on changes" approval rule the core points at is in
  [AGENTS.md](../../../AGENTS.md#working-with-the-user-on-changes) - the source of
  truth for the commit/push publish boundary and the not-approval phrasings, which
  has been violated on this repo specifically during PR-feedback rounds. Re-read it
  at the start of every invocation.

## Updating

Pull upstream changes to the core with `gh skill update address-pr-feedback`
(review the diff, re-pin). Keep touki-specific additions in this file, not in the
core.
