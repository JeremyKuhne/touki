# Native ownership and size units

Detail for [cswin32-interop](SKILL.md). Generated signatures make calls easier to
write; they do not infer who owns an output or whether a length is bytes,
characters, or elements. Read the native contract before choosing a wrapper.

## Record the ownership contract before calling

For every pointer or handle output, identify all four facts:

1. who allocates or increments the reference;
2. whether the result is owned or borrowed;
3. which operation releases it;
4. whether failure guarantees the output is initialized.

Common pairs include:

| Acquired value | Release operation |
| --- | --- |
| AddRef'd COM interface | `IUnknown::Release` (prefer a repository-provided owned-pointer scope) |
| COM task allocator memory, including many Shell PIDLs | `CoTaskMemFree` |
| `LocalAlloc` memory | `LocalFree` |
| `BSTR` | `SysFreeString` / `Marshal.FreeBSTR` |
| Owned Win32 handle | The API-specific close function, such as `CloseHandle` |
| Borrowed pointer/handle | No release; keep the owner alive for the whole use |

Do not infer the deallocator from the projected pointer type. Two `PWSTR` values
can have different allocators depending on the API that returned them.

## Cleanup must cover failure paths

A native API may leave an output untouched when it fails. If cleanup runs in a
`finally`, initialize the actual native output storage to null and prefer the raw
pointer overload so that initialization cannot be hidden by a convenience
wrapper:

```csharp
ITEMIDLIST* itemIdList = null;
HRESULT result;
fixed (char* pathPointer = path)
{
    result = PInvoke.SHParseDisplayName(
        pathPointer,
        null,
        &itemIdList,
        0,
        null);
}

try
{
    result.ThrowOnFailure();
    // Consume itemIdList.
}
finally
{
    PInvoke.CoTaskMemFree(itemIdList);
}
```

Freeing null is safe for the matching Windows allocators. Inspect generated
convenience-overload source before relying on its failure initialization or
ownership behavior.

## A retained COM pointer is still caller-owned

A helper that returns an AddRef'd COM pointer gives the caller one reference.
Passing it to a method such as `Advise`, `SetClientSite`, or another retaining
API does not transfer that caller reference. The callee adds its own reference
when its contract retains the pointer; the caller still releases its reference.
In the example, `AcquireOwnedCcwPointer<T>()` is pseudocode for a repository
helper that returns one caller-owned CCW interface pointer. Its implementation
may use classic COM marshalling or `ComWrappers`; only the latter can support a
NativeAOT path. Use the repository's owned-pointer scope when available;
otherwise make the release explicit:

```csharp
IEventSink* sink = AcquireOwnedCcwPointer<IEventSink>(managedSink);
try
{
    source->Advise(sink, &cookie).ThrowOnFailure();
}
finally
{
    if (sink is not null)
    {
        sink->Release();
    }
}
```

Pair every successful cookie-producing `Advise` with `Unadvise(cookie)` before
releasing the source object. Do not confuse a non-retaining parameter with a
borrowed pointer: an owned pointer passed to a non-retaining call still needs its
caller reference released, while a pointer that is itself borrowed must not be
released. Keep the borrowed pointer's owner alive for the whole call, or call
`AddRef` first when an owned scope is required.

## Length parameters: bytes are not elements

Read each length parameter's native documentation. A managed buffer abstraction
usually reports **elements**, while many NT and Win32 APIs accept and return
**bytes**. Convert explicitly with checked arithmetic:

```csharp
uint capacityInBytes = checked((uint)buffer.Length * sizeof(char));

// Native call writes requiredBytes.
int requiredElements = checked(
    (int)(((ulong)requiredBytes + sizeof(char) - 1) / sizeof(char)));
buffer.EnsureCapacity(requiredElements);
```

Use ceiling division for a required **capacity** because an odd byte count still
needs another element. For a payload length that must contain whole elements,
validate divisibility when malformed native data is possible, then divide
exactly. Also distinguish whether a returned byte count includes a header,
terminator, or only payload; size the whole native buffer but slice only the
payload field.

## Review and test shapes

When adding or migrating an interop call, cover the branches that expose the
contract:

- success and native failure, including cleanup after each;
- null/default output and double-dispose or repeated cleanup when supported;
- a result large enough to force buffer growth;
- a malformed or odd-sized payload when the input is not trusted;
- a retaining COM call followed by explicit disconnect and owner disposal.

A happy-path build cannot detect a leaked reference, the wrong allocator, or a
byte/element mismatch that only appears beyond the initial buffer.
