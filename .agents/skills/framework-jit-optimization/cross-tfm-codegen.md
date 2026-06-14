# Cross-TFM codegen: arithmetic, layout, allocation

Techniques that apply to **both** targets - they are about the C# compiler and the
shared codegen rules, not a specific RyuJIT version. The net481-vs-net10 split pages
(this skill's [unrolling.md](unrolling.md), [bcl-tradeoffs.md](bcl-tradeoffs.md),
[references/framework-span-performance.md](references/framework-span-performance.md))
and the net10-enablement page ([modern-net.md](modern-net.md)) sit on top of these
fundamentals.

Where a topic is already covered elsewhere this page only points at it - it does not
repeat it.

## 1. Arithmetic the JIT shapes for you - and when it can't

- **Division/modulo by a compile-time constant is already fast.** The JIT replaces
  `x / 10` (literal `10`) with a multiply-by-reciprocal + shift. The trap is a
  **runtime** divisor: `x / divisor` is a real `idiv` (20-90+ cycles, not
  pipelined). If the divisor is reused across many values, hoist the division out of
  the loop or precompute a reciprocal.
- **Power-of-two: type non-negative quantities `uint`/`nuint`.** For unsigned
  values the JIT lowers `x / 2^k` to `>> k` and `x % 2^k` to `& (2^k - 1)`. For
  **signed** values it must emit a sign-correction sequence (`x % 8` on a possibly
  negative `int` is *not* `x & 7`). Lengths, counts, indices, and hashes are
  non-negative by construction - typing them `uint`/`nuint` gets the single-
  instruction `/`, `%`, `>>` and documents the invariant. (For an open-addressed
  table or ring buffer, force a power-of-two capacity and write `index & (capacity -
  1)` yourself - far cheaper than `% capacity`.)
- **`Math.DivRem` for quotient + remainder** in one division instead of computing
  `x / y` and `x % y` separately. **`Math.BigMul`** for the full 64x64->128 (or
  32x32->64) product without manual hi/lo juggling. Where the repo polyfills these for
  the Framework target, prefer the helper over a hand-rolled equivalent.
- **`unchecked` hot numeric kernels** where overflow is impossible or intended to
  wrap; a `checked` context adds a `jo` per op. Do not disable checks where they
  catch real bugs.

## 2. Bit manipulation: use the intrinsic helper, not bit-twiddling

`System.Numerics.BitOperations` maps to dedicated instructions with a software
fallback: `PopCount` (`POPCNT`), `LeadingZeroCount`/`Log2` (`LZCNT`),
`TrailingZeroCount` (`TZCNT`), `RotateLeft`/`RotateRight` (the JIT also *recognizes*
the `(x << n) | (x >> (w-n))` idiom and folds it), `IsPow2`, `RoundUpToPowerOf2`.
Never hand-roll these. On `net10` it is in the BCL; on the Framework target it is
not - use the repo's polyfill if present rather than open-coding the bit tricks. The
branchless identities are still worth knowing because they feed bitset iteration:
`x & (x - 1)` clears the lowest set bit, `x & -x` isolates it.

## 3. Branchless: branchful is the cross-TFM-safe default

For an *unpredictable* condition on `net10`, a ternary may lower to `cmov` and a
per-element vector conditional to `Vector256.ConditionalSelect` (a branchless
clamp/select/ReLU). **But on `net481` the JIT frequently does not emit `cmov` for
`ushort`/`byte` stores** - see [antipatterns.md](antipatterns.md) - so a branchless
rewrite there guarantees a store every iteration and regresses on sparse data. The
branch predictor handles "almost never matches" extremely well. **Prefer the
branchful form**: it wins on `net481` and is not measurably worse on `net10`. Only
go branchless behind a `#if NET` guard with a measured win. A *predictable* branch
(error paths, `IsSupported` checks) is nearly free - leave it branched.

## 4. Memory layout (data layout, both TFMs)

- **Order struct fields largest-to-smallest.** A `struct` defaults to
  `LayoutKind.Sequential`, so declaration order is memory order and the runtime pads
  for alignment. `{ byte; long; int }` is 24 bytes; `{ long; int; byte }` is 16.
  Smaller hot structs mean more per 64-byte cache line and fewer misses. (A `class`
  is `LayoutKind.Auto` - the runtime packs it; you cannot and need not reorder.)
- **`readonly struct` + `readonly` members to avoid defensive copies.** Calling a
  non-`readonly` member on a `readonly` field, an `in` parameter, or a `static
  readonly` value forces the compiler to copy the whole struct first. A
  defensive-copy analyzer (and the post-build IL-copy-inspection skill) catches
  these; the fix is always to make the type and its members `readonly`.
- **AoS -> SoA when you process one field across many elements.** An array of
  `struct Particle { float X, Y, Z, ... }` strides `X` by `sizeof(Particle)` and
  wastes bandwidth pulling whole structs through cache; separate `float[] X, Y, Z`
  is contiguous (16 `X`s per line) and is the layout that lets the `net10` vectorizer
  ([modern-net.md](modern-net.md)) touch it at all. A structural decision - make it
  before writing the kernel.
- **False sharing** in parallel code: two threads writing two different variables
  that share a 64-byte line ping-pong the line between cores; the symptom is a
  parallel loop that scales *negatively*. Pad genuinely-contended hot counters onto
  their own line (`[StructLayout(LayoutKind.Explicit, Size = 128)]`). Pad only the
  contended field - padding everything wastes cache.

## 5. Zero-allocation static data

- **`static ReadOnlySpan<byte> Table => [ ... ];`** - Roslyn stores the bytes as a
  blob in the assembly and points the span at them: **no array allocation, ever**.
  The right way to ship immutable byte tables (parse maps, transition tables). It is
  a compiler feature, so it works on **both** targets; reliable for
  `byte`/`sbyte`/`bool` - verify the codegen (the performance-testing skill's
  codegen-reading page) before relying on it for wider element types.
- **`[InlineArray(N)]`** gives a fixed-size inline buffer inside a struct with no
  separate array and no `unsafe fixed`. It is a .NET 8+ runtime feature - available
  on `net10`, **not** on the Framework target, so `#if NET`-guard it or keep it off
  the shared path.

## 6. `const` vs `static readonly`, and `switch`

- **`const` folds; `static readonly` is a field load.** A `const` participates in
  constant propagation - it feeds the reciprocal-division and dead-branch folding in
  section 1 and [modern-net.md](modern-net.md). A `static readonly` is treated as an
  invariant after init but does not constant-fold an expression through it. Hot magic
  numbers should be `const`.
- A **dense integer `switch`** lowers to a jump table (one indirect branch); a
  sparse one to a branch tree. Keeping case values dense helps the table form and
  beats an `if/else` ladder.

## 7. Hot-path allocation anti-patterns

Each of these allocates on a path that runs thousands of times; all apply to both
targets. A repo's own utility types often exist to avoid several of these - a
stack-seeded value string builder, pooled list types, and enum-flag helpers that
sidestep `Enum.HasFlag` boxing. Prefer them on the hot path.

- **LINQ on a hot path** allocates enumerators and closures and blocks BCE,
  devirtualization, and inlining. Fine for clarity off the hot path; rewrite to loops
  or span APIs in kernels.
- **Capturing lambdas** allocate a display-class object per creation - per iteration
  in a loop. Use `static` lambdas and pass state explicitly, or the struct-generic
  pattern ([modern-net.md](modern-net.md)).
- **`async` state machines** can allocate, and awaiting already-completed work has
  builder overhead. Use `ValueTask`/`ValueTask<T>` for hot, usually-synchronous
  paths; do not make trivially-synchronous methods `async`.
- **`params object[]`** allocates per call. The `params ReadOnlySpan<T>` overload
  (C# 13) is allocation-free - prefer it.
- **`yield` iterators** allocate a state machine and add `MoveNext` dispatch per
  element. For hot internal sequences return a struct enumerator or fill a `Span`.
- **`+=` string building in a loop** is quadratic allocation. Use a stack-seeded
  pooled string builder, or `string.Create` with a span-fill callback when the final
  length is known.
- **Boxing in disguise** - a struct called through a non-generic interface, a struct
  in a non-generic collection, or `Enum.HasFlag` reached through generic code (use
  the repo's enum-flag helpers instead) - each box is a heap allocation. Watch the
  `Allocated` column (the performance-testing skill) and check for boxing in the IL
  (the IL-copy-inspection skill).

## See also

- [modern-net.md](modern-net.md) - the `net10`-only enablement (SIMD, intrinsics,
  struct-generic, escape analysis) built on these fundamentals.
- The performance-testing skill's `reading-codegen.md` - confirm the JIT emitted the
  reciprocal multiply / shift / jump table / blob load you expected.
