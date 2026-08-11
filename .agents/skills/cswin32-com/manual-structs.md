# Manual COM structs

Detail for [cswin32-com](SKILL.md). For an interface not in Win32 metadata, such
as Setup Configuration or a private / third-party interface, hand-write a struct
that lays out the vtable yourself. Put each interface in its own file. Include
or exclude that file using the same platform and target-framework decisions as
the paired interop skill; a cross-platform assembly may compile the declaration
as long as reachable calls remain Windows-only.

## Shape

Replace the placeholder with the interface's exact IID.

```csharp
internal unsafe struct IPrivateService : IComIID
{
#if NET
    static ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // 00112233-4455-6677-8899-AABBCCDDEEFF in native Guid layout.
            // In a ReadOnlySpan<byte> initializer, the compiler emits the bytes as RVA data (no runtime allocation).
            ReadOnlySpan<byte> data = new byte[]
            {
                0x33, 0x22, 0x11, 0x00,
                0x55, 0x44,
                0x77, 0x66,
                0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff
            };

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }
#else
    private static readonly Guid s_iid = new("00112233-4455-6677-8899-AABBCCDDEEFF");

    readonly ref readonly Guid IComIID.Guid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef(in s_iid);
    }
#endif

    private readonly void** _lpVtbl;

    public HRESULT QueryInterface(Guid* riid, void** ppvObject)
    {
          fixed (IPrivateService* pThis = &this)
              return ((delegate* unmanaged[Stdcall]<IPrivateService*, Guid*, void**, HRESULT>)
                _lpVtbl[0])(pThis, riid, ppvObject);
    }
    // AddRef @1, Release @2, then interface methods at their slot indices.
}
```

## Rules

- **`delegate* unmanaged[Stdcall]`** for the function-pointer cast. IDL
  `STDMETHODCALLTYPE` is `__stdcall` on Win32; the wrong convention silently
  corrupts the stack. This works on .NET Framework too (it lowers to an IL
  `calli`).
- **Vtable slots are exact and IUnknown-relative:** slot 0 = `QueryInterface`,
  1 = `AddRef`, 2 = `Release`, 3+ = interface methods in IDL order. When the
  interface derives from another, **add the parent's method count** first (e.g. a
  `v2` method after three `IUnknown` and three `v1` methods is at slot 6). Unused
  slots may be omitted as long as the ones you call have the correct index.
- **`IComIID` has two shapes** - a static-abstract `static ref readonly Guid
  IComIID.Guid` on the .NET 10 leg, and an instance
  `readonly ref readonly Guid IComIID.Guid` on .NET Framework. A dual-target
  manual struct spells out **both** arms (`#if NET` / `#else`). A .NET 10-only
  struct needs no conditional; a Framework-only struct uses only the
  instance form. CsWin32 handles this automatically for *generated* structs -
  see [comiid-and-cls.md](comiid-and-cls.md).
- **Choose IID storage by target.** On `NET`, use compiler-emitted RVA data: a
  constant 16-byte `ReadOnlySpan<byte>` returned by ref through
  `MemoryMarshal.GetReference` and `Unsafe.As<byte, Guid>`, as CsWin32 does. In
  native `Guid` layout, the 32-bit, 16-bit, and 16-bit components are
  little-endian; the final eight bytes retain display order. A non-`NET` target
  cannot use the RVA pattern; keep an initialized static `Guid` and return it by
  ref through `Unsafe.AsRef`, matching the down-level Madowaku pattern. Add a
  test on every target that dereferences the repository's `IID.Get<T>()`
  equivalent and compares it with `new Guid("...")` so byte-order or branch
  drift cannot survive review. Preserve a separately named public `IID_Guid`
  field only when an existing API contract requires that name.
- **Use the generated `PCWSTR` / `PWSTR`** for wide-string parameters (add them
  to `NativeMethods.txt`); raw `char*` with `fixed` only where no typed
  equivalent exists.
- **Vtable methods are blittable** - the same rules as any `[DllImport]`; see the
  blittable-signatures page of the paired cswin32-interop skill (`HRESULT`
  return, raw `T**` pointer outputs, `void*`, typed enum parameters).
- **Platform annotations may be type- or member-level.**
  `[SupportedOSPlatform]` is valid on structs. Apply it to the whole interface
  struct when every member has the same Windows contract, or to individual
  methods when versions differ. In dual-target source, place the attribute under
  `#if NET` unless the .NET Framework target provides a compatible source
  polyfill; .NET Framework reference assemblies do not define it.

## Strongly-typed handle and token wrappers

When the native side `typedef`s a primitive into a family of "same shape,
different meaning" aliases (e.g. `typedef ULONG32 mdToken; typedef mdToken
mdAssembly; ...`), mirror the hierarchy with distinct `readonly struct` wrappers,
each holding the single underlying primitive - the same pattern as CsWin32's
`HANDLE` / `HWND` / `HMODULE`. It stays blittable, so `delegate*` casts and arrays
cost nothing at the boundary. Conversions follow the typedef hierarchy:
**implicit** widening to the base (always safe), **explicit** narrowing (opt-in,
because the C side cannot enforce the kind at the cast site). Check the native
header for the canonical validation primitives before writing `IsNil` /
`IsValid` - the encoding often hides in macros (for `mdToken`, `IsNilToken` tests
the rid half, `RidFromToken(tk) == 0`, not the whole value, and a per-type nil
such as `mdAssemblyNil = 0x20000000` is the table tag, not `0`).
