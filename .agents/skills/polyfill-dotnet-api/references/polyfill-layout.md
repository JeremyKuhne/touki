# .NET Framework Polyfill Layout

When `touki` is consumed from the .NET Framework target (`net472`), the
package supplies polyfills for many modern .NET BCL members so the same
source code compiles and runs on both targets.

## Where the polyfills live

All polyfills live under [touki/Framework/Polyfills/](../../../../touki/Framework/Polyfills/),
organized into one folder per BCL namespace:

```text
touki/Framework/Polyfills/
├── System/                           # System.* polyfills
├── System.Buffers.Text/
├── System.Diagnostics/
├── System.Globalization/
├── System.Numerics/
├── System.Reflection/
├── System.Runtime.CompilerServices/
├── System.Runtime.InteropServices/
├── System.Security.Cryptography/
├── System.Text/
└── System.Threading/
```

Each file declares the same namespace as the BCL surface it's polyfilling
(e.g. `Convert.ToHexString` extensions live in
`Polyfills/System/ConvertExtensions.cs` with `namespace System;`). Mirroring
the BCL namespace means callers reach the polyfill through the same
`using System;` they already have for the BCL type.

Anything outside `Polyfills/` is **not** a polyfill of public BCL
surface - it is either Touki-specific functionality
(under `touki/Framework/Touki/`) or shared library code (under
[touki/Touki/](../../../../touki/Touki/)).

## How polyfill members coexist with future BCL members

### Extension members on existing BCL types (the common case)

Most polyfills are C# 14 `extension` blocks targeting an existing BCL type
(`Convert`, `Random`, `Encoding`, `HashCode`, `ReadOnlySpan<T>`, ...).
The wrapper class lives in the BCL namespace but adds **members**, not a
new type.

If a future Microsoft package backports the same member to net472,
**the BCL member wins lookup automatically** - instance and static
members defined on the type itself bind in preference to extension members.
The polyfill silently becomes inert and can be removed in a later Touki
release. **No `extern alias` is needed for callers** in this case.

### New types in `System.*` namespaces (the alias case)

A few polyfills introduce a *type* that doesn't exist on net472 at all,
so Touki adds it:

- `System.Threading.Lock` (and nested `Scope`/`State`/`ThreadId`/`TryLockResult`)
- `System.Numerics.BitOperations`
- `System.Runtime.InteropServices.NativeMemory`
- `System.Security.Cryptography.CryptographicOperations`
- `System.Runtime.CompilerServices.DefaultInterpolatedStringHandler`
- `System.Diagnostics.AssertInterpolatedStringHandler`
- `System.ISpanFormattable`
- `System.SpanSplitEnumerator<T>` (via `SpanExtensions`)

If a future Microsoft package ships these same types on net472, your
project will see two types with the same fully qualified name (one from
`KlutzyNinja.Touki`, one from the new package). The compiler reports
**CS0436** or **CS0433** - *the type exists in multiple referenced
assemblies*.

To pick a specific one, use **`extern alias`**:

```xml
<!-- In the consumer csproj: tag the Touki reference with an alias. -->
<ItemGroup>
  <PackageReference Include="KlutzyNinja.Touki" Aliases="touki" />
</ItemGroup>
```

```c#
// In any source file that needs to disambiguate:
extern alias touki;

using BclLock = global::System.Threading.Lock;       // BCL version
using TukLock = touki::System.Threading.Lock;        // Touki version

BclLock myLock = new();
TukLock theirLock = new();
```

`extern alias` is a one-time, per-source-file declaration. Files that
don't reference the conflicting type don't need it. In practice you'd
only add the alias for the brief window between "Microsoft ships the
polyfill on net472" and "I update Touki to a version that defers to it".

## Touki's commitment to deferring to .NET when it ships

Touki's polyfills exist to fill gaps in net472. **When Microsoft ships
official polyfills for the same APIs (via `System.Memory`,
`Microsoft.Bcl.*`, or new packages), Touki releases will defer to those
packages.** Concretely:

- A new Touki release will add a `<PackageReference>` to the official
  package on net472.
- The matching member or type in `touki/Framework/Polyfills/<Namespace>/`
  will be removed (or, when needed, replaced with a thin forwarder) so
  that callers automatically pick up the Microsoft-shipped version.
- This is a non-breaking change for callers using polyfilled members
  through extension lookup. Callers using a polyfill *type* (the alias
  case above) may need to adjust the alias and a small number of source
  files when the Touki type is removed; the release notes will call out
  these cases.

If you're actively using a polyfilled type and want to start migrating
ahead of a Touki release, prefer the official Microsoft package today
and use `extern alias` to disambiguate as shown above.
