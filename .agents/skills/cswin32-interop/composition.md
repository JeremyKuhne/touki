# Layered owner/extender composition

Detail for [cswin32-interop](SKILL.md). Use this model when one package owns the
public CsWin32 projection and another package adds APIs to that same callable
surface. This is different from sharing one internal projection with friend
assemblies: each layer runs CsWin32, but only one layer owns the receiver type.
For the broader managed-foundation / Win32-owner / domain-extender package
stack and release order, see [library-layering.md](library-layering.md).

## Choose the ownership boundary first

| Layer | Responsibility |
| --- | --- |
| Owner | Publishes the canonical `Windows.Win32.PInvoke` and shared projected or hand-authored Win32 types. |
| Extender | References the owner and projects additional APIs as C# 14 extension members on the owner's `PInvoke`. |
| Consumer | References the extender and calls owner and extender APIs through one `PInvoke` surface. |

The owner configuration names and publishes the receiver:

```json
{
  "$schema": "https://aka.ms/CsWin32.schema.json",
  "className": "PInvoke",
  "public": true,
  "allowMarshaling": false,
  "useSafeHandles": false
}
```

The extender uses a different host name and points `extensionReceiver` at the
owner type:

```json
{
  "$schema": "https://aka.ms/CsWin32.schema.json",
  "className": "Interop",
  "extensionReceiver": "PInvoke",
  "public": true,
  "allowMarshaling": false,
  "useSafeHandles": false
}
```

The extender project must reference the owner assembly at compile time and use a
C# version that supports extension blocks. Keep the CsWin32 package private in a
library package unless consumers need it directly; generated source is compiled
into the extender assembly.

## What composes, and what does not

- Call generated methods and read generated values through `PInvoke` in normal
  runtime code. The extender's `Interop` type is the generated implementation
  host, not a second public facade for ordinary calls.
- A generated constant remains a real `const` on the extender host, while its
  `PInvoke` projection is an extension property. Extension properties are not
  compile-time constants. Enum initializers, `case` labels, constant patterns,
  attributes, and optional-parameter defaults therefore use
  `Interop.CONSTANT`; runtime expressions use `PInvoke.CONSTANT`.
- Composition is receiver- and namespace-specific. A second projection root
  such as `Windows.Wdk.Interop` does not automatically appear on
  `Windows.Win32.PInvoke`; keep it separate or design another explicit receiver.
- `partial` does not cross assembly boundaries. Do not try to augment an
  owner-generated struct or `PInvoke` with a downstream `partial` declaration.
  Put shared public types in the owner and add downstream behavior with C# 14
  extension blocks. Give implementation-only provider types unique names so a
  consumer never sees two public types with the same fully qualified name.
- Read the generated extender output when overload resolution changes. A
  friendly generated overload can become ambiguous with a downstream helper;
  call the raw pointer overload explicitly or rename the helper rather than
  introducing another facade.
- Re-audit native ownership whenever a helper or call shape moves between
  layers. Generated overloads do not preserve allocator, COM reference, or
  byte/element obligations by themselves; use
  [ownership-and-units.md](ownership-and-units.md).

## Generated XML documentation diagnostics

Some CsWin32 versions emit cross-assembly XML `cref` values for
`extensionReceiver` members that Roslyn cannot resolve, producing `CS1574` or
`CS1580` from generated code. First verify the diagnostics originate only in
CsWin32 output and that the referenced API compiles. If so, suppress exactly
those two diagnostics in the extender project and record why. A project-level
suppression also applies to hand-authored source, so keep normal documentation
review in the validation path rather than disabling XML warnings broadly.

## Verify the packaged composition

A same-solution build is necessary but not sufficient. Pack both layers, then
build a scratch consumer that references **only the extender package** and
compiles at least:

1. one owner-provided `PInvoke` call;
2. one extender-provided `PInvoke` call;
3. any public extension member added to an owner-provided generated type.

Restore into a clean package cache when reusing a local version number. This
catches missing transitive dependencies, stale packages, duplicate generated
types, inaccessible receivers, and composition that worked only through project
references. Inspect the packed dependency graph too: an extender that directly
uses a foundation library should declare it directly, even when the owner also
depends on it.
