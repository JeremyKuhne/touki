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

## Approval-boundary override

Touki requires separate approval for committing and pushing review fixes. Where
the vendored core treats one publishing verb as approval for both actions, this
overlay narrows it:

- `git commit` requires an explicit commit instruction in the user's most recent
  message.
- `push`, `ship it`, or `send it` authorizes only pushing existing commits.
- `update the PR` or resolving review threads authorizes only the named remote PR
  actions; it does not authorize a prerequisite commit or push.
- Commit, push, and PR-operation approval do not imply one another. Every action
  performed must be explicit in the same most recent message.

The latest [AGENTS.md](../../../AGENTS.md#working-with-the-user-on-changes)
always wins over examples in the vendored core.

## Updating

Pull upstream changes to the core with `gh skill update address-pr-feedback`
(review the diff, re-pin). Keep touki-specific additions in this file, not in the
core.
