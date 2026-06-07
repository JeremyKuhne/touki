# traceq fixtures

The trace corpus the tests read, and the tooling that regenerates it.

## Layout

- `HotLoopBench/` - a small, dedicated BenchmarkDotNet benchmark (a deliberately
  hot string-building loop) whose EventPipe CPU profile seeds the corpus. It is
  **not** part of `traceq.slnx`, so the subtree CI and the extraction rehearsal
  stay light; it is a manual regeneration tool.
- `oracles/Get-TraceHotspots.ps1` - a frozen copy of the repo's parity oracle, so
  the fixture pipeline stays self-contained through promotion. Treated as a
  process whose output is compared, never referenced as code.
- `make-fixtures.ps1` - captures the profile, copies the speedscope export into
  the parity-test fixtures, and freezes the oracle's self / inclusive rankings as
  a golden the parity tests compare against.

## Regenerating

On a Windows machine with the .NET 10 SDK:

```powershell
pwsh traceq/fixtures/make-fixtures.ps1
```

This refreshes `tests/TraceQ.Parity.Tests/Fixtures/hotloop.speedscope.json` and
`hotloop.oracle.json` **together**, as a matched pair (each capture produces
different absolute timings; the parity test compares traceq against the oracle on
the same committed file, so the pair stays consistent). Run it when the benchmark,
TraceEvent, or BenchmarkDotNet version moves.

## What is committed vs regenerated

The committed in-repo fixture is the speedscope export (a few hundred KB). The
full `.nettrace` is left under `HotLoopBench/BenchmarkDotNet.Artifacts/`
(gitignored) - it is too large for the repo and is regenerated on demand; the
full corpus is destined for a release asset.

## Deferred: the net481 ETW half

The `.etl` / `.etlx` half of the corpus (captured with `[EtwProfiler]` on .NET
Framework) needs an elevated Windows session and is added here when captured. It
is also the O1 cross-OS ETLX spike fixture.
