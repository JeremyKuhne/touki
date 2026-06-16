# Touki overlay - fuzz-testing

Repo-specific companion to the vendored [fuzz-testing](SKILL.md) skill. The
`SKILL.md` and its `references/running.md` page are a **pinned copy of the
portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here.

> **Pinned to a release.** The core is pinned to the commons **v0.8.1** tag. Pull
> later upstream changes with `gh skill update fuzz-testing`.

**Authoritative mechanics.** The core's
[references/running.md](references/running.md) is the *generic* instrument-and-run
guide. Touki's concrete, authoritative commands and prerequisites live in
[touki.fuzz/README.md](../../../touki.fuzz/README.md) - read it first; this overlay
only binds the names.

## Concrete bindings for the core's placeholders

- **Harness project** (`<root>.fuzz`):
  [touki.fuzz](../../../touki.fuzz/touki.fuzz.csproj), namespace `Touki.Fuzz`. It
  cross-targets `net10.0` and `net481` (`<tfm>`), building `touki` as `net472`
  under the net481 target (the `DependencyTargetFramework` trick), so every target
  must compile on both. `ReadOnlySpan<T>` comes from `System.Memory` on net481.
- **Regression project** (`<root>.tests`): promote a reproduced crash into
  `touki.tests` so it runs on every PR.
- **Prerequisites**:
  [touki.fuzz/Install-FuzzPrereqs.ps1](../../../touki.fuzz/Install-FuzzPrereqs.ps1).
- **Target registration**: the `FUZZ_TARGET` switch in
  [touki.fuzz/Program.cs](../../../touki.fuzz/Program.cs); one `<Type>Target.cs`
  per target (e.g.
  [SpanReaderTarget.cs](../../../touki.fuzz/SpanReaderTarget.cs)). Current targets:
  `SpanReader`, `SpanWriter`, `RunLength`, `StringSegment`, `ValueStringBuilder`,
  `GlobSpecification`.
- **Invariant exception**:
  [touki.fuzz/FuzzInvariantException.cs](../../../touki.fuzz/FuzzInvariantException.cs).
- **Plan and phases**:
  [docs/fuzz-testing-plan.md](../../../docs/fuzz-testing-plan.md).
- **Coding style**: [AGENTS.md](../../../AGENTS.md).

## Cross-references (the core's "Related skills")

- [`security-review`](../security-review/SKILL.md) - the DoS / unchecked-length /
  `unsafe` checklist that motivates most fuzz targets.
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - new public parser /
  codec / buffer surface should have a fuzz target (or an explicit "not fuzzed"
  note).
- [`run-tests-on-wsl`](../run-tests-on-wsl/SKILL.md) - the Linux path for AFL /
  libFuzzer (optional; the prebuilt driver runs natively on Windows).
