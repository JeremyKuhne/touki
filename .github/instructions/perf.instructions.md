---
applyTo: 'touki.perf/**/*.cs'
---

# Performance test conventions for `touki.perf`

Rules for BenchmarkDotNet benchmarks under `touki.perf/`. See also the
[`performance-testing`](../../.agents/skills/performance-testing/SKILL.md) and
[`framework-jit-optimization`](../../.agents/skills/framework-jit-optimization/SKILL.md)
skills for the workflow and net481-specific JIT details.

## Run mode

- Always run benchmarks in **Release** configuration. Debug numbers are
  meaningless and will mislead any performance claim built on them.
- Benchmarks must run cleanly on **both** target frameworks (.NET 10 and
  .NET Framework 4.8.1). A regression on one TFM and not the other is a real
  signal, not noise - see the JIT-naming rule below.

## JIT-naming rule (non-negotiable)

When making any performance claim in code comments, PR descriptions, or
benchmark commentary, name the JIT explicitly:

- **".NET Framework 4.8.1 RyuJIT"** - older, no
  `EqualityComparer<T>.Default` intrinsic, no tiered JIT, weaker inlining,
  no dynamic PGO.
- **"modern .NET RyuJIT"** (.NET 6+) - devirtualization, tiered JIT,
  dynamic PGO, much more aggressive inlining.

Unqualified "RyuJIT" claims are wrong about half the time. Code changes in
`touki/Framework/` driven by such claims need a benchmark or an explicit
"not measured" note.

## Reading BenchmarkDotNet output

- **Mean** is what you compare; **Median** is a sanity check (large
  Mean-Median spread = unstable benchmark, fix before drawing
  conclusions).
- **Allocated** is per-operation managed allocation. **Any new allocation
  in a documented hot path is a regression**, regardless of throughput.
- **Gen0/Gen1/Gen2** are GC events per 1000 ops. Watch for new Gen1/Gen2
  promotion in steady-state benches.

## Regression threshold

Judge a delta against the benchmark's own run-to-run noise, not a fixed
percentage. The procedure:

- **Establish the noise floor first.** Re-run the unchanged baseline (or read
  `Error` / `StdDev` in the report). A well-formed microbenchmark here is
  usually stable to ~1-2 %. A change smaller than that spread, in either
  direction, is noise - say so explicitly and do not claim it.
- **A consistent slowdown above the noise floor is a regression, even if it is
  under 5 %.** Do not treat 5 % as a free budget; a reproducible 3-4 % loss is
  real and needs a justification, not a hand-wave. The only number that matters
  is "is it reliably outside the noise".
- **Any new allocation in a documented hot path is a regression**, regardless of
  throughput.
- **A small throughput cost is acceptable when it buys an allocation
  reduction.** A microbenchmark's `Mean` measures the steady-state per-op cost
  *after* warmup; it does not capture the amortized GC time that the reclaimed
  allocation would have cost in a real workload (collections, promotions, pause
  time). Trading a few percent of `Mean` for materially fewer `Allocated` bytes
  (or removing Gen1/Gen2 promotion) is usually a net win - quote both columns
  and the reasoning when you make that call. The reverse - spending allocation
  to win throughput - needs an explicit argument that the workload is
  allocation-insensitive.
- **State the comparison.** Always report the delta *and* the noise floor you
  measured it against ("-4 %, baseline StdDev ~1 %, reproduced over 2 runs"),
  and name the TFM/JIT. An unqualified percentage is not a conclusion.

## Authoring conventions

- One benchmark class per scenario; name it `<Subject>Perf` to match the
  existing `touki.perf/*Perf.cs` files.
- Use `[MemoryDiagnoser]` on every benchmark class.
- Parameterize with `[Params]` only when the parameter changes the algorithmic
  shape; otherwise hard-code and add a second class.
- Do not check generated reports under `BenchmarkDotNet.Artifacts/` into git
  unless the change is intentionally pinning a baseline.
