# Platform and target-framework guards

Detail for [cswin32-interop](SKILL.md). CsWin32 projects Windows APIs. Decide
source inclusion, runtime reachability, and the platform contract independently;
one guard does not automatically satisfy all three.

## Choose the guard from the constraint

| Constraint | Compile-time action | Runtime action |
| --- | --- | --- |
| Windows-specific TFM or assembly | Usually none; the assembly already carries the platform context. | None unless the API requires a newer Windows version than the assembly contract. |
| Cross-platform assembly; declarations compile on both runtime families | None. | Guard every reachable call with a recognized Windows check, or annotate the containing API as Windows-only. |
| A target intentionally omits the interop source or one of its dependencies | Exclude whole files with conditional `Compile` items, or use a documented project symbol for smaller regions. | Still guard calls on any included target that can run cross-platform. |
| A member exists only on the .NET 10 leg | Use `#if NET`. | Independent of the platform guard. |
| A member or polyfill exists only on .NET Framework | Use a Framework-only source folder or `#if NETFRAMEWORK`. | Independent of the platform guard. |

Generated P/Invoke declarations are not themselves a reason to remove source
from non-Windows builds. Use compile-time exclusion only when the project has a
real source or dependency constraint. When a whole file is conditional, prefer
an MSBuild item condition over a whole-file `#if`. Namespace `using` directives
and private helpers referenced only by a conditional region must use the same
condition. The overlay names any custom symbol or item condition.

For a cross-platform binary, use a guard recognized by the platform analyzer:

```csharp
#if NET
if (OperatingSystem.IsWindows())
#else
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
{
    _ = PInvoke.GetCurrentProcessId();
}
else
{
    // Cross-platform fallback.
}
```

Use `OperatingSystem.IsWindows()` on .NET 10 and
`RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` on .NET Framework. If the
interop call is also conditionally compiled, keep the compile condition outside
the runtime branch so the fallback remains complete when that source is absent.

## CA1416 platform compatibility

For a cross-platform assembly, satisfy the analyzer semantically rather than
suppressing it:

- Use `OperatingSystem.IsWindows()` or
  `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`. Annotate a custom
  boolean field, property, or method with
  `[SupportedOSPlatformGuard("windows")]` before relying on it as a guard.
- Apply `[SupportedOSPlatform("windows")]` to an assembly, type, or member when
  its contract is Windows-only. The attribute is valid on structs. Include a
  version only when the API has that real minimum, and match the documented
  version instead of applying one baseline to every CsWin32 call.
- Use `#pragma warning disable CA1416` only as a narrow, justified last resort
  when a recognized guard or platform annotation cannot express the contract.

The platform attributes are available on modern .NET but not in .NET Framework
reference assemblies. In dual-target source, place them under `#if NET` unless
the Framework target provides a compatible source polyfill.

## A stack-first scratch buffer

Win32 string and buffer APIs often want caller-allocated storage followed by a
length retry. Check for a CsWin32 `Span<T>` convenience overload before writing
a `fixed` block. For a bounded common case, start with a small stack span and
rent from `ArrayPool<T>` when the required length exceeds it. Choose the stack
size from element size, call depth, measured workloads, and the repository's
stack budget; there is no universal byte cutoff.

A repository may provide a disposable `ref struct` that combines the stack and
pool paths. Always dispose that scope so a rented array is returned. The
`scratch-buffer-strategy` skill covers the choice when installed, and the
overlay names any concrete helper. On retry, preserve the native API's exact
length semantics: bytes versus elements, and whether the required count includes
a terminator or header.

## Verify the guarded build

Anything referenced **only** inside a compile condition must itself be inside
that condition, or the excluded build can fail under warnings-as-errors. Watch
for `IDE0005` (unused `using`), `IDE0051` / `IDE0052` (unused private members),
`CA1823` (an unused private field), and `CS1587` (an XML doc comment left before
a conditional member).

Build all .NET 10 and .NET Framework TFMs and every custom-symbol state that
changes source inclusion. For a cross-platform binary, also execute an
unsupported-OS path and verify it takes the fallback without loading or calling
the Windows API.
