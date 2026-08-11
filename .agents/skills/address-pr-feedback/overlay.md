---
core: address-pr-feedback
core-pin: v0.15.0
---

# Touki overlay - address-pr-feedback

Repo-specific companion to the vendored [address-pr-feedback](SKILL.md) skill. The
`SKILL.md` and its sibling page [thread-workflow.md](thread-workflow.md) are a
**pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in its frontmatter). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here.

> **Pinned to a release.** The core is pinned to the commons **v0.15.0** tag. Pull
> later upstream changes with `gh skill update address-pr-feedback` (review the
> diff, re-pin to the new tag).

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
- `push`, `ship it`, or `send it` authorizes only pushing existing commits, plus
  the reply-and-resolve follow-through described below.
- `update the PR` authorizes only the named remote PR action; it does not
  authorize a prerequisite commit or push.
- Commit, push, and PR-operation approval do not imply one another. Every action
  performed must be explicit in the same most recent message, with one exception:
  replying to and resolving an addressed review thread, which is standing.

The latest [AGENTS.md](../../../AGENTS.md#working-with-the-user-on-changes)
always wins over examples in the vendored core.

## Standing approval: always resolve an addressed thread

The core's step 7 defers reply-and-resolve approval to repository guidance. In
touki that approval is **standing**: resolving review threads needs no
per-message approval, and `push` on a review round infers reply-and-resolve. The
push itself still needs its own verb.

A declined comment gets an in-thread reply and stays open.

## Getting the next review (the core's step 8)

**Copilot auto-review is always on for this repo.** A review posts automatically
a minute or two after the PR opens and after every subsequent push, so the core's
"repository automatically reviews pushes" branch is the one that applies: never
request or re-request a review, and do not poll for the automatic one. Say that a
review will land on its own, and expect a fresh round of comments on each new
commit.

Later rounds drift toward nits and false positives. Verify every claim against
the code before acting.

## Thread mechanics

The core's [thread-workflow.md](thread-workflow.md) is the `gh` fallback for
listing, replying to, and resolving threads when the PR tool cannot. The only
touki bindings it needs are the repository coordinates: owner `JeremyKuhne`, name
`touki`. There is no `upstream` remote, so the PR always lives on `origin`.

## Updating

Pull upstream changes to the core with `gh skill update address-pr-feedback`
(review the diff, re-pin). Keep touki-specific additions in this file, not in the
core.
