# IComIID across target frameworks, and CLS compliance

Detail for [cswin32-com](SKILL.md). `IComIID` is the interface CsWin32 uses to
recover a COM type's IID generically. A support helper such as `IID.Get<T>()`
can expose a `Guid*` to the type's `IComIID.Guid`. Its shape differs by target
framework. This page assumes the skill baseline, Microsoft.Windows.CsWin32
0.3.296 or later.

## Generated interfaces

The generator emits `IComIID` and attaches it to **every** generated COM struct
on both supported runtime families - a static-abstract
`static ref readonly Guid Guid` on the .NET 10 leg, and an instance
`ref readonly Guid Guid` on .NET Framework. Nothing to hand-author: adding a
generated COM type to `NativeMethods.txt` carries `IComIID` through an
`IComIID`-constrained scope automatically on both legs. A support helper reads
it via `T.Guid` on .NET 10 and `default(T).Guid` on .NET Framework.

Only **manual** structs implement `IComIID` by hand, matching the per-TFM shape
and storage strategy: compiler-emitted RVA data on `NET`, initialized static
`Guid` storage on non-`NET` (see [manual-structs.md](manual-structs.md)).

This both-family generation was added by
[microsoft/CsWin32#1705](https://github.com/microsoft/CsWin32/pull/1705).

## CLS compliance

A CLS-compliant assembly (`[assembly: CLSCompliant(true)]`) trips `CS3016` on
generated COM wrappers that carry CCW thunks (the `[UnmanagedCallersOnly(...)]`
array argument). For **internal** wrappers, the generator suppresses `CS3016`
in generated source. Internal projections therefore need no consumer
workaround. The .NET Framework target does not emit the thunks and needs no
suppression.

CsWin32 deliberately does not annotate **public** wrappers: the consuming
library owns its public CLS contract. In a CLS-compliant assembly with a public
projection, decide whether each wrapper belongs in the public surface. Mark an
intentionally non-CLS wrapper with a hand-authored `[CLSCompliant(false)]`
partial, reduce its visibility, or otherwise address the public contract rather
than applying a blanket suppression. See the overlay and
[microsoft/CsWin32#1706](https://github.com/microsoft/CsWin32/pull/1706).
The underlying array-argument diagnostic is tracked by
[dotnet/roslyn#68526](https://github.com/dotnet/roslyn/issues/68526).
