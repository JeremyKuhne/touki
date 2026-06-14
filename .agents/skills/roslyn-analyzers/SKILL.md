---
name: roslyn-analyzers
description: Design, build, validate, and ship a Roslyn analyzer (and optional code fix) in this repo's `touki.analyzers` project. Use when asked to "write an analyzer", "create a Roslyn/diagnostic analyzer", "add an analyzer rule", "add a code fix", "enforce <convention> at build time", or "flag <pattern> in code". ALWAYS starts by checking whether an existing analyzer suite (the .NET SDK CA/IDE rules, BannedApiAnalyzers, an EditorConfig rule, Roslynator, StyleCop, Meziantou, etc.) already covers the request before authoring anything new. Covers the netstandard2.0 project layout, packing into KlutzyNinja.Touki, the statelessness/concurrency and IOperation-vs-syntax design rules, the Microsoft.CodeAnalysis.Testing validation harness, and the in-IDE performance discipline. For BenchmarkDotNet runtime microbenchmarks see `performance-testing`; for auditing untrusted-input handling see `security-review`.
metadata:
    portability: semi-portable
---

# Roslyn analyzers

Author a Roslyn diagnostic analyzer (and optional code fix) the right way: confirm
nothing already does the job, build it to the analyzer-authoring rules, validate it
with real positive/negative cases, and keep it fast enough to run on every keystroke
in the IDE.

This repo already has a working analyzer to copy from:
[touki.analyzers/UseIsNullAnalyzer.cs](../../../touki.analyzers/UseIsNullAnalyzer.cs)
with tests in
[touki.analyzers.tests/UseIsNullAnalyzerTests.cs](../../../touki.analyzers.tests/UseIsNullAnalyzerTests.cs).
Read those two files before starting; the patterns below are distilled from them
and from the official Roslyn SDK guidance.

## Step 0 - the prime directive: is it already covered?

**Do not write a new analyzer until you have ruled out the existing ones.** A
hand-rolled analyzer is code you own, test, and pay for on every build and
keystroke forever. Most "I want to flag X" requests are already solved by a rule
that ships in the box and only needs a severity bump in `.editorconfig`, or by a
configuration-only analyzer (banned APIs, naming rules). The full survey and the
decision checklist are in [existing-analyzers.md](existing-analyzers.md).

The short version, in priority order:

1. **An existing rule + `.editorconfig` severity** (the .NET SDK `CA*`/`IDE*`
   analyzers are on by default). Cheapest possible answer.
2. **A configuration-only analyzer** - `BannedApiAnalyzers` (ban a type/member),
   an EditorConfig naming rule, `PublicApiAnalyzers` (lock public surface). No
   custom code.
3. **A third-party suite already in the graph or worth adding** - Roslynator,
   StyleCop, Meziantou, SonarAnalyzer.
4. **Only if none fit** - author a custom analyzer here. Continue below.

## Where analyzers live in this repo

- [touki.analyzers/](../../../touki.analyzers/touki.analyzers.csproj) - the
  analyzer assembly. `netstandard2.0`, `EnforceExtendedAnalyzerRules=true`,
  `IsPackable=false`, `IncludeBuildOutput=false`, signed. Needs both
  `AnalyzerReleases.Shipped.md` and
  [AnalyzerReleases.Unshipped.md](../../../touki.analyzers/AnalyzerReleases.Unshipped.md)
  or RS2008 fails the build.
- [touki.analyzers.tests/](../../../touki.analyzers.tests/touki.analyzers.tests.csproj) -
  `net10.0` MSTest project. Every test project needs a local `.editorconfig`
  disabling `CS1591` (see the existing one).
- [touki.analyzers.codefixes/](../../../touki.analyzers.codefixes/touki.analyzers.codefixes.csproj) -
  the `CodeFixProvider`s. A **separate** assembly because a code fix references the
  Roslyn Workspaces layer, which RS1022 forbids in the analyzer assembly. Only
  needed when you ship fixes; see the code-fix section in [design.md](design.md).
- The analyzer ships **inside** `KlutzyNinja.Touki`, not as its own package. The
  `_AddAnalyzersToPackage` target in
  [touki/touki.csproj](../../../touki/touki.csproj) packs both the analyzer and the
  code-fix assemblies to `analyzers/dotnet/cs/`, and the `OutputItemType="Analyzer"`
  project reference in the same file dogfoods the analyzers against touki's own
  sources.

## Workflow

1. **Run step 0.** Rule out existing analyzers ([existing-analyzers.md](existing-analyzers.md)).
   If one fits, configure it and stop - no new code.
2. **Pick the diagnostic ID and descriptor.** Stable `TOUKI####` ID, category,
   default severity, `helpLinkUri`. Add the row to
   [AnalyzerReleases.Unshipped.md](../../../touki.analyzers/AnalyzerReleases.Unshipped.md)
   in the same change or RS2000 fails.
3. **Design the analyzer** to the statelessness, registration, and
   `IOperation`-vs-syntax rules in [design.md](design.md). Copy the shape of
   `UseIsNullAnalyzer`.
4. **Validate** with positive, negative, and boundary cases per
   [validation.md](validation.md). Run in Debug *and* Release.
5. **Check performance** against the in-IDE budget in [performance.md](performance.md):
   cheap syntactic filter first, semantic model only after, symbols cached once per
   compilation, `EnableConcurrentExecution()`.
6. **Decide whether to dogfood it** on touki's own source. If yes, the analyzer must
   be clean against the existing tree or scoped off where it shouldn't apply (see the
   ported-polyfill exclusion in
   [touki/Framework/Polyfills/.editorconfig](../../../touki/Framework/Polyfills/.editorconfig)).
7. **Self-review and ship.** Run the `pre-pr-self-review` skill; new public diagnostic
   surface needs tests, and a perf claim needs a measurement.

## Deep dives

- [existing-analyzers.md](existing-analyzers.md) - the find-first survey: SDK
  `CA`/`IDE` rules, `BannedApiAnalyzers`, EditorConfig naming, `PublicApiAnalyzers`,
  Roslynator/StyleCop/Meziantou/Sonar, and how to tell what is already active.
- [design.md](design.md) - authoring rules: stateless and thread-safe, narrowest
  registration, `IOperation` over raw syntax, descriptors, release tracking, and a
  note on code-fix providers.
- [validation.md](validation.md) - testing: the `Microsoft.CodeAnalysis.Testing`
  markup harness, this repo's lightweight in-memory harness, the coverage checklist,
  and the dogfood probe.
- [performance.md](performance.md) - the in-IDE performance budget, the cheap-first
  rule, per-compilation symbol caching, concurrency, allocation hygiene, and how to
  measure with `ReportAnalyzer`.

## Cross-skill

- Validate library runtime perf (not analyzer perf) with `performance-testing`.
- Run `pre-pr-self-review` before opening a PR; `create-pr` to publish.
- If the analyzer parses or reinterprets untrusted input, run `security-review`.
- For a rule about struct copies (like the TOUKI0002-0004 defensive-copy /
  `[NonCopyable]` analyzers), `il-copy-inspection` is the post-build, ground-truth
  counterpart: it reads emitted IL to confirm a prediction and to find the
  compiler-synthesized copies an `IOperation`-based analyzer cannot see.

## Disambiguation

"Performance" means two different things here. Tuning how fast the **analyzer**
runs inside the IDE is this skill ([performance.md](performance.md)). Measuring how
fast the **library code** runs at execution time with BenchmarkDotNet is
`performance-testing`. They do not share a harness or a budget.
