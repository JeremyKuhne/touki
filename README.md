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
  [polyfill layout doc](docs/polyfill-layout.md) for the full list)
- A `DisposableBase` with double-disposal protection and disposal
  tracking helpers for diagnosing leaks
- Much more!

## Overviews

- [Configuring Your Project for Touki](sample/README.md)
- [Reducing String Allocations with Touki](docs/strings.md)
- [Low-Allocation Collections](docs/collections.md)
- [Buffers, Span Readers, and Span Writers](docs/buffers.md)
- [IO Helpers (globs, paths, temp folders, stream formatting)](docs/io.md)
- [.NET Framework Polyfill Layout & Disambiguation](docs/polyfill-layout.md)
- [Span Performance on .NET Framework (net472+)](docs/framework-span-performance.md)

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

## Requirements

- .NET 10.0 or later **OR** .NET Framework 4.7.2 or later
- C# 14.0 or later for the best experience

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for more information on how to contribute to this project.
