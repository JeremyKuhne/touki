# Running the fuzzer

Detail for the [fuzz-testing](../SKILL.md) skill: prerequisites, the
instrument-and-run commands, the no-tooling quick sweep, crash triage, and the
corpus commit policy. Project and assembly names below use `<root>` as a
placeholder for your library, so `<root>.fuzz` is the harness project and
`<root>.tests` the regression project. Where a command needs a target framework,
substitute `<tfm>` with the framework you publish (a modern-.NET TFM for the
first pass, your .NET Framework TFM for the second).

A single instrumented executable hosts every target. The target is selected
with the `FUZZ_TARGET` environment variable (the libFuzzer driver supplies its
own command-line arguments, so an environment variable is used instead). Each
target maps a `FUZZ_TARGET` value to a type under test and a `<Type>Target.cs`
file, for example:

| `FUZZ_TARGET` | Exercises | File |
| --- | --- | --- |
| `SpanReader` (default) | a span reader's reads / advances / splits | `<root>.fuzz/SpanReaderTarget.cs` |
| `StringBuilder` | a string builder's append / insert / truncate vs a `StringBuilder` oracle | `<root>.fuzz/StringBuilderTarget.cs` |

Each target uses the fuzz input as an opcode stream that drives a sequence of
operations, then re-checks structural invariants after every operation. Any
unhandled exception - including a dedicated invariant exception - is reported
to the fuzzer as a crash.

## Prerequisites

| Prerequisite | Why | Auto-installed? |
| --- | --- | --- |
| .NET 8+ SDK | Required by the SharpFuzz instrumentation tool (the target itself runs on your library's TFMs). | No - install from <https://dotnet.microsoft.com/download> |
| `SharpFuzz.CommandLine` global tool | Rewrites the assembly under test to emit coverage. | Yes (per-user, no elevation) |
| `libfuzzer-dotnet` driver | The libFuzzer engine that drives the harness. | Yes - a prebuilt, per-platform binary downloaded into `tools/` |

> **Coverage-guided fuzzing runs natively on Windows.** The upstream
> `libfuzzer-dotnet` project ships a prebuilt Windows driver
> (`libfuzzer-dotnet-windows.exe`), so no clang / LLVM toolchain or WSL is
> required - an install script just downloads it. Prebuilt Ubuntu and Debian
> drivers are downloaded the same way on Linux. The
> [quick sweep](#quick-sweep-no-native-tooling) below remains available as a
> coverage-blind in-process check that needs no driver at all.

### One-shot install

A bundled install script that checks each prerequisite and installs whatever is
missing is the smoothest path. No elevation is required; a `-Force`-style switch
should reinstall prerequisites that are already present. The script should not
install the .NET SDK itself (it is large and best managed deliberately) - it
should fail with guidance if a .NET 8+ SDK is missing.

### Manual install

```pwsh
# 1. SharpFuzz instrumentation tool (per-user)
dotnet tool install --global SharpFuzz.CommandLine

# 2. Prebuilt libfuzzer-dotnet driver (Windows)
$release = "v2025.05.02.0904"
Invoke-WebRequest "https://github.com/Metalnem/libfuzzer-dotnet/releases/download/$release/libfuzzer-dotnet-windows.exe" `
  -OutFile <root>.fuzz/tools/libfuzzer-dotnet.exe
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
dotnet run --project <root>.fuzz/<root>.fuzz.csproj -c Release -f <tfm>
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

## Running (coverage-guided)

1. Publish the harness:

   ```pwsh
   dotnet publish <root>.fuzz/<root>.fuzz.csproj -c Release -f <tfm> -o artifacts/fuzz/<tfm>
   ```

2. Instrument the assembly under test (the code being fuzzed); the SharpFuzz
   runtime in the harness assembly is what feeds coverage to the driver:

   ```pwsh
   sharpfuzz artifacts/fuzz/<tfm>/<root>.dll
   ```

3. Run the libFuzzer driver against the harness, selecting a target and a
   corpus directory:

   ```pwsh
   $env:FUZZ_TARGET = "SpanReader"
   <root>.fuzz/tools/libfuzzer-dotnet.exe `
     --target_path=artifacts/fuzz/<tfm>/<root>.fuzz.exe `
     <root>.fuzz/corpus/SpanReader
   ```

   Switch `FUZZ_TARGET` (and the corpus path) to fuzz the other targets.
   `--target_path` is the published apphost (`<root>.fuzz.exe` on Windows), not
   the managed `.dll`.

Stop a run with <kbd>Ctrl</kbd>+<kbd>C</kbd>. libFuzzer adds newly interesting
inputs to the corpus directory as it runs.

### Running on .NET Framework

The steps are identical with the .NET Framework `<tfm>` in the publish command
and a libFuzzer driver that loads the .NET Framework build. Always publish in
`Release` so the .NET Framework JIT codegen under test matches what ships.

## Triaging crashes

A crashing input is written to a file named `crash-<hash>` in the working
directory. Before treating it as a regression artifact, **confirm it is a
genuine defect**:

1. Replay it on its own (`FUZZ_MODE=sweep` with the input, or feed the single
   file back to the driver). A genuine crash reproduces deterministically.
2. If it does **not** reproduce, it is almost certainly a *kill-artifact* - the
   slow in-flight unit the driver dumps when it is interrupted mid-run
   (Ctrl+C, a killed process, or a `--max_total_time` expiry). Delete it; do
   not move it into the crashes directory.
3. Once reproduction is confirmed, move it under the crashes directory,
   minimize it, then promote a deterministic reproduction into `<root>.tests`
   so it runs on every PR.

## Corpus and crashes

What to commit, and what to leave out, of each fuzzer-produced location:

- `corpus/<target>/` - **commit only the curated seeds named `seed-*`.**
  libFuzzer also writes machine-generated, hash-named entries here as it runs;
  gitignore those (they churn on every run and would bloat the repo). Keep the
  committed seeds; do not delete them.
- `crashes/` - **commit only genuine, reproduced, minimized `crash-*` inputs.**
  An un-triaged or non-reproducing `crash-<hash>` does not belong here (see the
  triage steps above).
- `tools/` - downloaded driver binaries; not committed.
- Working directory (repo root) - stray driver output (`crash-*`, `leak-*`,
  `timeout-*`, `oom-*`, `slow-unit-*`) is root-anchored in the top-level
  `.gitignore` so it can never be accidentally staged. These are transient;
  delete them once triaged.

## Upstream references

The artifact names and the regression-replay workflow above come from the
upstream libFuzzer and SharpFuzz documentation:

- [libFuzzer - Options](https://llvm.org/docs/LibFuzzer.html#options) -
  `-artifact_prefix` / `-exact_artifact_path` document that failing inputs are
  saved as `crash-<sha1>`, `leak-<sha1>`, `timeout-<sha1>`, `oom-<sha1>`, and
  slow-unit artifacts. Passing a single file (rather than a directory) re-runs
  it as a regression test without fuzzing - the deterministic replay used to
  confirm a crash reproduces.
- [libFuzzer - Corpus](https://llvm.org/docs/LibFuzzer.html#corpus) and
  [Running](https://llvm.org/docs/LibFuzzer.html#running) - new interesting
  inputs are written to the first corpus directory (the machine-generated,
  hash-named churn that `corpus/.gitignore` excludes), and `-merge=1` minimizes
  a corpus while preserving coverage.
- [Using libFuzzer with SharpFuzz](https://github.com/Metalnem/sharpfuzz/blob/master/docs/libFuzzer.md) -
  the `libfuzzer-dotnet` driver and `Fuzzer.LibFuzzer.Run` entry point.
