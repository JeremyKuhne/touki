---
name: performance-testing
description: Author and run BenchmarkDotNet performance tests in the `touki.perf` project. Use when adding new benchmarks, running existing ones, comparing implementations, profiling to find which method dominates a benchmark, drilling from a benchmark down to the hot source line (via the `touki.mcp` trace analyzer), or evaluating allocations / memory usage for code in the `touki` library.
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

## 1. Authoring a benchmark

### File and class layout

- One benchmark class per file, file named after the class
  (e.g. [StoreInteger.cs](../../../touki.perf/StoreInteger.cs)).
- Namespace is always `touki.perf`.
- Class is `public` (BenchmarkDotNet requires this) and contains one or more methods
  marked `[Benchmark]`.
- Use the standard repo file header.
- Follow the repo coding style (no `var`, target-typed `new()`, C# keyword type names,
  `is null` / `is not null`, indented XML docs, etc.). See
  [AGENTS.md](../../../AGENTS.md).

### Globals already imported

[GlobalUsings.cs](../../../touki.perf/GlobalUsings.cs) already provides:

- `BenchmarkDotNet.Attributes` - `[Benchmark]`, `[MemoryDiagnoser]`, `[Params]`,
  `[GlobalSetup]`, etc.
- `BenchmarkDotNet.Jobs` - `RuntimeMoniker`, `[SimpleJob]`.
- `Touki` - the library root namespace.
- `Microsoft.IO` (on NETFRAMEWORK) or `System.IO` otherwise.

Do not re-import these.

### Required attributes for memory evaluation

Always annotate every benchmark class with `[MemoryDiagnoser]`, regardless of
its purpose. This adds three columns to the results table:
**Gen0 / Gen1 / Gen2** (collections per 1000 ops) and **Allocated** (bytes
per op). Without it you only get timings.

```c#
[MemoryDiagnoser]
public class StringFormatting
{
    [Benchmark(Baseline = true)]
    public string StringFormat() => string.Format("The answer is {0}.", _value);

    [Benchmark]
    public string StringsFormat() => string.FormatValue("The answer is {0}.", _value);
}
```

Mark one method `[Benchmark(Baseline = true)]` whenever you are comparing
alternative implementations - the report adds a **Ratio** and
**RatioSD** column relative to that baseline.

### Optional attributes

- `[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]`
  - cuts run time when iterating on a benchmark. Use only while developing; remove
  (or revert to defaults) before checking in stable measurements. See
  [StoreInteger.cs](../../../touki.perf/StoreInteger.cs) for an example.
- `[Params(...)]` on a public field/property to run the benchmark for each value.
- `[GlobalSetup]` for one-time setup outside the measured region.
- `[Arguments(...)]` to pass per-method parameters.

### What a benchmark method must do

- **Return a value derived from the measured work, even when measuring
  `void`-returning APIs.** Returning `void` (or a constant the JIT can
  prove invariant) lets dead-code elimination wipe the body, producing
  meaningless near-zero or wildly inconsistent timings. BenchmarkDotNet
  consumes the return value to prevent that. For buffer-mutating APIs
  (`Span<T>.Replace`, `Random.NextBytes`, `Encoding.GetBytes`) return
  `buffer[0]`, the last element, or an XOR/sum digest of the buffer
  - not `buffer.Length` or any other value invariant of execution.
  Symptoms of forgetting: "optimized" variants slower than the baseline
  (because the baseline got eliminated), >50% StdDev between runs,
  near-zero timings for non-trivial work. See
  [BenchmarkDotNet good practices](https://benchmarkdotnet.org/articles/guides/good-practices.html).
- Be cheap to call repeatedly - BenchmarkDotNet invokes it millions of times.
- Avoid per-call setup; move setup into `[GlobalSetup]` or readonly fields.
- Avoid wrapping the system-under-test in a helper method just to satisfy
  overload resolution - the helper's call frame, type check, and
  generic instantiation show up in the measurement. Either rename one
  overload temporarily while measuring, or split into two benchmark classes.
- Ref structs cannot be returned from `[Benchmark]` methods; consume them
  inside the method and return a representative scalar (length or hash).

## 2. Running benchmarks

The project's `Program.Main` uses `BenchmarkSwitcher.FromAssembly(...)` so all CLI
arguments are forwarded to BenchmarkDotNet. Run from the repo root in PowerShell.

### Specifying the target framework is required

Because `touki.perf` is multi-targeted, **`dotnet run` requires `-f <tfm>`** -
the SDK will not pick one for you and the run fails with NETSDK1005 or an ambiguous
target error if you omit it. Always pass `-f net10.0` or `-f net481` (or whatever
`$(DotNetCoreVersion)` currently resolves to).

```powershell
# Modern .NET
dotnet run -c Release -f net10.0 --project touki.perf -- --filter *StoreInteger*

# .NET Framework 4.8.1
dotnet run -c Release -f net481 --project touki.perf -- --filter *StoreInteger*
```

`-c Release` is **mandatory**. Debug builds are not representative and BenchmarkDotNet
will warn loudly.

When a change touches code that compiles for both targets, **run both TFMs**. The
results often differ dramatically - the modern runtime has vectorized BCL APIs
that .NET Framework lacks, so a hand-tuned net481 fast path may look identical to the
generic path on net10.

### Run a single class or method

```powershell
# All benchmarks in StoreInteger
dotnet run -c Release -f net10.0 --project touki.perf -- --filter *StoreInteger*

# A single method
dotnet run -c Release -f net10.0 --project touki.perf -- --filter *StoreInteger.As
```

### Interactive picker

```powershell
dotnet run -c Release --project touki.perf
```

With no `--filter`, BenchmarkSwitcher prints a numbered menu. Useful when exploring.

### Useful extra switches

- `--job short` - very fast, low-confidence run; good for smoke-testing a new
  benchmark before committing to a full run.
- `--memory` - enables the memory diagnoser for this run even if the class is not
  decorated. Prefer the attribute.
- `--exporters github` - emits a GitHub-flavored Markdown report alongside the
  default outputs.

### Profiling a benchmark: from operation to method to line

To find where a benchmark spends its time - optimizing a hot path or chasing a
regression - capture an EventPipe CPU trace on `net10.0`, then drill it with the
in-workspace [touki.mcp](../../../touki.mcp/touki.mcp.csproj) analyzer. It reads
the trace through TraceEvent, folds the JIT-helper sampling artifacts, and ranks
by method or by source `file:line`. One capture serves both:

```powershell
# Capture once. --keepFiles preserves BDN's build so its PDB GUID survives for
# the line ranking below.
dotnet run -c Release -f net10.0 --project touki.perf -- `
    --filter '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingleWithRoot' -p EP --keepFiles

$trace = (Get-ChildItem BenchmarkDotNet.Artifacts `
    -Filter '*GlobEnumeratorExtGlobSingleWithRoot*.nettrace' |
    Sort-Object LastWriteTime | Select-Object -Last 1).FullName
# The exact build BDN profiled - its PDB GUID matches the trace:
$sym = 'artifacts/x64/Release/touki.perf/net10.0/touki.perf-DefaultJob-1/bin/Release/net10.0'

# Method ranking, scoped to a workload frame (which method owns the self-time).
dotnet run --project touki.mcp -c Release -- analyze $trace --root 'RecordedDirectoryEnumerator.MoveNext' --top 25

# Line ranking inside the dominant method (which lines of its hot loop dominate).
dotnet run --project touki.mcp -c Release -- analyze $trace --lines RunEngine --symbols $sym --top 30

# Who calls a folded JIT-helper artifact, to confirm what it's attributable to.
dotnet run --project touki.mcp -c Release -- analyze $trace --callers 'BulkMoveWithWriteBarrier'
```

An agent that speaks MCP calls the equivalent tools directly (`hotspots_self`,
`hotspots_inclusive`, `hot_lines`, `callers_of`, `load_trace`, `list_threads`).

Things that bite, kept short - full rationale in
[docs/performance-investigation.md](../../../docs/performance-investigation.md)
sections 3a (methods) and 3f (lines):

- **EventPipe is net10.0-only** - net481 needs `[EtwProfiler]` + admin.
- **Fold the artifacts.** A raw self-time view shows `0 ms` per method (the leaf
  is a synthetic `CPU_TIME` marker), and the managed-only walker mislabels
  JIT-helper thunks (`BulkMoveWithWriteBarrier`, `Thread.PollGCWorker`,
  `Buffer.Memmove`) as the hotspot. The analyzer folds both by default; a
  `BulkMoveWithWriteBarrier` over a GC-ref-free struct is always an artifact.
- **`--root` must be a frame inside the workload**, not the benchmark method
  name (that also matches BDN's `Activity Benchmark(...)` wrapper and pulls in
  idle threadpool threads).
- **Line ranking needs `--symbols` pointing at BDN's `...-DefaultJob-N/bin/...`
  build** - `touki.dll` ships its PDB embedded, and the symbols build's GUID
  must match the trace (hence `--keepFiles`). A wrong dir resolves frames to
  `<no source>` or is rejected with a GUID mismatch.
- **Line attribution stops at inlined boundaries** - a fully-inlined callee
  collapses onto its caller's call-site line. If the ranking piles onto one or
  two call-site lines, scope `--lines` to the callee (`--lines ExtGlobEngine`)
  or add a temporary `[MethodImpl(MethodImplOptions.NoInlining)]`.

The older `Profile-Benchmark.ps1` / `Get-TraceHotspots.ps1` scripts predate the
analyzer and remain as a no-MCP fallback (plus `speedscope-to-flamegraph.ps1`
for SVG) in
[docs/performance-investigation-without-mcp.md](../../../docs/performance-investigation-without-mcp.md).

## 3. Evaluating memory usage

With `[MemoryDiagnoser]` (or `--memory`), each row of the results table includes:

| Column      | Meaning                                                      |
| ----------- | ------------------------------------------------------------ |
| `Gen0`      | Gen0 collections per 1000 operations.                        |
| `Gen1`      | Gen1 collections per 1000 operations.                        |
| `Gen2`      | Gen2 collections per 1000 operations.                        |
| `Allocated` | Managed bytes allocated per single operation.                |

### Reading the numbers

- **`Allocated` is the primary signal.** A method that should be allocation-free must
  report `-` or `0 B`. Anything else is a regression.
- A `Ratio` column appears when one method is `Baseline = true`. Use it together with
  `Allocated` to confirm a perf change is not just trading CPU for allocations (or
  vice versa).
- `Gen0` ticking up while `Allocated` stays flat usually means you allocated a lot of
  short-lived objects on previous iterations - recheck `[GlobalSetup]`.
- Stack-only types (`Span<T>`, `ref struct`s, `stackalloc`) do not contribute to
  `Allocated`. Boxing of value types does - watch for accidental boxing through
  `object`, non-generic interfaces, or `string.Format`.
- On `net481` the JIT and BCL allocate differently than on the modern .NET target
  (`net10.0`). Always run both before declaring a win.

### Where the report lives

After each run BenchmarkDotNet writes artifacts under
`BenchmarkDotNet.Artifacts/results/` next to the executable, including:

- `*.md` - GitHub-friendly Markdown table (paste this into PR descriptions).
- `*.csv` and `*.html` - for spreadsheets / browser viewing.
- `*-report-full.json` - raw measurements; useful for diffing two runs
  programmatically.

The same table is also printed to the console at the end of the run.

## 4. Workflow checklist for a new benchmark

1. Add a `<Name>.cs` file under `touki.perf/` with a `public` class in `touki.perf`.
2. Decorate the class with `[MemoryDiagnoser]`.
3. Add `[Benchmark(Baseline = true)]` to the reference implementation and `[Benchmark]`
   to each variant.
4. Make every benchmark method **return a value** derived from the work, never `void`.
5. Avoid helper-method indirection between the benchmark and the system-under-test.
   If overload resolution forces it, temporarily rename one overload in source while
   measuring, then revert.
6. Build Release: `dotnet build -c Release touki.perf`.
7. Smoke-test with `--job short --filter *<Name>*` on each target framework
   individually using `-f net10.0` and `-f net481`.
8. Run the full benchmark on both target frameworks (drop `--job short`).
9. Inspect `Allocated` and `Ratio` columns; copy the Markdown report into the PR.
10. If one method dominates and you need to know which - or which *line* inside
    it - profile it; see *Profiling a benchmark: from operation to method to
    line* in &sect;2.

## 5. Codegen-level optimization rules

For decisions about *how to write* a hot path - whether to specialize a
generic for primitives, choose between scalar/unrolled forms, defer to BCL
primitives like `IndexOf` / `SequenceEqual`, or interpret a `net481`-vs-`net10`
divergence - see the
[framework-jit-optimization](../framework-jit-optimization/SKILL.md) skill.
That skill is the right entry point for "this loop is slow on `net481`, what
should I try?" questions, while this one is about authoring and running the
benchmarks themselves.

## 6. Tuple-swap on .NET Core hot paths

`IDE0180` ("use tuple to swap values") is disabled globally in
[.editorconfig](../../../.editorconfig) because the auto-fix is unsafe on
`net481` - see [SpanSwapPerf.cs](../../../touki.perf/SpanSwapPerf.cs)
for the measurements. The summary is:

| Form | net481 RyuJIT | .NET 10 RyuJIT |
| --- | --- | --- |
| Plain-local `(a, b) = (b, a)` | ~23% slower | equivalent |
| Paired `Span<T>` indexed deconstruction | ~9% slower | ~13% **faster** |
| Single `Span<T>` indexed or `ref` local deconstruction | equivalent | equivalent |

That means a `#if NET` (modern-only) hot path that performs paired indexed
swaps is one of the few cases where tuple swap is genuinely worth it. If a
benchmark in `touki.perf/` confirms the win for a specific call site, opt in
with a localized pragma rather than re-enabling the rule globally:

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

Do **not** apply this to code under `touki/Framework/` (compiled only for
.NET Framework) or to code shared across both targets without a
`#if NET` / `#else` split - the .NET Framework branch will regress.
