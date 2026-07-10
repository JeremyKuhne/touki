---
core: code-comprehension
core-pin: v0.10.0
---

# Touki overlay - code-comprehension

Repo-specific companion to the vendored [code-comprehension](SKILL.md) skill. The
`SKILL.md` and its bundled [references/research.md](references/research.md) are a
**pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`'s frontmatter). Do not hand-edit the
core or its `references/` - `gh skill update` would flag the drift. Everything
touki-specific lives here instead.

## The style authority the core defers to

The core treats its threshold numbers as screening heuristics and says the
consuming repo's own style guide or `.editorconfig` wins on any conflict. In touki
that authority is, in order:

- [AGENTS.md](../../../AGENTS.md) - the canonical "Coding style" and "Line breaks
  and whitespace" rules (explicit types over `var`, `is null` checks, the file
  header, blank-line and operator-placement rules).
- [.editorconfig](../../../.editorconfig) - the machine-enforced formatting and
  analyzer severities.
- [docs/coding_guidelines.md](../../../docs/coding_guidelines.md) - the rationale
  behind the rules (it links back to comprehension as the reason they exist).

Where a core threshold disagrees with these, these win. The one that actually
differs:

- **Line length.** The core flags at 101-120 and refactors at >=121 columns. touki
  breaks a line before 120 characters only when it would otherwise exceed 150, so
  lines up to ~120 are normal and up to 150 are tolerated. Use touki's rule, not
  the core's 80-column lean.

The vendored core also contains one upstream em dash. Keep the core byte-identical
to its pin; Touki-authored additions use plain ASCII and the punctuation fix belongs
upstream before the next re-vendor.

## Cross-references (the core's review-time siblings)

- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - the pre-PR checklist;
  the core's "keep a change small and reviewable" rule feeds directly into it.

## Touki docs

- The bundled [references/research.md](references/research.md) is the canonical
  copy of the readability / cognitive-load evidence.
  [docs/code_comprehension.md](../../../docs/code_comprehension.md) now redirects
  here - do not re-add a `docs/` duplicate; extend this overlay, or push generic
  changes upstream to the commons core.

## Updating

Pull upstream changes to the core (and its `references/`) with
`gh skill update code-comprehension` (review the diff, re-pin). Keep touki-specific
additions in this file, not in the core.
