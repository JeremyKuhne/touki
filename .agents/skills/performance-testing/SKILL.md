---
name: performance-testing
description: Author and run BenchmarkDotNet performance tests in a multi-targeted .NET library's perf project, and translate a user's outcome-shaped performance question into a measurement. Use when adding new benchmarks, running existing ones, comparing implementations, profiling to find which method or source line dominates, evaluating allocations / memory usage, reading the generated code (sharplab / DisassemblyDiagnoser / HardwareCounters), or when a user asks how long something takes, how much memory it uses, where time is spent, or to help make a method faster - which this skill turns into a scenario, a benchmark, and a drill-down.
license: MIT
metadata:
  github-path: skills/performance-testing
  github-pinned: 4dadc23e28750f9da50ebeb56a9e18ac73d61d34
  github-ref: refs/heads/add-performance-testing
  github-repo: https://github.com/JeremyKuhne/agent-skills
  github-tree-sha: c7319b537b3e4c9b1d13244c7c7a9a364cbb36a2
  portability: semi-portable
---

# Performance testing with BenchmarkDotNet

This skill covers authoring and running
[BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks in a multi-targeted
.NET library's **perf project** (`<root>.perf` by convention), and - just as
important - turning a user's vague, outcome-shaped performance question into a
concrete measurement and a useful answer.

A consuming repository wires the concrete project name, target frameworks,
cross-skill links, and profiling tooling in its overlay. This core uses
`<root>.perf` for the perf project and `<tfm>` for a target-framework moniker;
the overlay supplies the real names. The two framework monikers this skill names
directly - modern .NET (`net10.0` or whatever the repo's current version is) and
.NET Framework (`net481`) - are the common multi-targeting pair; a single-target
repo simply ignores the second.

Note: code in a Framework-only source tree (the `Framework/` subtree by
convention, excluded from the modern build) compiles only for the .NET Framework
target. References to those types from a benchmark must be guarded with
`#if NETFRAMEWORK`.

## Starting from a user's question

Users ask outcome questions - "how long does this take?", "how much memory does
this use?", "where is the time going?", "help me make this faster?" - not tooling
commands. They will not pick a scenario, write a benchmark, or capture a trace on
their own; **translating the question into a measurement and leading them through
it is the job.** Before reaching for the mechanics below, read
[interpreting-requests.md](interpreting-requests.md): it maps each kind of
question to the right workflow, says which clarifications to ask (and which to
answer yourself from the code), walks the "make X faster" journey end to end, and
lists the follow-ups to offer once a result is in hand. The rest of this skill is
the *how*; that page is the *what to measure and why*.

**Related skills** (a consuming repo links the ones it vendors in its overlay):

- A **trace-analyzer** skill - the profiler this skill drives to find the hot
  method or source line. The overlay names it and the concrete profiling page.
- A **framework-JIT-optimization** skill - decisions about specialization,
  unrolling, and BCL-delegation on the older Framework JIT that the benchmarks
  here exist to validate.
- A **scratch-buffer-strategy** skill - choosing between zeroed `stackalloc`,
  `[SkipLocalsInit]`, a stack-with-pool-fallback buffer, and an `ArrayPool`
  rental; several benchmarks exist to validate those crossovers.
- A **pre-pr-self-review** skill - which requires a benchmark (or an explicit
  "not measured" note) for any perf claim that drives a Framework-only code
  change.

## The rules that always apply

Five rules cover most benchmark work; the sub-pages hold the rest.

1. **Pass `-f <tfm>`.** A multi-targeted perf project makes `dotnet run` fail
   without an explicit target framework (e.g. `-f net10.0` or `-f net481`).
2. **`-c Release` is mandatory.** Debug runs are not representative.
3. **`[MemoryDiagnoser]` on every class.** It adds the `Allocated` column, which
   is usually the point.
4. **Every `[Benchmark]` method returns a value derived from the work**, never
   `void` - otherwise dead-code elimination wipes the body and the numbers are
   meaningless. See [authoring.md](authoring.md).
5. **Run both TFMs** for any code that compiles for both. Results diverge: the
   modern runtime has vectorized BCL APIs the older Framework runtime lacks.

```powershell
# The canonical run, one TFM. Repeat with the other -f <tfm>.
dotnet run -c Release -f <tfm> --project <root>.perf -- --filter *MyBenchmark*
```

## Workflow checklist for a new benchmark

1. Add a `<Name>.cs` file under the perf project with a `public` class in the
   perf namespace. See [authoring.md](authoring.md) for layout, globals, and
   attributes.
2. Decorate the class with `[MemoryDiagnoser]`.
3. Add `[Benchmark(Baseline = true)]` to the reference implementation and
   `[Benchmark]` to each variant.
4. Make every benchmark method **return a value** derived from the work, never
   `void`.
5. Avoid helper-method indirection between the benchmark and the
   system-under-test. If overload resolution forces it, temporarily rename one
   overload in source while measuring, then revert.
6. Build Release: `dotnet build -c Release <root>.perf`.
7. Smoke-test with `--job short --filter *<Name>*` on each target framework
   individually. See [running.md](running.md).
8. Run the full benchmark on both target frameworks (drop `--job short`).
9. Inspect `Allocated` and `Ratio` columns; copy the Markdown report into the PR.
   See [interpreting-results.md](interpreting-results.md).
10. If one method dominates and you need to know which - or which *line* inside
    it - profile it. See your repo's profiling overlay (the trace-analyzer skill).

When the change is driven by a profile, follow the before/after discipline in
[interpreting-results.md](interpreting-results.md) (baseline both TFMs, re-run
both, keep full rows, confirm the targeted frame moved).

## Codegen-level optimization rules

For decisions about *how to write* a hot path - whether to specialize a generic
for primitives, choose between scalar/unrolled forms, defer to BCL primitives
like `IndexOf` / `SequenceEqual`, or interpret a Framework-vs-modern divergence -
see the framework-JIT-optimization skill. That skill is the right entry point for
"this loop is slow on the older Framework JIT, what should I try?" questions,
while this one is about authoring and running the benchmarks themselves.

To see *why* a result is what it is - the C# lowering, the IL, or the JIT asm
behind a number - see [reading-codegen.md](reading-codegen.md). It covers
sharplab, BenchmarkDotNet's `[DisassemblyDiagnoser]` / `[HardwareCounters]`, the
`DOTNET_JitDisasm*` knobs, and the tiering/PGO traps that bite codegen
inspection.

## Sub-pages

- [interpreting-requests.md](interpreting-requests.md) - turning a user's
  outcome question ("how long?", "how much memory?", "where's the time?", "make
  it faster") into a scenario, a measurement, an answer in their words, and the
  next follow-up to offer. Start here when the request is a question, not a task.
- [authoring.md](authoring.md) - file/class layout, the imported globals, the
  required and optional attributes, and what a benchmark method must do.
- [running.md](running.md) - the `-f <tfm>` requirement, filtering to a class or
  method, the interactive picker, and useful switches.
- [interpreting-results.md](interpreting-results.md) - before/after discipline on
  both TFMs and reading the memory columns.
- [reading-codegen.md](reading-codegen.md) - seeing the C# lowering, IL, and JIT
  asm behind a number: sharplab, `[DisassemblyDiagnoser]`, `[HardwareCounters]`,
  the `DOTNET_JitDisasm*` knobs, and the tiering/PGO inspection traps.

Profiling a benchmark down to the hot method or source line is a repo-specific
page supplied by the overlay (it drives the repo's trace analyzer), not part of
this core.
