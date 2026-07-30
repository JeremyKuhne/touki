# Types and constants

Detail for [cswin32-interop](SKILL.md). Prefer the CsWin32 projection over any
hand-written copy, and let the typed value flow as far as possible before
casting.

## Prefer the generated projection - grep the metadata first

Before defining a `private const int ERROR_*` or a `private enum FooFlags`,
search the generated metadata; CsWin32 almost certainly already projects it:

- `ERROR_*` (Win32 error codes) -> `WIN32_ERROR.ERROR_*` (a `uint` enum)
- `HRESULT` codes -> `HRESULT.S_OK`, etc.
- File flags -> `FILE_FLAGS_AND_ATTRIBUTES`, `FILE_ACCESS_RIGHTS`,
  `FILE_SHARE_MODE`, `FILE_CREATION_DISPOSITION`
- Process flags -> `PROCESS_CREATION_FLAGS`, `STARTUPINFOW_FLAGS`,
  `PROCESS_ACCESS_RIGHTS`
- Memory mapping -> `PAGE_PROTECTION_FLAGS`, `FILE_MAP`
- Standard handles / known folders -> `STD_HANDLE`, `KNOWN_FOLDER_FLAG`

Add the name to `NativeMethods.txt` if not yet generated, then read the emitted
`Windows.Win32.<Name>.g.cs` under the generator's output for the exact shape.

In a layered `extensionReceiver` project, the real `const` remains on the
extender's generated host (for example, `Interop.VALUE`); the member projected
onto the owner's `PInvoke` is an extension property. Use the host in enum
initializers, `case` labels, constant patterns, attributes, and optional defaults,
because an extension property is not a compile-time constant. Use the unified
`PInvoke.VALUE` surface in runtime expressions. See
[composition.md](composition.md).

## Some flags are standalone constants, not enum members

A Win32 `#define` that sits outside a `typedef enum` generates as a `const` on
the configured generated host, not as an enum member. Its visibility follows
the CsWin32 `public` setting. Add the **constant name** to `NativeMethods.txt`
like any API, then OR it onto an enum of matching width and cast back where
needed. Do not reintroduce a local `const` the generator already emits.

## Match local types to the generated type

Declare the CsWin32 type and let it flow, instead of casting to `int` / `uint`
at every use:

```csharp
// Keep the generated type rather than storing the result as int.
WIN32_ERROR result = PInvoke.RmStartSession(...);

// Cast only at a non-CsWin32 boundary.
Win32Exception exception = new((int)result, ...);
```

The same applies to `HRESULT`, `BOOL`, `HANDLE`, and every flag enum. **Delete
local mirror enums** that exist only to duplicate a Win32 one - the generated
type is the source of truth.

## Type conversions

- `HANDLE` <-> `nint`: `(HANDLE)ptr` / `(nint)h.Value`. Sentinels:
  `HANDLE.Null`, `HANDLE.INVALID_HANDLE_VALUE`.
- `BOOL` is a struct - use its implicit conversion to `bool`, never a raw
  `int` / `uint`.
- `HRESULT` - check `.Succeeded` / `.Failed`; cast to `int` only at a boundary
  with a non-CsWin32 API.
- Nullable value-type parameters: `(SECURITY_ATTRIBUTES?)null`.
- **Anonymous unions** surface as nested `Anonymous` members
  (`info.Anonymous.Anonymous.wProcessorArchitecture`) - read the generated
  `*.g.cs` for the exact path.
- **Enum flags:** test with bitwise `&`. `Enum.HasFlag` boxes on .NET Framework;
  avoid it on hot paths. A repo may ship allocation-free flag extensions - see
  the overlay.

## FILETIME

`FILETIME` is a split 32/32 value; convert with a helper, never hand-roll
`DateTime.FromFileTime((long)hi << 32 | lo)`. Note CsWin32 uses
`ComTypes.FILETIME` (int fields) for COM members and
`Windows.Win32.Foundation.FILETIME` (uint fields) for kernel ones; a shared
extension usually targets `ComTypes.FILETIME`. Read the producing API's time
contract and choose the desired managed result deliberately. `GetFileTime` and
the creation / exit outputs from `GetProcessTimes` are UTC-based timestamps: use
`FromFileTimeUtc` to preserve UTC, or `FromFileTime` only when intentionally
converting the result to local time. The kernel / user outputs from
`GetProcessTimes` are elapsed 100-nanosecond durations, not dates; combine their
64-bit values and convert them to `TimeSpan` ticks. Do not infer time-zone or
duration semantics from the projected struct shape.

## Native integers

Prefer `nint` / `nuint` for new native-sized values. Preserve
`IntPtr` / `UIntPtr` where an existing public contract or managed API requires
those names, and convert at that boundary rather than carrying both forms
through the implementation. Note that `nint` does not implement
`IEquatable<nint>` on .NET Framework, so a generic method constrained
`where T : unmanaged, IEquatable<T>` cannot be instantiated with `nint` for a
cross-target call site - pick a concrete value type or split the path by TFM.
