# Blittable signatures

Detail for [cswin32-interop](SKILL.md). CsWin32 is configured
`allowMarshaling: false`, so every `[DllImport]` and every manual COM vtable
method must be **blittable** - the runtime does no marshalling at the boundary.
These rules apply to both surfaces.

- **Return `HRESULT`, not `int`,** from HRESULT-returning APIs. `HRESULT` is
  blittable (a single `int` field) and exposes `.Succeeded` / `.Failed` /
  `.Value` / `.ThrowOnFailure()`. Use `HRESULT.S_OK` instead of `0`; cast
  `e.HResult` to `(HRESULT)` when wrapping. `AddRef` / `Release` return `uint`
  (the `IUnknown` contract).
- **Throw with `hr.ThrowOnFailure()`** rather than
  `if (hr.Failed) Marshal.ThrowExceptionForHR(hr)`. It is the idiomatic helper,
  produces the same `IErrorInfo`-enriched exception, and reads cleanly at the
  call site: `iface->Method(...).ThrowOnFailure();`. Branch on the raw `hr` only
  when you handle a specific code (e.g. `ERROR_INSUFFICIENT_BUFFER`) before
  throwing.
- **Prefer `T**` for raw pointer outputs and vtable signatures.** `out T*` is
  also blittable and represents the same native indirection, but it introduces
  managed by-reference syntax and cannot bind to helpers that expose `T**` or
  `void**`. Keep `out T*` only when the hand-written declaration intentionally
  wants C# definite-assignment semantics; match the raw ABI with `T**` by
  default.
- **Use `void*` for opaque / reserved parameters** and pass `null` literally -
  never round-trip through `IntPtr.Zero` inside `unsafe`.
- **Prefer `nint` / `nuint` for new native-sized values.** Keep
  `IntPtr` / `UIntPtr` only where an existing public contract or managed API
  requires those names (`Marshal.*`, `SafeHandle.DangerousGetHandle`, or a
  compatibility-sensitive public API), and convert once at that boundary.
- **Use the generated `PCWSTR` / `PWSTR` for wide strings,** never a managed
  `string`. The caller pins with `fixed (char* p = managedString)` (implicit on
  most overloads). Add the type to `NativeMethods.txt` if not yet generated.
- **No managed reference types** (`string`, `StringBuilder`, arrays) in a
  blittable signature.
- **Do not set `PreserveSig = true` on `[DllImport]`** - it is the default. Use
  `PreserveSig = false` only to force the marshaller to throw on failure
  HRESULTs (rare; prefer returning `HRESULT` and calling `.ThrowOnFailure()`).
  Note `[ComImport]` defaults the opposite way, but struct-based COM uses raw
  `delegate*` invocation and is unaffected.
- **Constrain flag / option parameters to a typed `enum`.** When a native
  `DWORD` / `ULONG` / `int` is documented as a `typedef enum` or a `#define`
  set, declare a matching C# `[Flags] enum Foo : uint` (same underlying type)
  and use it in the signature (and in the `delegate*` cast for a COM method).
  Mirror the constraint even when the native side has no named enum:
  `OpenScope(path, CorOpenFlags.ofRead, ...)` self-documents where
  `OpenScope(path, 0, ...)` does not. Co-locate the enum with its consumer.
