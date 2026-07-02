# Code comprehension

> **Moved to a skill.** The readability and cognitive-load guidance - the
> high-confidence rules, the screening thresholds, and the full research evidence
> (with links) - now lives in the vendored
> [`code-comprehension`](../.agents/skills/code-comprehension/SKILL.md) skill, with
> the evidence catalog in its
> [references/research.md](../.agents/skills/code-comprehension/references/research.md).
> The skill auto-invokes on asks like "review this for readability", "is this too
> complex", or "reduce nesting / cognitive load".

The touki-specific bindings - which style authority wins on a conflict and the
line-length rule that overrides the core's 80-column lean - are in the
[touki overlay](../.agents/skills/code-comprehension/overlay.md).

For the touki coding rules the skill defers to, see
[coding_guidelines.md](coding_guidelines.md) and the "Coding style" section of
[AGENTS.md](../AGENTS.md).
