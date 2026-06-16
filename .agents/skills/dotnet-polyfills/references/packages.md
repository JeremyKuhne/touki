# Official .NET downlevel package catalog

Reference for the [dotnet-polyfills](../SKILL.md) skill. The Microsoft-shipped
packages that backport modern BCL surface to **.NET Framework** (net472 /
net481) and older `netstandard`. Reach for one of these before hand-rolling a
polyfill; add it to an ItemGroup conditioned on the downlevel target only.

## The catalog

Ordered foundational-first. Many of the lower rows depend transitively on
`System.Memory`, so adding it (or a package that pulls it) often closes several
gaps at once.

| Package | Supplies |
| ------- | -------- |
| `System.Memory` | `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, `ReadOnlyMemory<T>`, the base `MemoryExtensions`, `MemoryMarshal`, `BinaryPrimitives`, `ArrayPool<T>`, and (transitively) `System.Runtime.CompilerServices.Unsafe`. The foundation almost everything else builds on. |
| `Microsoft.Bcl.Memory` | `Index`, `Range` (so `x[1..^1]` and `^1` work downlevel), plus newer `MemoryExtensions` members (`Count`, `CommonPrefixLength`, `ContainsAnyExcept`, `IsWhiteSpace`, ...). Layers on `System.Memory`. |
| `Microsoft.Bcl.HashCode` | `System.HashCode` (the `Add` / `Combine` struct combiner). |
| `System.Threading.Tasks.Extensions` | `ValueTask`, `ValueTask<T>`. |
| `Microsoft.Bcl.AsyncInterfaces` | `IAsyncEnumerable<T>`, `IAsyncEnumerator<T>`, `IAsyncDisposable` - the plumbing behind `await foreach` and async-returning interfaces. |
| `Microsoft.Bcl.TimeProvider` | `TimeProvider` and `ITimer` (the testable clock abstraction). |
| `Microsoft.IO.Redist` | The modern `System.IO` surface - notably `Directory` / `DirectoryInfo` enumeration with `EnumerationOptions`. Reach it via `global using Microsoft.IO;`. |
| `Microsoft.Bcl.Numerics` | `MathF` and newer `System.Math` members downlevel. |
| `System.Text.Json` | `System.Text.Json` (serializer + DOM) on .NET Framework. |
| `System.Collections.Immutable` | `ImmutableArray<T>`, `ImmutableList<T>`, and the rest of the immutable collections. |

The list is not exhaustive - the `Microsoft.Bcl.*` / `System.*` family is large.
When a type is missing downlevel, search NuGet for an official
`Microsoft.Bcl.<Area>` or `System.<Area>` package before assuming you must
hand-roll it.

## Referencing them safely

- **Downlevel target only.** Wrap the references in an ItemGroup conditioned on
  the framework target so they never ship on modern .NET:

  ```xml
  <ItemGroup Condition="'$(TargetFramework)' == 'net481'">
    <PackageReference Include="System.Memory" />
    <PackageReference Include="Microsoft.Bcl.Memory" />
    <PackageReference Include="Microsoft.Bcl.HashCode" />
    <PackageReference Include="Microsoft.IO.Redist" />
  </ItemGroup>
  ```

  (With central package management the versions live in
  `Directory.Packages.props`; the project file carries only the
  `Include`.)

- **Pin centrally, update deliberately.** These packages move with the BCL.
  Representative versions at the time of writing: `System.Memory` 4.6.x,
  `Microsoft.Bcl.Memory` 10.0.x, `Microsoft.Bcl.HashCode` 6.0.x,
  `Microsoft.IO.Redist` 6.1.x. Treat those as examples, not a recommendation -
  check NuGet for the current release.

- **Mind the transitive graph.** `System.Memory` pulls `System.Buffers` and
  `System.Runtime.CompilerServices.Unsafe`; adding it may already give you a type
  you were about to reference separately. Probe (build the downlevel target)
  after each addition to see what is now covered.
