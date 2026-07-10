---
core: create-pr
core-pin: v0.10.0
---

# Touki overlay - create-pr

Repo-specific companion to the vendored [create-pr](SKILL.md) skill. The
`SKILL.md` is a **pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in its frontmatter). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here.

## Cross-references (the core names these skills generically)

- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - the checklist to walk
  before running this workflow.
- [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) - the design rules the
  self-review validates against for a .NET Framework polyfill.
- [`address-pr-feedback`](../address-pr-feedback/SKILL.md) - the post-PR workflow
  for subsequent edit rounds.

## Touki specifics

- The "Working with the user on changes" approval rule the core points at is in
  [AGENTS.md](../../../AGENTS.md#working-with-the-user-on-changes) (the canonical
  source of the commit/push publish-boundary rule and the not-approval phrasings).
- touki is the canonical repo (no `upstream` remote in the usual clone), so PRs
  target `origin/main`.

## Updating

Pull upstream changes to the core with `gh skill update create-pr` (review the
diff, re-pin). Keep touki-specific additions in this file, not in the core.
