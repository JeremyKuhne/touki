---
compatibility: Requires the .NET SDK, package restore access, and a project targeting .NET Framework alongside modern .NET.
description: Set up and use the standard .NET downlevel polyfill stack so a multi-targeted library can call modern BCL APIs on .NET Framework - PolySharp for compiler and language attributes, the official Microsoft downlevel NuGet packages (System.Memory, Microsoft.Bcl.Memory, Microsoft.Bcl.HashCode, Microsoft.IO.Redist, and the other Microsoft.Bcl.* / System.* backports), and the KlutzyNinja.Touki package's runtime polyfills layered on top. Use when adding a downlevel net472 / net481 target, choosing which package supplies a missing type before hand-rolling, configuring PolySharp, or checking whether an API is already polyfilled. For authoring a new hand-rolled polyfill inside a repo's own Framework tree, see polyfill-dotnet-api.
license: MIT
metadata:
    applicability: dotnet-framework
    binding: optional-overlay
    github-path: skills/dotnet-polyfills
    github-pinned: v0.10.0
    github-ref: refs/tags/v0.10.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: ae0aa41d3c5c002298172ba4e5f7b24eb615d9db
    maturity: canary
    portability: portable
    related: pre-pr-self-review, framework-jit-optimization
    requires: none
    risk: local-write
name: dotnet-polyfills
---
# .NET downlevel polyfills

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

The package-and-generator stack that lets a multi-targeted library call modern
BCL APIs (`Span<T>`, `Index` / `Range`, `HashCode`, the C# language attributes,
...) on **.NET Framework** (net472 / net481) without hand-writing a shim for
each one. Most "this type doesn't exist downlevel" gaps are closed by adding the
right package or source generator - reach for those before writing any polyfill
of your own.

## The decision order

Work top to bottom and **stop at the first hit**. Never hand-roll something a
package or generator already ships.

1. **Probe first** - the member may already compile downlevel via a package you
   already reference.
2. **An official Microsoft package** supplies the runtime type or method.
3. **PolySharp** supplies it, if it is a compiler / language *attribute*.
4. **The `KlutzyNinja.Touki` package** already polyfills it, if you reference Touki.
5. **Hand-roll** it - last resort, and a different skill (see the end).

## 1. Probe before you polyfill

Before adding anything, confirm the gap is real. Write a tiny `#if NETFRAMEWORK`
snippet that calls the candidate API and build the **downlevel** target (e.g.
`dotnet build -f net472`). If it compiles, a package you already reference
supplies the member - delete the probe and move on. This catches the common case
where `System.Memory` (or a transitive dependency) already covers the API.

## 2. Official Microsoft downlevel packages

The `Microsoft.Bcl.*` and `System.*` backport packages cover most runtime gaps -
spans, `Index`/`Range`, `HashCode`, async interfaces, `TimeProvider`, modern
`System.IO`, and more. The full catalog (package -> what it covers) is in
[references/packages.md](references/packages.md).

Two rules when adding one:

- **Reference it for the downlevel target only.** Put the `<PackageReference>`
  inside an ItemGroup conditioned on the framework target (e.g.
  `Condition="'$(TargetFramework)' == 'net481'"` or the project's framework-version
  property) - never unconditional, or it ships dead weight on modern .NET.
- **Prefer the package over a hand-roll even for one member.** A referenced
  package is maintained, correct on edge cases, and free; a hand-rolled shim is
  none of those.

### Microsoft.IO.Redist needs a namespace alias

`Microsoft.IO.Redist` is the one package with an integration wrinkle worth
spelling out. It backports the modern (.NET Core) `System.IO` surface - the fast
`Directory` / `File` / `Path` helpers and `EnumerationOptions`-based enumeration -
but it places those types under the **`Microsoft.IO`** namespace so they do not
collide with the framework's built-in `System.IO`. To facilitate interchange it
deliberately does **not** redefine the existing non-static *exchange* types
(`Stream`, `StreamReader` / `StreamWriter`, `TextWriter`, `FileStream`, ...) -
those have to stay the single `System.IO` type that the rest of the framework
passes around.

So alias the *namespace* per target, and alias the exchange *types* back to
`System.IO` unconditionally (they are the same type on both targets) - this is the
whole integration, usually in a `GlobalUsings.cs`:

```csharp
#if NETFRAMEWORK
global using Microsoft.IO;             // Directory, File, Path, EnumerationOptions (backported static helpers)
global using Microsoft.IO.Enumeration; // FileSystemEnumerable<T>, FileSystemEntry, ...
#else
global using System.IO;
global using System.IO.Enumeration;
#endif

// Microsoft.IO does not provide these non-static exchange types - alias them to
// System.IO so call sites stay uniform across both targets.
global using Stream = System.IO.Stream;
global using StreamWriter = System.IO.StreamWriter;
global using StringWriter = System.IO.StringWriter;
global using TextWriter = System.IO.TextWriter;
```

Now `Directory.EnumerateFiles(...)` binds to the fast `Microsoft.IO` helper on
.NET Framework and to `System.IO` on modern .NET, while `Stream` and the other
exchange types resolve to the one `System.IO` definition everywhere.

## 3. PolySharp for compiler and language attributes

[PolySharp](https://github.com/Sergio0694/PolySharp) source-generates the
**compiler / language attributes** that newer C# needs but .NET Framework's
reference assemblies lack - it does **not** supply runtime types. Use it for
`IsExternalInit` (init-only setters and records), `RequiredMemberAttribute`,
`CompilerFeatureRequiredAttribute`, `CallerArgumentExpressionAttribute`,
`ModuleInitializerAttribute`, `SkipLocalsInitAttribute`, and the nullable
analysis attributes (`NotNullWhen`, `MaybeNullWhen`, `MemberNotNull`,
`DoesNotReturn`, ...).

Reference it for the downlevel target only, as an analyzer/source-generator
(`PrivateAssets="all"`), and enable the generated surface as needed:

```xml
<PropertyGroup>
  <PolySharpUsePublicAccessibilityForGeneratedTypes>true</PolySharpUsePublicAccessibilityForGeneratedTypes>
  <PolySharpIncludeRuntimeSupportedAttributes>true</PolySharpIncludeRuntimeSupportedAttributes>
</PropertyGroup>
```

Never hand-write an attribute polyfill - PolySharp generates them, and a
hand-written copy collides with the generated one.

## 4. KlutzyNinja.Touki runtime polyfills on top

The [`KlutzyNinja.Touki`](https://www.nuget.org/packages/KlutzyNinja.Touki)
package ships a layer of **runtime** polyfills *on top of* the official packages -
the members the Microsoft backports leave out. It uses C# `extension` blocks so
member lookup binds the real BCL member on modern .NET and the polyfill on
net472 / net481, which means callers reach them through the `using` they already
have. The surface includes extra `MemoryExtensions` overloads, `Convert` /
`Encoding` / cryptography helpers, span-based string helpers, and pooled buffer
and collection utilities.

If your project references `KlutzyNinja.Touki`, **check whether it already
provides the member before hand-rolling** - it covers a large slice of the "the
package doesn't have this one either" gap. (Touki also re-exposes the official
packages it builds on, so referencing Touki often removes the need to add
`System.Memory` and friends yourself.)

## 5. Hand-rolling with extension types

Only when no package, no generator, and no referenced library supplies the
member - and there is a real caller for it - do you write your own. The portable
technique is a C# **extension block** (the C# 14 extension-members feature). The
polyfill binds the real BCL member on modern .NET and your version on net472 /
net481, so call sites reach it through the `using` they already have for the BCL
type - no `#if` at the call site.

Put the extension in a static class whose **namespace matches the extended type's
namespace** (so the caller's existing `using` finds it), and compile the polyfill
only for the downlevel target.

**Extending an instance type** - add a missing instance member; the receiver
parameter (`encoding`) is the instance:

```csharp
#if NETFRAMEWORK
namespace System.Text;

public static class EncodingPolyfills
{
    extension(Encoding encoding)
    {
        public string GetString(ReadOnlySpan<byte> bytes) { /* ... */ }
    }
}
#endif
```

**Extending a static class** - the newer capability that makes most BCL polyfills
reachable: an extension block with **no receiver parameter** adds *static* members
to the named type, including a `static` class like `Convert` or `MemoryMarshal`
that classic extension methods could never touch:

```csharp
#if NETFRAMEWORK
namespace System;

public static class ConvertPolyfills
{
    extension(Convert)   // no parameter name -> static members on the Convert type
    {
        public static string ToHexString(ReadOnlySpan<byte> bytes) { /* ... */ }
    }
}
#endif
```

Call sites then write `Convert.ToHexString(data)` unchanged on both targets. Keep
the polyfill scoped to the member that has a caller - "completeness" polyfills
just grow the surface.

The repo-specific authoring conventions - where the files live, the
behavior-parity and allocation rules, and the net472 / net481 codegen gotchas
(like the `Unsafe.As` sign-extension trap) - are a separate skill,
**polyfill-dotnet-api**. Come here first to ask "which package or generator covers
this"; go there once the answer is genuinely "none." A consuming repository wires
the concrete link to that authoring skill and its downlevel-package and
polyfill-layout specifics in its overlay.
