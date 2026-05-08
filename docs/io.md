# IO Helpers

[`Touki.Io`](../touki/Touki/Io/) collects file-system, path, and stream
helpers that are useful on both .NET 10 and .NET Framework 4.7.2.

## `MSBuildEnumerator`: glob-style file matching

[`MSBuildEnumerator`](../touki/Touki/Io/MSBuildEnumerator.cs) walks the
file system using the same wildcard semantics MSBuild uses for items
like `<Compile Include="src/**/*.cs" Exclude="**/obj/**"/>`. It builds
on `Microsoft.IO.Enumeration` (or `System.IO.Enumeration` on .NET) and
is allocation-free until it produces a match.

Supported wildcards:

| Pattern | Meaning |
| --- | --- |
| `*` | Zero or more characters within a single file or directory name. |
| `**` | Zero or more directories (recursive). |
| `?` | A single character. |

```csharp
using Touki.Io;

string projectDirectory = @"C:\repos\my-project";

foreach (string path in MSBuildEnumerator.Create(
    fileSpec: @"src\**\*.cs",
    excludeSpecs: @"**\obj\**;**\bin\**",
    projectDirectory: projectDirectory))
{
    Console.WriteLine(path);
}
```

By default, paths are returned relative to `projectDirectory` when the
spec is not fully qualified, matching MSBuild's behavior. Casing follows
the OS (case-insensitive on Windows / macOS / iOS, case-sensitive on
Linux); pass an `EnumerationOptions` to override.

## `Paths`

[`Paths`](../touki/Touki/Io/Paths.cs) exposes:

* `MaxShortPath` (260) for stack-allocation sizing.
* `OSDefaultMatchCasing` and `GetFinalCasing(MatchCasing)` for
  resolving `MatchCasing.PlatformDefault` consistently across .NET 10
  and .NET Framework.
* `Matches(expression, name, matchCasing)` for one-off glob matching
  without spinning up an enumerator.

## `TempFolder`

[`TempFolder`](../touki/Touki/Io/TempFolder.cs) creates a uniquely-named
folder under the OS temp directory and recursively deletes it on
`Dispose`. Implicitly converts to `string`, so it slots into existing
`Path.Combine` / `File.WriteAllText` calls:

```csharp
using TempFolder folder = new();

string file = Path.Combine(folder, "input.txt");
File.WriteAllText(file, "...");

// Folder and all contents are deleted when 'folder' goes out of scope.
```

Failures during deletion (e.g. files held open by another process) are
swallowed so `Dispose` is safe to call from `finally` blocks and test
teardown.

## `StreamExtensions` and `TextWriterExtensions`

[`StreamExtensions`](../touki/Touki/Io/StreamExtensions.cs) and
[`TextWriterExtensions`](../touki/Touki/Io/TextWriterExtensions.cs) add
`WriteFormatted` overloads that accept a `ValueStringBuilder`-backed
interpolated string handler, so formatted output flows directly into the
target without an intermediate `string` allocation. See
[strings.md](strings.md) for the full picture.

```csharp
using FileStream stream = File.Create("log.txt");
stream.WriteFormatted($"Started at {DateTime.UtcNow:O} for user {userId}");
```
