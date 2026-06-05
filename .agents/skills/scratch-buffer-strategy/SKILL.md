---
description: Choose how a hot path gets a short-lived scratch buffer - zeroed `stackalloc`, `[SkipLocalsInit]` + `stackalloc`, `BufferScope<T>` (stack with pool fallback), or an `ArrayPool<T>.Shared` rental - and apply the net481/net10 size crossovers. Use when designing or reviewing a performance-sensitive path that needs a temporary buffer, when deciding "should I rent or stackalloc?", when weighing `[SkipLocalsInit]`, or when evaluating buffer/allocation cost. Defers the backing measurements and full reasoning to the bundled references/arraypool-performance.md.
license: MIT
metadata:
    github-path: skills/scratch-buffer-strategy
    github-pinned: v0.2.0
    github-ref: refs/tags/v0.2.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: ae13c55e430ac3dcbf4a2698130abc7cd78762b1
    portability: semi-portable
name: scratch-buffer-strategy
---
# Scratch buffer strategy (net481 + net10)

Pick the cheapest correct way to get a short-lived scratch buffer on a hot path.
This skill is the compact decision aid; the measured numbers, the per-call
ArrayPool autopsy, the inlining traps, and the references live in the bundled
[references/arraypool-performance.md](references/arraypool-performance.md). Read
that doc before quoting a number or making a non-obvious call.

A multi-targeted library runs on **net481** (older RyuJIT: unvectorized `memset`,
no tiered JIT/PGO, weaker inliner) and **net10**. Buffer costs differ enough
between them that every decision must hold on both. Name the JIT in any writeup
(the JIT-naming rule).

## Four options, ranked for the common case

1. **`[SkipLocalsInit]` + `stackalloc`** - fastest (~1-2 ns, both TFMs).
   Requires: fixed/bounded size, stack-safe, and **every read slot written
   first**. Put the attribute on the method that physically contains the
   `stackalloc`.
2. **Zeroed `stackalloc`** - safe default. Cost scales with byte count;
   net481 zeroing is ~3.6x net10 for a **compile-time-constant** size (for a
   runtime-variable size both TFMs zero at ~the same rate - see the doc).
3. **`BufferScope<T>(stackalloc T[N], minimumLength)`** - variable/unbounded
   size that is usually small. Stays on the stack for the common case, rents
   from `ArrayPool` only on overflow. Wrapper overhead ~1 ns net481 / ~0.3 ns
   net10. Forwards the caller's `[SkipLocalsInit]` to the stack buffer.
   (`BufferScope<T>` is a stack-with-pool-fallback wrapper; Touki ships one.)
4. **`ArrayPool<T>.Shared` Rent/Return** - large, unbounded, recursive, or must
   escape the frame. Has a fixed per-call floor that warmup never removes
   (~10 ns/op net481, ~4 ns/op net10). Rent **once** and reuse across the loop.

## Decision tree

```text
Short-lived scratch buffer on a hot path?
|
+- Fixed/small (one frame, few KB), not recursive/looped?
|   +- Write every slot before reading?  --> [SkipLocalsInit] + stackalloc
|   +- Cannot guarantee that?            --> zeroed stackalloc (or zero only
|                                            the prefix you read)
|
+- Usually small but occasionally large (variable size)?
|   --> BufferScope<T>(stackalloc T[N], minLen); put [SkipLocalsInit] on the
|       CALLING method to drop the stack-buffer zeroing
|
+- Large / unbounded / recursive / must escape frame?
    --> ArrayPool<T>.Shared, rented once and reused
```

## Crossover thresholds (zeroed stackalloc vs a rental)

For a **runtime-variable** size, zeroing a `stackalloc` is cheaper than renting
below these sizes; above them the rental's flat floor wins (only if you can use
the memory uninitialized):

| Rental kind | net481 | net10 |
| --- | --: | --: |
| Warm TLS hit (first rental) | ~1.3 KB | ~190 B |
| Locked (second same-bucket rental) | ~3 KB | ~1.8 KB |

A **compile-time-constant** size zeroes far cheaper on net10, pushing its
crossover higher. If you would have to `Clear()` the rented array, add that cost
back to the rental side - the crossover then exceeds any sane stack size, so
just stack-allocate.

## Load-bearing facts (do not re-derive)

- **net481 honors `[SkipLocalsInit]`.** The `localsinit`-flag suppression works
  on the desktop CLR exactly as on CoreCLR; the attribute *type* is
  source-generated downlevel by PolySharp. A 4 KB clear drops from ~53 ns to
  ~1.7 ns on net481.
- **Only byte count drives zeroing cost** - struct arrays zero exactly like
  primitive arrays of the same size.
- **A warm pool is not a free pool.** Rent/Return pays bucket-index math, a
  one-deep TLS cache (two same-bucket rentals collide into the locked stack),
  and a separate pool per element type - on every call, net481 ~2.5x worse.
- **`[SkipLocalsInit]` hands you uninitialized memory.** Use it only where a
  profile shows the clear is hot, every read slot is provably written first, and
  no unwritten bytes can escape (info-disclosure risk). The JIT inliner does not
  spread the attribute; net481's weaker inliner can move where zeroing lands, so
  verify on both TFMs.
- **`Unsafe.SkipInit` does NOT suppress the zeroing.** Common mistake: assuming
  `Unsafe.SkipInit(out x)` makes a stack buffer un-zeroed. It compiles to a bare
  `ret` and only satisfies C# definite-assignment so you can read an unassigned
  local; the runtime still zeroes via the method's `.locals init` flag, which
  only `[SkipLocalsInit]` (or `[module: SkipLocalsInit]`) removes. A "fixed
  buffer left uninitialized with `Unsafe.SkipInit`" but *without*
  `[SkipLocalsInit]` pays full zeroing. See
  [references/arraypool-performance.md](references/arraypool-performance.md)
  section 8.

## Stack-safety guardrail

`stackalloc` only belongs on the stack at all if it is bounded: keep a single
frame's `stackalloc` to a few KB, and never `stackalloc` on a recursive or
unbounded-loop path (CA2014). Default Windows managed stack is 1 MB, but library
code may run on a 256 KB partial-trust thread. Past those limits, rent once and
reuse instead.

## Related

- [references/arraypool-performance.md](references/arraypool-performance.md) -
  the full measured tables, the ArrayPool per-call autopsy, the `BufferScope`
  overhead numbers, the inlining trap, and references. **This is the backing
  data; cite it.**
- The repository's performance-testing and framework-JIT-optimization skills
  (author/run the BenchmarkDotNet benchmarks that produced these numbers, and
  net481 loop tuning once a buffer choice is made) - a consuming repository
  wires the concrete cross-references in its overlay.
