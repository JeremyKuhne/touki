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

### Lightweight-harness traps

- **Reject accidental compiler errors.** A harness that returns only analyzer
  diagnostics makes an invalid snippet look like a successful "reports nothing"
  test. Unless a test intentionally covers erroneous code, fail when
  `compilation.GetDiagnostics()` contains an error. For intentional errors,
  assert the expected compiler diagnostic explicitly.
- **Implement every option member the analyzer uses.** A dictionary-backed
  `AnalyzerConfigOptions` double must override `Keys` when the analyzer enumerates
  configuration; the base implementation throws. Its provider must return the
  intended options for the tested syntax tree.
- **Enable disabled rules in the compilation.** An analyzer whose descriptor has
  `isEnabledByDefault: false` produces nothing until the test sets its ID through
  `CompilationOptions.WithSpecificDiagnosticOptions`, for example to
  `ReportDiagnostic.Warn`. Otherwise every negative test passes vacuously.

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
- **Each supported language** - use separate C# and VB test methods and fixtures.
  A language-neutral `IOperation` analyzer still receives language-specific syntax,
  conversions, and error recovery; one language passing does not establish the
  other.
- **Equivalent call shapes** - for argument-sensitive rules, cover positional,
  named, and reordered arguments, plus the target call or argument nested inside a
  larger expression. Bind arguments by parameter identity rather than source order.
- **Negative - already correct** - the idiomatic form the rule steers toward does
  **not** fire. (`AnalyzeComparison_IsNullPattern_ReportsNoDiagnostic`.)
- **Negative - lookalike** - similar-but-fine code does not fire (comparing two
  non-null values, comparing against a named constant rather than the `null`
  literal). (`AnalyzeComparison_NonNullEquality_ReportsNoDiagnostic`.)
- **Boundary / known false-positive risks** - generated code (must stay silent given
  `ConfigureGeneratedCodeAnalysis(None)`), partial/erroneous code the IDE feeds while
  the user is mid-edit, null/error types, deconstruction and multi-local declarations,
  constructed generic symbols, default/unknown options, unsupported map entries,
  nullable vs non-nullable contexts, and expression vs statement position.
- **Adversarial depth** - if source or embedded input controls traversal depth, use a
  fixture beyond the known failing shape. Run a recursion-regression probe in a child
  process so `StackOverflowException` cannot terminate the test runner.
- **Lifecycle and cancellation** - for pooled or cached components, run sequential
  requests with distinct cancellation tokens and repeated create/dispose or
  open/close cycles; assert cache cardinality or collection where practical.
- **Exact location** - when using the official harness, assert the span with markup,
  not just presence.
- **The code fix** (if any) - before/after equality; trivia preservation; no action
  offered when the code is already correct, uneditable, or outside the fixer's
  supported shapes; and explicit coverage for every known invalid rewrite context.
  Keep diagnostic tests for those unsupported shapes to prove reporting does not
  depend on fix eligibility. If an action may change semantics, assert its
  `(may change semantics)` title and the intended changed interpretation.
- **FixAll** - prove the combined result across multiple occurrences and every
  applicable conflict shape. Read [fix-all.md](fix-all.md); include a positive
  control that fails if only one occurrence is processed.

A useful discipline from the Roslyn SDK tutorial: write the "should not fire" tests
*first*. They are where real analyzers go wrong, because the cheap syntactic match
over-triggers until the semantic guards are added.

For a performance or stability regression, retain the triggering scale dimension
and verify that a compiling mutation of the fix makes the test fail. A deep-input
test that never reaches the former recursion depth, or a semantic test whose source
does not exercise the guarded null/constructed shape, pins nothing.

## Validate false positives on real code

Unit tests establish behavior on cases you anticipated; they do not measure the
false-positive rate on code you did not design. Before enabling a rule by default,
run it against at least one large, representative codebase that does not already
conform to the rule by construction.

Triage every report from a bounded run:

1. Record the repository revision, analyzer revision, configuration, and total
   compilations or projects analyzed.
2. Classify each report as actionable, intentional pattern, analyzer defect,
   duplicate/noise, or uncertain. Preserve representative source shapes without
   copying proprietary code into fixtures.
3. Convert every analyzer defect and important uncertain shape into a focused
   positive or negative test before changing the implementation.
4. Rerun the same revision and configuration. Compare report counts and every
   classification that should not have changed.

Treat default severity as an evidence claim. A clean unit suite is insufficient to
justify enabling a warning broadly; use the observed precision, impact, and cost of
remediation. If the sample is too small or domain-specific, ship disabled by
default and state what evidence is still missing.

## Prove no-fix contexts explicitly

For every source shape where the analyzer reports but the fixer must decline, use
the code-fix harness to assert zero registered actions. Do not infer this from an
unchanged fixed document: that result cannot distinguish "no action offered" from
an action that ran and returned its input.

Keep the matching analyzer assertion in the same fixture or a paired test. This
proves that diagnostic eligibility remains broader than fix eligibility. Include
metadata-only declarations, stale or malformed diagnostic properties, missing
additional locations, unsupported syntax shapes, and semantic preconditions that
cannot be re-established in the current document when those cases apply.

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

For a configurable rule, add a second temporary probe that changes the relevant
`.editorconfig` option and proves the real build changes behavior. In-memory option
doubles test analyzer logic; they do not prove the compiler is supplying the
repository's configuration as intended.

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
