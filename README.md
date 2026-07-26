# Touki (登器): Code for .NET and .NET Framework

[![Build](https://github.com/JeremyKuhne/touki/actions/workflows/dotnet.yml/badge.svg)](https://github.com/JeremyKuhne/touki/actions/workflows/dotnet.yml)
[![codecov](https://codecov.io/gh/JeremyKuhne/touki/branch/main/graph/badge.svg)](https://codecov.io/gh/JeremyKuhne/touki)
[![NuGet](https://img.shields.io/nuget/v/KlutzyNinja.Touki.svg)](https://www.nuget.org/packages/KlutzyNinja.Touki/)

Provides useful functionality both for .NET and .NET Framework applications.

Tōki (登器) is the Japanese word for (Ninja) "climbing gear" or "climbing equipment". This library is designed to help
developers "climb" the challenges of cross framework .NET development by providing tools and utilities that enhance
performance and efficiency.

Some of the design goals include:

- Avoiding unnecessary allocations
- Avoiding code that prevents AOT compilation on .NET
- Leveraging the latest C# (14+) features to improve usability and performance

## Features

- Non allocating interpolated string support on .NET Framework (`$"Age: {age}"`)
- Formatting directly into `Stream` and `TextWriter` without unnecessary allocations
- Robust and performant `StringSegment` struct for working with substrings without allocations
- `SpanReader` and `SpanWriter` for efficient reading and writing of data in spans
- `BufferScope<T>` for easy management of temporary `ArrayPool` and stack based buffers
- `Value` struct for creating strongly typed arbitrary collections of values without boxing most primitives
- Pooled and single-item-optimized list types (`ArrayPoolList<T>`,
  `SingleOptimizedList<TItem, TList>`) and ref-counted resource caches
- `MSBuildEnumerator` for MSBuild-style glob enumeration with no
  allocations until a match is produced
- Polyfills for many modern .NET BCL APIs on .NET Framework 4.7.2 (see the
  [polyfill layout doc](.agents/skills/polyfill-dotnet-api/references/polyfill-layout.md) for the full list)
- A `DisposableBase` with double-disposal protection and disposal
  tracking helpers for diagnosing leaks
- [Roslyn analyzers](docs/analyzers.md) that ship in the package and run
  automatically - no separate analyzer package to install
- Much more!

## Overviews

- [Configuring Your Project for Touki](sample/README.md)
- [Analyzers Shipped in the Package](docs/analyzers.md)
- [Reducing String Allocations with Touki](docs/strings.md)
- [Low-Allocation Collections](docs/collections.md)
- [Buffers, Span Readers, and Span Writers](docs/buffers.md)
- [IO Helpers (globs, paths, temp folders, stream formatting)](docs/io.md)
- [.NET Framework Polyfill Layout & Disambiguation](.agents/skills/polyfill-dotnet-api/references/polyfill-layout.md)
- [Span Performance on .NET Framework (net472+)](.agents/skills/framework-jit-optimization/references/framework-span-performance.md)
- [ArrayPool vs Stack Scratch Buffers (net481/net10)](.agents/skills/scratch-buffer-strategy/references/arraypool-performance.md)

## Package Installation

Using the .NET CLI:

```
dotnet add package KlutzyNinja.Touki
```

Or with the NuGet Package Manager:

```
PM> Install-Package KlutzyNinja.Touki
```




[View on NuGet.org](https://www.nuget.org/packages/KlutzyNinja.Touki/)

## Analyzers

The package includes Roslyn analyzers. They are delivered inside
`KlutzyNinja.Touki` itself, so referencing the package is all it takes - there
is nothing extra to install and nothing to switch on.

They encode the conventions the library is built on: avoid hidden struct
copies, release resources deterministically, keep large scratch buffers off the
stack, and keep types easy to find by file name.

| ID | Rule | Default severity |
|----|------|------------------|
| TOUKI0001 | Use pattern matching for null checks | Warning |
| TOUKI0002 | Defensive copy of a struct | Hidden |
| TOUKI0003 | Defensive copy of a non-copyable struct | Warning |
| TOUKI0004 | By-value copy of a non-copyable struct | Warning |
| TOUKI0010 | Dispose a `[MustDispose]` value deterministically | Warning |
| TOUKI0011 | Avoid large `stackalloc` allocations | Warning |
| TOUKI0020 | Declare one type per file | Warning |
| TOUKI0021 | File name should match the type it declares | Warning |
| TOUKI0030 | Use `ValueStringBuilder` to build strings | Warning |

Every rule can be re-scoped or turned off per directory through
`.editorconfig`, and TOUKI0011 and TOUKI0021 take options of their own:

```ini
# Severity, available on every rule
dotnet_diagnostic.TOUKI0030.severity = none

# Rule-specific options
dotnet_code_quality.TOUKI0011.max_stackalloc_bytes = 512
dotnet_code_quality.TOUKI0021.file_name_detail_separators = .-
```

See [Analyzers Shipped in the Package](docs/analyzers.md) for what each rule
flags, why, and how to configure it.

## Requirements

- .NET 10.0 or later **OR** .NET Framework 4.7.2 or later
- C# 14.0 or later for the best experience
- Any CPU architecture supported by .NET - the package ships
  architecture-neutral ("AnyCPU") assemblies with no native components, so
  x64 and ARM64 are both fully supported on Windows, Linux, and macOS

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for more information on how to contribute to this project.
