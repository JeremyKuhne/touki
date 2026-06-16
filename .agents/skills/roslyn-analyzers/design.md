# Designing the analyzer

Detail for the [roslyn-analyzers](SKILL.md) skill. Authoring rules for a correct,
maintainable `DiagnosticAnalyzer`. The running example is a `UseIsNull` analyzer
(flag `== null` / `!= null`, prefer `is null`); this page explains the choices
behind it.

## Project setup recap

The analyzer assembly must be `netstandard2.0` so it loads in every compiler host
(command-line `csc`, the .NET SDK, Visual Studio's .NET Framework host, VS Code).
RS1038 enforces this. Keep `EnforceExtendedAnalyzerRules=true` on - it turns on the
`RS####` authoring analyzers that catch most of the mistakes below at build time.
Both `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` must exist
as `AdditionalFiles` (RS2008), and every new rule ID must be listed in the
unshipped file (RS2000).

## Rule 1: an analyzer instance is stateless and thread-safe

The host creates one analyzer instance and reuses it across compilations, threads,
and (in the IDE) edits. Never store per-analysis state in an instance or static
field. All state lives in locals inside the registered callbacks, or in
per-compilation state captured by `RegisterCompilationStartAction` (see
[performance.md](performance.md)). Storing a `Compilation` or `ISymbol` in a field
is doubly wrong: besides the race, it **roots** that compilation's object graph
across every later edit. Resolve well-known symbols at compilation start and capture
them in the closure - see the rooting rule in
[performance.md](performance.md#the-rooting-rule-capture-in-the-closure-never-in-a-field).

In `Initialize`, always:

```csharp
public override void Initialize(AnalysisContext context)
{
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();
    // register narrow actions here
}
```

- `EnableConcurrentExecution()` lets the host run your callbacks in parallel. It is
  also an assertion that they are thread-safe - which they are, if you followed the
  no-shared-state rule.
- `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` stops the
  analyzer from firing on generated code (designer files, source-generator output).
  Skipping this is a common false-positive source.

## Rule 2: register the narrowest action, with a kind filter

The registration *is* the performance contract - the host only calls you for the
kinds you ask for. Prefer, in order:

1. **`RegisterSymbolAction(..., SymbolKind.X)`** - for declaration-level rules
   (types, methods, properties). The host walks symbols for you.
2. **`RegisterOperationAction(..., OperationKind.X)`** - for semantic, code-body
   rules. `IOperation` is the bound, language-agnostic tree; prefer it for anything
   that depends on what the code *means* (see rule 3).
3. **`RegisterSyntaxNodeAction(..., SyntaxKind.X)`** - for purely syntactic rules
   where you only need the shape of the source, like `UseIsNullAnalyzer` keying on
   `EqualsExpression` / `NotEqualsExpression`.

Avoid `RegisterSyntaxTreeAction` and `RegisterSemanticModelAction` unless you truly
need to scan a whole file - they hand you the entire tree and make *you* do the
walking, which is where slow analyzers come from. Never call
`SyntaxNode.DescendantNodes()` to hunt for nodes a registration filter would have
delivered directly.

## Rule 3: prefer `IOperation` over raw syntax when semantics matter

Raw syntax is a literal transcription of the source - it cannot tell you what a
name binds to, whether an implicit conversion happened, or whether `+` is string
concatenation or numeric addition. If your rule depends on meaning, register an
operation action and inspect the `IOperation`; it is bound, so you get the resolved
symbol, the converted type, and constant values without re-deriving them, and the
same analyzer then works for both C# and VB.

Use raw syntax only when the rule genuinely is about source shape (a specific
operator token, a `using` directive's position, brace style). `UseIsNullAnalyzer`
is legitimately syntactic: "is this the `== null` *spelling*" is a question about
the source text, not the binding, so it keys on `SyntaxKind` and never touches the
semantic model.

When a syntactic match needs *one* semantic confirmation, do the cheap syntax check
first and only then reach for `context.SemanticModel` - never the other way round
(see [performance.md](performance.md)).

## Rule 4: a stable, well-formed `DiagnosticDescriptor`

```csharp
private static readonly DiagnosticDescriptor s_rule = new(
    id: "ABCD0001",
    title: "Use pattern matching for null checks",
    messageFormat: "Use '{0}' instead of '{1}'",
    category: "Usage",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description: "Comparisons against the null literal should use 'is null' / 'is not null'.",
    helpLinkUri: "https://github.com/your-org/your-repo");
```

- **ID** is a permanent contract - users pin severities to it in `.editorconfig`.
  Pick the `<PREFIX>####` prefix and never reuse or renumber an ID.
- Cache the descriptor in a `static readonly` field and return a cached
  `ImmutableArray` from `SupportedDiagnostics` (`ImmutableArray.Create(s_rule)`).
  Do not allocate a new descriptor or array per call.
- `messageFormat` is a format string; pass arguments at `Diagnostic.Create`. Do not
  pre-format with interpolation - it defeats localization and allocates.
- Set a real `helpLinkUri`.
- For an analyzer you intend to *ship and localize*, move `title`/`messageFormat`/
  `description` into a `.resx` and use `LocalizableResourceString`. For an
  internal, English-only repo rule the inline strings above are acceptable.

## Rule 5: report at the tightest location

Report the diagnostic on the smallest span that identifies the problem - the
offending operator, identifier, or argument - not the whole statement. The squiggle
should land exactly on what the user must change. Pass the precise
`Location`/`SyntaxNode.GetLocation()` to `Diagnostic.Create`.

## Rule 6: honor configuration; do not hardcode severity behavior

Report the diagnostic unconditionally and let the host's `.editorconfig` severity
mapping decide whether it is an error, warning, suggestion, or suppressed. Do not
read severities yourself or branch on configuration; the descriptor's
`defaultSeverity` plus user `.editorconfig` lines are the entire mechanism.

## Rule 7: release tracking is part of the change

Every new or changed rule updates the analyzer release files in the same commit:

- New rule -> add a row under `### New Rules` in
  `AnalyzerReleases.Unshipped.md`.
- On release, entries move from `Unshipped` to `Shipped` under a version heading.

RS2000/RS2002 fail the build if the unshipped file and `SupportedDiagnostics`
disagree, so this cannot be forgotten if you build before committing.

## Code-fix providers (optional)

A code fix is a separate type from the analyzer:

- `[ExportCodeFixProvider(LanguageNames.CSharp)]` plus `[Shared]` on a
  `CodeFixProvider`.
- `FixableDiagnosticIds` returns the analyzer's ID(s). Hardcode the id strings (a
  stable public contract) rather than referencing the analyzer assembly - the
  code fix lives in a different assembly (see below).
- Implement `RegisterCodeFixesAsync`; compute the edit as an immutable
  `Document`/`Solution`/`SyntaxNode` transformation and register a `CodeAction`.
  `SyntaxGenerator` (from `Microsoft.CodeAnalysis.Editing`) is the language-neutral
  way to edit modifiers/declarations - e.g. `generator.WithModifiers(decl,
  generator.GetModifiers(decl).WithIsReadOnly(true))`.
- Override `GetFixAllProvider()` (usually `WellKnownFixAllProviders.BatchFixer`) so
  "fix all occurrences" works.
- Use a stable, descriptive `equivalenceKey` on the `CodeAction` so FixAll can group
  identical fixes.
- Only offer the fix when the target is editable - check
  `member.DeclaringSyntaxReferences` is non-empty before registering, so the fix
  does not appear on members defined in metadata.

A fix that can change behavior (not just style) should be offered conservatively
and covered by before/after tests ([validation.md](validation.md)).

### Code fixes go in a SEPARATE assembly (RS1022)

A `CodeFixProvider` references the Roslyn **Workspaces** layer
(`Document`, `CodeAction`, `SyntaxGenerator`). **RS1022 forbids any reference to
Workspaces types from an assembly that also contains a `DiagnosticAnalyzer`** -
analyzers must stay Workspaces-free so they load in the command-line compiler. So
a repo that ships code fixes needs a second project:

- `<root>.analyzers` - the `DiagnosticAnalyzer`s. References
  `Microsoft.CodeAnalysis.CSharp`, `EnforceExtendedAnalyzerRules=true`.
- `<root>.analyzers.codefixes` -
  the `CodeFixProvider`s. `netstandard2.0`, signed, `IsPackable=false`,
  `IncludeBuildOutput=false`. References
  `Microsoft.CodeAnalysis.CSharp.Workspaces`. Do **not** set
  `EnforceExtendedAnalyzerRules` and do **not** add
  `Microsoft.CodeAnalysis.Analyzers` here - those are for the analyzer assembly.

Both assemblies pack into `analyzers/dotnet/cs/` from the **same**
`_AddAnalyzersToPackage` target (a second `MSBuild Targets="GetTargetPath"` call
feeding the same `_PackageFiles` group). The command-line compiler loads the
code-fix dll but never instantiates the provider (no analyzer in it), so the
absence of Workspaces at build time is fine; the IDE supplies Workspaces when it
offers the fix. This is the standard StyleCop/Roslynator split.

**Packaging gotcha:** `MSBuild Targets="GetTargetPath"` returns the code-fix
assembly path but does **not** build it, and nothing else in the library's graph builds
it either (it is not an analyzer *of* the library). Add a build-ordering
`ProjectReference` from `<root>.csproj` to the code-fix project with
`ReferenceOutputAssembly="false" PrivateAssets="all"` and **no**
`OutputItemType="Analyzer"` (adding that would load Workspaces into the
command-line compiler's analyzer context). Without the reference the pack fails
with "Could not find a part of the path ...\<root>.analyzers.codefixes\...".

## Checklist

- [ ] `netstandard2.0`, `EnforceExtendedAnalyzerRules=true`, release files present.
- [ ] No instance/static mutable state.
- [ ] `EnableConcurrentExecution()` + `ConfigureGeneratedCodeAnalysis(None)`.
- [ ] Narrowest registration with a kind filter; no manual tree walks.
- [ ] `IOperation` where semantics matter; raw syntax only for source-shape rules.
- [ ] Cached descriptor + cached `SupportedDiagnostics`; `messageFormat` args, not interpolation.
- [ ] Diagnostic reported at the tightest location.
- [ ] New rule ID recorded in `AnalyzerReleases.Unshipped.md`.
