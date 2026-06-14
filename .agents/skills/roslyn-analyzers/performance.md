# Analyzer performance

Detail for the [roslyn-analyzers](SKILL.md) skill. An analyzer is not batch tooling
- it runs **inside the IDE on every keystroke**, concurrently with every other
analyzer, on the UI-latency path. A slow analyzer does not just slow itself; it
degrades typing responsiveness across the whole solution. The Roslyn SDK guidance
is blunt about it: *an analyzer should exit as quickly as possible, doing minimal
work.* Treat the in-IDE per-edit budget as the design constraint.

## The cardinal rule: cheap filter first, semantics last

Order every callback from cheapest check to most expensive, and `return` the moment
a check fails. The expensive thing is the semantic model; most invocations should
never reach it.

1. **Syntax kind / token text** - free; you already filtered to it via the
   registration, then narrow further on shape (which operator, is an operand the
   `null` literal). `UseIsNullAnalyzer` does its entire job here and never touches
   the semantic model.
2. **Cheap syntactic structure** - child-node kinds, modifier lists, argument count.
3. **Semantic model calls** - `GetSymbolInfo`, `GetTypeInfo`, `GetDeclaredSymbol`,
   data-flow analysis. These bind and are comparatively expensive. Only call them
   after the syntactic guards have already established the node is a real candidate.

The Roslyn SDK tutorial makes the same point structurally: it does the syntactic
filtering in one loop and defers the semantic checks to a second loop "because they
have a greater impact on performance."

## Cache well-known symbols once per compilation

If your rule compares against specific types or members (e.g. "is this
`System.Enum.HasFlag`", "does this type carry `[NonCopyable]`"), do **not** resolve
them inside the per-node callback. Resolve them once at compilation start and
capture them:

```csharp
context.RegisterCompilationStartAction(static start =>
{
    INamedTypeSymbol? target = start.Compilation.GetTypeByMetadataName("System.Some.Type");

    // Bail out of the whole analyzer if the type isn't even referenced here.
    if (target is null)
    {
        return;
    }

    start.RegisterOperationAction(
        ctx => AnalyzeInvocation(ctx, target),
        OperationKind.Invocation);
});
```

- **Resolve by metadata name, compare by symbol identity - never by string.**
  `Compilation.GetTypeByMetadataName("Namespace.TypeName")` returns the
  `INamedTypeSymbol` for a fully qualified CLR metadata name (or null on absence /
  ambiguity). The official Roslyn analyzer tutorial is explicit: check types
  through the semantic model and *"don't compare the string from `ToString()`."*
  `ToString()` / `ToDisplayString()` allocate a fresh string on every call;
  `SymbolEqualityComparer.Default.Equals(symbol, target)` is allocation-free.
- `GetTypeByMetadataName` once per compilation, not once per node.
- **Bail early**: if the type/API the rule is about is not referenced by the
  compilation, register nothing and the analyzer costs ~zero on that project. (Only
  bail when *every* diagnostic needs the symbol; if one rule still fires without it,
  pass the nullable symbol through and branch instead.)

### Matching an attribute on a type

To answer "is this type marked `[SomeAttribute]`?", resolve the attribute symbol
once (above) and walk the candidate's already-materialized attribute list,
comparing by identity:

```csharp
foreach (AttributeData attribute in type.GetAttributes())
{
    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
    {
        return true;
    }
}
```

`GetAttributes()` returns the symbol's existing attribute array, so this iterate-
and-compare is allocation-free - no dictionary, no string build. A
`ConcurrentDictionary<INamedTypeSymbol, bool>` "cache" on top of this is usually
overhead, not savings: the work it memoizes is already cheap, and the dictionary
itself allocates. Reach for a memo only when a *profile* shows the lookup dominating.

Matching the fully qualified metadata name (via `GetTypeByMetadataName`) is also
stricter and safer than matching the attribute's *simple* name
(`AttributeClass.Name == "SomeAttribute"`): simple-name matching silently accepts an
unrelated same-named attribute in another namespace. Match the simple name only when
you deliberately want consumers to be able to declare their own marker attribute.

### The rooting rule: capture in the closure, never in a field

A `Compilation` (and the symbols hanging off it) is a large object graph. The
analyzer **instance is reused across compilations and edits**, so storing a
`Compilation`, `ISymbol`, or symbol-keyed collection in a **static or instance
field** of the analyzer roots that graph and leaks memory across edits. Capturing
the resolved symbol in the `RegisterCompilationStartAction` lambda (as above) is the
correct pattern: the closure lives only as long as that one compilation's analysis,
so nothing is rooted afterward. The danger is the field, not the capture - a
per-compilation local or closure is fine.

## Enable concurrency

`context.EnableConcurrentExecution()` lets the host parallelize your callbacks
across cores. It is free throughput *if* your analyzer holds no shared mutable state
(see [design.md](design.md)). Always enable it; always earn it by being stateless.

## Allocation and walk hygiene

Per-keystroke, per-node code is the wrong place to allocate:

- **No LINQ in hot callbacks.** `Where`/`Select`/`Any` allocate enumerators and
  closures on a path that runs thousands of times. Use direct loops and early
  `return`.
- **No `DescendantNodes()` / manual subtree walks** to find something a narrower
  registration would have delivered. The registration filter is the walk; re-walking
  is duplicated work.
- **Cache the descriptor array.** Return a `static readonly`
  `ImmutableArray<DiagnosticDescriptor>` from `SupportedDiagnostics`; never build it
  per access.
- **Prefer `static` lambdas** and pass state as an argument (as above) so the host
  does not allocate a closure per registration.
- **Don't register broad actions.** `RegisterSyntaxTreeAction` /
  `RegisterSemanticModelAction` hand you a whole file every time; use them only when
  the rule is genuinely file-global.

## Measure it

Do not guess - measure the analyzer's execution time:

```pwsh
dotnet build touki/touki.csproj -c Release -p:ReportAnalyzer=true -bl
```

- `-p:ReportAnalyzer=true` makes the compiler emit a per-analyzer execution-time
  report. Reading it from console output is unreliable (it is easy to bury, as seen
  when wiring up this repo's analyzer); the dependable read is the `-bl` binary log
  opened in the **MSBuild Structured Log Viewer**, where you can find the analyzer
  summary and each analyzer's time.
- Compare your analyzer's time against the others in the same build. An analyzer
  that is a multiple of its peers' time is a red flag - usually an un-cached symbol
  lookup or a semantic call that should have been gated behind a syntactic check.
- In Visual Studio, the IDE can surface per-analyzer CPU; if a specific project
  feels slow to type in, that is the signal to re-profile.

## Quick reference

| Symptom | Cause | Fix |
| --- | --- | --- |
| Slow on every project | Semantic call before syntactic gate | Reorder: cheap checks first, `return` early |
| Slow even where rule is irrelevant | No early bail | Resolve target symbol at compilation start; return if absent |
| Time scales with file size | `DescendantNodes()` / tree walk | Register a narrower node/operation kind |
| GC pressure during typing | LINQ / closures in callback | Direct loops, `static` lambdas, cached arrays |
| GC pressure on type checks | `ToString()` / `ToDisplayString()` compares | Resolve once with `GetTypeByMetadataName`, compare via `SymbolEqualityComparer` |
| Memory grows across edits | `Compilation`/`ISymbol` in a static/instance field | Capture per-compilation symbols in the closure, never a field |
| Wrong results under parallelism | Shared mutable state | Remove it; keep state per-callback or per-compilation |
