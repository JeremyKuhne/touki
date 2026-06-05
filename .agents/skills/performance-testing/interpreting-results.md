# Interpreting results

Detail for the [performance-testing](SKILL.md) skill. Covers the before/after
measurement discipline, reading the memory columns, and the one .NET Core hot
path where tuple swap is worth it.

## Before/after measurement discipline (both TFMs, every time)

A perf change is not done until you can show it helped and harmed nothing. The
trace analyzer answers *where* to optimize (see [profiling.md](profiling.md));
only a before/after benchmark answers *whether it worked*. Treat the following as
mandatory whenever a code change is driven by a profile:

- **Capture a baseline first, on both `net10.0` and `net481`.** Run the affected
  benchmark on each TFM (`-f net10.0` and `-f net481`) *before* touching the
  source and save the full result rows. The line-level EventPipe profiling that
  guides the edit is `net10.0`-only, but the change still has to be measured on
  `net481` - the older RyuJIT and BCL allocate and inline differently, and a
  win on one TFM can be a wash or a regression on the other. **Never iterate on
  net10 line info without also tracking the net481 overall throughput and
  allocation numbers.**
- **Re-run both TFMs after the change** and diff against the saved baseline.
- **Record the full BenchmarkDotNet rows, not just a one-line summary.** Keep
  the whole table - `Mean`, `Error`, `StdDev`, `Gen0`, `Gen1`, `Gen2`,
  `Allocated` - for both the before and after runs on both TFMs. A summary like
  "~7% faster" loses the error bars (is the delta inside the noise?) and the
  allocation column (did the speedup trade CPU for garbage?). Save the raw
  output to `artifacts/<name>-baseline-<tfm>.txt` and
  `artifacts/<name>-after-<tfm>.txt` so the full diff is reproducible. Pipe the
  run through `Tee-Object` to keep the console table.
- **Allocation must not regress.** Compare the `Allocated` column before and
  after on *both* TFMs. "No additional allocations" is only true if both columns
  are unchanged.
- **Confirm the targeted cost actually moved.** Re-capture a `net10.0` trace
  after the change and check that the specific method/line the heat map flagged
  dropped (e.g. `System.Array.Copy` self-time falling from 30 ms to 19 ms). A
  faster wall-clock with the targeted frame unchanged usually means the win came
  from somewhere else - or from noise.
- **Distrust sub-microsecond deltas from this machine.** The i9-14900K harness
  shows thermal/variance swings; trust the `Allocated` column and a unit test
  over a small `Mean` delta. Deltas comfortably outside the `Error`/`StdDev` and
  consistent in direction across both TFMs are credible.

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

## Tuple-swap on .NET Core hot paths

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
