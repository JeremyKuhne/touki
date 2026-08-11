# Cross-assembly CCW composition

Detail for [cswin32-com](SKILL.md). The paired interop skill owns the general
owner/extender package model; this page covers the COM-specific boundary when
one package owns generated COM structs and another adds COM-callable wrappers or
helper behavior.

## The owner owns generated partial hooks

CsWin32's generated `IVTable<TComInterface, TVTable>` support initializes the
three `IUnknown` slots by calling `Windows.Win32.ComHelpers.PopulateIUnknown<T>()`.
That method delegates to a generated partial method named
`PopulateIUnknownImpl<T>()`. A partial implementation must be in the **same
assembly and compilation** as its declaration; an extender package cannot
implement the owner's hook.

If an owner publishes CCW-capable generated types, implement the hook in the
owner. On the .NET 10 leg, a `ComWrappers` provider can copy the runtime's
canonical `IUnknown` entry points into the generated vtable:

```csharp
using System.Collections;
using System.Runtime.InteropServices;

namespace Windows.Win32;

public static unsafe partial class ComHelpers
{
    static partial void PopulateIUnknownImpl<TComInterface>(
        System.Com.IUnknown.Vtbl* vtable)
        where TComInterface : unmanaged
        => IUnknownVtableProvider.Populate(vtable);

    private sealed class IUnknownVtableProvider : ComWrappers
    {
        public static void Populate(System.Com.IUnknown.Vtbl* vtable)
        {
            GetIUnknownImpl(out nint queryInterface, out nint addRef, out nint release);
            vtable->QueryInterface_1 =
                (delegate* unmanaged[Stdcall]<System.Com.IUnknown*, Guid*, void**, Foundation.HRESULT>)queryInterface;
            vtable->AddRef_2 =
                (delegate* unmanaged[Stdcall]<System.Com.IUnknown*, uint>)addRef;
            vtable->Release_3 =
                (delegate* unmanaged[Stdcall]<System.Com.IUnknown*, uint>)release;
        }

        protected override ComInterfaceEntry* ComputeVtables(
            object obj,
            CreateComInterfaceFlags flags,
            out int count)
            => throw new NotSupportedException();

        protected override object CreateObject(
            nint externalComObject,
            CreateObjectFlags flags)
            => throw new NotSupportedException();

        protected override void ReleaseObjects(IEnumerable objects)
            => throw new NotSupportedException();
    }
}
```

Gate this implementation with `#if NET`; .NET Framework does not provide the
real `ComWrappers` support needed to populate the vtable. Without the hook,
generated vtable creation throws `NotImplementedException` when it sees an
uninitialized `QueryInterface` slot; moving the same partial declaration into
an extender does not fix it.

## Extend owner types; do not redeclare them

A `partial struct` cannot span assemblies. When the owner already publishes
`IDispatch`, `IUnknown`, or another generated COM struct:

- add convenience behavior with a C# 14 extension block, using a `ref` receiver
  when the method needs the struct address;
- keep runtime pointers typed as the owner's imported struct;
- do not publish another struct with the same fully qualified name;
- put behavior shared by all extenders in the owner rather than duplicating it.

For example:

```csharp
public static unsafe class IDispatchExtensions
{
    extension(ref IDispatch dispatch)
    {
        public HRESULT InvokeMember(/* blittable parameters */)
        {
            fixed (IDispatch* pointer = &dispatch)
            {
                return pointer->Invoke(/* ... */);
            }
        }
    }
}
```

## Use a uniquely named CCW provider when necessary

An extender may need a writable vtable provider even though runtime calls use the
owner's imported interface. If the owner type cannot serve that role, define a
uniquely named implementation-only provider such as `IDispatchCcw` that
implements `IComIID` and `IVTable<TComInterface, TVTable>`. Its vtable contains
the interface-specific slots; the owner hook supplies the inherited `IUnknown`
slots.

A nested managed `[ComImport]` interface is acceptable when it exists solely to
ask the classic COM marshaller for a CCW or to describe the managed callback
contract. It is not the runtime pointer surface: native calls still go through
the owner-generated struct and raw vtable. This is the same narrow exception used
by the test bridge in [migration-and-testing.md](migration-and-testing.md). A
path that calls `Marshal.GetComInterfaceForObject` depends on built-in COM
interop and is not a NativeAOT path; use `ComWrappers` for the AOT-capable CCW
implementation.

When a manual lifetime wrapper is generic on its vtable type, use that **exact
same vtable type** in allocation and every callback that recovers the object or
updates its reference count. Do not substitute a layout-compatible generated
vtable type in `GetObject`, `AddRef`, or `Release`; generic type identity is part
of the wrapper contract even when the current native layouts happen to match.

Every AddRef'd pointer returned by a CCW helper is caller-owned. Scope it when
passing it to `Advise` or another retaining API, and pair successful connection
cookies with `Unadvise`; see [lifetime.md](lifetime.md).

## Validation

Test more than compilation:

1. construct the extender's COM interface table so the owner-side
   `PopulateIUnknownImpl` hook executes;
2. obtain a CCW pointer for a managed implementation and query the expected
   interface;
3. call through the owner-generated pointer struct and verify HRESULT behavior;
4. build all .NET 10 and .NET Framework TFMs so `ComWrappers`-dependent code is
   absent or explicitly unsupported on .NET Framework;
5. pack the owner and extender and compile a consumer that references only the
   extender package.

These checks catch the failure mode where project references compile but the
owner omitted its partial hook, the extender accidentally redeclared an imported
COM type, or the packaged dependency graph exposes two incompatible projections.
