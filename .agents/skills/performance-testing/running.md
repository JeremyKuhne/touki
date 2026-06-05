# Running benchmarks

Detail for the [performance-testing](SKILL.md) skill. The project's
`Program.Main` uses `BenchmarkSwitcher.FromAssembly(...)` so all CLI arguments
are forwarded to BenchmarkDotNet. Run from the repo root in PowerShell.

For drilling a benchmark down to the hot method or source line, see
[profiling.md](profiling.md). For before/after discipline and reading the
result columns, see [interpreting-results.md](interpreting-results.md).

## Specifying the target framework is required

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

## Run a single class or method

```powershell
# All benchmarks in StoreInteger
dotnet run -c Release -f net10.0 --project touki.perf -- --filter *StoreInteger*

# A single method
dotnet run -c Release -f net10.0 --project touki.perf -- --filter *StoreInteger.As
```

## Interactive picker

```powershell
dotnet run -c Release --project touki.perf
```

With no `--filter`, BenchmarkSwitcher prints a numbered menu. Useful when exploring.

## Useful extra switches

- `--job short` - very fast, low-confidence run; good for smoke-testing a new
  benchmark before committing to a full run.
- `--memory` - enables the memory diagnoser for this run even if the class is not
  decorated. Prefer the attribute.
- `--exporters github` - emits a GitHub-flavored Markdown report alongside the
  default outputs.
