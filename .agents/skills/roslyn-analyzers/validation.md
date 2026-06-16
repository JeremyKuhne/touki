# Validating the analyzer

Detail for the [roslyn-analyzers](SKILL.md) skill. An analyzer is only as good as
its test suite: it must fire on exactly the code it should and stay silent on
everything else. False positives are worse than a missing rule because they train
users to ignore (or suppress) the analyzer. The examples below use the test suite
for a `UseIsNull` analyzer (`UseIsNullAnalyzerTests.cs`) as a running example.

## Two harness options

### The official `Microsoft.CodeAnalysis.Testing` harness

The Roslyn SDK ships purpose-built test packages -
`Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`,
`Microsoft.CodeAnalysis.CSharp.CodeFix.Testing`, and the runner-specific variants
(`.MSTest` / `.XUnit` / `.NUnit`). Prefer this harness for anything beyond a trivial
diagnostic-only analyzer, and **always** for code fixes. It gives you:

- A **markup syntax** that pins the exact expected diagnostic span in the source:
  - `[|text|]` - a diagnostic is reported on `text` (single-descriptor analyzers).
  - `{|ABCD0001:text|}` - a diagnostic with that specific ID is reported on `text`.
- `VerifyCS.VerifyAnalyzerAsync(source)` - asserts the marked diagnostics, and only
  those, are produced.
- `VerifyCS.VerifyCodeFixAsync(source, fixedSource)` - applies the fix and asserts
  the result equals `fixedSource`, including a FixAll pass.
- Control over reference assemblies / target framework, so you can test the analyzer
  against the same surface your consumers compile against.
- The ability to embed expected compiler diagnostics (e.g. `{|CS0029:...|}`) so a
  test snippet that intentionally does not compile still asserts cleanly.

Span-accurate location testing is the main reason to use this harness: it catches
the "fires, but squiggles the wrong token" bug that a presence-only check misses.

### A lightweight in-memory harness

A minimal hand-written harness (`AnalyzerTestHarness.cs`)
compiles a snippet with `CSharpCompilation.Create` (references pulled from
`TRUSTED_PLATFORM_ASSEMBLIES`), runs the analyzer via
`compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync()`, and returns
the raw `Diagnostic` list. It is deliberately minimal - good enough for
diagnostic-only analyzers where you assert on ID and count, and it avoids taking a
dependency on the testing packages.

Use it for simple presence/absence assertions. Reach for the official harness when
you need exact span markup, code-fix verification, or controlled reference sets.
Either way, if your repo treats warnings as errors or enforces XML-doc comments,
the test project may need a local `.editorconfig` to relax rules that fire on the
inline test snippets - for example disabling `CS1591` (missing XML docs).

### Testing a code fix without the official harness

If you skip `Microsoft.CodeAnalysis.CSharp.CodeFix.Testing`, a code fix can still be
exercised in-memory with an `AdhocWorkspace` (the test project references
`Microsoft.CodeAnalysis.CSharp.Workspaces`):
`<root>.analyzers.tests/CodeFixTestHarness.cs`
adds a `Document` to an ad-hoc project, runs the analyzer to get the diagnostic,
calls `provider.RegisterCodeFixesAsync` with a `CodeFixContext` whose registration
delegate captures the offered `CodeAction`s, then applies the first action via
`action.GetOperationsAsync()` -> `ApplyChangesOperation.ChangedSolution` and returns
the changed document's text to assert on. The test project also needs a project
reference to `<root>.analyzers.codefixes`. Pin the before/after source and assert the
expected member gained `readonly`; test the fix on a **non-mutating** member, since
"make readonly" on a genuinely mutating member would produce a compiler error.

## Coverage checklist

For every rule, test all of:

- **Positive** - the canonical violation fires exactly one diagnostic with the right
  ID. (`AnalyzeComparison_EqualsNull_ReportsDiagnostic`.)
- **Both/all shapes** that should fire - operand on the left vs right, `==` vs `!=`,
  each `OperationKind`/`SyntaxKind` you registered.
  (`AnalyzeComparison_NullOnLeft_ReportsDiagnostic`.)
- **Negative - already correct** - the idiomatic form the rule steers toward does
  **not** fire. (`AnalyzeComparison_IsNullPattern_ReportsNoDiagnostic`.)
- **Negative - lookalike** - similar-but-fine code does not fire (comparing two
  non-null values, comparing against a named constant rather than the `null`
  literal). (`AnalyzeComparison_NonNullEquality_ReportsNoDiagnostic`.)
- **Boundary / known false-positive risks** - generated code (must stay silent given
  `ConfigureGeneratedCodeAnalysis(None)`), partial/erroneous code the IDE feeds while
  the user is mid-edit, nullable vs non-nullable contexts, generics, expression
  vs statement position.
- **Exact location** - when using the official harness, assert the span with markup,
  not just presence.
- **The code fix** (if any) - before/after equality, that the fix is a no-op /
  not offered when the code is already correct, and that **FixAll** produces the same
  result across many occurrences.

A useful discipline from the Roslyn SDK tutorial: write the "should not fire" tests
*first*. They are where real analyzers go wrong, because the cheap syntactic match
over-triggers until the semantic guards are added.

## Run in Debug and Release

Run `dotnet test -c Release`, not just Debug, before declaring the analyzer done.
Analyzers are ordinary IL subject to the same Release inlining and optimization
differences as the rest of the codebase.

```pwsh
dotnet test <root>.analyzers.tests/<root>.analyzers.tests.csproj -c Release
```

## The dogfood probe

If you wire the analyzer to run on the library's own sources (`OutputItemType="Analyzer"`
in `<root>.csproj`), prove it is actually live -
a misconfigured analyzer reference fails open and silently analyzes nothing. The
cheapest proof is a temporary violation:

1. Introduce one line that should trip the rule in a real source file.
2. `dotnet build <root>.csproj -c Release` - confirm it now reports the
   diagnostic (it is fatal as a build **error** if the consumer repo sets
   `TreatWarningsAsErrors`).
3. Revert the line and confirm the build is green again.

This is more reliable than reading the analyzer-execution report from build output,
which is easily buried (see [performance.md](performance.md)). Do not leave the
probe behind.

## When the analyzer should not apply everywhere

Dogfooding can collide with code you deliberately do not want restyled - e.g. the
faithfully-ported BCL polyfills under `src/_generated/`. Scope the rule
off for that subtree with a folder `.editorconfig` rather than rewriting ported
code:

```ini
# src/_generated/.editorconfig
[*.cs]
dotnet_diagnostic.ABCD0001.severity = none
```
