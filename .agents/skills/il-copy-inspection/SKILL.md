---
name: il-copy-inspection
description: Find struct value copies in a method's compiled IL - defensive copies, boxing, by-value field/argument/return copies - by reading the emitted bytecode rather than predicting from source. Use when asked to "find struct copies", "where does the compiler copy this struct", "is this a defensive copy", "check for boxing in IL", "did the compiler emit a copy here", "confirm the analyzer's defensive-copy warning", or "audit a [NonCopyable] type's copies after build". Post-build, ground-truth counterpart to the source-level `roslyn-analyzers` defensive-copy rules (TOUKI0002-0004): IL is post-lowering, so synthesized copies the analyzer cannot see are visible here. Not for wall-clock/allocation measurement (that is `performance-testing`) nor for JIT-emitted machine code (that is `framework-jit-optimization` + `DisassemblyDiagnoser`).
metadata:
    portability: semi-portable
---

# Inspecting IL for struct copies

A struct copy is invisible in C# source but concrete in IL. This skill reads a
method's **emitted bytecode** to find where the compiler actually copies a value
type - defensive copies, boxing, and by-value field/argument/return copies - and
maps each back to a source line. It is the ground-truth, post-build complement to
the predictive, in-IDE `roslyn-analyzers` rules.

## The four layers (where this sits)

A struct copy can be reasoned about at four levels. Pick the layer that answers the
question; they are complementary, not interchangeable.

| Layer | Artifact | Tool / skill | What it tells you |
| ----- | -------- | ------------ | ----------------- |
| Source | `IOperation` (pre-lowering) | `roslyn-analyzers` (TOUKI0002-0004) | A copy is *predicted* from C# rules, live in the IDE |
| **IL** | **compiled bytecode (post-lowering)** | **this skill** (`ildasm` / `ilspycmd` / Cecil) | **A copy was *emitted* by the C# compiler** |
| Asm | JIT machine code | `framework-jit-optimization` + `DisassemblyDiagnoser` | Whether the JIT *kept* the copy or elided it |
| Runtime | ETW / wall-clock | `performance-testing` + `filtrace` | Whether the copy *costs* measurable time / allocation |

The IL layer is uniquely able to see copies the **compiler synthesizes during
lowering** - async / iterator state-machine field hoisting, closures, thunks -
which have no source operation and are therefore out of reach for an analyzer. It
is also the cheapest way to *confirm* a TOUKI0002/0003 prediction: if the IL has the
defensive-copy signature, the analyzer was right.

## Step 0 - is something already answering this?

- **"Does my code have a defensive copy I can fix in the editor?"** -> the
  `roslyn-analyzers` rules already flag the common cases live. Use this skill only
  to (a) confirm a flagged case, or (b) find the synthesized copies the analyzer
  documents as out of scope.
- **"Is this copy actually costing me time / allocations?"** -> `performance-testing`
  (boxing shows as `Allocated` under `[MemoryDiagnoser]`; a hot defensive copy shows
  in a trace).
- **"Did the JIT keep the copy?"** -> `framework-jit-optimization` /
  `[DisassemblyDiagnoser]`. The C# compiler may emit an IL copy that the JIT then
  elides, so IL presence is necessary but not sufficient for a runtime cost.
- Otherwise, read the IL (below).

## Workflow

1. **Build Release with a portable PDB.** Optimized IL is what ships; Debug IL has
   extra copies and spills. The PDB's sequence points are what map IL offsets back
   to `file:line`. (`touki` already emits embedded portable PDBs - see
   `<DebugType>embedded</DebugType>` in
   [touki/touki.csproj](../../../touki/touki.csproj).)
2. **Extract the method's IL.** Pick a tool from the table below. Disassemble the
   single method of interest, not the whole assembly.
3. **Match the copy patterns.** Scan the method body for the copy opcodes and the
   defensive-copy temp signature. The full catalog - every opcode, the
   `ldobj`/`stloc`/`ldloca`/`call` defensive sequence, `box`, `cpobj`, `ldfld` vs
   `ldflda` - is in
   [references/copy-opcodes.md](references/copy-opcodes.md).
4. **Map IL offset to source.** Use the PDB sequence points (most disassemblers can
   interleave source, e.g. `ilspycmd -il` with debug info, or read sequence points
   via `System.Reflection.Metadata`) to attribute each copy to a line - the same
   artifact-to-source drill the `performance-testing` skill does with `filtrace`.
5. **Report** the type copied, the mechanism (defensive / box / by-value), and the
   source line. Distinguish *intended* copies (a documented value transfer) from
   *unintended* ones; IL alone cannot, so use the source context.

## Tool options

| Tool | Good for | Note |
| ---- | -------- | ---- |
| `ilspycmd -il <dll>` ([ICSharpCode.ILSpyCmd](https://github.com/icsharpcode/ILSpy)) | One-method IL dump, scriptable | `dotnet tool install -g ilspycmd`; cross-platform |
| `ildasm` | Quick Windows dump | Ships with the .NET SDK / VS; Windows-centric |
| `Mono.Cecil` | Programmatic body walking, custom rules | Best when scripting a repeatable copy audit |
| `System.Reflection.Metadata` (`MetadataReader`, `MethodBodyBlock`) | In-proc, no extra dependency, owns the PDB sequence-point mapping | Most code; the right base for an automated pass |

For a repeatable, automated audit, `Mono.Cecil` or `System.Reflection.Metadata`
walking the method body and matching the opcode patterns from the catalog is the
durable choice; `ilspycmd` is the fastest manual spot-check.

## Constraints

- **Needs a build and a PDB.** Unlike an analyzer (source only), this requires
  compiled output. No PDB means no source-line mapping - opcodes only.
- **IL copy != runtime cost.** The JIT may elide an emitted copy. Confirm cost at
  the asm or runtime layer before claiming a perf impact.
- **Generics share IL.** A copy of `T` appears once in the shared body; the real
  cost depends on the instantiation. Note the open generic, then reason per
  value-type substitution.
- **Intent is lost in IL.** A by-value copy that is a deliberate transfer looks
  identical to an accidental one. Keep the source open to classify; this is the same
  "unintended vs intended" limit the `roslyn-analyzers` defensive-copy docs call out.

## Cross-skill

- `roslyn-analyzers` - the source-level, predictive side (TOUKI0002-0004). This
  skill confirms its predictions and covers the synthesized copies it cannot see.
- `framework-jit-optimization` - the next layer down (asm); whether the JIT kept the
  copy, and what to do about it on net481.
- `performance-testing` - whether the copy costs measurable time / allocation.
- `scratch-buffer-strategy` - many `[NonCopyable]` types are pooled buffers; this
  skill audits whether one is being copied by value.

## Disambiguation

"Find the copies" is ambiguous across layers. Route by artifact: **predict from
source, live** -> `roslyn-analyzers`; **read the compiler's emitted IL** -> this
skill; **read the JIT's machine code** -> `framework-jit-optimization`; **measure
the runtime cost** -> `performance-testing`. This skill never runs the code and
never measures time - it reads static bytecode.
