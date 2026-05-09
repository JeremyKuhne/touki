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
  signal, not noise &mdash; see the JIT-naming rule below.

## JIT-naming rule (non-negotiable)

When making any performance claim in code comments, PR descriptions, or
benchmark commentary, name the JIT explicitly:

- **".NET Framework 4.8.1 RyuJIT"** &mdash; older, no
  `EqualityComparer<T>.Default` intrinsic, no tiered JIT, weaker inlining,
  no dynamic PGO.
- **"modern .NET RyuJIT"** (.NET 6+) &mdash; devirtualization, tiered JIT,
  dynamic PGO, much more aggressive inlining.

Unqualified "RyuJIT" claims are wrong about half the time. Code changes in
`touki/Framework/` driven by such claims need a benchmark or an explicit
"not measured" note.

## Reading BenchmarkDotNet output

- **Mean** is what you compare; **Median** is a sanity check (large
  Mean&ndash;Median spread = unstable benchmark, fix before drawing
  conclusions).
- **Allocated** is per-operation managed allocation. **Any new allocation
  in a documented hot path is a regression**, regardless of throughput.
- **Gen0/Gen1/Gen2** are GC events per 1000 ops. Watch for new Gen1/Gen2
  promotion in steady-state benches.

## Regression threshold

- > 5 % throughput delta vs the prior baseline, **or** any new allocation in a
  hot path = regression.
- Below 5 % with no new allocation = noise. Say so explicitly when reporting.

## Authoring conventions

- One benchmark class per scenario; name it `<Subject>Perf` to match the
  existing `touki.perf/*Perf.cs` files.
- Use `[MemoryDiagnoser]` on every benchmark class.
- Parameterize with `[Params]` only when the parameter changes the algorithmic
  shape; otherwise hard-code and add a second class.
- Do not check generated reports under `BenchmarkDotNet.Artifacts/` into git
  unless the change is intentionally pinning a baseline.
