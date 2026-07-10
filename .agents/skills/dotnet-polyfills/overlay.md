---
core: dotnet-polyfills
core-pin: v0.10.0
---

# Touki overlay - dotnet-polyfills

Repo-specific companion to the vendored [dotnet-polyfills](SKILL.md) skill. The
`SKILL.md` and its `references/packages.md` page are a **pinned copy of the
portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`). Do not hand-edit the core.

> **Pinned to a release.** The core is pinned to the commons **v0.10.0** tag.

## Touki bindings

- **`KlutzyNinja.Touki` is *this* repo.** The core's "use Touki's runtime
  polyfills on top" step points back here: touki *is* the package that ships those
  polyfills. So in touki you are usually the one *adding* a polyfill, not consuming
  one.
- **Hand-rolled polyfills live under**
  [touki/Framework/Polyfills/](../../../touki/Framework/Polyfills/), namespaced to
  the BCL namespace they extend.
- **Package versions** are centralized in
  [Directory.Packages.props](../../../Directory.Packages.props) (the official
  `System.Memory`, `Microsoft.Bcl.*`, `Microsoft.IO.Redist`, and `PolySharp`
  versions).

## Cross-reference

- [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) - touki's authoring
  skill for *writing* a hand-rolled polyfill (layout, behavior-parity, the net481
  codegen gotchas). Come to `dotnet-polyfills` to ask "which package or generator
  covers this"; go to `polyfill-dotnet-api` once the answer is genuinely "none."
