# Authoring a benchmark

Detail for the [performance-testing](SKILL.md) skill. Covers file/class layout,
the globals already imported, the required and optional attributes, and what a
benchmark method must do.

## File and class layout

- One benchmark class per file, file named after the class (e.g. `MyBenchmark.cs`).
- Namespace is the perf project's namespace (`<root>.perf` by convention).
- Class is `public` (BenchmarkDotNet requires this) and contains one or more
  methods marked `[Benchmark]`.
- Use the repo's standard file header.
- Follow the repo coding style (the conventions in its `AGENTS.md` /
  contributor guide - type-name style, null-check style, XML-doc indentation,
  etc.).

## Globals already imported

A perf project usually centralizes common usings in a `GlobalUsings.cs`. Typical
contents:

- `BenchmarkDotNet.Attributes` - `[Benchmark]`, `[MemoryDiagnoser]`, `[Params]`,
  `[GlobalSetup]`, etc.
- `BenchmarkDotNet.Jobs` - `RuntimeMoniker`, `[SimpleJob]`.
- The library root namespace.
- Any per-TFM IO/compat namespaces the repo standardizes on.

Check the perf project's `GlobalUsings.cs` and do not re-import what it already
provides.

## Required attributes for memory evaluation

Always annotate every benchmark class with `[MemoryDiagnoser]`, regardless of its
purpose. This adds three columns to the results table: **Gen0 / Gen1 / Gen2**
(collections per 1000 ops) and **Allocated** (bytes per op). Without it you only
get timings.

```c#
[MemoryDiagnoser]
public class StringFormatting
{
    [Benchmark(Baseline = true)]
    public string StringFormat() => string.Format("The answer is {0}.", _value);

    [Benchmark]
    public string CustomFormat() => MyFormatter.Format("The answer is {0}.", _value);
}
```

Mark one method `[Benchmark(Baseline = true)]` whenever you are comparing
alternative implementations - the report adds a **Ratio** and **RatioSD** column
relative to that baseline.

## Optional attributes

- `[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]`
  - cuts run time when iterating on a benchmark. Use only while developing;
  remove (or revert to defaults) before checking in stable measurements.
- `[Params(...)]` on a public field/property to run the benchmark for each value.
- `[GlobalSetup]` for one-time setup outside the measured region.
- `[Arguments(...)]` to pass per-method parameters.

## What a benchmark method must do

- **Return a value derived from the measured work, even when measuring
  `void`-returning APIs.** Returning `void` (or a constant the JIT can prove
  invariant) lets dead-code elimination wipe the body, producing meaningless
  near-zero or wildly inconsistent timings. BenchmarkDotNet consumes the return
  value to prevent that. For buffer-mutating APIs (`Span<T>.Replace`,
  `Random.NextBytes`, `Encoding.GetBytes`) return `buffer[0]`, the last element,
  or an XOR/sum digest of the buffer - not `buffer.Length` or any other value
  invariant of execution. Symptoms of forgetting: "optimized" variants slower
  than the baseline (because the baseline got eliminated), >50% StdDev between
  runs, near-zero timings for non-trivial work. See
  [BenchmarkDotNet good practices](https://benchmarkdotnet.org/articles/guides/good-practices.html).
- Be cheap to call repeatedly - BenchmarkDotNet invokes it millions of times.
- Avoid per-call setup; move setup into `[GlobalSetup]` or readonly fields.
- Avoid wrapping the system-under-test in a helper method just to satisfy
  overload resolution - the helper's call frame, type check, and generic
  instantiation show up in the measurement. Either rename one overload
  temporarily while measuring, or split into two benchmark classes.
- Ref structs cannot be returned from `[Benchmark]` methods; consume them inside
  the method and return a representative scalar (length or hash).
