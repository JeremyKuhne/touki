# Caller-validated APIs (`unsafe`, `Unsafe.*`, `MemoryMarshal.*`)

Detail for &sect;7 of the [security-review](SKILL.md) audit
[checklist.md](checklist.md). Mandatory whenever a PR touches one of these.
The compiler offloads safety to you; the review *is* the safety check.

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
| `Unsafe.As<TFrom, TTo>(ref TFrom)` / `Unsafe.As<T>(object)` | `sizeof(TTo) <= sizeof(TFrom)`; GC reference layout matches. | Reads garbage past source; corrupts GC heap if reference layout differs. **Older-JIT pitfall:** on .NET Framework RyuJIT, an `[AggressiveInlining]` method that reinterprets a signed-primitive parameter via `Unsafe.As<T, byte>(ref param)` can propagate the caller's int-promoted negative value into the comparison immediate; mask explicitly. | One test per primitive width including signed/negative values; mask with `& 0xFF` / `& 0xFFFF` if needed. |
| `Unsafe.SkipInit<T>(out T)` | Caller fully initializes every reachable field before any read. | Stale-pointer foot-gun for ref fields; uninitialized read otherwise. | Test every code path that reads from the skip-init local. |
| `MemoryMarshal.Cast<TFrom, TTo>` / `AsBytes` | Result length is `source.Length * sizeof(TFrom) / sizeof(TTo)`; partial elements vanish silently. | Caller treats a truncated result as the full payload. | Partial-element case (source length doesn't divide evenly). |
| `MemoryMarshal.CreateSpan` / `CreateReadOnlySpan` | Returned span lifetime &le; referenced storage lifetime. | Use-after-free when the storage was a stack local or short rental. | Audit every caller for lifetime escape; no automated test catches this. |
| `fixed (T* p = ...)` | `p` valid only inside the `fixed` block. | Dangling pointer after GC relocates. | Manual review: `p` not stashed in a field, passed to `async`/iterator continuations, or returned. |
| `stackalloc T[n]` | `n` small and bounded. | Stack-overflow DoS if `n` comes from input. | Max-allowed size succeeds; one element more is rejected before `stackalloc` runs. Use a pooled / growable scratch-buffer helper when the input could exceed the stack budget. |
| `[DllImport]` / `LibraryImport` | Marshaller attrs (`[MarshalAs]`, `SizeConst`, `string` vs `IntPtr`) silently change buffer sizes / ownership. | Buffer overrun, double-free, type confusion across the managed/native boundary. | Cross-check signature against native docs; re-verify on every TFM. |
| Any BCL API whose XML doc contains "unsafe" or "caller must" | Whatever the doc specifies. | Whatever the doc warns about. | Edge case where the precondition *almost* holds (one byte short, off-by-one alignment). |
