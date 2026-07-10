---
core: agent-files-review
core-pin: v0.10.0
---

# Touki overlay - agent-files-review

Repo-specific companion to the vendored [agent-files-review](SKILL.md) skill. The
`SKILL.md` is a **pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in its frontmatter). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here.

## Touki scaffold paths (the core names these by convention)

- The validator is
  [tools/Validate-AgentFiles.ps1](../../../tools/Validate-AgentFiles.ps1); run it
  plain to validate and with `-Fix` to regenerate the
  [.github/copilot-instructions.md](../../../.github/copilot-instructions.md)
  mirror from [AGENTS.md](../../../AGENTS.md).
- The mixed-catalog and portfolio wrapper is
  [tools/Validate-AgentSkills.ps1](../../../tools/Validate-AgentSkills.ps1). It
  runs the bundled skill validator, strict commons validation, and Touki's
  overlay, relationship, and catalog checks.
- The relative-link checker is
  [tools/Test-AgentFileLinks.ps1](../../../tools/Test-AgentFileLinks.ps1)
  (`-ChangedOnly` and `-Base <ref>` options).
- The CI workflow is
  [.github/workflows/agent-files.yml](../../../.github/workflows/agent-files.yml)
  (markdownlint + offline lychee, `ubuntu-latest`).
- The markdownlint config is
  [.markdownlint.jsonc](../../../.markdownlint.jsonc) - it disables MD013,
  MD022/MD032, MD033, MD041, MD060 and keeps MD040, no-trailing-spaces,
  no-hard-tabs, MD047.
- Touki skill names use the ASCII subset `^[a-z0-9-]{1,64}$`, enforced by
  [Validate-AgentFiles.ps1](../../../tools/Validate-AgentFiles.ps1). The vendored
  [frontmatter reference](frontmatter.md) describes a broader portable rule; use
  Touki's stricter rule for files authored in this repository.

## Cross-references

- [`manage-skills`](../manage-skills/SKILL.md) - the lifecycle skill for adding /
  updating / vendoring skills (distinct from this file-syntax review).
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - its section 4
  final-audit step calls the link checker for changes under `.agents/`,
  `AGENTS.md`, or the mirror.

## Touki war-story (the core anonymized this)

The "stale branch predating a sister PR" link-failure the core describes generically
was PR #110: the branch predated PR #109 (which added the polyfill files), so every
cross-reference to `ConvertExtensions.cs`, `RandomExtensions.cs`,
`StringExtensions.cs`, `EncodingExtensions.cs`, `HashCodeExtensions.cs`,
`CryptographicOperations.cs`, `SpanExtensions.StartsEndsWith.cs`, `StartsWithPerf.cs`,
and `StringExtensionsConcatTests.cs` failed lychee even though those files existed on
the canonical `main`. The fix was rebase-then-link-audit before pushing.

## Updating

Pull upstream changes to the core with `gh skill update agent-files-review` (review
the diff, re-pin). Keep touki-specific additions in this file, not in the core.
