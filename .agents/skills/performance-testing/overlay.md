---
core: performance-testing
core-pin: v0.10.0
---

# Touki overlay - performance-testing

Repo-specific companion to the vendored [performance-testing](SKILL.md) skill.
The `SKILL.md` and its five sibling pages (`authoring.md`, `running.md`,
`interpreting-requests.md`, `interpreting-results.md`, `reading-codegen.md`) are
a **pinned copy of the portable core** from
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see the
`metadata.github-*` provenance in `SKILL.md`'s frontmatter). Do not hand-edit the
core - `gh skill update` would flag the drift. Everything touki-specific lives
here, plus the two profiling pages below, which are a touki overlay (they drive
the repo's trace analyzer).

> **Pinned to a release.** The core is pinned to the commons **v0.10.0** tag. Pull
> later upstream changes with `gh skill update performance-testing` (review the
> diff, re-pin to the new tag).

## Concrete bindings for the core's placeholders

- **Perf project**: the core's `<root>.perf` is [touki.perf](../../../touki.perf/touki.perf.csproj),
  namespace `touki.perf`. It multi-targets the current modern .NET version (see
  `$(DotNetCoreVersion)` in [Directory.Build.props](../../../Directory.Build.props),
  currently `net10.0`) and `net481`, and references both the main library and the
  test project (so internal helpers used in tests are available to perf code).
- **Target frameworks**: `<tfm>` is `net10.0` or `net481`.
- **Globals**: [touki.perf/GlobalUsings.cs](../../../touki.perf/GlobalUsings.cs)
  already imports `BenchmarkDotNet.Attributes`, `BenchmarkDotNet.Jobs`, `Touki`,
  and `Microsoft.IO` (on NETFRAMEWORK) / `System.IO` otherwise. Do not re-import.
- **Coding style**: follow [AGENTS.md](../../../AGENTS.md) (no `var`, target-typed
  `new()`, C# keyword type names, `is null` / `is not null`, indented XML docs).
- **Example benchmark**: [StoreInteger.cs](../../../touki.perf/StoreInteger.cs)
  shows the layout and a `[SimpleJob]` example.

## Cross-references (the core's "Related skills")

- [`filtrace`](../filtrace/SKILL.md) - the trace analyzer this skill drives to
  find the hot method or source line. See the two profiling pages below.
- [`framework-jit-optimization`](../framework-jit-optimization/SKILL.md) -
  specialization, unrolling, and BCL-delegation on net481 that the benchmarks
  here exist to validate.
- [`scratch-buffer-strategy`](../scratch-buffer-strategy/SKILL.md) - choosing
  between zeroed `stackalloc`, `[SkipLocalsInit]`, `BufferScope<T>`, and an
  `ArrayPool` rental; several benchmarks validate those crossovers.
- [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) - reasons to add a
  polyfill (and therefore a benchmark) in the first place.
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - requires a benchmark in
  `touki.perf/` (or an explicit "not measured" note) for any perf claim that
  drives a code change in `touki/Framework/`.

## Profiling (the trace-analyzer overlay pages)

The core defers "profile a benchmark down to the hot method or source line" to
the consuming repo. In touki that is the [`filtrace`](../filtrace/SKILL.md) skill,
driven by two pages kept here:

- [profiling.md](profiling.md) - capturing an EventPipe trace and drilling it with
  filtrace from operation to method to line, and *reading* the line ranking
  (prologue-dominated = call-count-bound; a helper recurring across branches = the
  real target). Also the net481 ETW path and the EventPipe-vs-ETW attribution flip.
- [graphical-viewers.md](graphical-viewers.md) - the optional last step: handing a
  human an interactive flame graph in speedscope or Perfetto.

The full field manual is [docs/performance-investigation.md](../../../docs/performance-investigation.md)
(profiling sections 3a methods, 3f lines); the no-filtrace PowerShell fallback is
[docs/performance-investigation-without-mcp.md](../../../docs/performance-investigation-without-mcp.md).

## Candidate upstream improvements

The generic parts of [profiling.md](profiling.md) added after the July 2026 NRBF
investigation are intentionally being validated in Touki before promotion to
[`JeremyKuhne/agent-skills`](https://github.com/JeremyKuhne/agent-skills).

Implemented locally and candidates for promotion:

- separate harness guidance for one-shot phase measurement versus adaptive phase
  profiling;
- an experiment ledger that retains rejected variants and allocation outcomes.

Periodic CPU sample-quality, provider-state, source-resolution, BenchmarkDotNet
scope, and trace-manifest contracts are now owned by the filtrace 0.6 tool-shipped
skill. They remain cross-referenced from Touki's profiling page but are not
`agent-skills` promotion candidates.

Further portable candidates are specified in
[performance-investigation-agent-tooling-retrospective.md](../../../docs/performance-investigation-agent-tooling-retrospective.md)
but are not yet implemented as local skill guidance: exact-source comparison and
reconstructable run-artifact provenance. Keep that distinction until the workflow
has been exercised locally or promoted directly upstream.

Do not copy these changes into the pinned core locally. Once the guidance has
settled, uplevel the portable wording to `agent-skills`, publish a new commons
version, and re-vendor/re-pin the performance-testing core here. Filtrace release
[0.6.0](https://github.com/JeremyKuhne/filtrace/releases/tag/v0.6.0) completed the
product, capture-script, and tool-shipped-skill work from
[filtrace#42](https://github.com/JeremyKuhne/filtrace/issues/42); do not promote
those tool-specific details to `agent-skills`.

## Tuple-swap on .NET Core hot paths (touki measurement)

`IDE0180` ("use tuple to swap values") is disabled globally in
[.editorconfig](../../../.editorconfig) because the auto-fix is unsafe on
`net481` - see [SpanSwapPerf.cs](../../../touki.perf/SpanSwapPerf.cs) for the
measurements:

| Form | net481 RyuJIT | .NET 10 RyuJIT |
| --- | --- | --- |
| Plain-local `(a, b) = (b, a)` | ~23% slower | equivalent |
| Paired `Span<T>` indexed deconstruction | ~9% slower | ~13% **faster** |
| Single `Span<T>` indexed or `ref` local deconstruction | equivalent | equivalent |

So a `#if NET` (modern-only) hot path that performs paired indexed swaps is one
of the few cases where tuple swap is genuinely worth it. If a `touki.perf/`
benchmark confirms the win for a specific call site, opt in with a localized
pragma rather than re-enabling the rule globally:

```c#
#if NET
#pragma warning disable IDE0180 // Tuple swap measured faster on .NET 10 RyuJIT
        (keys[i], keys[j], items[i], items[j]) =
            (keys[j], keys[i], items[j], items[i]);
#pragma warning restore IDE0180
#else
        TKey tk = keys[i]; keys[i] = keys[j]; keys[j] = tk;
        TValue tv = items[i]; items[i] = items[j]; items[j] = tv;
#endif
```

Do **not** apply this to code under `touki/Framework/` (compiled only for .NET
Framework) or to code shared across both targets without a `#if NET` / `#else`
split - the .NET Framework branch will regress.

## Reading codegen - the touki deeper tool

The core's `reading-codegen.md` mentions an "IL-copy-inspection skill (if the repo
vendors one)". touki has it: [`il-copy-inspection`](../il-copy-inspection/SKILL.md)
- the deeper tool for reading IL to find struct copies and boxing.

## Updating

Pull upstream changes to the core (and its siblings) with
`gh skill update performance-testing` (review the diff, re-pin). Keep
touki-specific additions - including the two profiling pages - in this overlay,
not in the core.
