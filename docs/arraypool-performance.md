# ArrayPool vs stack scratch: performance on net481 and net10

When a hot path needs a short-lived scratch buffer, there are three ways to get
one: `stackalloc` (zero-initialized by default), `ArrayPool<T>.Shared`
rent/return, or a `stackalloc`/fixed buffer with the zeroing suppressed. The
right choice depends on the buffer's size, lifetime, and - critically - which
target framework's JIT is running. This document captures the measured costs and
a decision procedure for choosing between them.

All numbers below come from the BenchmarkDotNet projects in `touki.perf/`
(listed in section 7), run in Release on this repo's dev machine against
**.NET Framework 4.8.1 RyuJIT** and **modern .NET RyuJIT** (.NET 10). Per the
repo's JIT-naming rule, every claim names the JIT, because the two behave very
differently here.

For the compact decision aid (decision tree + crossover thresholds without the
backing tables), see the
[`scratch-buffer-strategy`](../.agents/skills/scratch-buffer-strategy/SKILL.md)
skill. This document is the data it cites.

---

## 1. The headline result

For a fixed-size scratch buffer whose every slot is written before it is read,
**`[SkipLocalsInit]` + `stackalloc` is the fastest option on both target
frameworks** - faster than an `ArrayPool` rental and far faster than a
zero-initialized `stackalloc`. The pool only wins when the buffer is large,
unbounded, or must escape the stack frame.

The single most important and most counter-intuitive fact:

> **.NET Framework 4.8.1 RyuJIT honors `[SkipLocalsInit]`.** Suppressing the
> `localsinit` flag removes the stack zeroing on net481 just as it does on
> modern .NET. A 4 KB stack buffer drops from ~53 ns to ~1.7 ns on net481.

This corrects the common assumption that Framework "always zeroes stackalloc".
It does zero by default, but the absence of the `localsinit` flag is respected by
the desktop runtime, so the zeroing is fully suppressible.

---

## 2. The cost of zeroing

`stackalloc` without `[SkipLocalsInit]` clears the buffer on every call. That
clear is a `memset` whose cost scales with the **byte size** of the buffer.

### 2.1 Zeroing cost by size

| Buffer | net481 RyuJIT | net10 RyuJIT |
|---|--:|--:|
| `byte[256]` (256 B) | 7.0 ns | 1.7 ns |
| `byte[1024]` (1 KB) | 14.0 ns | 5.8 ns |
| `byte[4096]` (4 KB) | 53.4 ns | 14.5 ns |

Two things to read off this:

- The cost is roughly linear in bytes once past the smallest sizes.
- **net481 zeroing is ~3.6x more expensive than net10 zeroing** for the same
  buffer (53 ns vs 15 ns at 4 KB). The desktop `memset` path is not vectorized
  the way the modern runtime's is. This is why a zeroing cost that is a minor
  line item on net10 can become the dominant per-call cost on net481.

This ~3.6x gap holds for a **compile-time-constant** size, which is what these
measurements use. For a runtime-variable `stackalloc byte[n]` net10 loses the
vectorized clear and zeroes at nearly the same rate as net481 - see the note in
section 3.2.

### 2.2 Struct arrays zero exactly like primitive arrays

A frequent worry is that an array of structs costs more to clear than an array
of primitives. It does not - zeroing is byte-wise and ignores the element type.
At an equal 4 KB:

| Buffer | Bytes | net481 RyuJIT | net10 RyuJIT |
|---|--:|--:|--:|
| `byte[4096]` | 4096 | 53.4 ns | 14.5 ns |
| `int[1024]` | 4096 | 51.3 ns | 14.6 ns |
| `Range16[256]` (4x `int`) | 4096 | 52.4 ns | 15.0 ns |

The three are within noise of each other on both TFMs. **Only the byte count
matters.** Do not restructure a struct buffer into a primitive one hoping to
zero faster - reduce the byte count or suppress the zeroing instead.

---

## 3. The cost of an ArrayPool rental

`ArrayPool<T>.Shared` hands back **uninitialized** memory, so it never pays the
zeroing tax. But Rent/Return are not free, and - this is the key gotcha - the
overhead is **per-call bookkeeping that warmup cannot eliminate**.

`ArrayPoolSeedRentPerf` measures the exact shape the extglob engine uses: five
buffers (a 28-byte frame struct x32, a 16-byte range struct x128, that range
struct x32 twice, and an `int[99]`), rented and returned together, ~4.3 KB
aggregate, with the pool pre-warmed 64x in `[GlobalSetup]`.

| Operation | net481 RyuJIT | net10 RyuJIT |
|---|--:|--:|
| 5x Rent + 5x Return (`PoolRent`) | 104.3 ns | 40.6 ns |
| equivalent zeroed `stackalloc` | 55.2 ns | 20.9 ns |
| **pool penalty over stackalloc** | **+49 ns** | **+20 ns** |

The pool is **slower than even a zeroing `stackalloc`** on both TFMs for this
size. Roughly 10 ns per Rent/Return pair on net481, 4 ns on net10.

### 3.1 Why warmup does not hide it

The pool is fully warm - `[GlobalSetup]` and BenchmarkDotNet's own warmup run it
hundreds of times - yet the cost persists, because it is steady-state work done
on **every** call:

1. **Bucket-index math per call.** `Shared` is
   `TlsOverPerCoreLockedStacksArrayPool<T>`. Each Rent computes a bucket index
   (a log2) from the requested length; each Return recomputes it from
   `array.Length`. Ten times for five buffers.
2. **The thread-local cache holds one array per bucket.** If two rentals hit the
   same bucket - in the engine, `work` and `rest` are both `ProgramRange[32]` -
   the second misses the TLS fast path and falls through to the per-core
   **locked** stack (an interlocked/`Monitor` acquire). So does the second
   return.
3. **Separate pools per element type.** `Frame`, `ProgramRange`, and `int` are
   three independent `ArrayPool<T>.Shared` instances with three separate per-core
   stacks; no sharing, three sets of the above.
4. **net481 makes all of it ~2.5x worse.** The pool internals are not inlined
   (no tiered JIT, no dynamic PGO), the locked-stack path is heavier, and the
   bucket math is not folded into the caller. That is why the penalty is +49 ns
   on net481 versus +20 ns on net10.

The lesson: **a warm pool is not a free pool.** Rent/Return has a fixed per-call
floor that no amount of warmup removes.

### 3.2 Where zeroing crosses the rental floor

The previous tables compare a single fixed size. The more actionable question is
"above what size does zeroing a `stackalloc` cost more than renting?" Because
zeroing scales with the byte count while a rental is a flat per-call floor, there
is a crossover size. `ArrayPoolCrossoverPerf` sweeps `stackalloc byte[N]` against
a rental for `N` from 64 to 8192 bytes, separating the two rental floors:

- **TLS hit** (`RentTls`): the first rental of a bucket, served from the
  one-deep thread-local cache.
- **Locked** (`RentLocked`): a second same-bucket rental taken while the first is
  still checked out, so it falls to the per-core locked stack. Its marginal cost
  is `RentLocked - RentTls`.

Measured means (ns); the rental rows are essentially flat across size, the
`ZeroStack` row rises with it:

| Bytes | net481 ZeroStack | net481 RentTls | net481 RentLocked | net10 ZeroStack | net10 RentTls | net10 RentLocked |
|--:|--:|--:|--:|--:|--:|--:|
| 64   |   2.1 |  21.4 | 43.3 |   2.3 | 4.9 | 30.2 |
| 128  |   3.3 |  21.3 | 42.9 |   3.7 | 4.9 | 31.1 |
| 256  |   5.7 |  20.9 | 42.2 |   6.6 | 5.0 | 31.2 |
| 512  |   7.3 |  21.1 | 41.8 |  12.4 | 4.9 | 30.9 |
| 1024 |  16.0 |  20.7 | 41.6 |  22.4 | 5.0 | 30.9 |
| 2048 |  30.9 |  20.8 | 41.6 |  33.9 | 4.9 | 31.7 |
| 4096 |  53.8 |  20.7 | 41.9 |  56.9 | 4.8 | 30.8 |
| 8192 | 100.2 |  20.5 | 41.0 | 103.8 | 5.0 | 31.2 |

> **Why net10's `ZeroStack` here is ~4x its section 2.1 figure.** This sweep uses
> a runtime-variable `stackalloc byte[size]` (the size is a benchmark parameter),
> which is the realistic shape of the "should I rent?" question. RyuJIT emits its
> fast vectorized clear only for a **compile-time-constant** size, so net10's
> variable-size zeroing (~57 ns at 4 KB) is roughly 4x its constant-size cost
> (~14.5 ns in section 2.1) and lands right on top of net481. For a
> runtime-determined size the two TFMs zero at nearly the same rate; net10's
> ~3.6x advantage applies only to constant sizes. A constant-size buffer
> therefore pushes net10's crossover well above the figures below.

The rental floors are roughly constant - net481 ~21 ns (TLS) / ~42 ns (locked),
net10 ~5 ns (TLS) / ~31 ns (locked) - confirming a rental does not scale with
size, only the zeroing does. The crossover sizes (where `ZeroStack` overtakes the
flat rental line):

| Rental kind | net481 crossover | net10 crossover |
|---|--:|--:|
| TLS hit (first rental) | ~1.3 KB | ~190 bytes |
| Locked (second rental) | ~3 KB | ~1.8 KB |

How to read it:

- **Below the crossover, the zeroed `stackalloc` is cheaper** than even a warm
  TLS rental - so for small fixed buffers, do not rent; stack-allocate and (if
  the write-before-read invariant holds) add `[SkipLocalsInit]` to drop the
  zeroing entirely.
- **net10's TLS rental is remarkably cheap (~5 ns)**, so on modern .NET the bar
  for "rent instead of zero" is low - anything past ~190 bytes that you would
  otherwise zero is a candidate. **net481's rental is ~4x that floor (~21 ns)**,
  so on Framework you have to be allocating roughly **1.3 KB** before a rental
  beats zeroing, and **3 KB** before it beats zeroing if the rental collides on a
  busy bucket.
- **The locked (second-rental) floor is the realistic one for hot, concurrent
  paths** where the one-deep TLS slot is frequently already drained. Against that
  floor zeroing wins up to ~1.8 KB on net10 and ~3 KB on net481.
- These crossovers assume the rental memory is **uninitialized-safe** (you write
  every slot before reading). If you would have to `Clear()` the rented array,
  add the zeroing cost back to the rental side and the crossover moves much
  higher - usually past any sane stack size, i.e. just stack-allocate.

---

## 4. Suppressing the zeroing instead

If the buffer is a fixed, stack-friendly size and the code writes every slot
before reading it, the best move is to keep it on the stack and turn the zeroing
off. There are two ways; one is clearly better.

### 4.1 `[SkipLocalsInit]` on the method (preferred)

Annotate the method that contains the `stackalloc` with
`[System.Runtime.CompilerServices.SkipLocalsInit]`. The C# compiler omits the
`localsinit` flag, and both runtimes skip the clear. Requires
`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in the project (already set in
`touki.csproj`, and the attribute is already used throughout the library).

`[SkipLocalsInit]` is a **compiler + CLR** mechanism, not a BCL feature. Per the
[C# attribute reference](https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/general#skiplocalsinit-attribute),
the attribute "prevents the compiler from setting the `.locals init` flag when
emitting to metadata," and "the `.locals init` flag causes the CLR to initialize
all of the local variables." The desktop .NET Framework CLR honors the absence
of that flag exactly as CoreCLR does - which is why the net481 column above
shows the zeroing disappearing.

A common source of confusion: the
[`SkipLocalsInitAttribute` API page](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.skiplocalsinitattribute)
lists "Applies to: .NET 5-11" and does **not** list .NET Framework. That only
means the attribute *type* does not ship in the Framework BCL - it says nothing
about whether the runtime honors the flag. Because the attribute is just a
marker the compiler reads, you supply the type yourself downlevel; this repo
source-generates it with **PolySharp** (`PolySharpIncludeRuntimeSupportedAttributes`
in `touki.csproj`), so `[SkipLocalsInit]` compiles and takes effect on net481.

There is a real caveat, but it does **not** apply to `stackalloc`. The attribute
only skips zero-init that is otherwise redundant; the JIT still zeroes (and
GC-tracks) any local that contains **managed references**, for GC safety - see
the GC-reference note in
[Unsafe code best practices](https://learn.microsoft.com/dotnet/standard/unsafe-code/best-practices).
But `stackalloc T[]` requires `T` to be an
[unmanaged type](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/unmanaged-types),
and `fixed` buffers are likewise unmanaged, so a stack scratch buffer has no
managed references and the zeroing is fully elided. The caveat would only bite
if you applied `[SkipLocalsInit]` expecting it to skip a plain local (or a
struct) that holds object references - that is still zeroed.

| 4 KB scratch | net481 RyuJIT | net10 RyuJIT |
|---|--:|--:|
| zeroed `stackalloc` | 53.0 ns | 14.8 ns |
| `[SkipLocalsInit]` `stackalloc` | **1.7 ns** | **1.3 ns** |

The zeroing essentially vanishes on both TFMs - a ~30x win on net481, ~11x on
net10. This is the cleanest option: one attribute, one code path, no pool
bookkeeping, allocation-free.

### 4.2 Fixed-buffer ref struct + `Unsafe.SkipInit` (special cases only)

The pattern the BCL sometimes uses: a struct carrying an inline `fixed` buffer,
left uninitialized with `Unsafe.SkipInit(out scratch)`, exposing a scoped `Span`
via an `[UnscopedRef]` property. `Unsafe.SkipInit` ships in the `System.Runtime`
/ netstandard `Unsafe` surface and **is available on net481**.

| 4 KB scratch | net481 RyuJIT | net10 RyuJIT |
|---|--:|--:|
| `[SkipLocalsInit]` `stackalloc` | 1.7 ns | 1.3 ns |
| fixed-buffer + `Unsafe.SkipInit` | 16.3 ns | 26.3 ns |

**This pattern is slower than plain `[SkipLocalsInit]` on both TFMs, and on net10
it is slower than just zeroing.** The cost is the `Unsafe.AsPointer` + `Span`
construction and the fixed-buffer addressing the JIT cannot fold as well as a
raw `stackalloc`. Reach for it only when you genuinely need the scratch to live
in a struct that is passed around (so a bare `stackalloc` local will not do) -
not as a default. For a plain local buffer, `[SkipLocalsInit]` + `stackalloc`
wins.

### 4.3 `BufferScope<T>`: stack buffer with a pool fallback

The size sweep in section 3.2 assumes you know the size up front. Often you do
not: the common size is small and stack-friendly, but a rare large input must
still be handled without overflowing the stack. `Touki.Buffers.BufferScope<T>`
is built for exactly this - **start on a `stackalloc` buffer, transparently fall
back to an `ArrayPool<T>` rental only when the requested capacity exceeds it**:

```csharp
[SkipLocalsInit]
static int Process(ReadOnlySpan<char> input)
{
    // Common case stays on the stack; large input rents from the pool.
    using BufferScope<char> buffer = new(stackalloc char[256], input.Length);
    // ... use buffer as a Span<char> ...
}
```

`BufferScope<T>` is a `ref struct` that wraps the caller's `stackalloc` span and
only rents when `minimumLength` overflows it; `Dispose` returns the rental (and
is a no-op on the stack-only path). Crucially, **the `stackalloc` lives in the
caller**, so the caller's `[SkipLocalsInit]` controls whether that buffer is
zeroed - the wrapper never clears stack memory itself.

`BufferScopeOverheadPerf` measures the wrapper against the hand-written
equivalent on each path. The 256-byte stack buffer either satisfies the request
(`StackOnly`) or is overflowed by a 1024-byte request that forces a rental
(`Rent`):

| Path | net481 RyuJIT | net10 RyuJIT |
|---|--:|--:|
| direct `stackalloc`, `[SkipLocalsInit]` | 7.7 ns | 0.90 ns |
| `BufferScope` stack-only, `[SkipLocalsInit]` | 8.8 ns | 1.23 ns |
| `BufferScope` stack-only, **zeroed** (no SkipLocalsInit) | 11.6 ns | 2.26 ns |
| direct pool Rent/Return | 25.3 ns | 4.99 ns |
| `BufferScope` grow-to-rental | 28.6 ns | 5.20 ns |

Two things to take from this:

- **The wrapper overhead is small** - about +1 ns on net481 / +0.3 ns on net10
  for the stack-only path, and +3 ns / +0.2 ns for the rental path. You are
  paying a constructor and a `Dispose` branch on top of the underlying
  stackalloc-or-rent cost, nothing more.
- **`BufferScope` is `[SkipLocalsInit]`-friendly.** Compare the stack-only rows:
  with `[SkipLocalsInit]` on the calling method the scope costs 8.8 ns (net481)
  / 1.23 ns (net10); without it, the same scope pays the 256-byte zeroing and
  rises to 11.6 ns / 2.26 ns. The wrapper passes the localsinit decision
  straight through to the caller's `stackalloc`, so you keep the zeroing-
  suppression win on the common path **and** get a safe pool fallback for the
  rare large input. This is the recommended shape whenever the size is variable
  or unbounded but usually small.

### 4.4 Risks of skipping init (and the inlining trap)

`[SkipLocalsInit]` is `unsafe` for a reason: it hands you **uninitialized
memory**. The
[best-practices guidance](https://learn.microsoft.com/dotnet/standard/unsafe-code/best-practices)
is to use it only where a profile shows the zeroing is hot. Specific risks:

- **You must write before you read.** With the `localsinit` flag gone, a
  `stackalloc` (or any local) contains whatever was previously on the stack.
  Reading a slot you have not assigned yields a garbage value - and worse, it is
  *non-deterministic*: it passes in tests where the stack happened to be zero and
  fails in production. Only apply `[SkipLocalsInit]` to a buffer whose every read
  slot is provably written first (e.g. you fill `[0..count]` and only ever read
  `[0..count]`).
- **It is a potential information-disclosure vector.** Uninitialized stack memory
  can contain remnants of previous frames (other callers' data). If any unwritten
  portion of the buffer can be observed - returned, copied out, hashed, or
  serialized - you may leak unrelated data. The `<AllowUnsafeBlocks>` requirement
  exists precisely to flag this.
- **The attribute's scope follows inlining, which you do not fully control.**
  `[SkipLocalsInit]` applied to a method also covers its nested local functions
  and lambdas, but it does **not** transfer across a normal call boundary: a
  helper you call keeps its own localsinit setting. The subtle part is the
  interaction with the JIT inliner:
  - If method `A` is `[SkipLocalsInit]` and the JIT **inlines** a non-attributed
    helper `B` into `A`, `B`'s `stackalloc`/locals do **not** retroactively
    become skip-init - the localsinit decision is fixed by the compiler per
    method at IL-emit time, before the JIT inlines. So you cannot rely on
    inlining to "spread" the attribute. Put `[SkipLocalsInit]` on the method that
    physically contains the `stackalloc`.
  - Conversely, inlining can move where the zeroing *appears* to happen. A tiny
    `stackalloc` helper that is **not** `[NoInlining]` may be inlined into a
    caller, and if that caller is also hot the zeroing folds into the caller's
    prologue. This is why the benchmarks here force `[NoInlining]` on the touch
    helpers: to measure the buffer cost in isolation rather than have the JIT
    relocate or eliminate it. In real code the opposite is true - **let the small
    helper inline**, and put `[SkipLocalsInit]` on whichever method ends up
    owning the `stackalloc` after inlining (the one with the attribute, since the
    compiler honored it there).
  - On net481 the inliner is weaker (no tiered JIT, no dynamic PGO), so a helper
    that inlines on net10 may stay a real call on net481, changing where the
    zeroing lands and how much the surrounding code can be folded. Always verify
    the win on **both** TFMs rather than assuming the net10 inlining shape
    carries over.

The safe default remains: prefer plain zeroed `stackalloc` (or `BufferScope`
without `[SkipLocalsInit]`); reach for `[SkipLocalsInit]` only when a profile
shows the clear is hot, the write-before-read invariant is guaranteed, and no
unwritten bytes can escape.

---

## 5. Is the buffer stack-safe? Sizing guidance

Suppressing the zeroing only makes sense if the buffer belongs on the stack at
all. The constraint is the thread's stack budget. On both .NET Framework and
modern .NET on Windows the default managed thread stack is **1 MB** (set in the
executable's PE header; the
[`Thread` constructor docs](https://learn.microsoft.com/dotnet/api/system.threading.thread.-ctor)
state "the default stack size (1 megabyte)" and warn against raising it). But
library code may run on threads with smaller stacks: under partial trust on
.NET Framework the minimum is **256 KB**
([same docs](https://learn.microsoft.com/dotnet/api/system.threading.thread.-ctor)),
and some hosts constrain it further. The C# reference for
[`stackalloc`](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/stackalloc)
adds two rules of its own: cap the allocation at a conservative limit (its
example uses 1024 bytes) and **never `stackalloc` inside a loop** (analyzer
[CA2014](https://learn.microsoft.com/dotnet/core/compatibility/code-analysis/5.0/ca2014-stackalloc-in-loops)).
So the working rule is **keep a single frame's `stackalloc` to a few KB and
never put a `stackalloc` on a recursive or unbounded-loop path.**

Apply this to the extglob engine seed (`RunEngine`):

| Buffer | Element size | Count | Bytes |
|---|--:|--:|--:|
| `Frame[]` | 28 B | 32 | 896 |
| `ProgramRange[]` (arena) | 16 B | 128 | 2048 |
| `ProgramRange[]` (work) | 16 B | 32 | 512 |
| `ProgramRange[]` (rest) | 16 B | 32 | 512 |
| `int[]` (key) | 4 B | 99 | 396 |
| **Total** | | | **4364 (~4.3 KB)** |

This is safe to keep on the stack because:

- **It is a single frame, ~4.3 KB.** Well under any reasonable per-call budget.
- **It is not on a recursive path.** `RunEngine` allocates the seed once.
  The negation handler's bounded re-entry goes through `RunEngineCore`, which
  reuses caller-supplied probe buffers and does **not** `stackalloc` again, so
  the 4.3 KB never multiplies with recursion depth.
- **Every slot is written before it is read** (the work/rest/key buffers are
  seeded by `CopyTo` and the per-production builders; frames/arena are written
  before use and spill to `ArrayPool` only when an adversarial input outgrows
  the seed). So it does not depend on zero-init and is a correct candidate for
  `[SkipLocalsInit]`.

Had the aggregate been tens of KB, or had the buffer sat on the recursive
re-entry path, the answer would flip to a pool rental (rented once and reused
across the recursion) despite the per-call overhead, to avoid stack overflow.

---

## 6. Decision procedure

```
Need a short-lived scratch buffer on a hot path?
│
├─ Is the size fixed and small (single frame ≲ a few KB),
│  and NOT on a recursive / unbounded-loop path?
│  │
│  ├─ YES ──> stackalloc.
│  │         Does the code write every slot before reading it?
│  │         ├─ YES ──> add [SkipLocalsInit]. (best: ~1-2 ns, both TFMs)
│  │         └─ NO  ──> plain zeroed stackalloc, OR zero only the
│  │                    prefix you read. (net481 zeroing ~3.6x net10)
│  │
│  ├─ Usually small but occasionally large (variable / unbounded
│  │  size, common case stack-friendly)? ──>
│  │            BufferScope<T>(stackalloc T[N], minimumLength):
│  │            stays on the stack for the common case, rents from the
│  │            pool only when it overflows. Wrapper overhead ~1 ns
│  │            net481 / ~0.3 ns net10. Put [SkipLocalsInit] on the
│  │            CALLING method to drop the stack-buffer zeroing; the
│  │            scope passes it through.
│  │
│  └─ NO (large, unbounded, recursive, or must escape the frame) ──>
│            ArrayPool<T>.Shared, rented ONCE and reused across the
│            loop/recursion. Accept the per-call Rent/Return floor
│            (~10 ns/op net481, ~4 ns/op net10); minimize the number
│            of distinct rentals and avoid two same-bucket rentals
│            colliding in the TLS cache.
│
└─ Need the scratch to live inside a struct that is passed around?
          fixed-buffer + Unsafe.SkipInit (available on net481), but
          know it is slower than a bare [SkipLocalsInit] stackalloc -
          use only when a local stackalloc genuinely will not fit the
          design.
```

Key rules of thumb:

- **Measure the zeroing before assuming it is the cost.** Sampling profilers
  attribute stack zeroing to `System.Buffer.ZeroMemoryInternal` (or `memset`);
  if that frame is hot, a `[SkipLocalsInit]` experiment is the first thing to
  try. But scope the profile to the measured workload - warmup/JIT dilute the
  percentage and a tiny hot frame can be over-attributed by the sampler.
- **net481 amplifies both costs.** Constant-size zeroing is ~3.6x and pool
  overhead ~2.5x more expensive than net10. A buffer strategy that looks free on
  net10 can dominate on Framework; always check both TFMs.
- **Element type does not change zeroing cost** - only byte count does.
- **Know the crossover size before choosing rent vs zero** (section 3.2): for a
  runtime-variable size, a zeroed `stackalloc` is cheaper than a rental below
  roughly **190 bytes on net10 / 1.3 KB on net481** against a warm TLS rental,
  and below roughly **1.8 KB on net10 / 3 KB on net481** against a contended
  (locked) rental. Above those sizes a rental's flat floor wins - provided you
  can use the uninitialized memory without clearing it. A compile-time-constant
  size zeroes far cheaper on net10, pushing its crossover higher.
- **A pool rental is not free even when warm.** Prefer suppressing the zeroing on
  a stack buffer over renting, whenever the size and lifetime allow it.
- **For variable sizes, reach for `BufferScope<T>` instead of choosing up front.**
  It stays on the stack for the common case and rents only when overflowed, for
  a ~1 ns (net481) / ~0.3 ns (net10) wrapper cost, and forwards the calling
  method's `[SkipLocalsInit]` to the stack buffer.
- **`[SkipLocalsInit]` hands you uninitialized memory - use it carefully.** Only
  where a profile shows the clear is hot, every read slot is written first, and
  no unwritten bytes can escape. Place it on the method that physically holds the
  `stackalloc`; the JIT inliner will not spread it for you, and net481's weaker
  inliner can change where the zeroing lands, so verify on both TFMs.

---

## 7. References

- [`SkipLocalsInit` attribute - C# attribute reference](https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/general#skiplocalsinit-attribute) - the compiler/CLR `.locals init` mechanism.
- [`SkipLocalsInitAttribute` class](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.skiplocalsinitattribute) - the "Applies to: .NET 5-11" type-availability note (polyfilled downlevel by PolySharp).
- [Unsafe code best practices](https://learn.microsoft.com/dotnet/standard/unsafe-code/best-practices) - `[SkipLocalsInit]`/`Unsafe.SkipInit` guidance and the GC-reference caveat.
- [`stackalloc` expression](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/stackalloc) - unmanaged-type requirement, undefined initial contents, sizing and loop rules.
- [CA2014: Do not use `stackalloc` in loops](https://learn.microsoft.com/dotnet/core/compatibility/code-analysis/5.0/ca2014-stackalloc-in-loops).
- [`Thread` constructor](https://learn.microsoft.com/dotnet/api/system.threading.thread.-ctor) - 1 MB default stack, 256 KB partial-trust minimum on .NET Framework.
- Benchmarks backing the numbers: `touki.perf/StackZeroInitPerf.cs`, `touki.perf/ArrayPoolSeedRentPerf.cs`, `touki.perf/ArrayPoolCrossoverPerf.cs` (the size sweep in section 3.2), and `touki.perf/BufferScopeOverheadPerf.cs` (the `BufferScope` overhead in section 4.3).