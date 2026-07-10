# Source preference order

Detail for the [polyfill-dotnet-api](SKILL.md) skill. Work the list below in
order; stop at the first hit. Don't add a hand polyfill for something a
Microsoft package or PolySharp already ships.

## 1. Microsoft-shipped NuGet packages

The vendored [dotnet-polyfills](../dotnet-polyfills/SKILL.md) skill (and its
`references/packages.md`) is the full catalog of which official package supplies
which API. The touki-specific bindings:

- **Already referenced** (net472/net481 target only): `System.Memory`,
  `Microsoft.Bcl.Memory`, `Microsoft.Bcl.HashCode`, `Microsoft.IO.Redist`.
- **Probe before polyfilling**: write a tiny `#if NETFRAMEWORK` snippet in
  `touki.tests/Framework/` that calls the candidate API and try a `net472`
  build. If it compiles, a referenced package already supplies the member;
  delete the probe.
- **When adding a package**, place the `<PackageReference>` inside the
  `Condition="'$(TargetFramework)' == '$(DotNetFrameworkVersion)'"` ItemGroup -
  never unconditional.

## 2. PolySharp source-generated polyfills

See [dotnet-polyfills](../dotnet-polyfills/SKILL.md) for what PolySharp covers
(compiler / language attributes, not runtime types - never hand-write them).
Touki enables:

```xml
<PolySharpUsePublicAccessibilityForGeneratedTypes>true</PolySharpUsePublicAccessibilityForGeneratedTypes>
<PolySharpIncludeRuntimeSupportedAttributes>true</PolySharpIncludeRuntimeSupportedAttributes>
```

## 3. Hand-rolled polyfill in `touki/Framework/Polyfills/<BclNamespace>/`

Last resort, when a runtime member (instance method, static helper,
ctor) isn't supplied by a package and isn't an attribute. Polyfill only
when there's a real caller; "completeness" polyfills bloat the surface.

- **Folder = BCL namespace, dotted.** `System.Convert` ->
  `touki/Framework/Polyfills/System/ConvertExtensions.cs`.
  `System.Text.Encoding` ->
  `touki/Framework/Polyfills/System.Text/EncodingExtensions.cs`.
  `System.Security.Cryptography.CryptographicOperations` ->
  `touki/Framework/Polyfills/System.Security.Cryptography/CryptographicOperations.cs`.
- **`namespace` matches the folder.** `Polyfills/System/Foo.cs` declares
  `namespace System;`, `Polyfills/System.Text/Foo.cs` declares
  `namespace System.Text;`. Callers reach the polyfill through the same
  `using` they already had for the BCL type.
- **Use C# 14 `extension` blocks** (e.g. `extension(Encoding encoding) { ... }`)
  rather than static-class extension methods. Lookup picks the BCL
  member on modern .NET and the polyfill on net472. See
  [ConvertExtensions.cs](../../../touki/Framework/Polyfills/System/ConvertExtensions.cs).
- **Don't `#if NETFRAMEWORK` inside `touki/Framework/`** - the
  whole folder is already framework-only.
- **Touki-specific helpers (not polyfills)** live in
  `touki/Framework/Touki/...` with `Touki.*` namespaces. If your file
  isn't shadowing a public modern .NET BCL member, it doesn't go under
  `Polyfills/`.
