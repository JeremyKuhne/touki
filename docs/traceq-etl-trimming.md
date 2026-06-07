# traceq ETL trimming - state and follow-up

Status: parked (2026-06-07). This document captures the full state of the
physical ETW trace-trimming investigation so it can be picked up later. The
lossless analysis-time alternative (process-tree scoping in the ETL reader) is
the path being built now; physical trimming remains a wanted-but-deferred
capability.

## Why trimming is wanted

An ETW kernel capture is machine-wide. A capture of a single benchmark run on a
developer machine is dominated by other processes' context switches and the
image / symbol rundown for every loaded module - in one measured capture, a
small benchmark produced a 111 MB `.etl` (164 MB `.etlx`) of which roughly 67 %
was context-switch noise from 488 unrelated processes. Two concrete reasons to
physically trim such a file to just the scenario's process tree:

- **Avoid repeated filtering.** Every analysis pass over the machine-wide trace
  re-reads and re-filters all the noise. A one-time trim to the scenario means
  every later `rank` / `callers` / `diff` reads a small file.
- **Transport a smaller trace.** Handing a trace to a teammate, attaching it to
  a bug, or pulling it into CI is far cheaper for a sub-megabyte scenario file
  than for a multi-hundred-megabyte machine-wide capture.

Neither reason is required for correctness - the full trace analyses perfectly -
so trimming is an optimization, not a blocker.

## What was built

A `trim` verb in the fixture tool
([traceq/fixtures/HotLoopBench/Program.cs](../traceq/fixtures/HotLoopBench/Program.cs)):

```
trim <inEtl> <outEtl> <processNameSubstring> [--no-children]
```

It relogs an `.etl` with `ETWReloggerTraceEventSource`, keeping only the events
of a **process tree**: every process whose name contains the substring, plus
(by default) all of their descendants. `--no-children` restricts to the matched
processes only. It runs unelevated - relogging an existing file is not a live
kernel session.

Following children is not optional polish; it is the core of the
BenchmarkDotNet scenario (see below) and the right default for "profile my app",
whose real work frequently runs in child processes.

## The technical journey (so it isn't re-derived)

Getting the relog to preserve a usable trace took cracking five distinct layers.
Each is non-obvious and each was confirmed against the PerfView / TraceEvent
sources at `n:\repos\perfview\src\TraceEvent`:

1. **Stack-key compression.** Deep / repeated stacks (the managed hot path) are
   not stored inline. The sampled event carries a `StackWalkStackKeyKernel` /
   `StackWalkStackKeyUser` *reference* to a `StackKey`, and the frames live in a
   separate `StackWalkKeyRundown` *definition* emitted at session end. Keeping
   only the inline `StackWalkStack` events preserves just the shallow native
   stacks. Fix: keep the key references (matched to their target), record their
   `StackKey`, and keep the matching rundown definitions.
2. **Stack-to-event association.** A kernel stack-walk event does not carry a
   reliable owning thread in the raw relogger - `StackWalkStackTraceData`'s own
   `FixupData` only repairs the header thread when it reads `-1`, and the payload
   thread is not public. Match a stack to its target event by timestamp
   (`EventTimeStampRelativeMSec`) instead, which is exactly how `TraceLog` folds
   stacks onto events. (`TimeStampQPC` is obsolete; the relative-MSec form
   converts the same QPC tick through the same function, so the two compare
   bit-identically.)
3. **Wrong process.** BenchmarkDotNet runs each workload in a **child** process
   (`HotLoopBench-Job-...`); the orchestrating **host** only builds and launches
   it, and the build makes the host the *higher-CPU* process. Selecting "the
   busiest matching process" therefore picks the host and drops the child that
   actually ran the workload. This is what motivated process-tree following.
4. **Kernel provider GUIDs.** To avoid writing the typed kernel events twice, the
   catch-all handler must skip them - but skipping by
   `KernelTraceEventParser.ProviderGuid` is wrong, because the Windows kernel
   logs under several provider GUIDs, so a GUID check silently drops the kernel
   `Image` events that map instruction pointers to modules. Skip by event *type*
   instead.
5. **Process tree.** Keep the matched roots plus all descendants by walking
   `TraceProcess.Parent`. After this, the trimmed file kept the host, the job
   child, and `conhost`, with all the CLR method / module / process / thread
   events present and identical to the full trace.

## The blocker

After all five layers, the trimmed file resolves **native** modules (`clr!`,
`ntdll!`, `kernel32!`) but **never the JITted managed methods**
(`EtwLoop.BuildLabel`), even though its CLR method-load, module-load, process,
and thread events are byte-identical to the full trace, which resolves the
managed frames perfectly.

The cause is fundamental to the raw relogger: `ETWReloggerTraceEventSource`
re-injects events one by one (`m_relogger.Inject`), which preserves the static
image / address-to-module map but does **not** rebuild the JITted-method address
map that `TraceLog`'s full native conversion constructs. This is layer six, and
unlike layers one through five it is not a "keep more events" fix - the events
are already all present.

Measured end state of the trim: ~1 MB output, 755 CPU samples (381 with a
stack), 275 frames resolved to a native module, 3 benchmark methods and 902
method-load records present in the rundown, but zero managed frames resolved.

## Why this is parked, not abandoned

The user's actual goal - capture a BenchmarkDotNet (or plain executable) run and
analyze it with the tool, scoped to the workload and its children - does **not**
require physically trimming the file. The full trace resolves everything,
including the whole process tree. So process-tree scoping belongs at **analysis
time**, over the fully-resolved `TraceLog`, where it is lossless:

> The ETL reader opens the full `.etl` (TraceLog resolves all managed + native
> frames), walks the samples, and keeps only those whose process is in the
> workload tree. The child-following logic built for `trim` moves into this
> filter.

That delivers the seamless "capture -> analyze scoped to the workload tree" flow
for both the BenchmarkDotNet and the direct-executable workflows, with no
managed-resolution loss, and it mirrors the existing `ScopeFilter`.

## Follow-up options for physical trimming

When physical trimming is revisited (for the two motivations at the top), the
candidate approaches, roughly in order of promise:

1. **Re-convert instead of relog.** After scoping at analysis time, write a new
   trace from the fully-resolved `TraceLog` rather than relogging raw events, so
   the managed-method map is rebuilt rather than copied. Needs a TraceLog ->
   `.etl` / `.etlx` writer that carries JIT symbol info.
2. **Trim to `.etlx`, not `.etl`.** The cross-machine hand-off format is `.etlx`,
   which already contains the resolved symbol indices. Trimming or sub-setting at
   the `.etlx` layer may sidestep the relogger's managed-map gap entirely.
3. **Capture smaller at the source.** A standalone, long-lived capturer that runs
   its own short ETW session around just the target process tree (rather than a
   machine-wide BenchmarkDotNet `[EtwProfiler]` session) yields a small file with
   no trimming. This also makes the "profile my app" workflow a first-class
   capture path.
4. **Release-asset transport.** For the narrower "transport a smaller trace"
   need, the plan's section 3 already prescribes attaching the full corpus to a
   GitHub release and pulling on demand, with a tiny committed smoke - a
   transport answer that needs no managed-map fix at all.

## Pointers

- Trim + inspect implementation:
  [traceq/fixtures/HotLoopBench/Program.cs](../traceq/fixtures/HotLoopBench/Program.cs)
  (the `Trim` and `Inspect` methods; `inspect` reports CPU-sample stack
  resolution, which is how the managed-resolution gap was measured).
- Relogger internals: `n:\repos\perfview\src\TraceEvent\ETWReloggerTraceEventSource.cs`
  and `Parsers\KernelTraceEventParser.cs` (the `StackWalk*` / `StackKey*` event
  types and their `FixupData`).
- Overall plan and milestone context:
  [docs/traceq-implementation-plan.md](traceq-implementation-plan.md).
