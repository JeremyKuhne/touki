---
name: performance-testing
description: Author and run BenchmarkDotNet performance tests in the `touki.perf` project. Use when adding new benchmarks, running existing ones, comparing implementations, profiling to find which method dominates a benchmark, drilling from a benchmark down to the hot source line (via the `touki.mcp` trace analyzer), or evaluating allocations / memory usage for code in the `touki` library.
metadata:
  portability: semi-portable
---

# Performance testing in `touki.perf`

The [touki.perf](../../../touki.perf/touki.perf.csproj) project hosts all
[BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks for the library. It
multi-targets the current modern .NET version (see `$(DotNetCoreVersion)` in
[Directory.Build.props](../../../Directory.Build.props), currently `net10.0`) and
`net481`, so benchmarks run on both .NET and .NET Framework. It references both the
main library and the test project (so internal helpers used in tests are also
available to perf code).

Note: code under `touki/Framework/` is only compiled for the .NET Framework target.
References to those types from a benchmark must be guarded with `#if NETFRAMEWORK`.

**Related skills:**

- [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) - reasons
  to add a polyfill (and therefore a benchmark) in the first place.
- [`framework-jit-optimization`](../framework-jit-optimization/SKILL.md) -
  decisions about specialization, unrolling, and BCL-delegation on net481 that the
  benchmarks here exist to validate.
- [`scratch-buffer-strategy`](../scratch-buffer-strategy/SKILL.md) - choosing
  between zeroed `stackalloc`, `[SkipLocalsInit]`, `BufferScope<T>`, and an
  `ArrayPool` rental; several benchmarks here exist to validate those crossovers.
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - requires a benchmark
  in `touki.perf/` (or an explicit "not measured" note) for any perf claim that
  drives a code change in `touki/Framework/`.

## The rules that always apply

Five rules cover most benchmark work; the sub-pages hold the rest.

1. **Pass `-f <tfm>`.** `touki.perf` is multi-targeted, so `dotnet run` fails
   without an explicit `-f net10.0` or `-f net481`.
2. **`-c Release` is mandatory.** Debug runs are not representative.
3. **`[MemoryDiagnoser]` on every class.** It adds the `Allocated` column, which
   is usually the point.
4. **Every `[Benchmark]` method returns a value derived from the work**, never
   `void` - otherwise dead-code elimination wipes the body and the numbers are
   meaningless. See [authoring.md](authoring.md).
5. **Run both TFMs** for any code that compiles for both. Results diverge: the
   modern runtime has vectorized BCL APIs net481 lacks.

```powershell
# The canonical run, one TFM. Repeat with -f net481.
dotnet run -c Release -f net10.0 --project touki.perf -- --filter *StoreInteger*
```

## Workflow checklist for a new benchmark

1. Add a `<Name>.cs` file under `touki.perf/` with a `public` class in `touki.perf`.
   See [authoring.md](authoring.md) for layout, globals, and attributes.
2. Decorate the class with `[MemoryDiagnoser]`.
3. Add `[Benchmark(Baseline = true)]` to the reference implementation and `[Benchmark]`
   to each variant.
4. Make every benchmark method **return a value** derived from the work, never `void`.
5. Avoid helper-method indirection between the benchmark and the system-under-test.
   If overload resolution forces it, temporarily rename one overload in source while
   measuring, then revert.
6. Build Release: `dotnet build -c Release touki.perf`.
7. Smoke-test with `--job short --filter *<Name>*` on each target framework
   individually using `-f net10.0` and `-f net481`. See [running.md](running.md).
8. Run the full benchmark on both target frameworks (drop `--job short`).
9. Inspect `Allocated` and `Ratio` columns; copy the Markdown report into the PR.
   See [interpreting-results.md](interpreting-results.md).
10. If one method dominates and you need to know which - or which *line* inside
    it - profile it. See [profiling.md](profiling.md).

When the change is driven by a profile, follow the before/after discipline in
[interpreting-results.md](interpreting-results.md) (baseline both TFMs, re-run
both, keep full rows, confirm the targeted frame moved).

## Codegen-level optimization rules

For decisions about *how to write* a hot path - whether to specialize a
generic for primitives, choose between scalar/unrolled forms, defer to BCL
primitives like `IndexOf` / `SequenceEqual`, or interpret a `net481`-vs-`net10`
divergence - see the
[framework-jit-optimization](../framework-jit-optimization/SKILL.md) skill.
That skill is the right entry point for "this loop is slow on `net481`, what
should I try?" questions, while this one is about authoring and running the
benchmarks themselves.

## Sub-pages

- [authoring.md](authoring.md) - file/class layout, the imported globals, the
  required and optional attributes, and what a benchmark method must do.
- [running.md](running.md) - the `-f <tfm>` requirement, filtering to a class or
  method, the interactive picker, and useful switches.
- [profiling.md](profiling.md) - capturing an EventPipe trace and drilling it
  with the `touki.mcp` analyzer from operation to method to line.
- [interpreting-results.md](interpreting-results.md) - before/after discipline on
  both TFMs, reading the memory columns, and the tuple-swap exception.
