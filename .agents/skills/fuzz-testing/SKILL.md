---
name: fuzz-testing
description: Author and run SharpFuzz coverage-guided fuzz targets in the `touki.fuzz` project. Use when adding a new fuzz target, running the fuzzer locally on net10 or net481, installing the fuzzing prerequisites, or promoting a crashing input into a `touki.tests` regression. Covers the cross-TFM harness, the libFuzzer driver workflow, and the crash-to-regression loop.
---

# Fuzz testing in `touki.fuzz`

The [touki.fuzz](../../../touki.fuzz/touki.fuzz.csproj) project hosts the
[SharpFuzz](https://github.com/Metalnem/sharpfuzz) coverage-guided fuzzing
harness for the library. It is a stand-alone executable, **not** a test
project - the normal `dotnet test` run never executes it. It cross-targets
`net10.0` and `net481` so the same targets run on both runtimes.

The overall strategy, phases, and PR-integration cadence live in
[docs/fuzz-testing-plan.md](../../../docs/fuzz-testing-plan.md).

**Read [touki.fuzz/README.md](../../../touki.fuzz/README.md) first** - it is the
authoritative source for installing prerequisites and the exact
instrument-and-run commands. This skill covers *when* and *how* to extend the
harness; the README covers the mechanics.

**Related skills:**

- [`security-review`](../security-review/SKILL.md) - the DoS / unchecked-length /
  backtracking / `unsafe` checklist that motivates most fuzz targets. When that
  review flags a parser, codec, or buffer primitive, add or extend a fuzz target
  here instead of relying on a single hand-written case.
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) - new public parser /
  codec / buffer surface should have a fuzz target (or an explicit "not fuzzed"
  note).
- [`run-tests-on-wsl`](../run-tests-on-wsl/SKILL.md) - the Linux path for AFL /
  libFuzzer when fuzzing on WSL (optional; the prebuilt driver runs natively on
  Windows, so WSL is not required for coverage-guided fuzzing).

## Installing prerequisites

Run the bundled script from the repo root; it installs what is missing. No
elevation is required - the libFuzzer driver is downloaded as a prebuilt,
per-platform binary (coverage-guided fuzzing runs natively on Windows):

```pwsh
pwsh touki.fuzz/Install-FuzzPrereqs.ps1
```

See the [README prerequisites section](../../../touki.fuzz/README.md#prerequisites)
for the manual path and the per-prerequisite rationale.

## Authoring a fuzz target

- One target per file, named `<Type>Target.cs`, in the `Touki.Fuzz` namespace
  (e.g. [SpanReaderTarget.cs](../../../touki.fuzz/SpanReaderTarget.cs)).
- Expose a single `internal static void Run(ReadOnlySpan<byte> data)` entry
  point and register it in the `FUZZ_TARGET` switch in
  [Program.cs](../../../touki.fuzz/Program.cs).
- Treat the fuzz input as an **opcode stream**: decode operations from the
  bytes and drive the type under test. Compute every length / count / position
  argument **in range** so that any thrown exception is a genuine defect, not a
  fuzzer-supplied out-of-range argument.
- **Drive the loop from an opcode cursor that the operations cannot move.** When
  the type under test is itself a cursor (e.g. `SpanReader<T>`), use a *separate*
  reader to pull opcodes and a *separate* subject instance for the operations.
  Sharing one reader lets `Reset` / `Rewind` / position-set rewind the opcode
  cursor, so the driving loop re-reads the same bytes forever (a hang, not a
  crash). The in-process sweep's watchdog (`FUZZ_MODE=sweep`) catches this and
  reports the offending input; libFuzzer just appears to stall.
- Re-check structural invariants after every operation and throw a
  [FuzzInvariantException](../../../touki.fuzz/FuzzInvariantException.cs) on
  violation (SharpFuzz reports any unhandled exception as a crash; the dedicated
  type makes invariant failures easy to spot during triage).
- Follow the repo coding style (no `var`, target-typed `new()`, C# keyword type
  names, `is null` / `is not null`, indented XML docs). See
  [AGENTS.md](../../../AGENTS.md).
- Add at least one seed input under `touki.fuzz/corpus/<TargetName>/`.

### Cross-TFM gotchas

- The harness builds `touki` as net472 under the net481 target (the
  `DependencyTargetFramework` trick in the csproj), so targets must compile on
  both TFMs. `ReadOnlySpan<T>` comes from `System.Memory` on net481.
- A `stackalloc` / collection-expression span **cannot** be passed to a
  ref-struct method whose parameter is not `scoped` (e.g.
  `SpanReader<T>.TryAdvancePast`). Pass a slice of the caller-scoped input span
  instead.
- The net481 pass exists specifically to catch Release-mode RyuJIT divergences
  (the `[AggressiveInlining]` + `Unsafe.As<T, byte/ushort>` sign-extension
  foot-gun). Always publish in `Release` for the net481 run.

## Crash-to-regression loop

A finding is only useful once it is pinned deterministically:

1. Move the crashing input under `touki.fuzz/crashes/`.
2. Reproduce, then minimize it.
3. Promote a **deterministic** reproduction (the input bytes + the asserted
   invariant) into `touki.tests`, running on both `net10.0` and `net481`, so it
   is enforced on every PR.
4. Never delete corpus entries - they keep coverage from regressing.
