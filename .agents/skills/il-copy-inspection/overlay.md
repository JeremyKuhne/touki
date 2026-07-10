---
core: il-copy-inspection
core-pin: v0.10.0
---

# Touki overlay - il-copy-inspection

Repo-specific companion to the vendored [il-copy-inspection](SKILL.md) skill. The
`SKILL.md` and its `references/copy-opcodes.md` page are a **pinned copy of the
portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`). Do not hand-edit the core.

> **Pinned to a release.** The core is pinned to the commons **v0.10.0** tag.

## Touki bindings

- **Diagnostic IDs**: the source-level defensive-copy / `[NonCopyable]` rules this
  skill confirms are `TOUKI0002`-`TOUKI0004`, in
  [touki.analyzers](../../../touki.analyzers/touki.analyzers.csproj).
- **The analyzers ship in** `KlutzyNinja.Touki` via
  [touki/touki.csproj](../../../touki/touki.csproj).

## Cross-references (the core's "Related skills")

- [`roslyn-analyzers`](../roslyn-analyzers/SKILL.md) - the source-level predictive
  side (`TOUKI0002`-`TOUKI0004`); this skill confirms its predictions in emitted
  IL.
- [`framework-jit-optimization`](../framework-jit-optimization/SKILL.md) - the next
  layer down (asm): whether the JIT kept the copy.
- [`performance-testing`](../performance-testing/SKILL.md) - whether the copy costs
  measurable time / allocation.
- [`scratch-buffer-strategy`](../scratch-buffer-strategy/SKILL.md) - pooled buffer
  `[NonCopyable]` types audited for by-value copies.
