# Lifetime and access

Detail for [cswin32-com](SKILL.md). Struct-based COM has no runtime to release
references for you. Release every reference the caller owns, and never release a
borrowed pointer without first acquiring a reference.

## Where a COM pointer may live

A raw `T*` (a CsWin32 COM struct pointer) is simplest as a local in an `unsafe`
method, a parameter, or a field of a `ref struct`. Avoid storing one directly in
a class or non-`ref` struct by default: the owner must track whether the pointer
is owned or borrowed, enforce the interface's apartment and thread contract,
release an owned reference on the correct apartment, and prevent access after
disposal. A finalizer on an arbitrary thread is not a substitute for releasing
an apartment-affine pointer on its owning apartment. Use a field-lifetime
strategy below unless the type explicitly enforces all of those invariants.

## ComScope<T> - transient pointers

The examples use a support-library `ref struct` that owns one AddRef'd reference
and calls `Release` on dispose. Always use it in a `using` declaration. It is the
default for an **owned** pointer that does not outlive the current method. The
conventional type implicitly converts to `T**` and `void**`, so an out-param
writes straight into the scope:

```csharp
using ComScope<ISomething> scope = new();
factory.Pointer->QueryInterface(IID.Get<ISomething>(), scope).ThrowOnFailure();
scope.Pointer->DoThing(...);
```

- Access methods via `scope.Pointer->Method(...)`; null-check with
  `scope.IsNull`.
- Applies to every COM out-param whose contract returns an owned reference -
  `CoCreateInstance`, `QueryInterface`, many `IEnumXxx::Next` and factory
  methods, and app-local `STDAPI Get*` functions. Read the contract before
  scoping an unusual borrowed output. Declare raw outputs with `T**` when the
  scope's implicit pointer-to-pointer conversion should bind.

## Passing pointers to retaining APIs

A helper that creates a CCW or queries an interface normally returns one owned,
AddRef'd pointer. Passing that pointer to `Advise`, `SetClientSite`, or another
API that retains it does **not** transfer the caller's reference. When the call
succeeds and its contract retains the pointer, the callee acquires its own
reference; scope and release the caller's reference after the call:

```csharp
using ComScope<IEventSink> sink = new(AcquireOwnedCcwPointer<IEventSink>(managedSink));
source.Pointer->Advise(sink.Pointer, &cookie).ThrowOnFailure();
```

The same scope is appropriate when an **owned** pointer is passed to a
non-retaining call such as a verb or callback: the scope releases the caller's
reference afterward. A pointer that is itself borrowed is different. Do not put
it in a releasing scope; keep its owner alive for the call, or call `AddRef`
before creating an owned scope.

A successful cookie-producing subscription is a second lifetime edge. Store the
cookie and call `Unadvise(cookie)` before releasing the source object. Use a
`try`/`finally` in disposal so failure to disconnect cannot prevent releasing the
source pointer. If subscription failure is deliberately non-fatal, initialize
the cookie to zero and skip disconnect when no cookie was issued.

## Ownership transfer out of a helper

A helper that acquires a pointer can return `ComScope<T>` directly; intermediate
pointers stay in their own `using` scopes and release on the helper's `return`. A
`default` `ComScope<T>` is null and its `Dispose` is a no-op, so callers need no
extra null guard:

```csharp
private static ComScope<ISomething> Acquire()
{
    ComScope<ISomething> result = new();
    Guid clsid = SomeCoClass.CLSID;
    HRESULT createResult = PInvoke.CoCreateInstance(
        &clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.Get<ISomething>(), result);
    if (createResult.Failed)
    {
        result.Dispose();
        return default;
    }

    return result;
}
```

Disposing the scope on failure covers APIs that leave a non-null output despite
failing. On success, returning the scope transfers that one owned reference to
the caller. The concrete support type must make `default` null and disposal a
no-op for this shape to be valid.

## BSTR - COM strings

A COM `BSTR` out-param is owned by the caller and must be freed with
`SysFreeString` (equivalently `Marshal.FreeBSTR`). CsWin32 generates `BSTR` as a
plain value - it does **not** implement `IDisposable` - so free it explicitly:

```csharp
BSTR version = default;
try
{
    instance.Pointer->GetVersion(&version).ThrowOnFailure();
    string managed = version.ToString();
}
finally
{
    Marshal.FreeBSTR((nint)version.Value);
}
```

A repo may extend `BSTR` with `IDisposable` (a `Dispose` that calls
`SysFreeString` / `FreeBSTR` and clears the field), enabling the same scoped
shape as `ComScope<T>` - `using BSTR version = default;` - the recommended
convenience where available. Because such a `Dispose` writes through the storage
in place, the `using` must be on the storage location, not a method-returned
copy.

## A COM pointer that outlives a method

Choose a holder that matches the interface's apartment contract:

- A same-apartment owner may keep an owned pointer only when it enforces access
  and deterministic disposal on that apartment.
- For cross-apartment access, marshal the interface. A Global Interface Table
  holder stores one registered reference and retrieves an apartment-appropriate
  proxy into a short-lived owned scope for each call. The GIT does not make the
  underlying object agile.
- For an interface documented as agile, a holder may use an agile-reference or
  equivalent strategy, but it must still own and release the correct reference.

When the source pointer is already owned by a `ComScope<T>` that will release,
register without taking that caller reference; the GIT acquires its own. Take
ownership only when no other code path will release the source reference. Every
holder needs deterministic disposal, and any finalizer fallback must be valid
for the chosen marshalling strategy. The concrete holder type is
repository-specific - see the overlay.
