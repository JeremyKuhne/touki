---
description: Author and run SharpFuzz coverage-guided fuzz targets for a .NET library. Use when adding a new fuzz target, running the fuzzer locally, installing the fuzzing prerequisites, or promoting a crashing input into a regression test. Covers the cross-TFM harness, the libFuzzer driver workflow, and the crash-to-regression loop.
license: MIT
metadata:
    github-path: skills/fuzz-testing
    github-pinned: v0.8.1
    github-ref: refs/tags/v0.8.1
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: f944ff4acb2beb257bc15f0addc4b49a144734ea
    portability: semi-portable
name: fuzz-testing
---
# Fuzz testing

A coverage-guided fuzzing harness built on
[SharpFuzz](https://github.com/Metalnem/sharpfuzz) finds defects that
hand-written cases miss: it mutates a byte stream, watches which branches the
code under test takes, and steers toward inputs that reach new code. The
harness is a stand-alone executable, **not** a test project - the normal
`dotnet test` run never executes it. A cross-targeted harness can run the same
targets against both a modern .NET runtime and .NET Framework.

This skill covers *when* and *how* to extend a fuzz harness. The exact
instrument-and-run commands, prerequisites, and corpus mechanics live in
[references/running.md](references/running.md).

**Related skills:** a `security-review` of a parser, codec, or buffer primitive
is what motivates most fuzz targets - when that review flags untrusted-input
handling, add or extend a fuzz target instead of relying on a single
hand-written case. A `pre-pr-self-review` should treat new public parser /
codec / buffer surface as needing a fuzz target (or an explicit "not fuzzed"
note). On Windows the prebuilt driver runs natively, so a Linux / WSL path is
optional rather than required. A consuming repository wires the concrete
harness- and regression-project names, the prerequisites script, and the target
list in its overlay.

## Installing prerequisites

The harness needs the SharpFuzz instrumentation tool and a `libfuzzer-dotnet`
driver binary. Both install per-user with no elevation; the driver is a
prebuilt, per-platform download (coverage-guided fuzzing runs natively on
Windows). See [references/running.md](references/running.md) for the one-shot
script, the manual path, and the per-prerequisite rationale.

## Authoring a fuzz target

- One target per file, named `<Type>Target.cs`, in the harness namespace.
- Expose a single `internal static void Run(ReadOnlySpan<byte> data)` entry
  point and register it in the target-selection switch in the harness entry
  point.
- Treat the fuzz input as an **opcode stream**: decode operations from the
  bytes and drive the type under test. Compute every length / count / position
  argument **in range** so that any thrown exception is a genuine defect, not a
  fuzzer-supplied out-of-range argument.
- **Drive the loop from an opcode cursor that the operations cannot move.** When
  the type under test is itself a cursor (a reader or writer with a movable
  position), use a *separate* reader to pull opcodes and a *separate* subject
  instance for the operations. Sharing one reader lets a `Reset` / `Rewind` /
  position-set operation rewind the opcode cursor, so the driving loop re-reads
  the same bytes forever (a hang, not a crash). An in-process sweep with a
  watchdog catches this and reports the offending input; libFuzzer just appears
  to stall.
- Re-check structural invariants after every operation and throw a dedicated
  invariant-exception type on violation (SharpFuzz reports any unhandled
  exception as a crash; a dedicated type makes invariant failures easy to spot
  during triage).
- Follow the repo coding style.
- Add at least one seed input under the target's corpus directory.

### Cross-TFM gotchas

- A cross-targeted harness may build the library under an older TFM than the
  runtime it ships on, so targets must compile on **every** TFM the library
  supports. On .NET Framework `ReadOnlySpan<T>` comes from the `System.Memory`
  package.
- A `stackalloc` or collection-expression span **cannot** be passed to a
  ref-struct method whose span parameter is not `scoped`. Pass a slice of the
  caller-scoped input span instead.
- A .NET Framework pass exists specifically to catch Release-mode JIT
  divergences (for example the `[MethodImpl(AggressiveInlining)]` plus
  `Unsafe.As<T, byte/ushort>` sign-extension foot-gun, which only misbehaves in
  optimized .NET Framework codegen). Always publish in `Release` for the .NET
  Framework run.

## Crash-to-regression loop

A finding is only useful once it is pinned deterministically:

1. **Verify the crash reproduces before doing anything else.** Replay the
   `crash-<hash>` input on its own. If it does *not* reproduce, it is a
   *kill-artifact* - the slow in-flight unit the driver dumps when it is
   interrupted mid-run (Ctrl+C, a killed process, or a `--max_total_time`
   expiry). Delete it; do not move it into the crashes directory or commit it.
2. Once reproduction is confirmed, move the input under the crashes directory.
3. Minimize it.
4. Promote a **deterministic** reproduction (the input bytes plus the asserted
   invariant) into the regression test project, running on every supported TFM,
   so it is enforced on every PR.

## What to commit of the fuzzer's output

The fuzzer produces files in several places; only curated artifacts belong in
git. `.gitignore` files should enforce this, but know the intent:

- **`corpus/<target>/seed-*`** - commit. Hand-authored seeds that bootstrap
  coverage. A `seed-*` prefix is a clean allowlist.
- **`corpus/<target>/<hash>`** - do not commit. Machine-generated entries churn
  on every run; gitignore them. Never delete them locally - they preserve
  coverage.
- **`crashes/crash-*`** - commit *only* genuine, reproduced, minimized inputs
  (step 1 above). An un-triaged or non-reproducing `crash-<hash>` is noise.
- **Downloaded driver binaries** - do not commit.
- **Stray driver output in the working directory** (`crash-*`, `leak-*`,
  `timeout-*`, `oom-*`, `slow-unit-*`) - do not commit. Root-anchored patterns
  in the top-level `.gitignore` keep these from being staged; delete them once
  triaged.

The rule of thumb: nothing the fuzzer emits autonomously lands in git until a
human has decided it carries lasting value - seeds you wrote, or crashes you
have reproduced and reduced to a regression test.

## References

The mechanics, artifact names, and corpus model trace back to upstream
documentation, collected in [references/running.md](references/running.md):

- [libFuzzer documentation](https://llvm.org/docs/LibFuzzer.html) - options,
  corpus model, and the failing-input artifact names (`crash-<sha1>`,
  `leak-<sha1>`, `timeout-<sha1>`, `oom-<sha1>`, slow-unit). Passing a single
  file re-runs it as a regression test without fuzzing - the deterministic
  replay used to confirm a crash reproduces.
- [Using libFuzzer with SharpFuzz](https://github.com/Metalnem/sharpfuzz/blob/master/docs/libFuzzer.md) -
  the `libfuzzer-dotnet` driver and `Fuzzer.LibFuzzer.Run` entry point.
