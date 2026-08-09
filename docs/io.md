# IO Helpers

[`Touki.Io`](../touki/Touki/Io/) collects file-system, path, and stream
helpers that are useful on both .NET 10 and .NET Framework 4.7.2.

## Glob matching and enumeration

Touki supports reusable compiled matching and file-system enumeration across
POSIX, Bash/extglob, Git/gitignore, MSBuild,
`Microsoft.Extensions.FileSystemGlobbing`, PowerShell, and simple wildcard
dialects.

[`GlobSpecification`](../touki/Touki/Io/Globbing/GlobSpecification.cs) compiles
an immutable pattern for repeated `IsMatch` calls.
[`GlobEnumerator`](../touki/Touki/Io/GlobEnumerator.cs) applies one include and
zero or more exclude patterns to a directory tree. See
[Compiled Glob Matching and File-System Enumeration](globbing.md) for the
dialect matrix, options, examples, and the distinction between these APIs and
MSBuild item enumeration.

## `MSBuildEnumerator`: MSBuild item specifications

[`MSBuildEnumerator`](../touki/Touki/Io/MSBuildEnumerator.cs) walks the
file system using MSBuild-style item include and exclude specifications such as
`<Compile Include="src/**/*.cs" Exclude="**/obj/**"/>`. It builds on
`Microsoft.IO.Enumeration` (or `System.IO.Enumeration` on .NET). After setup,
enumeration is lazy and matched paths are materialized as strings.

Supported wildcards:

| Pattern | Meaning |
| --- | --- |
| `*` | Zero or more characters within a single file or directory name. |
| `**` | Zero or more directories (recursive). |
| `?` | A single character. |

```csharp
using Touki.Io;

string projectDirectory = @"C:\repos\my-project";

using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(
  new(
    include: @"src\**\*.cs",
    projectDirectory,
    excludes: @"**\obj\**;**\bin\**"));

while (enumerator.MoveNext())
{
  Console.WriteLine(enumerator.Current);
}
```

By default, paths are returned relative to `projectDirectory` when the
spec is not fully qualified. A null project directory produces fully qualified
results. Physical filesystem traversal follows platform casing (case-insensitive
on Windows / macOS / iOS, case-sensitive on Linux); MSBuild's logical wildcard
post-filter phases remain case-insensitive. Pass an `EnumerationOptions` to
override the physical matching options.

Use `MSBuildEnumerator.CreateResult(request)` when the caller needs to distinguish a
normal [`MSBuildSearchResult`](../touki/Touki/Io/MSBuildSearchResult.cs), an invalid
specification returned as [`MSBuildReturnLiteralResult`](../touki/Touki/Io/MSBuildReturnLiteralResult.cs),
an [`MSBuildEmptyResult`](../touki/Touki/Io/MSBuildEmptyResult.cs), or an
[`MSBuildRejectedResult`](../touki/Touki/Io/MSBuildRejectedResult.cs). Set
`MSBuildEnumerationRequest.AllowDriveEnumeration` only when whole-drive or whole-share
recursion is intentional. The caller owns and must dispose
`MSBuildSearchResult.Enumerator`.
Invalid excludes do not abort a wildcard search, matching `FileMatcher`; they are
retained as exact literal result filters in source order through
`MSBuildSearchResult.InvalidExcludeSpecifications`.

The matcher retries repeated anchors after `**` with bounded, nonrecursive state.
File-only excludes filter matching files without suppressing traversal, while a
terminal `**` exclude can prune a proven complete subtree. Wildcard-relative parent
segments are rejected before path collapsing, and error-preserving split results
retain source order.

Filename enumeration follows characterized `FileMatcher` behavior, including
extensionless DOS-dot patterns (`*.*`, `name.*`, `*.`), platform casing, and MSBuild's
logical post-filter for loose `?` / three-character-extension filesystem matches.
This API still intentionally differs from no-wildcard literal shortcuts, process-trait
drive handling, 8.3/lexical path identity, raw ordering, symlink traversal, and some
I/O-failure tuple details. It should not be treated as a byte-for-byte replacement for
every internal MSBuild outcome.

## Gitignore rules and matcher composition

[`GitIgnoreRules`](../touki/Touki/Io/GitIgnoreRules.cs) compiles `.gitignore` text into
immutable ordered rules. `IsIgnoredFile` evaluates canonical root-relative `/` paths;
`CreateIncludedMatcher` and `CreateIgnoredMatcher` expose definitions with explicit
polarity for enumeration. Ancestors are evaluated before descendants, so a child
cannot rescue itself through an ignored parent; re-including the parent reopens its
subtree.

Touki currently strips all trailing spaces and tabs from rules. Unlike Git, an
escaped trailing space is not preserved as part of the pattern.

[`FileSystemMatcher`](../touki/Touki/Io/FileSystemMatcher.cs) creates callback-native
or canonical-path definitions and immutable exclusion-wins / ordered compositions.
Definitions are borrowed and reusable; each enumeration owns only its session. Use
[`FileSystemPathEnumerator`](../touki/Touki/Io/FileSystemPathEnumerator.cs) for a
ready-made canonical relative-path enumerator, or derive
`FileSystemMatchEnumerator<TResult>` for custom results. `CreatePath` makes regex and
other contiguous-path predicates easy to mix with compiled globs without forcing path
construction onto callback-native matchers.

## Clipboard

[`Clipboard`](../touki/Touki/Io/Clipboard.cs) is a best-effort plain-text
clipboard API. Check `IsAvailable`, then use `TryGetText`, `TrySetText`, or
`TryClear`; transport failures and clipboard contention are reported as `false`
rather than thrown exceptions.

On .NET, Touki supports the Windows clipboard, macOS AppKit when available, and
Linux Wayland/X11 when a supported helper (`wl-copy` / `wl-paste`, `xclip`, or
`xsel`) is present. The .NET Framework build uses the Windows provider. Headless
or unsupported environments fall back to an unavailable provider whose
operations return `false`.

## `Paths`

[`Paths`](../touki/Touki/Io/Paths.cs) exposes:

* `MaxShortPath` (260) for stack-allocation sizing.
* `OSDefaultMatchCasing` and `GetFinalCasing(MatchCasing)` for
  resolving `MatchCasing.PlatformDefault` consistently across .NET 10
  and .NET Framework.
* `MatchesExpression(name, expression, matchCasing, matchType)` for
  one-off glob matching without spinning up an enumerator.
* `IsSameOrSubdirectory(firstDirectory, secondDirectory, ignoreCase)` for
  normalized, fully qualified string-path comparisons. It does not resolve
  symbolic links or junctions.
* `RemoveRelativeSegments(...)` for collapsing separator runs and `.` / `..`
  segments without first combining with a root.
* `ChangeAlternateDirectorySeparators(string)` for normalizing separator
  characters to the platform primary separator.

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

## `Stream` and `TextWriter` extensions

[`StreamExtensions`](../touki/Touki/Io/StreamExtensions.cs) adds synchronous and
asynchronous `Read` / `Write` overloads for `ArraySegment<byte>`, including
cancellation-token support for the asynchronous forms.

### `WriteFormatted`

[`TextWriterExtensions`](../touki/Touki/Io/TextWriterExtensions.cs) (and
its `Stream`-targeted partial in
[`StreamExtensions.cs`](../touki/Touki/Io/StreamExtensions.cs)) adds
`WriteFormatted` overloads that accept a `ValueStringBuilder`-backed
interpolated string handler, so formatted output flows directly into the
target without an intermediate `string` allocation. See
[strings.md](strings.md) for the full picture.

```csharp
using Touki.Io;

int userId = 42;
using StreamWriter writer = File.CreateText("log.txt");
writer.WriteFormatted($"Started at {DateTime.UtcNow:O} for user {userId}");
```

`Stream.WriteFormatted` is a lower-level overload that writes the builder's raw
UTF-16 code-unit bytes without a text encoding or byte-order mark. Use a
`TextWriter` such as `StreamWriter` for encoded text files and protocols.
