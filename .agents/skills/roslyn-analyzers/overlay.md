---
core: roslyn-analyzers
core-pin: v0.12.0
---

# Touki overlay - roslyn-analyzers

Repo-specific companion to the vendored [roslyn-analyzers](SKILL.md) skill. The
`SKILL.md` and its five sibling pages (`design.md`, `validation.md`,
`existing-analyzers.md`, `performance.md`, `suppressors.md`) are a **pinned copy of
the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here.

> **Pinned to a release.** The core is pinned to the commons **v0.12.0** tag. Pull
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

The same convention applies to code ported **into** the analyzer project:
[touki.analyzers/NamingStyles/](../../../touki.analyzers/NamingStyles/) is a port of
Roslyn's naming engine and carries its own `.editorconfig` relaxations plus a
`THIRD-PARTY-NOTICES.TXT` entry. Ported files keep both the touki header and the
.NET Foundation MIT header, and deviations from upstream are marked in a comment
starting `DEVIATION from dotnet/roslyn:` with the issue number that motivated them.

## Symbol-walking analyzers

Hard-won specifics from writing [NamingStyleAnalyzer](../../../touki.analyzers/NamingStyleAnalyzer.cs),
which visits every declared symbol in a compilation:

- **An indexer's `ISymbol.Name` is the synthetic `this[]`.** Any analyzer that reasons
  about names has to filter `IPropertySymbol { IsIndexer: true }` or it reports on a
  name nobody wrote and no code fix can change.
- **Filter what cannot be fixed at the report site**: `symbol.IsOverride` and non-empty
  `ExplicitInterfaceImplementations` both mean the name is dictated elsewhere. Report
  the declaration, not the copy.
- **`RegisterSymbolAction` does not cover parameters or type parameters.** Visit them
  from the declaring symbol's action (`INamedTypeSymbol.TypeParameters`,
  `IMethodSymbol.Parameters`), and reach locals and local functions with
  `RegisterOperationAction(OperationKind.VariableDeclarator, OperationKind.LocalFunction)`.
- **An unhandled exception in an analyzer surfaces as `AD0001`, not as a stack trace**,
  and under `TreatWarningsAsErrors` it fails the build with only the exception type and
  message. `ArgumentNullException` with `Parameter 'value'` from a symbol-walking
  analyzer is almost always `string.StartsWith(null)` reached through a `default` struct.
- **Static initializers run in declaration order.** A `static readonly`/`{ get; } =`
  member whose initializer reads another static member declared *below* it gets that
  member's zero value. For a struct with `string` fields that is a fully null instance
  that only explodes later, inside the analyzer, as `AD0001`. Declare the dependency
  first.

## Test harness gotchas

[touki.analyzers.tests/AnalyzerTestHarness.cs](../../../touki.analyzers.tests/AnalyzerTestHarness.cs)
compiles a snippet in memory and runs one analyzer over it. Two things it has to do that
are easy to miss:

- **`AnalyzerConfigOptions.Keys` throws in the base class.** The test double
  [TestAnalyzerConfigOptions](../../../touki.analyzers.tests/TestAnalyzerConfigOptions.cs)
  must override it, or any analyzer that *discovers* configuration by walking keys
  (rather than asking for a known one) fails only under test while working in a real build.
- **A disabled-by-default rule produces nothing until the compilation enables it.** Pass
  `WithSpecificDiagnosticOptions(id => ReportDiagnostic.Warn)`; the harness exposes this
  as the `diagnosticOptions` parameter.

## Disabled-by-default rules
A rule that encodes house style should ship `isEnabledByDefault: false` so it does not
impose itself on package consumers, and be raised in the repo's own `.editorconfig`.
Two consequences to document wherever the rule is configured:

- `dotnet_diagnostic.<id>.severity` is what **enables** it. There is no other switch.
- Once set, that key also **overrides** any per-report severity the analyzer passes to
  `Diagnostic.Create(..., effectiveSeverity, ...)`. If sub-rules need their own
  strictness, the only level that still works is "off", which the analyzer must honor
  itself by not reporting.

## Release tracking (`AnalyzerReleases.*.md`)

The core's design.md Rule 7 covers the happy path. Touki specifics and the traps:

- Release headings must be **numeric** (`## Release 0.4.0`). A prerelease label such
  as `0.4.0-alpha.1` does not parse, so record the exact shipped version in a `;`
  comment above the heading instead.
- Once a rule is listed under a shipped release its id, category, and severity are
  frozen; changing one needs a `### Changed Rules` entry. Renumbering is free only
  while the rule is still unshipped.
- The only valid headings are `### New Rules`, `### Removed Rules`, and
  `### Changed Rules`. Anything else fails with **RS2007**.
- A suppression id cannot be a live row - **RS2002**. See
  [suppressors.md](suppressors.md) Rule 5 for the commented-row convention this repo
  uses instead.

Which rule fires when:

| Code | Means |
| --- | --- |
| RS2000 | A declared diagnostic id is missing from the unshipped file |
| RS2001 | A listed entry's category/severity disagrees with the descriptor |
| RS2002 | A listed id is not a supported diagnostic of any analyzer |
| RS2007 | Invalid or unknown heading in a release file |
| RS2008 | Release tracking not enabled (files missing from `AdditionalFiles`) |

## Documentation and help links

[HelpLinks.ForRule](../../../touki.analyzers/HelpLinks.cs) derives every descriptor's
`helpLinkUri` from the diagnostic id:
`https://github.com/JeremyKuhne/touki/blob/main/docs/analyzers.md#<id lowercased>`.

So **a new rule is not done until [docs/analyzers.md](../../../docs/analyzers.md) has
a matching `## TOUKI####` heading** - otherwise the link in every IDE and build log
resolves to the page but not the anchor. Nothing validates this; the id-to-anchor
coupling is the whole point of deriving the link, and the only guard is remembering
to add the section.

A rule's entry in that file is three things, all of which need updating together:

1. A row in the rules table (ID, rule, category, default severity, configurable,
   requires).
2. A `## TOUKI####` section: what it reports, a short before/after example, every
   `dotnet_code_quality.TOUKI####.*` option with its default, and what the rule
   deliberately stays silent about.
3. A row in the release-history table, whose column is "Rules and suppressions added"
   because suppression ids appear there too.

When citing an external issue as the reason a rule exists, link it in the docs *and*
name it in the source `<remarks>`, so the justification survives someone reading only
one of the two.

## Cross-references (the core's "Related skills")

- [`performance-testing`](../performance-testing/SKILL.md) - validate library
  runtime perf (not analyzer perf).
- [`security-review`](../security-review/SKILL.md) - when the analyzer parses or
  reinterprets untrusted input.
- [`il-copy-inspection`](../il-copy-inspection/SKILL.md) - the post-build,
  ground-truth counterpart for the `TOUKI0002`-`TOUKI0004` defensive-copy rules.
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) and
  [`create-pr`](../create-pr/SKILL.md) - before opening a PR.
