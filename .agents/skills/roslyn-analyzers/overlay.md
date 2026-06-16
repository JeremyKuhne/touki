# Touki overlay - roslyn-analyzers

Repo-specific companion to the vendored [roslyn-analyzers](SKILL.md) skill. The
`SKILL.md` and its four sibling pages (`design.md`, `validation.md`,
`existing-analyzers.md`, `performance.md`) are a **pinned copy of the portable
core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here.

> **Pinned to a release.** The core is pinned to the commons **v0.8.1** tag. Pull
> later upstream changes with `gh skill update roslyn-analyzers` (review the diff,
> re-pin to the new tag).

## Concrete bindings for the core's placeholders

- **Analyzer project** (`<root>.analyzers`):
  [touki.analyzers](../../../touki.analyzers/touki.analyzers.csproj) - the
  `netstandard2.0` `DiagnosticAnalyzer` assembly, with
  [AnalyzerReleases.Shipped.md](../../../touki.analyzers/AnalyzerReleases.Shipped.md)
  and
  [AnalyzerReleases.Unshipped.md](../../../touki.analyzers/AnalyzerReleases.Unshipped.md).
- **Test project** (`<root>.analyzers.tests`):
  [touki.analyzers.tests](../../../touki.analyzers.tests/touki.analyzers.tests.csproj),
  with the lightweight
  [AnalyzerTestHarness.cs](../../../touki.analyzers.tests/AnalyzerTestHarness.cs)
  and
  [CodeFixTestHarness.cs](../../../touki.analyzers.tests/CodeFixTestHarness.cs).
- **Code-fix project** (`<root>.analyzers.codefixes`):
  [touki.analyzers.codefixes](../../../touki.analyzers.codefixes/touki.analyzers.codefixes.csproj).
- **Diagnostic-ID prefix** (`<PREFIX>`): `TOUKI`. `TOUKI0001` is the running
  example (`UseIsNull`); `TOUKI0002`-`TOUKI0004` are the defensive-copy /
  `[NonCopyable]` rules.
- **Library package**: the analyzer ships **inside** `KlutzyNinja.Touki`, not as
  its own package. [touki/touki.csproj](../../../touki/touki.csproj) packs the
  analyzer and code-fix assemblies to `analyzers/dotnet/cs/`, with
  `OutputItemType="Analyzer"` for the dogfood run.
- **Working example to copy**:
  [touki.analyzers/UseIsNullAnalyzer.cs](../../../touki.analyzers/UseIsNullAnalyzer.cs)
  and its tests
  [touki.analyzers.tests/UseIsNullAnalyzerTests.cs](../../../touki.analyzers.tests/UseIsNullAnalyzerTests.cs).
- **Coding style**: follow [AGENTS.md](../../../AGENTS.md) (no `var`, target-typed
  `new()`, C# keyword type names, `is null` / `is not null`, indented XML docs).

## Dogfood scoping

When an analyzer dogfoods on touki's own sources, scope it **off** the
faithfully-ported BCL polyfills with
[touki/Framework/Polyfills/.editorconfig](../../../touki/Framework/Polyfills/.editorconfig)
(set the rule's severity to `none` there) rather than restyling ported code.

## Cross-references (the core's "Related skills")

- [`performance-testing`](../performance-testing/SKILL.md) - validate library
  runtime perf (not analyzer perf).
- [`security-review`](../security-review/SKILL.md) - when the analyzer parses or
  reinterprets untrusted input.
- [`il-copy-inspection`](../il-copy-inspection/SKILL.md) - the post-build,
  ground-truth counterpart for the `TOUKI0002`-`TOUKI0004` defensive-copy rules.
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) and
  [`create-pr`](../create-pr/SKILL.md) - before opening a PR.
