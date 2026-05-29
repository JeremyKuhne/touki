# touki.fuzz

Coverage-guided fuzzing harness for `touki`, built on
[SharpFuzz](https://github.com/Metalnem/sharpfuzz). This is a stand-alone
executable, not a test project - the normal `dotnet test` run does not execute
it. See [docs/fuzz-testing-plan.md](../docs/fuzz-testing-plan.md) for the
overall plan and phases.

The project cross-targets `net10.0` and `net481` so the same fuzz targets can
be run against both runtimes. Phase 1 enables the local **.NET 10** workflow;
Phase 2 adds the **net481** Release pass.

## Targets

A single instrumented executable hosts every target. The target is selected
with the `FUZZ_TARGET` environment variable (the libFuzzer driver supplies its
own command-line arguments, so an environment variable is used instead):

| `FUZZ_TARGET` | Exercises | File |
| --- | --- | --- |
| `SpanReader` (default) | `Touki.Io.SpanReader<byte>` reads/advances/splits | [SpanReaderTarget.cs](SpanReaderTarget.cs) |
| `SpanWriter` | `Touki.Io.SpanWriter<byte>` writes/advances/rewinds | [SpanWriterTarget.cs](SpanWriterTarget.cs) |
| `RunLength` | `Touki.Buffers.RunLengthEncoder` encode roundtrip / arbitrary decode | [RunLengthTarget.cs](RunLengthTarget.cs) |

Each target uses the fuzz input as an opcode stream that drives a sequence of
operations, then re-checks structural invariants (position in range, span
length stable, `Unread`/`End` consistency) after every operation. Any
unhandled exception - including a `FuzzInvariantException` - is reported to the
fuzzer as a crash.

## Prerequisites

| Prerequisite | Why | Auto-installed? |
| --- | --- | --- |
| .NET 8+ SDK | Required by the SharpFuzz instrumentation tool (the target itself runs on .NET 10 or .NET Framework 4.8.1). | No - install from <https://dotnet.microsoft.com/download> |
| `SharpFuzz.CommandLine` global tool | Rewrites the assembly under test to emit coverage. | Yes (per-user, no elevation) |
| `libfuzzer-dotnet` driver | The libFuzzer engine that drives the harness. | Yes - a prebuilt, per-platform binary is downloaded into `tools/` |

> **Coverage-guided fuzzing runs natively on Windows.** The upstream
> `libfuzzer-dotnet` project ships a prebuilt Windows driver
> (`libfuzzer-dotnet-windows.exe`), so no clang/LLVM toolchain or WSL is
> required - the install script just downloads it. Prebuilt Ubuntu and
> Debian drivers are downloaded the same way on Linux. The
> [quick sweep](#quick-sweep-no-native-tooling) below remains available as a
> coverage-blind in-process check that needs no driver at all.

### One-shot install

Run the bundled script from the repo root. It checks each prerequisite and
installs whatever is missing. No elevation is required:

```pwsh
pwsh touki.fuzz/Install-FuzzPrereqs.ps1
```

Add `-Force` to reinstall / re-download prerequisites that are already present.

> The script does not install the .NET SDK itself (it is large and best
> managed deliberately); it fails with guidance if a .NET 8+ SDK is missing.

### Manual install

If you prefer to install by hand:

```pwsh
# 1. SharpFuzz instrumentation tool (per-user)
dotnet tool install --global SharpFuzz.CommandLine

# 2. Prebuilt libfuzzer-dotnet driver (Windows)
$release = "v2025.05.02.0904"
Invoke-WebRequest "https://github.com/Metalnem/libfuzzer-dotnet/releases/download/$release/libfuzzer-dotnet-windows.exe" `
  -OutFile touki.fuzz/tools/libfuzzer-dotnet.exe
```

On Linux, download `libfuzzer-dotnet-ubuntu` (or `libfuzzer-dotnet-debian`)
from the same release instead, or compile
[`libfuzzer-dotnet.cc`](https://github.com/Metalnem/libfuzzer-dotnet/blob/master/libfuzzer-dotnet.cc)
with `clang -fsanitize=fuzzer`. See the SharpFuzz docs for full driver details:
<https://github.com/Metalnem/sharpfuzz/blob/master/docs/libFuzzer.md>.

## Quick sweep (no native tooling)

For a fast smoke run that needs neither `clang` nor the `libfuzzer-dotnet`
driver, set `FUZZ_MODE=sweep`. The harness then drives the selected target with
deterministic, fixed-seed random inputs in-process and reports the exact input
of any crash or stall:

```pwsh
$env:FUZZ_MODE = "sweep"
$env:FUZZ_TARGET = "SpanReader"
dotnet run --project touki.fuzz/touki.fuzz.csproj -c Release -f net10.0
```

A watchdog flags any iteration that fails to make progress (for example an
infinite loop) and prints the offending input instead of hanging. Because the
seed is fixed, a failing input reproduces on every run. Tuning variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `FUZZ_ITERATIONS` | `2000000` | Number of inputs to try. |
| `FUZZ_MAX_LENGTH` | `64` | Maximum input length in bytes. |
| `FUZZ_SEED` | `1` | Seed for the input generator. |
| `FUZZ_TIMEOUT_MS` | `5000` | Watchdog stall threshold per iteration. |

This is a coverage-blind sweep, not a substitute for the coverage-guided
libFuzzer run below; use it for a quick check or to reproduce a known input.

## Running (local .NET 10 - Phase 1)

1. Publish the harness for .NET 10:

   ```pwsh
   dotnet publish touki.fuzz/touki.fuzz.csproj -c Release -f net10.0 -o artifacts/fuzz/net10
   ```

2. Instrument the assemblies under test. Instrument the `touki` assembly (the
   code being fuzzed); the SharpFuzz runtime in the harness assembly is what
   feeds coverage to the driver:

   ```pwsh
   sharpfuzz artifacts/fuzz/net10/touki.dll
   ```

3. Run the libFuzzer driver against the harness, selecting a target and a
   corpus directory:

   ```pwsh
   $env:FUZZ_TARGET = "SpanReader"
   touki.fuzz/tools/libfuzzer-dotnet.exe `
     --target_path=artifacts/fuzz/net10/touki.fuzz.exe `
     touki.fuzz/corpus/SpanReader
   ```

   Switch `FUZZ_TARGET` (and the corpus path) to `SpanWriter` or `RunLength`
   to fuzz the other targets. `--target_path` is the published apphost
   (`touki.fuzz.exe` on Windows), not the managed `.dll`.

Stop a run with <kbd>Ctrl</kbd>+<kbd>C</kbd>. libFuzzer adds newly interesting
inputs to the corpus directory as it runs.

### Running on net481 (Phase 2)

The steps are identical with `-f net481` in the publish command and a
libFuzzer driver that loads the .NET Framework build. Always publish in
`Release` so the net481 RyuJIT codegen under test matches what ships.

## Triaging crashes

A crashing input is written to a file named `crash-<hash>` in the working
directory. To keep it as a regression artifact:

1. Move it under [crashes/](crashes/).
2. Reproduce and minimize it, then promote a deterministic reproduction into
   `touki.tests` so it runs on every PR (see Phase 4 in the plan).

## Corpus and crashes

- `corpus/<target>/` - curated seed inputs named `seed-*.bin` are committed;
  add interesting hand-picked inputs over time. libFuzzer also writes
  machine-generated, hash-named entries here as it runs - those are gitignored
  (see `corpus/.gitignore`) because they churn on every run and would bloat the
  repo. Keep the committed seeds; do not delete them.
- `crashes/` - minimized crashing inputs kept as regression artifacts.
- `tools/` - downloaded driver binaries; not committed.
