# Interpreting results

Detail for the [performance-testing](SKILL.md) skill. Covers the before/after
measurement discipline and reading the memory columns.

## Before/after measurement discipline (both TFMs, every time)

A perf change is not done until you can show it helped and harmed nothing.
Profiling a trace answers *where* to optimize; only a before/after benchmark
answers *whether it worked*. Treat the following as mandatory whenever a code
change is driven by a profile:

- **Capture a baseline first, on both TFMs.** Run the affected benchmark on each
  target framework *before* touching the source and save the full result rows.
  The line-level EventPipe profiling that guides the edit is modern-runtime-only,
  but the change still has to be measured on .NET Framework - the older RyuJIT
  and BCL allocate and inline differently, and a win on one TFM can be a wash or
  a regression on the other. **Never iterate on modern-runtime line info without
  also tracking the .NET Framework overall throughput and allocation numbers.**
- **Re-run both TFMs after the change** and diff against the saved baseline.
- **Record the full BenchmarkDotNet rows, not just a one-line summary.** Keep the
  whole table - `Mean`, `Error`, `StdDev`, `Gen0`, `Gen1`, `Gen2`, `Allocated` -
  for both the before and after runs on both TFMs. A summary like "~7% faster"
  loses the error bars (is the delta inside the noise?) and the allocation column
  (did the speedup trade CPU for garbage?). Save the raw output to
  `artifacts/<name>-baseline-<tfm>.txt` and `artifacts/<name>-after-<tfm>.txt`
  so the full diff is reproducible. Pipe the run through `Tee-Object` to keep the
  console table.
- **Allocation must not regress.** Compare the `Allocated` column before and
  after on *both* TFMs. "No additional allocations" is only true if both columns
  are unchanged.
- **Confirm the targeted cost actually moved.** Re-capture a trace after the
  change and check that the specific method/line the heat map flagged dropped
  (e.g. `System.Array.Copy` self-time falling from 30 ms to 19 ms). A faster
  wall-clock with the targeted frame unchanged usually means the win came from
  somewhere else - or from noise.
- **Distrust sub-microsecond deltas from a noisy machine.** A busy or thermally
  throttled host shows variance swings; trust the `Allocated` column and a unit
  test over a small `Mean` delta. Deltas comfortably outside the `Error`/`StdDev`
  and consistent in direction across both TFMs are credible.

Present both TFMs' before/after tables together when reporting the result, plus
the line-level evidence that the targeted hot spot shrank.

## Evaluating memory usage

With `[MemoryDiagnoser]` (or `--memory`), each row of the results table includes:

| Column      | Meaning                                                      |
| ----------- | ------------------------------------------------------------ |
| `Gen0`      | Gen0 collections per 1000 operations.                        |
| `Gen1`      | Gen1 collections per 1000 operations.                        |
| `Gen2`      | Gen2 collections per 1000 operations.                        |
| `Allocated` | Managed bytes allocated per single operation.                |

### Reading the numbers

- **`Allocated` is the primary signal.** A method that should be allocation-free
  must report `-` or `0 B`. Anything else is a regression.
- A `Ratio` column appears when one method is `Baseline = true`. Use it together
  with `Allocated` to confirm a perf change is not just trading CPU for
  allocations (or vice versa).
- `Gen0` ticking up while `Allocated` stays flat usually means you allocated a
  lot of short-lived objects on previous iterations - recheck `[GlobalSetup]`.
- Stack-only types (`Span<T>`, `ref struct`s, `stackalloc`) do not contribute to
  `Allocated`. Boxing of value types does - watch for accidental boxing through
  `object`, non-generic interfaces, or `string.Format`.
- On .NET Framework the JIT and BCL allocate differently than on the modern
  target. Always run both before declaring a win.

### Where the report lives

After each run BenchmarkDotNet writes artifacts under
`BenchmarkDotNet.Artifacts/results/` next to the executable, including:

- `*.md` - GitHub-friendly Markdown table (paste this into PR descriptions).
- `*.csv` and `*.html` - for spreadsheets / browser viewing.
- `*-report-full.json` - raw measurements; useful for diffing two runs
  programmatically.

The same table is also printed to the console at the end of the run.
