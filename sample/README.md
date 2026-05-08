# Configuring Your Project for Touki

The `sample` project is a template-style project that shows how to wire
up your own multi-targeted projects to take full advantage of Touki. The
same source compiles on .NET 10 and .NET Framework 4.7.2 because the
project file and `GlobalUsings.cs` together hide the differences.

## Project file setup

See [sample.csproj](sample.csproj) for a concrete example. The key
points are:

- Target both .NET Framework (4.7.2 or later) and .NET (10.0 or later)
  via `<TargetFrameworks>`.
- Set `<ImplicitUsings>disable</ImplicitUsings>` so you can redirect
  `System.IO` to `Microsoft.IO` on .NET Framework without ambiguity.
- Reference `KlutzyNinja.Touki`. On .NET Framework, also reference
  `Microsoft.Bcl.Memory` (Touki's transitive dependency surfaces a
  security advisory until the Touki package is updated).
- On .NET (non-framework), opt into `<IsAotCompatible>true</IsAotCompatible>`
  to get the AOT analyzer. Touki itself is written to be AOT-friendly.

## Global usings

[GlobalUsings.cs](GlobalUsings.cs) is what makes Touki feel like a part
of the BCL in your code:

- Re-adds the usings you'd normally get from `<ImplicitUsings>` (`System`,
  `System.Collections.Generic`, `System.Diagnostics`, `System.Numerics`,
  `System.Threading`, ...).
- Redirects `System.IO` to `Microsoft.IO` on .NET Framework, with
  explicit aliases for the exception types that don't move.
- Pulls in `Touki`, `Touki.Collections`, `Touki.Exceptions`,
  `Touki.Interop`, `Touki.Io`, and `Touki.Text` so extension members
  light up on the BCL types you already use (`Convert`, `Random`,
  `string`, `Stream`, ...).
- On .NET Framework, adds `Framework.Touki` for the small amount of
  Touki-specific framework-only code.

## What the example demonstrates

[ExampleClass.cs](ExampleClass.cs) calls a handful of APIs that "just
work" on both targets thanks to Touki:

- `DefaultInterpolatedStringHandler` (interpolated strings without
  allocations on .NET Framework).
- `System.Numerics.BitOperations` (`LeadingZeroCount`, `TrailingZeroCount`,
  `PopCount`, `IsPow2`).
- `System.Threading.Lock` and `lock(Lock)` syntax.
- `Math.DivRem` returning a tuple.
- `Interlocked` overloads for `uint`.
- `char.IsAsciiLetter`/`IsAsciiDigit`/`IsAsciiHexDigit`/`IsAsciiLetterOrDigit`.
- `ArgumentNullException.ThrowIfNull`.
- Collection expressions, switch expressions, and ranges/spans.

For a wider tour of what Touki provides, see the docs linked from the
[top-level README](../README.md).
