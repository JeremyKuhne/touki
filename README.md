# Touki (登器): Code for .NET and .NET Framework

[![Build][build-badge]][build-workflow]
[![codecov][codecov-badge]][codecov]
[![NuGet][nuget-badge]][nuget]

[build-badge]: https://github.com/JeremyKuhne/touki/actions/workflows/dotnet.yml/badge.svg
[build-workflow]: https://github.com/JeremyKuhne/touki/actions/workflows/dotnet.yml
[codecov-badge]: https://codecov.io/gh/JeremyKuhne/touki/branch/main/graph/badge.svg
[codecov]: https://codecov.io/gh/JeremyKuhne/touki
[nuget-badge]: https://img.shields.io/nuget/v/KlutzyNinja.Touki.svg
[nuget]: https://www.nuget.org/packages/KlutzyNinja.Touki/

Provides useful functionality both for .NET and .NET Framework applications.

Tōki (登器) is the Japanese word for (Ninja) "climbing gear" or "climbing equipment". This library is designed to help
developers "climb" the challenges of cross framework .NET development by providing tools and utilities that enhance
performance and efficiency.

Some of the design goals include:

- Avoiding unnecessary allocations
- Avoiding code that prevents AOT compilation on .NET
- Leveraging the latest C# (14+) features to improve usability and performance

## Features

- Low-allocation interpolated string support on .NET Framework that avoids
  boxing and intermediate formatting strings (`$"Age: {age}"`)
- Handler-backed formatting into `TextWriter`, or raw UTF-16 code-unit output
  to `Stream`, without materializing a result string
- Robust and performant `StringSegment` struct for working with substrings without allocations
- `SpanReader`, `SpanWriter`, and span-based byte run-length encoding
- `BufferScope<T>` for easy management of temporary `ArrayPool` and stack based buffers
- `Value` for carrying primitives, nullables, enums, and object values through
  one API without boxing supported primitives
- Pooled, single-item-optimized, and sequence-interning collections
  (`ArrayPoolList<T>`, `SingleOptimizedList<TItem, TList>`, `SequenceSet<T>`),
  plus ref-counted resource caches
- Reusable compiled glob matching for POSIX, Bash/extglob, Git/gitignore,
  MSBuild, `Microsoft.Extensions.FileSystemGlobbing`, PowerShell, and simple
  wildcard dialects
- Lazy file-system glob enumeration with include/exclude patterns,
  plus a compatibility-focused MSBuild result API with drive-root safety
- Gitignore parsing with ordered include/exclude rules and best-effort text
  clipboard access across supported desktop platforms
- Raw `.resources` inspection, registered-type NRBF deserialization for trusted
  payloads, and culture side-file string resources
- Interop ownership helpers that pair native handles with their managed owners
- Polyfills for many modern .NET BCL APIs on .NET Framework 4.7.2 (see the
  [polyfill guide](https://github.com/JeremyKuhne/touki/blob/main/.agents/skills/polyfill-dotnet-api/references/polyfill-layout.md))
- A `DisposableBase` with double-disposal protection and disposal
  tracking helpers for diagnosing leaks
- [Roslyn analyzers](https://github.com/JeremyKuhne/touki/blob/main/docs/analyzers.md)
  that run automatically through the package's `KlutzyNinja.Touki.Analyzers`
  dependency, which can also be installed independently
- Much more!

## Overviews

- [Configuring Your Project for Touki](https://github.com/JeremyKuhne/touki/blob/main/sample/README.md)
- [Touki Analyzers](https://github.com/JeremyKuhne/touki/blob/main/docs/analyzers.md)
- [Reducing String Allocations with Touki](https://github.com/JeremyKuhne/touki/blob/main/docs/strings.md)
- [Low-Allocation Collections](https://github.com/JeremyKuhne/touki/blob/main/docs/collections.md)
- [Interning Variable-Length Sequences with `SequenceSet<T>`](https://github.com/JeremyKuhne/touki/blob/main/docs/sequence-set.md)
- [Buffers, Span Readers, and Span Writers](https://github.com/JeremyKuhne/touki/blob/main/docs/buffers.md)
- [Compiled Glob Matching and File-System Enumeration](https://github.com/JeremyKuhne/touki/blob/main/docs/globbing.md)
- [IO Helpers (globs, gitignore, clipboard, paths, temp folders, streams)](https://github.com/JeremyKuhne/touki/blob/main/docs/io.md)
- [Resources and Legacy Serialization](https://github.com/JeremyKuhne/touki/blob/main/docs/resources.md)
- .NET Framework polyfill layout and disambiguation:
  [Polyfill guide](https://github.com/JeremyKuhne/touki/blob/main/.agents/skills/polyfill-dotnet-api/references/polyfill-layout.md)
- Span performance on .NET Framework (net472+):
  [Reference](https://github.com/JeremyKuhne/touki/blob/main/.agents/skills/framework-jit-optimization/references/framework-span-performance.md)
- Stack and pooled scratch-buffer performance (net481/net10):
  [Reference](https://github.com/JeremyKuhne/touki/blob/main/.agents/skills/scratch-buffer-strategy/references/arraypool-performance.md)

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

The package depends on `KlutzyNinja.Touki.Analyzers`, so referencing
`KlutzyNinja.Touki` is all it takes to run the Roslyn analyzers. The analyzer
package can also be referenced independently when the runtime library is not
needed, and it has its own release version.

They encode the conventions the library is built on, across a few areas:

- **Reliability** - hidden struct copies, values that must be disposed
  deterministically, and `stackalloc` requests large enough to threaten the
  stack.
- **Maintainability** - one type per file, and file names that say which type
  a file holds.
- **Performance** - building strings without the `StringBuilder` allocation.
- **Usage and naming** - pattern matching for null checks, and an opt-in
  naming engine that replaces IDE1006.

See [Touki Analyzers](https://github.com/JeremyKuhne/touki/blob/main/docs/analyzers.md) for the full rule list, what each
one flags, and how to configure it.

## Requirements

- .NET 10.0 or later **OR** .NET Framework 4.7.2 or later
- C# 14.0 or later for the best experience
- Any CPU architecture supported by .NET - the package ships
  architecture-neutral ("AnyCPU") assemblies with no native components, so
  x64 and ARM64 are both fully supported on Windows, Linux, and macOS

## Contributing

See [CONTRIBUTING.md](https://github.com/JeremyKuhne/touki/blob/main/CONTRIBUTING.md) for more information on how to contribute to this project.
