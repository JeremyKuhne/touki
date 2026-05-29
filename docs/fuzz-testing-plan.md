# Fuzz-Testing Integration Plan for `touki`

> **Workflow rule (non-negotiable).** No commits, pushes, or pull requests
> are to be made while executing this plan without **explicit user consent**
> for that specific action. Local edits and local build/test runs are fine;
> publishing of any kind (`git commit`, `git push`, opening or updating a PR)
> requires an explicit publishing verb from the user first. This mirrors the
> repository's [AGENTS.md](../AGENTS.md) "Working with the user on changes"
> contract.

## Guiding decisions

- **SharpFuzz works cross-TFM.** The package targets `netstandard2.0`, so a
  console harness can target **both `net10.0` and `net481`**. The
  instrumentation tool needs a .NET 8+ SDK host but rewrites IL for any
  managed assembly; the fuzzing engine (libFuzzer for native Windows,
  AFL/libFuzzer under WSL) is the only OS-level gate - not the target's TFM.
- Coverage-guided fuzzing lives in a **stand-alone project** (`touki.fuzz`).
  A thin, deterministic slice of any crash found gets promoted into the
  existing `touki.tests` so it runs on **every PR**; broad/long fuzzing runs
  on a separate cadence.

## Target surfaces, in rollout order

1. `SpanReader<T>` ([touki/Touki/Buffers/SpanReader.cs](../touki/Touki/Buffers/SpanReader.cs))
   and `SpanWriter<T>` ([touki/Touki/Buffers/SpanWriter.cs](../touki/Touki/Buffers/SpanWriter.cs)) -
   the narrow, low-level primitives. `unmanaged`/`unsafe` ref structs with
   `Position` setters, `Try*`/`Advance*` methods, and `UnsafeAdvance` -
   exactly the kind of bounds/offset logic worth fuzzing first.
2. RLE codec in [touki/Touki/RunLengthEncoder.cs](../touki/Touki/RunLengthEncoder.cs) -
   `GetEncodedLength` / `GetDecodedLength` / `TryEncode` / `TryDecode` (encode
   and decode both live in this type). Round-trip and length-invariant fuzzing
   builds directly on the `SpanReader`/`SpanWriter` work since RLE is
   implemented on top of them.

---

## Phase 1 - Stand up the cross-compiled SharpFuzz project; fuzz `SpanReader`/`SpanWriter` on .NET 10 locally

1. Create `touki.fuzz/touki.fuzz.csproj` targeting **`net10.0;net481`** (use
   the `$(DotNetCoreVersion)` property like the other projects). Reference
   `touki`. Add `PackageVersion Include="SharpFuzz"` to
   [Directory.Packages.props](../Directory.Packages.props) and a
   `PackageReference` in the new project. Keep it **out of the default
   solution test run** (not picked up by normal `dotnet test`).
2. Add fuzz entry points - one `Action<ReadOnlySpan<byte>>` / `Stream` target
   each:
   - **`SpanReader`**: drive a sequence of reads (`TryRead`, `TryReadTo`,
     `AdvancePast`, `Position` set) decoded from the fuzz bytes; assert no
     out-of-bounds, no read past `Length`, `Position` round-trips, and
     `Unread.Length + Position == Length`.
   - **`SpanWriter`**: drive `TryWrite`/advance ops; assert no write past
     capacity and `Position`/`End` consistency.
3. Document the local **.NET 10** loop: `dotnet tool install -g
   SharpFuzz.CommandLine`, instrument the net10 build, run under libFuzzer
   (native Windows) with a checked-in seed corpus. This is the **first
   workflow to enable**.
4. Establish the repo layout the later phases reuse: `touki.fuzz/corpus/<target>/`,
   `touki.fuzz/crashes/`, and a short README of the run commands.

**Exit criteria:** `SpanReader`/`SpanWriter` targets run clean locally on net10
with a seed corpus; harness layout and corpus/crash dirs established.

## Phase 2 - Same scenario, local runs on `net481`

1. Instrument and run the **`net481`** build of the same `SpanReader`/`SpanWriter`
   targets locally (libFuzzer on Windows, or AFL/libFuzzer under WSL).
2. This phase specifically hunts the **net481 Release RyuJIT divergences**
   documented in repo memory (the `[AggressiveInlining]` +
   `Unsafe.As<T, byte/ushort>` sign-extension foot-gun) - `SpanReader`/`SpanWriter`
   are `unsafe` ref structs over `unmanaged` T, the highest-risk place for
   codegen divergence. Run the instrumented build in **Release**.
3. Reuse the Phase 1 corpus as seeds so the two TFMs share inputs.

**Exit criteria:** net481 targets run clean in Release; any divergence found is
minimized into a regression case (see Phase 4).

## Phase 3 - Roll out RLE encode/decode scenarios

1. Add fuzz targets for the codec:
   - **Round-trip**: `TryDecode(TryEncode(x)) == x` for arbitrary byte input.
   - **Length invariants**: `GetEncodedLength` / `GetDecodedLength` match the
     actual `written` counts; encode/decode never write past the reported
     length.
   - **Malformed decode**: feed raw fuzz bytes straight to `TryDecode`
     (odd-length / truncated count-value streams per the format note) and
     assert it fails cleanly rather than over-reading.
2. Run these on **both** net10 and net481, using the same local workflows as
   Phases 1-2.

## Phase 4 - PR integration vs. expanded cadence

- **On every PR (in `touki.tests`):** promote each interesting fuzz finding to
  a **deterministic** xUnit case (a captured input byte array plus the
  asserted invariant), running on both `net10.0` and `net481`. Optionally add
  a small **fixed-seed, low-iteration** in-proc property check per target so
  the invariants are continuously exercised without flakiness. The PR gate
  never runs the coverage-guided fuzzer itself.
- **Expanded cadence (scheduled, not PR-gated):** longer, **random-seed**
  coverage-guided runs of the `touki.fuzz` project against the persisted
  corpus, on both TFMs, with the net481 Release pass included to keep flushing
  codegen divergences. Crashes -> minimize -> commit to `crashes/` -> promote
  to a `touki.tests` regression row. Never delete corpus entries.

---

## Cross-cutting

- **Skill:** add a `fuzz-testing` skill under [.agents/skills/](../.agents/skills/)
  covering the instrument/run commands for both engines, the cross-TFM
  harness, the seed-corpus / crash-promotion workflow, and the net481-Release
  codegen caveat. Wire `security-review` and `pre-pr-self-review` to point new
  parser/codec/buffer surfaces at it.
- **CPM / MSBuild:** SharpFuzz version centralized in
  [Directory.Packages.props](../Directory.Packages.props); `touki.fuzz.csproj`
  follows the repo's msbuild instructions and stays excluded from the normal
  test run.

## Tooling reference

- **SharpFuzz** (`netstandard2.0`) - IL-instrumentation fuzzing library;
  `Fuzzer.LibFuzzer.Run` / `Fuzzer.OutOfProcess.Run` entry points.
- **SharpFuzz.CommandLine** - global tool (`dotnet tool install -g
  SharpFuzz.CommandLine`), requires a .NET 8+ SDK host, instruments any
  managed assembly including net481 builds.
- **Engines:** libFuzzer (`libfuzzer-dotnet`) for native Windows; AFL or
  libFuzzer under WSL for the Linux path.
