---
name: security-review
description: Security-focused review of pending changes. Audit any change that could affect safety or correctness under abusive input or unchecked preconditions &mdash; oversized values, malformed structures, integer/length overflow, catastrophic backtracking, allocation pressure, other denial-of-service shapes, and any use of `unsafe` code or the `Unsafe` / `MemoryMarshal` / `Marshal` static helpers (which trade compiler safety guarantees for speed and need extra scrutiny). Add regression tests that pin safe behavior even when the current implementation already handles the input correctly. Use when asked to "assess for security vulnerabilities", "do a security review", "check for ReDoS / DoS", "audit untrusted input handling", or before publishing any change that adds or modifies code that parses, decodes, encodes, compiles, marshals, or reinterprets memory.
---

# Security review

Surface and pin behavior under **malformed input** and **caller-validated
APIs** (everything the C# compiler doesn't check for you).

**Related skills:**

- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) &mdash; broader
  self-review; run this skill alongside it before any publish.
- [`performance-testing`](../performance-testing/SKILL.md) &mdash; use
  when you need to *measure* a worst-case input rather than just bound
  it with a `Stopwatch`.

## When to run

- The user asks for a security assessment / vulnerability review.
- A change adds or modifies a member that accepts caller-supplied data
  (function args, file contents, network bytes, env vars, anything
  upstream of those).
- A change introduces or modifies `unsafe` code, calls into
  `Unsafe.*` / `MemoryMarshal.*` / `Marshal.*`, `fixed`, raw pointers,
  unbounded `stackalloc`, or any BCL API whose XML doc says "unsafe"
  or "caller must". Applies even when inputs are fully internal &mdash;
  preconditions drift across refactors.
- A CVE in a comparable library prompts "do we have the same shape?".

## Core principles

1. **Test safe properties, even when current code is already
   correct.** A safe property = the method terminates within a stated
   bound, returns a defined error for malformed input, returns the
   documented default for empty input, doesn't read/write past the
   buffer, or doesn't crash. These tests must pass against current
   code; commit them as the regression lock.
2. **Never write a test that pins observable wrong output.** A test
   asserting "this silently truncates" locks the bug in. If current
   code is wrong, skip the test until the fix lands and pin the
   *corrected* output then.
3. **Test the shape, not the specific exploit.** "Input at the
   declared limit" lasts forever; "exact pattern that triggered CVE-X"
   decays.
4. **Boundary tests come in pairs.** One at the limit (succeeds), one
   just over (fails in a defined way).
5. **Surface findings before patching production code.** When a
   finding needs a production change, output the options report,
   **end your turn, and do not produce the patch or any failing test
   on that turn**. Resume after the user replies with an approval
   verb (`go`, `apply A`, `fix it`). Passing safe-property tests
   (principle 1) may ship on the same turn as the report; failing
   tests may not.

### Severity rubric

Label every finding before the report.

- **High** &mdash; memory corruption, OOB read/write, type confusion,
  lifetime escape, RCE, disclosure of secrets/addresses, auth bypass.
  Fix on the same PR.
- **Medium** &mdash; DoS (CPU, memory, stack, FDs), crashing unhandled
  exception, parser accepting malformed input as valid, ambiguous
  parsing that smuggles a different shape past validation. Usually
  fix on the same PR; deferral needs a documented mitigation.
- **Low** &mdash; silent truncation still bounded by a downstream BCL
  slice check, documented contract violations on edge cases,
  non-sensitive error-message leakage. Safe to defer; still pin the
  *corrected* behavior with a regression test once the fix lands.

## Audit checklist

Walk every new/modified member through the categories below. The
question is whether the *input shape* or *unchecked precondition*
could push the code into the failure mode, not whether it currently
does.

When attention is limited, allocate it by tier:

- **Critical (never skip):** &sect;1, &sect;4, &sect;7.
- **Standard (every member):** &sect;2, &sect;5, &sect;9.
- **Conditional (when the code shape matches):** &sect;3 (allocates
  proportional to input), &sect;6 (encoder uses in-band sentinels),
  &sect;8 (path-handling API).

### 1. Length / size of inputs

- Is there an upper bound? What stops a caller from passing a
  multi-MB / multi-GB value?
- Is the bound at the API boundary, not buried in a helper?
- For composed inputs (list of strings, stream of records), is the
  **total** size bounded, not just per-element?
- Is the default safe for untrusted callers, with an opt-out for
  trusted ones? See `DefaultMaxPatternLength` / `maxPatternLength`
  in [touki/Touki/Text/Globbing/GlobMatcherFactory.cs](../../../touki/Touki/Text/Globbing/GlobMatcherFactory.cs).

**Tests:** at-limit success, over-limit failure with documented
error, opt-out (e.g. `-1`) disables the check.

### 2. Integer / length-field overflow

- Are length sums (`a.Length + b.Length`, `count * elementSize`)
  protected by `checked()` where crafted input can overflow?
- Are running lengths cast to a narrower type (`(char)len`,
  `(byte)count`)? Silent truncation lets oversized fields slip
  through and be re-read as smaller values, misdecoding downstream
  data as structural metadata. Check the counter *before* the cast.

**Tests:** boundary inside the representable range plus boundary
outside. Specialized fast paths often bypass the affected code
&mdash; pick an input shape that **forces the general path**.

### 3. Allocation pressure / memory DoS

- Does work allocate proportional to (or worse than) input size?
- Is the worst case bounded by an explicit cap (&sect;1) or only by
  available memory?
- Do buffers rent from `ArrayPool<T>.Shared` (good), allocate fresh
  per call (acceptable if bounded), or grow without limit (red flag)?

**Tests:** a "large but legitimate" input completes within a
`Stopwatch` bound (`Should().BeLessThan(TimeSpan.FromSeconds(2))`).
Reach for BenchmarkDotNet's `MemoryDiagnoser` only to pin a specific
allocation count.

### 4. Computational complexity / algorithmic DoS

- Worst-case runtime: linear, polynomial, or exponential?
- ReDoS shape: matcher with multiple wildcards (`*`, `**`, `?(...)`,
  alternation) that retries every wildcard on mismatch. Safe shape:
  **fixed savepoint slots** (each new wildcard overwrites the
  previous slot of the same kind), bounding the worst case to
  O(n&middot;m).
- Hashtable attacks: are user-controlled keys hashed with a
  randomized hash (modern .NET `string.GetHashCode()` and
  `Dictionary<,>` are per-process randomized), or a deterministic one
  a caller could pre-image into a single bucket?

**Tests** (pin the safe property):

```csharp
[Fact]
public void Method_PathologicalInput_TerminatesPromptly()
{
    /* construct adversarial pattern + input */

    Stopwatch sw = Stopwatch.StartNew();
    bool result = m.IsMatch(input);
    sw.Stop();

    result.Should().Be(/* expected */);
    sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
}
```

Add one per "we use the safe algorithm" claim; the property is
invisible to refactors, the test is the lock.

### 5. Malformed structure / parser robustness

- Truncated input handled gracefully (no `IndexOutOfRangeException`
  past the end)?
- Unmatched opens (`[`, `{`, `(`, dangling `\\` / `%`) produce a
  defined error, not an unrelated runtime exception?

**Tests:** one per malformed shape asserting the exact error type;
empty, single-character, and "exactly the sentinel" inputs (`""`,
`"["`, `"\\"`).

### 6. Sentinel / in-band markers colliding with input

- Encoder uses in-band sentinel values (opcode chars, length prefix
  bytes, control chars)? If user input contains those same values,
  are they distinguishable from real structure (escaping, length
  framing, reserved noncharacter code points)?

**Test:** input containing every sentinel value used as ordinary
data; the member treats them as data.

### 7. `unsafe`, `Unsafe.*`, `MemoryMarshal.*`, and other caller-validated APIs

Mandatory whenever a PR touches one of these. The compiler offloads
safety to you; the review *is* the safety check.

For each use site answer:

1. What precondition does the API require? (Read the XML doc.)
2. Where in the calling code is it established? If it's a *different*
   method, what stops a future refactor from desyncing them?
3. Is the precondition still satisfied when the input is empty,
   `null`, zero-length, or `default`? (Empty is the most common
   blow-up.)

Walk every use site against this table; rows are independent.

| API pattern | Precondition (compiler does NOT check) | Failure mode if violated | Required test shape |
| ----------- | -------------------------------------- | ------------------------ | ------------------- |
| `MemoryMarshal.GetReference(span)` / `GetPinnableReference` / `Unsafe.AsPointer(ref span[0])` / `GetArrayDataReference` | Returned `ref` only valid when `span.Length > 0`; empty span yields the managed-null sentinel. | Crash or wild read at null. | `Method_EmptyInput_DoesNotDereferenceNullReference` returns documented default with no exception. |
| `Unsafe.Add(ref T, int)` | Index `< span.Length` (or backing buffer element count). | OOB read leaks adjacent-page memory; OOB write corrupts heap. | One test at the last in-range index (loop guard's last iteration is the off-by-one site). |
| `Unsafe.ReadUnaligned<T>` / `WriteUnaligned<T>` | `ref byte` points at `sizeof(T)` valid bytes; alignment is your problem. | OOB read or torn write. | Boundary tests at exactly `sizeof(T)` bytes and one less. |
| `Unsafe.As<TFrom, TTo>(ref TFrom)` / `Unsafe.As<T>(object)` | `sizeof(TTo) <= sizeof(TFrom)`; GC reference layout matches. | Reads garbage past source; corrupts GC heap if reference layout differs. **net481 RyuJIT pitfall:** `[AggressiveInlining]` + `Unsafe.As<T, byte>(ref param)` propagates the caller's int-promoted negative value into the comparison immediate. See [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md). | One test per primitive width including signed/negative values; mask with `& 0xFF` / `& 0xFFFF` if needed. |
| `Unsafe.SkipInit<T>(out T)` | Caller fully initializes every reachable field before any read. | Stale-pointer foot-gun for ref fields; uninitialized read otherwise. | Test every code path that reads from the skip-init local. |
| `MemoryMarshal.Cast<TFrom, TTo>` / `AsBytes` | Result length is `source.Length * sizeof(TFrom) / sizeof(TTo)`; partial elements vanish silently. | Caller treats a truncated result as the full payload. | Partial-element case (source length doesn't divide evenly). |
| `MemoryMarshal.CreateSpan` / `CreateReadOnlySpan` | Returned span lifetime &le; referenced storage lifetime. | Use-after-free when the storage was a stack local or short rental. | Audit every caller for lifetime escape; no automated test catches this. |
| `fixed (T* p = ...)` | `p` valid only inside the `fixed` block. | Dangling pointer after GC relocates. | Manual review: `p` not stashed in a field, passed to `async`/iterator continuations, or returned. |
| `stackalloc T[n]` | `n` small and bounded. | Stack-overflow DoS if `n` comes from input. | Max-allowed size succeeds; one element more is rejected before `stackalloc` runs. Use [`BufferScope<T>`](../../../touki/Touki/Buffers/BufferScope.cs) when input could exceed the stack budget. |
| `[DllImport]` / `LibraryImport` | Marshaller attrs (`[MarshalAs]`, `SizeConst`, `string` vs `IntPtr`) silently change buffer sizes / ownership. | Buffer overrun, double-free, type confusion across the managed/native boundary. | Cross-check signature against native docs; re-verify on every TFM. |
| Any BCL API whose XML doc contains "unsafe" or "caller must" | Whatever the doc specifies. | Whatever the doc warns about. | Edge case where the precondition *almost* holds (one byte short, off-by-one alignment). |

### 8. Path traversal / sandbox escape

- API takes a root + relative input and returns paths &mdash; results
  stay inside the root? Symlink behavior is the caller's documented
  choice, not silently re-enabled?
- Caller-supplied fragments concatenated without normalization?
  `Path.Combine` honors absolute inner segments, which is a foot-gun.

**Tests:** inputs with `..` segments, absolute paths, alternate
separators, and symlink-style names; enumerator stays inside the
configured root.

### 9. Argument validation at the boundary

- `ArgumentNullException.ThrowIfNull` on every reference-type
  parameter the contract forbids `null` for.
- `ArgumentOutOfRangeException.ThrowIfNegative` / `ThrowIfGreaterThan`
  on numeric range checks.
- Enum parameters: validated against the declared range, or
  documented to accept any underlying value with explicit defaulting.

**Test:** one per parameter for each forbidden violation mode. Don't
trust the downstream BCL primitive to throw &mdash; the contract is
yours.

## Review procedure

1. **Inventory.** `git status --short` and read every new/modified
   file. Tag each member that takes external input or uses a
   caller-validated API.
2. **Walk each tagged member through the tiered categories.** The
   test you'd write to prove it's fine is the deliverable.
3. **Place tests in `<TypeName>.Security.cs` partial-class files**
   beside the production type. Create one if it doesn't exist.
4. **Add safe-property tests now (principle 1); commit failing /
   wrong-output tests only with the fix.**
5. **If a test fails against current code, stop and report.** Don't
   patch without surfacing first.
6. **Run tests on every TFM.** Timing bounds and allocation behavior
   differ across BCL versions.

## Options report format

When the report contains any finding that requires a production-code
change: output the report and **end your turn**. Don't produce the
patch, don't produce a failing test, don't run further tool calls
beyond what's needed to classify the finding. Resume only after the
user replies with an approval verb (`go`, `apply Option A`, `fix it`,
or similar). Passing safe-property tests (principle 1) may be added
on the same turn; they don't change production behavior.

```text
### Finding #N -- <one-line summary>

<which file(s), which lines, what the failure mode is>
**Severity:** High / Medium / Low (per rubric)

**Option A -- <approach>**
- <change shape, ~LoC>
- Pro: ...
- Con: ...

**Option B -- ...**

**My recommendation:** <one of the above, with rationale.>
```

Low-severity findings can be deferred entirely; Medium/High deferrals
need an explicit reason in the PR body.

## Don'ts

- **Don't patch production code without surfacing the finding first.**
- **Don't pin observable wrong output in a test.** That locks the bug
  in. Either ship the corrected-behavior test *alongside* the fix, or
  skip the test until the fix lands. Safe-property tests (principle 1)
  are not this case.
- **Don't claim "safe by construction" without a regression test.**
  The safe property is invisible to a future refactor.
- **Don't assume `internal` / `InternalsVisibleTo` members are out of
  scope.** Tests reach them; refactors promote them. Audit them on
  the same basis as `public`.
