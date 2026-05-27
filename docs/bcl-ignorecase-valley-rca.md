# BCL `OrdinalIgnoreCase` valley — root cause analysis

Detailed analysis of why
`MemoryExtensions.Equals(span, span, StringComparison.OrdinalIgnoreCase)`
exhibits a perf valley at 8–15 chars on .NET 10 RyuJIT. Sourced from
`BenchmarkDotNet.Artifacts/results/touki.perf.AsciiIgnoreCasePerf-asm.md`
(generated with `[DisassemblyDiagnoser(maxDepth: 3)]`). The takeaway
sidesteps the "span efficiency" hypothesis: **the valley is a JIT codegen
issue in one specific generic instantiation of the BCL's vector helper,
not a span performance problem.**

Hardware: Intel Core i9-14900K, .NET 10.0.7, x64 RyuJIT x86-64-v3, AVX2.

This document is a worked case study. The general field manual for
span-walking code on .NET Framework — slow-span layout, the Strategy
A-through-E hierarchy, the within-noise rule for TFM-split
implementations — lives in
[framework-span-performance.md](framework-span-performance.md).

---

## 1. What the BCL is actually doing at each length

`MemoryExtensions.Equals(ReadOnlySpan<char> a, ReadOnlySpan<char> b, StringComparison)`
with `OrdinalIgnoreCase` ends in a length-cascade dispatch
(disassembled from the inlined caller):

```asm
M00_L05:
    test r10d, r10d      ; length == 0?
    je   M00_L08         ; yes -> return true
    cmp  r10d, 8
    jl   M00_L07         ; len < 8 -> small-string path
    cmp  r10d, 10        ; (0x10 = 16)
    jl   M00_L06         ; 8 <= len < 16 -> Vector128 path  <-- THE VALLEY
    jmp  qword ptr [...] ; len >= 16 -> Vector256 path     <-- FAST
M00_L06:
    jmp  qword ptr [7FF9...4648]
    ; System.Globalization.Ordinal.EqualsIgnoreCase_Vector
    ;   <Vector128<UInt16>>(Char&, Char&, Int32)
M00_L07:
    jmp  qword ptr [7FF9...67C0]   ; len < 8 small-string special case
```

Three discrete code paths in the BCL handle the three regimes. The valley
is the middle one (`Vector128<UInt16>` specialization).

---

## 2. The Vector256 path (length ≥ 16) is properly vectorized

`Ordinal.EqualsIgnoreCase_Vector<Vector256<UInt16>>`:

- **Stack frame: 56 bytes** (`sub rsp, 38`).
- One saved non-volatile XMM (`vmovaps [rsp+20], xmm6`).
- Inner loop (per 16 chars):

```asm
M01_L00:
    vmovups   ymm3, [rcx+r10*2]    ; load 16 chars from a
    vmovups   ymm4, [rdx+r10*2]    ; load 16 chars from b
    vpor      ymm5, ymm3, ymm4     ; combined ASCII check
    vpand     ymm5, ymm5, ymm2
    vptest    xmm6, xmm6           ; any non-ASCII?
    jne       M01_L03              ; -> fall to invariant culture
    vextractf128 xmm5, ymm5, 1     ; check upper half
    vptest    xmm5, xmm5
    ...
    vpcmpeqw  ymm5, ymm3, ymm4     ; vector compare equal
    vpcmpeqd  ymm6, ymm6, ymm6     ; all-ones
    vpxor     ymm5, ymm6, ymm5     ; invert
    vptest    ymm5, ymm5
    je        M01_L01              ; all equal already
    vpor      ymm3, ymm3, ymm0     ; vector fold: OR with 0x20
    vpor      ymm4, ymm4, ymm0
    vpsubw    ymm6, ymm3, ymm1     ; range check for ASCII letter
    vpand     ymm5, ymm6, ymm5
    vpaddw    ymm5, ymm5, ymm6
    vpcmpgtw  ymm5, ymm5, [...]    ; in-range mask
    vptest    ymm5, ymm5
    ...
    vpcmpeqw  ymm3, ymm3, ymm4
    vpmovmskb r11d, ymm3           ; extract mask in one op
    cmp       r11d, 0FFFFFFFF
    jne       M01_L05
M01_L01:
    add       r10, 10              ; advance 16 chars
    cmp       r10, r8
    jbe       M01_L00              ; loop
```

This is well-written SIMD code. Length 20 measures **2.6 ns**, length 64
measures **6.3 ns** — within reach of memcpy throughput.

---

## 3. The Vector128 path (length 8–15) is *not* vectorized in the inner compare

`Ordinal.EqualsIgnoreCase_Vector<Vector128<UInt16>>`:

- **Stack frame: 336 bytes** (`sub rsp, 150`). **6× larger than the
  Vector256 version.**
- Same non-volatile XMM save.
- Inner loop:

```asm
M01_L00:
    vmovups   xmm3, [rcx+r10*2]     ; load 8 chars from a
    vmovups   xmm4, [rdx+r10*2]     ; load 8 chars from b
    vpor      xmm5, xmm3, xmm4      ; combined ASCII check (vector)
    vpand     xmm5, xmm5, xmm2
    vmovaps   [rsp+130], xmm5       ; SPILL TO STACK
    vxorps    xmm5, xmm5, xmm5
    vmovaps   [rsp+120], xmm5       ; SPILL ZEROS TO STACK
    mov       r8, [rsp+130]         ; reload as scalar
    mov       [rsp+108], r8
    mov       r8, [rsp+120]
    mov       [rsp+100], r8
    xor       r11d, r11d
    movzx     ebx, word ptr [rsp+108]   ; per-lane compare via stack
    cmp       bx, [rsp+100]
    jne       M01_L02
M01_L01:
    inc       r11d
    cmp       r11d, 4               ; 4-iteration scalar loop
    jge       M01_L07
    lea       rbx, [rsp+108]
    movsxd    rsi, r11d
    movzx     ebx, word ptr [rbx+rsi*2]
    lea       rdi, [rsp+100]
    cmp       bx, [rdi+rsi*2]
    je        M01_L01
```

After the initial vector load + OR, **every per-lane compare reads operands
from stack slots**. The JIT failed to keep the Vector128 contents in
registers across the lane-iteration logic. Three more similarly-shaped
blocks follow for the rest of the function body.

This is the smoking gun: **the Vector128 specialization of
`EqualsIgnoreCase_Vector` has bad codegen.** It's not slow because of any
fundamental property of length-10 inputs; it's slow because the JIT
materializes vector lanes through 336 bytes of stack instead of keeping
them in `xmm` registers.

Measured cost: **9.4 ns at length 10**.

---

## 4. The helper's hot loop, by comparison

`SpanExtensions.EqualsOrdinalIgnoreCase` inlined into the benchmark for length 10:

```asm
; prologue: zero 64 bytes of stack for the cold BCL-fallback ReadOnlySpans
sub       rsp, 60
vxorps    xmm4, xmm4, xmm4
vmovdqu   ymmword ptr [rsp+20], ymm4
vmovdqu   ymmword ptr [rsp+40], ymm4
...
; stage spans to stack so the cold BCL-fallback can take them by ref
mov       [rsp+50], rbx     ; a.ptr
mov       [rsp+58], edi     ; a.length
mov       [rsp+40], rsi     ; b.ptr
mov       [rsp+48], edi     ; b.length

; hot loop body:
M00_L02:
    mov       rdx, [rsp+50]           ; load a.ptr from stack
    mov       r8d, ecx
    movzx     edx, word ptr [rdx+r8*2] ; load a[i]
    cmp       ecx, [rsp+48]            ; bounds check on b length
    jae       M00_L14
    mov       rax, [rsp+40]            ; load b.ptr from stack
    movzx     r8d, word ptr [rax+r8*2] ; load b[i]
    mov       eax, edx
    or        eax, r8d
    cmp       eax, 7F                  ; ASCII check
    ja        M00_L10                  ; -> cold BCL fallback
    cmp       edx, r8d
    je        M00_L03                  ; bytes equal
    or        edx, 20                  ; fold a
    lea       eax, [rdx-61]            ; check 'a'..'z'
    cmp       eax, 19
    ja        M00_L13                  ; not letter -> fail
    or        r8d, 20                  ; fold b
    lea       eax, [r8-61]
    cmp       eax, 19
    ja        M00_L13
    cmp       edx, r8d                 ; folded compare
    jne       M00_L13
M00_L03:
    inc       ecx
    cmp       ecx, [rsp+58]
    jl        M00_L02
```

**~12 ops per iteration × 10 iterations = ~120 ops total.** Modern RyuJIT
on a Raptor Lake P-core retires ~4 µops/cycle in steady state → ~30
cycles ≈ 7 ns. Measured: **6.6 ns**. Match.

The helper is 30% faster than the BCL at length 10 because:
1. **No 336-byte stack frame.** Helper uses 96 bytes.
2. **No vector setup.** No `vbroadcastss` of three constants, no
   `vmovaps` of `xmm6`.
3. **No per-lane stack reloads.** The hot loop reads through 4 fixed stack
   slots (`[rsp+50], [rsp+58], [rsp+40], [rsp+48]`) once each per iteration
   instead of streaming vector lanes through new stack slots.

---

## 5. "How much is span inefficiency?"

**Roughly 10–20% of the helper's code size, none of the BCL's slowness.**

### On the helper side

The 64 bytes of zero-init in the prologue and the 4 stack slots holding
span fields (`[rsp+40..58]`) exist for **one reason**: the cold
BCL-fallback branch (`M00_L10`) on non-ASCII characters needs to pass
`ReadOnlySpan<char>` arguments by ref to
`MemoryExtensions.Equals(span, span, OrdinalIgnoreCase)`. The JIT
therefore stages the spans on the stack at method entry rather than
keeping them in registers.

This costs:
- 64 bytes of stack zero-init in the prologue (~5 cycles for `vmovdqu`).
- 4 stack loads per loop iteration instead of register reads.

For a 10-char input, the staging cost dilutes the win the helper would
otherwise have. The hot loop reads `[rsp+50]` and `[rsp+40]` every
iteration — these become L1-cache loads (~5 cycle latency) instead of
register accesses (~0).

**This is real span efficiency cost**, but it's bounded. Estimated impact:
~1 ns over the loop at length 10.

### On the BCL side

The 336-byte stack frame in `EqualsIgnoreCase_Vector<Vector128<UInt16>>`
is **not** a span issue. The function receives `Char&` references (not
spans). The bloat comes from the **failed JIT optimization** of
`Vector128<UInt16>` lane operations, which expand to stack-spill-and-
reload sequences rather than to vector instructions.

The Vector256 specialization of the *same generic method* has a 56-byte
frame and keeps everything in `ymm` registers. The codegen difference is
entirely in the JIT's handling of the generic vector type parameter —
not in spans.

---

## 6. Mitigations

### 6.1 Already applied

| Mitigation | Effect |
|---|---|
| `SpanExtensions.EqualsOrdinalIgnoreCase` for span lengths < 16 | Sidesteps the Vector128 valley. 10-char compare 10.04 → 4.79 ns. |
| `[AggressiveInlining]` on the dispatch wrapper, separate non-inlined fold loop | On net481 saves ~7 ns by allowing the inliner to fold the length-check into the call site. On net10 zero-effect. |
| Crossover threshold at 16 chars | Matches `Vector128<short>.Count × 2` — the BCL's own dispatch point. |

### 6.2 Potential incremental wins

**(a) `[SkipLocalsInit]` on `SpanExtensions.EqualsOrdinalIgnoreCase` and the
shared ASCII fold core.**
Drops the 64-byte stack zero-init. Likely ~0.3 ns measurable on the
length-5/10 case where the prologue is a significant fraction of total time.
**Risk: zero** — the fold loop never reads uninitialized memory.
**Status: candidate.**

**(b) Hoist the BCL-fallback span construction out of the hot path.**
Currently the JIT keeps the spans staged on the stack throughout the hot
loop because of the cold `a[i..].Equals(...)` call site on the non-ASCII
branch. Replacing that branch with a separate `[MethodImpl(NoInlining)]`
helper that takes `(ref char a, ref char b, int remaining)` would let the
hot loop hold spans in registers.

Sketch:

```csharp
private static bool EqualsAsciiFold(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
{
    for (int i = 0; i < a.Length; i++)
    {
        char x = a[i];
        char y = b[i];
        if (((uint)x | y) > 0x7F)
        {
            // Don't construct slices here; the JIT keeps them in registers.
            return FallbackInvariantFold(in a, in b, i);
        }
        // ... fold + compare ...
    }
    return true;
}

[MethodImpl(MethodImplOptions.NoInlining)]
private static bool FallbackInvariantFold(in ReadOnlySpan<char> a, in ReadOnlySpan<char> b, int from) =>
    a[from..].Equals(b[from..], StringComparison.OrdinalIgnoreCase);
```

Caveat: `in ReadOnlySpan<char>` may itself force stack residence. The
truly clean form is to pass `(ref char, ref char, int)` and rebuild the
spans inside the noinline function. Worth measuring.

**Estimated impact**: −0.5 to −1 ns on length-10 hit. Roughly matches the
"span inefficiency" budget identified in §5.
**Status: candidate. Worth a focused slice.**

**(c) File an issue against dotnet/runtime for the Vector128 specialization
of `Ordinal.EqualsIgnoreCase_Vector`.** The Vector256 sibling does the
same logic correctly. A repro is captured in this very disassembly file.
If the runtime fixes the codegen, the entire reason for the
`SpanExtensions` ASCII fast-path
disappears on net10.

**Status: filing recommended; do not block on it.**

### 6.3 Not worth doing

- **A Vector128 hand-roll in `SpanExtensions.CompareOrdinalIgnoreCase`
  for length 8–15.** This
  would require re-implementing the BCL's logic correctly. The helper's
  scalar fold loop already beats the BCL Vector128 path; building a
  correct Vector128 path means matching the Vector256 path's quality,
  which is a substantial body of work duplicating the BCL.
- **Increasing the BCL crossover threshold past 16.** Empirically the
  Vector256 path is faster than the scalar fold at length ≥ 16.
- **Removing the BCL fallback on non-ASCII.** Loses correctness for
  inputs with `>U+007F` characters. Documented as part of the contract.

---

## 7. Recommended follow-up slices

Order revised after experimental validation on net481 (see §B.5):

| Slice | What | Expected impact | Status |
|---|---|---|---|
| 7b | Move non-ASCII BCL fallback into a `[NoInlining]` helper taking `ref char` | net10: −0.5 to −1 ns on length-10 hit. net481: ~−1 ns on length-5 hit (eliminates the per-iter stack load). | **Recommended.** |
| 7c | File runtime issue for `Ordinal.EqualsIgnoreCase_Vector<Vector128<UInt16>>` codegen on net10 | Eventually removes the need for the helper on net10 at length 8–15. No effect on net481. | Filing recommended; not blocking. |
| 7a | `[SkipLocalsInit]` on the `SpanExtensions` ASCII-fold methods | net10: ≈ 0 (below noise). **net481: 0 — attribute not honored by net481 RyuJIT, see §B.5.** | **Already applied for hygiene** but produces no measurable benefit on either TFM. Keep for code-style consistency; do not cite it as a perf mitigation. |
| 7d (new) | Force-inline the scalar ASCII fold into `SpanExtensions.EqualsOrdinalIgnoreCase` so net481 sees only one function call instead of two | net481 length-5: ~−6 ns predicted (one full call frame). Risk: code bloat at every call site. Worth a focused experiment with rollback if call sites grow. | **Candidate — biggest remaining net481 win**. |

---

## Appendix A: cross-checking the math

Length 10, helper, 6.6 ns measured:

- 96-byte stack frame setup: ~3 cycles
- 64-byte zero-init via 2 `vmovdqu`: ~2 cycles
- Span staging (4 mov): ~1 cycle
- 10 iterations × 12 ops each = 120 ops at ~4 IPC: ~30 cycles
- Epilogue: ~3 cycles
- Total: ~40 cycles ≈ 10 ns. Measured 6.6 ns suggests heavy out-of-order
  overlap — typical for hot, predictable straight-line code.

Length 10, BCL Vector128, 9.4 ns measured:

- 116-byte caller dispatch frame setup: ~3 cycles
- Indirect `jmp` to specialization: ~5 cycles
- 336-byte stack frame setup: ~5 cycles
- `vmovaps` to save xmm6: ~1 cycle
- 3 vector constants via `vbroadcastss`: ~3 cycles
- Vector load (×2), OR, AND: ~3 cycles
- Stack spill + 4-iter scalar lane loop: ~20 cycles
- Three similar blocks for fold/range/compare: ~30 cycles
- Epilogue: ~5 cycles
- Total: ~75 cycles ≈ 19 ns. Measured 9.4 ns suggests a substantial
  fraction of the work is loaded into the OOO window during the dispatch.
  The gap between predicted and measured is consistent with the BCL hot
  path being predictable; the cost is real, just well-overlapped.

The point of the math is not to predict to the nanosecond — it's to show
the work the JIT chose to emit, and that the BCL is doing roughly 3× more
work than the helper at this length.

---

## Appendix B: .NET Framework 4.8.1 disassembly

`[DisassemblyDiagnoser]` **does** work on .NET Framework — that note in an
earlier draft of this doc was wrong. The full net481 disasm is captured in
`BenchmarkDotNet.Artifacts/results/touki.perf.AsciiIgnoreCasePerf-asm.md`
(regenerate with the diagnoser attribute on `AsciiIgnoreCasePerf`). The
analysis below is from that capture on the same hardware.

Net481 RyuJIT is a fundamentally different runtime: no tiered JIT, no
dynamic PGO, no `Vector128/Vector256` intrinsics, and crucially **a "slow
span" implementation** where `ReadOnlySpan<char>` stores a managed object
reference and a byte offset, not a raw pointer.

### B.1 Net481 measurements (recap)

| Length | Helper | BCL | Gap |
|---:|---:|---:|---:|
| 5 | 19.9 ns | 26.6 ns | **−6.7 ns (helper wins by 25%)** |
| 10 | 26.7 ns | 28.9 ns | −2.2 ns |
| 20 | 41.5 ns | 41.9 ns | ≈ tie (helper delegates to BCL) |
| 64 | 72.2 ns | 69.7 ns | +2.5 ns (helper has small delegation tax) |

The largest delta is at length 5 — exactly the opposite of net10, where
length 10 was the valley. On net481 there is **no valley**, because there
is no vectorized path that suddenly cuts in at length 16. The cost grows
roughly linearly with length on both helper and BCL.

### B.2 Net481 BCL: a stack of dispatch shims, no vectorization

`Bcl_HitDifferentCase` at length 5 disassembled, top-to-bottom:

```asm
; touki.perf.AsciiIgnoreCasePerf.Bcl_HitDifferentCase()
    push      rdi
    push      rsi
    sub       rsp, 58              ; 88 bytes
    mov       rsi, rcx
    lea       rdi, [rsp+28]
    mov       ecx, 0C              ; 12 dwords = 48 bytes
    xor       eax, eax
    rep stosd                      ; *** zero-init 48 bytes ***
    mov       rcx, rsi
    ; ... build two ReadOnlySpan<char> structs on stack ...
    ; ... copy string ptr/offset/length triplets into two stack slots ...
    mov       r8d, 5               ; StringComparison.OrdinalIgnoreCase
    mov       rax, offset System.MemoryExtensions.Equals(span, span, StringComparison)
    call      qword ptr [rax]      ; *** first call ***
```

Inside `MemoryExtensions.Equals`:

```asm
; System.MemoryExtensions.Equals(span, span, StringComparison)
    push      r14
    push      rdi
    push      rsi
    push      rbp
    push      rbx                  ; 5 callee-saved registers
    sub       rsp, 50              ; 80 bytes
    mov       ecx, 0C              ; 12 dwords
    xor       eax, eax
    rep stosd                      ; *** another 48-byte zero-init ***
    ...
    cmp       edi, 4               ; StringComparison.Ordinal?
    jne       short M01_L02        ; no -> check 5 (OrdinalIgnoreCase)
M01_L02:
    cmp       edi, 5               ; OrdinalIgnoreCase?
    jne       short M01_L06        ; no -> fall to invariant culture
M01_L03:
    ; length compare, then:
    lea       rcx, [rsp+38]
    ; stage span on stack again ...
    call      qword ptr [7FF9F98367E8]
    ; System.Runtime.InteropServices.MemoryMarshal.GetReference<char>(span)
    mov       r14, rax
    lea       rcx, [rsp+38]
    ; stage second span...
    call      qword ptr [7FF9F98367E8]
    ; ... MemoryMarshal.GetReference<char>(span) again
    mov       ecx, ebp
    shl       rcx, 1
    ...
    call      qword ptr [7FF9F983B1A0]  ; the actual ordinal-ignore-case compare
```

Four call frames deep before any character is compared:

1. Bench wrapper (`Bcl_HitDifferentCase`): 88 B stack + 48 B `rep stosd`.
2. `MemoryExtensions.Equals(StringComparison)`: 80 B stack + 48 B `rep stosd`
   + 5 callee-saved registers preserved.
3. `MemoryMarshal.GetReference<char>(span)` × 2 separate calls.
4. The underlying ordinal-ignore-case compare via function pointer.

**There is no vectorization anywhere in this chain on net481.** RyuJIT on
.NET Framework lacks the `Vector<T>` plumbing the BCL would need to
emit `EqualsIgnoreCase_Vector` here. The BCL falls back to a scalar
character-by-character compare.

### B.3 Net481 helper: shorter call chain, same per-char cost

The helper at length 5 dispatches similarly but with **one fewer call
frame**:

```asm
; touki.perf.AsciiIgnoreCasePerf.Helper_HitDifferentCase()
    push      rdi
    push      rsi
    sub       rsp, 58              ; 88 bytes
    mov       rsi, rcx
    lea       rdi, [rsp+28]
    mov       ecx, 0C
    xor       eax, eax
    rep stosd                      ; *** 48-byte zero-init ***
    ; ... build spans ...
    cmp       r9d, 10              ; length >= 16?
    jl        short M00_L05        ; no -> EqualsAsciiFold (length 5 goes here)
M00_L05:
    ; stage spans, then:
    call      Touki.SpanExtensions.CompareOrdinalIgnoreCaseAsciiFold(span, span)
```

`EqualsAsciiFold` itself:

```asm
; Touki.SpanExtensions.CompareOrdinalIgnoreCaseAsciiFold(span, span)
    push      rdi
    push      rsi
    sub       rsp, 58              ; 88 bytes
    mov       ecx, 0C
    xor       eax, eax
    rep stosd                      ; *** another 48-byte zero-init ***
    ...
M02_L00:
    cmp       r8d, r9d
    jae       near ptr M02_L09
    cmp       qword ptr [rcx], 0   ; *** span has a managed-object backing? ***
    jne       short M02_L01
    mov       rax, [rcx+8]         ; offset
    movsxd    r10, r8d
    shl       r10, 1
    add       rax, r10             ; raw pointer path (rare on managed strings)
    jmp       short M02_L02
M02_L01:
    mov       r11, [rcx]           ; load the array object reference
    cmp       [r11], r11d          ; ?? possibly a GC liveness fence
    lea       r10, [r11+8]         ; skip array header (8 bytes)
    mov       rax, [rcx+8]         ; load offset
    add       r10, rax
    movsxd    r11, r8d
    mov       rax, r11
    shl       rax, 1
    add       r10, rax
    xchg      rax, r10
M02_L02:
    movzx     eax, word ptr [rax]  ; *** finally load a[i] ***
```

**This is the "slow span" tax on net481, per character:**

- `cmp qword ptr [rcx], 0` — check if span is backed by a managed object
- `mov r11, [rcx]` — load the object pointer
- `cmp [r11], r11d` — touch the object (GC liveness / null check fold)
- `lea r10, [r11+8]` — skip the array header
- `mov rax, [rcx+8]` — load the offset field
- `add r10, rax` — apply offset
- `movsxd r11, r8d`, `shl rax, 1`, `add r10, rax` — index by 2 (char size)
- `movzx eax, word ptr [rax]` — load the character

**Roughly 8 µops to load one character** on net481 vs **1 µop** on net10
(where spans store a raw `ref char` that compiles to a register move).
The same dance happens for `b[i]` immediately after. **~16 µops per
loop iteration just for the two character loads**, before any compare or
fold work.

At length 10 that's ~160 µops just for memory loads, ~40 cycles ≈ 10 ns
at 4 IPC. The measured 26.7 ns matches this estimate plus the call
overhead.

### B.4 Where the net481 helper's win comes from at length 5

Helper at length 5: 19.9 ns. BCL at length 5: 26.6 ns. The 6.7 ns gap is
**not** the per-character work — it's the call-chain overhead the BCL pays
that the helper avoids:

| Cost | Helper | BCL |
|---|---:|---:|
| Bench wrapper (88 B stack + 48 B zero-init) | yes (~6 ns) | yes (~6 ns) |
| `MemoryExtensions.Equals` (80 B + 48 B zero-init + 5 callee-saved) | — | yes (~6 ns) |
| `MemoryMarshal.GetReference<char>` × 2 | — | yes (~2 ns) |
| `EqualsAsciiFold` (88 B + 48 B zero-init) | yes (~6 ns) | — |
| Underlying compare (5 chars × 16 µops/char) | yes (~4 ns) | yes (~4 ns) |

The BCL has **two more function calls** in the chain. Each adds a stack
frame, a `rep stosd` zero-init, and (in `MemoryExtensions.Equals`'s
case) 5 callee-saved register saves. At length 5 these constant costs
dominate.

### B.5 Net481-specific mitigations — what actually works

The recurring overhead in the helper path is the **48-byte `rep stosd`
zero-init** at every function entry, performed because the C# compiler
emits `initlocals = true` by default. A reasonable hypothesis was that
`[SkipLocalsInit]` would eliminate it. **The experimental result
disproves this**:

| Length | Without `[SkipLocalsInit]` | With `[SkipLocalsInit]` | Δ |
|---:|---:|---:|---:|
| 5 | 19.9 ns | 20.1 ns | ≈ 0 (noise) |
| 10 | 26.7 ns | 27.2 ns | ≈ 0 (noise) |
| 20 | 41.5 ns | 42.9 ns | ≈ 0 (noise) |
| 64 | 72.2 ns | 71.1 ns | ≈ 0 (noise) |

Disassembling `EqualsAsciiFold` *after* applying the attribute confirms
the cause:

```asm
; Touki.SpanExtensions.CompareOrdinalIgnoreCaseAsciiFold(...)
    sub       rsp, 58
    ...
    mov       ecx, 0C
    xor       eax, eax
    rep stosd        ; <-- still emitted despite [SkipLocalsInit]
```

**`[SkipLocalsInit]` is a no-op on net481 RyuJIT.** The C# compiler
emits `.localsinit false` in the IL header for the attributed method
(verifiable with ildasm), but the .NET Framework 4.8.1 RyuJIT ignores the
flag and unconditionally zero-inits locals. The modern .NET RyuJIT
honors the flag (where measured impact on net10 happens to be below the
~0.1 ns noise floor for our 64-byte buffer anyway, but the attribute is
respected).

This means:

- **Pinned rule**: `[SkipLocalsInit]` is correctness-neutral on both TFMs
  but only has any chance of measurable effect on net10. On net481 it is
  pure code-noise.
- **The 48-byte `rep stosd` per function entry on net481 is irreducible
  from user code.** Saving it requires either inlining all the way
  through (eliminate the call frame entirely) or convincing the IL
  verifier the method doesn't need init (only possible from C source
  via native interop, not from C#).

The mitigations that *do* still help on net481:

1. **Reduce the number of function calls in the chain.** Currently:
   - bench wrapper (out of our control — BDN-generated)
   - → `SpanExtensions.EqualsOrdinalIgnoreCase` (inlined into the wrapper by AggressiveInlining)
   - → `CompareOrdinalIgnoreCaseAsciiFold` (not inlined — separate call)

   Forcing inlining of `CompareOrdinalIgnoreCaseAsciiFold` into the wrapper
   would eliminate one call frame on net481 (~6 ns at length 5). The risk
   is bloating every call site that uses `EqualsOrdinalIgnoreCase`. We
   chose the split intentionally to keep `EqualsOrdinalIgnoreCase` small
   enough for the net481 inliner to take. **Reconsider via a focused
   experiment.** See §B.7 below.

2. **Don't keep paths in the helper that net481 will never benefit from.**
   The non-ASCII BCL fallback (`a[i..].Equals(b[i..], OrdinalIgnoreCase)`)
   exists to handle code points > U+007F. On net481 that BCL call is the
   four-frames-deep chain analyzed in §B.2. Moving it behind a noinline
   helper taking `ref char` (Slice 7b) would let the JIT keep spans in
   registers for the hot path and only spill them when the cold path
   fires. **Estimated savings on net481 length 5: ~1 ns** (one stack
   load per loop iteration is eliminated).

3. **Accept that `[SkipLocalsInit]` does nothing on net481** and stop
   recommending it as a net481 mitigation.

### B.6 Why "always inline (skip BCL delegation)" on net481 is still wrong

A natural conclusion from §B.4 might be "if the BCL is slow on net481 at
all lengths, don't delegate to it at length ≥ 16." The measurements rule
this out:

| Length | Helper if we skipped BCL delegation | BCL |
|---:|---:|---:|
| 5 | 19.9 ✓ | 26.6 |
| 10 | 26.7 ✓ | 28.9 |
| 20 | 45.4 (would be, scalar loop) | 41.9 ✓ |
| 64 | 105.3 (would be) | 69.7 ✓ |

The crossover at length 16 is correct for both TFMs, just for different
reasons. On net10 the BCL switches from a buggy Vector128 codegen to a
clean Vector256 codegen. On net481 the BCL has no vectorization either
side of the threshold, but at length ≥ 16 its scalar inner loop is
slightly tighter than ours because it doesn't pay the per-char ASCII
range-check overhead — the input is precompared scalar-wise without the
fold-letter-range check. We do extra work per char to support the fold;
that work pays off at short lengths (where it's still very few
iterations) but not at long lengths.

### B.7 Updated recommendation (after experimental validation)

Revised after the net481 `[SkipLocalsInit]` experiment in §B.5 disproved
the original prediction.

1. **Slice 7d (force-inline the scalar ASCII fold into
   `SpanExtensions.EqualsOrdinalIgnoreCase`)**
   — predicted ~−6 ns on net481 length 5 by eliminating one function-call
   frame (the chain becomes bench → fused helper, instead of
   bench → wrapper → fold). Risk: code bloat at every call site if too
   aggressive. Worth a focused, measured experiment.
2. **Slice 7b (move non-ASCII fallback into a `[NoInlining]` helper
   taking `ref char`)** — predicted −0.5 to −1 ns on both TFMs at the
   typical glob length. Modest but real.
3. **Slice 7c (file runtime issue for the Vector128 codegen on net10)**
   — only relevant to net10; out of touki's scope to fix but worth
   reporting.
4. **Slice 7a (`[SkipLocalsInit]`)** — verified no-op on net481 (the
   attribute is ignored by RyuJIT 4.8.1) and below-noise on net10.
   Keep applied for code-style consistency; **do not credit it as a
   perf mitigation**.

The disasm files captured in
`BenchmarkDotNet.Artifacts/results/touki.perf.AsciiIgnoreCasePerf-asm.md`
(both TFMs, when the `[DisassemblyDiagnoser]` attribute is enabled) are
the primary evidence for these recommendations.
