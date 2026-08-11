# Migration parity and testing

Detail for [cswin32-com](SKILL.md). Two things bite when moving off `[ComImport]`
and RCWs: preserving the old throw-versus-return behavior, and mocking a
struct-based interface in tests.

## Error-handling parity when migrating

Struct-based COM returns a raw `HRESULT`; `[ComImport]` and built-in activation
threw automatically. Preserve the old throw-versus-return at each call site:

| Old shape | Threw via | Migrated shape |
| --- | --- | --- |
| `new SomeCoClass()` | built-in activation | `CoCreateInstance(...).ThrowOnFailure()` |
| `(IFoo)rcw` cast | `InvalidCastException` on QI | `QueryInterface(&iid, scope).ThrowOnFailure()` |
| `[ComImport]` method, default `PreserveSig=false` | marshaller throws on `FAILED(hr)` | `.ThrowOnFailure()` at the call site |
| `[ComImport]` method with `[PreserveSig]` | caller inspects the return | mirror the existing `hr` branch - do not start throwing |

**Factory exception:** when the old contract returned `null` for legitimate
rejection (e.g. `Create(path)` on bad input), keep the null-return only for that
path; activation and QI failures still throw.

**A swallowing caller does not define a shared helper's contract.** If the
operation documents an expected "absent" result, an exception-free
`Try*` / `false` / `default` path can avoid allocation and preserve the raw
`HRESULT`. Otherwise retain `.ThrowOnFailure()` even when one top-level caller
catches `COMException`. Change a shared helper only after auditing every caller
and preserving its public failure contract.

## Mocking struct-based COM in tests

Struct-based calls go through raw vtable pointers, so a managed mock cannot be
passed where a `T*` is expected. Bridge with the built-in COM marshaller: the mock
implements the *managed* interface (a BCL
`System.Runtime.InteropServices.ComTypes.*` interface when ABI-compatible - true
for `ITypeLib` / `ITypeInfo` - otherwise CsWin32's nested `[ComImport] internal
interface Interface`), and the test passes a **CCW** pointer to the code under
test:

```csharp
// MockTypeLib : ComTypes.ITypeLib  (a normal managed class)
nint ccw = Marshal.GetComInterfaceForObject(mock, typeof(ComTypes.ITypeLib));
try
{
    walker.Analyze((ITypeLib*)ccw);
}
finally
{
    Marshal.Release(ccw);
}
```

The CCW exposes a real vtable, so no hand-rolled native vtable is needed. But the
mock must now behave like real COM:

- **Every `[out]` param is written**, even ones the caller ignores - passing
  `null` for a trailing out-param faults the marshaller.
- **Exception identity is lost.** An HRESULT-returning method resurfaces a thrown
  exception as a *new* `COMException` (HRESULT preserved, instance not); a `uint`
  / `void` method with no HRESULT channel swallows it. Assert on type or count,
  not the injected instance.
- **Throw `COMException` on bad input; do not `Assert`** inside the mock - an
  assertion exception escapes the code-under-test's `catch (COMException)`, while
  a `COMException` becomes a failing HRESULT it handles normally.

The managed `[ComImport]` interface in this bridge is deliberately not the
runtime pointer API. Keep native calls on the generated struct. This classic
marshaller bridge is for tests or other built-in-COM paths, not NativeAOT. In an
owner/extender package design, the owner must also provide the generated
`PopulateIUnknownImpl` partial hook before an extender's `IVTable` provider can
construct a CCW; see [ccw-composition.md](ccw-composition.md).
