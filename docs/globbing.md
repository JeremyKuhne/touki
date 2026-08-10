# Compiled Glob Matching and File-System Enumeration

Touki separates glob compilation from file-system enumeration. Use
[`Glob`](../touki/Touki/Io/Globbing/Glob.cs) for one-shot matching and simple
single-pattern enumeration,
[`GlobSpecification`](../touki/Touki/Io/Globbing/GlobSpecification.cs) to
compile and reuse a pattern, [`GlobEnumerator`](../touki/Touki/Io/GlobEnumerator.cs)
for Touki's advanced include/exclude enumeration API, and
[`MSBuildEnumerator`](../touki/Touki/Io/MSBuildEnumerator.cs) when compatibility
with MSBuild item specifications is the primary requirement.

These APIs are available on .NET 10, .NET 11, and .NET Framework 4.7.2.

## Compile and reuse a pattern

`GlobSpecification` is immutable and can be reused concurrently. Evaluation
reuses the compiled strategy; path-aware dialects that need to coalesce runs of
separators may rent a temporary buffer.

```csharp
using Touki.Io.Globbing;

GlobSpecification specification = GlobSpecification.Compile(
    pattern: "**/*.cs",
    dialect: GlobDialect.PosixPath,
    options: GlobOptions.AllowGlobStar);

bool matches = specification.IsMatch("src/Program.cs");
```

Use `TryCompile` when a pattern can be malformed. It returns a
[`GlobCompileError`](../touki/Touki/Io/Globbing/GlobCompileError.cs) instead of
throwing `GlobFormatException`. For patterns supplied by untrusted input, pass an
application-specific `maxPatternLength` to bound compilation work.

## Dialects

[`GlobDialect`](../touki/Touki/Io/Globbing/GlobDialect.cs) selects the behavior
that the compiler models:

| Dialect | Behavior modeled |
| --- | --- |
| `Posix` | POSIX `fnmatch` without path-aware separator handling. |
| `PosixPath` | POSIX path matching, where ordinary wildcards do not cross separators. |
| `Bash` | Bash pattern matching, with opt-in globstar and extglob constructs. |
| `Git` | Git `wildmatch` / `.gitignore`, including negation and path anchors. |
| `MSBuild` | MSBuild item-wildcard matching. |
| `FileSystemGlobbing` | `Microsoft.Extensions.FileSystemGlobbing.Matcher` behavior; ordinary `?` is literal. |
| `Simple` | `System.IO.Enumeration.FileSystemName` simple `*` and `?` matching. |
| `PowerShell` | PowerShell `-like` / `WildcardPattern` behavior. |

The dialect controls defaults such as path awareness, separators, case folding,
leading-dot handling, escaping, and whether `**` is enabled. The names identify
the behavior being modeled; they do not promise drop-in parity for every default
or edge case.

### Compatibility notes

* **`Posix` / `PosixPath`** - Touki applies `FNM_PERIOD`-style leading-dot
    protection by default; POSIX `fnmatch` requires the `FNM_PERIOD` flag. Use
    `MatchLeadingDot` to model a call without that flag. Path mode currently
    protects only the start of the complete input, not each segment.
* **`Git`** - Touki protects a leading dot by default, while Git `wildmatch`
    normally lets wildcards consume it. Use `MatchLeadingDot` for that behavior.
* **`Simple`** - Touki defaults to case-sensitive matching and treats backslash
    literally. `FileSystemName.MatchesSimpleExpression` defaults to
    `ignoreCase: true` and uses backslash as an escape. `IgnoreCase` aligns
    casing; there is no Simple-dialect escape mode today.
* **`FileSystemGlobbing`** - Touki defaults to case-sensitive matching. The
    parameterless `Matcher` defaults to case-insensitive matching; use
    `IgnoreCase` to align it. As in `Matcher`, ordinary `?` characters are
    literals rather than single-character wildcards.
* **`MSBuild`** - `GlobSpecification` models `MSBuildGlob`'s in-memory logical
    matcher, including recursive repeated anchors, `*.*`, and trailing-dot
    patterns. The dialect uses Unicode ordinal case-insensitive matching by
    default. Current `MSBuildGlob` and `FileMatcher.IsMatch` APIs do not expose
    case-sensitive matching. Physical `FileMatcher.GetFiles` also inherits
    platform filesystem wildcard behavior; use `MSBuildEnumerator` when that
    enumeration behavior is required.
* **`PowerShell`** - Touki defaults to case-sensitive matching like a bare
    `WildcardPattern`; PowerShell `-like` is case-insensitive, so use
    `IgnoreCase` for that casing. Touki additionally accepts POSIX-style bracket
    negation and named classes, which PowerShell does not.

See the MSBuild section below and [Extended Glob Patterns](extglob.md) for
additional result-model, multiple-asterisk, extglob, and leading-dot limits.

## Options

[`GlobOptions`](../touki/Touki/Io/Globbing/GlobOptions.cs) enables behavior that
is not already provided by a dialect's defaults:

| Option | Effect |
| --- | --- |
| `IgnoreCase` | Enables the dialect's case-insensitive comparison mode; redundant for `MSBuild`, which is already case-insensitive by default. |
| `MatchLeadingDot` | Allows wildcards to consume a leading `.`. |
| `NoEscape` | Treats the dialect's escape character as a literal. |
| `AllowGlobStar` | Enables path-aware `**` matching where it is not enabled by default. |
| `AllowExtGlob` | Enables `?(...)`, `*(...)`, `+(...)`, `@(...)`, and `!(...)`. |

See [Extended Glob Patterns](extglob.md) for extglob syntax, limits, and the
intentional differences between supported dialects.

## Enumerate a directory tree

`Glob.EnumerateFiles` is the simplest way to lazily enumerate one pattern. It
compiles the pattern and snapshots the traversal options when called. Each
enumeration creates an independent matcher session and returns canonical
root-relative paths with `/` separators.

```csharp
using Touki.Io.Globbing;

IEnumerable<string> sourceFiles = Glob.EnumerateFiles(
    rootDirectory: projectDirectory,
    pattern: "**/*.cs",
    dialect: GlobDialect.PosixPath,
    options: GlobOptions.AllowGlobStar);

foreach (string file in sourceFiles)
{
    Console.WriteLine(file);
}
```

Pattern and root validation happen before `EnumerateFiles` returns; file-system
traversal starts during enumeration. Relative roots are resolved against the current
directory at call time. The convenience method is intended for trusted or prevalidated
patterns because it does not expose `maxPatternLength`; compile untrusted patterns
separately with a finite limit and use `FileSystemPathEnumerator`.

For multiple excludes, Touki also provides `GlobEnumerator`. It accepts one include and
a `GlobEnumerationOptions` object containing zero or more excludes, the dialect, glob
options, and optional traversal options. Its default dialect is `PosixPath`; select
another dialect explicitly when the pattern comes from another ecosystem.

```csharp
using Touki.Io;
using Touki.Io.Globbing;

string projectDirectory = @"C:\repos\my-project";

using GlobEnumerator enumerator = GlobEnumerator.Create(
    includePattern: "**/*.cs",
    rootDirectory: projectDirectory,
    options: new GlobEnumerationOptions
    {
        ExcludePatterns = ["**/obj/**"],
        Dialect = GlobDialect.PosixPath,
        GlobOptions = GlobOptions.AllowGlobStar
    });

while (enumerator.MoveNext())
{
    Console.WriteLine(enumerator.Current);
}
```

Results are relative to `rootDirectory`. Except for `MSBuild`, dialects are
case-sensitive by default; use `GlobOptions.IgnoreCase` to select case-insensitive
matching. Set `GlobEnumerationOptions.EnumerationOptions` to customize recursion,
inaccessible-directory handling, and other file-system traversal behavior.

## Compose custom matchers

`GlobSpecification.CreateFileSystemMatcher()` returns a reusable definition whose
sessions keep callback-native split spans. `FileSystemMatcher.CreatePath` adapts a
canonical root-relative `/` path predicate, making regular expressions straightforward:

```csharp
using System.Text.RegularExpressions;
using Touki.Io;
using Touki.Io.Globbing;

GlobSpecification sources = GlobSpecification.Compile(
    "src/**/*.cs",
    GlobDialect.PosixPath,
    GlobOptions.AllowGlobStar);
Regex generated = new("(?:^|/)Generated[^/]*\\.cs$", RegexOptions.CultureInvariant);
IFileSystemMatcher matcher = FileSystemMatcher.CreateExclusionWins(
    [sources.CreateFileSystemMatcher()],
    [FileSystemMatcher.CreatePath(path => generated.IsMatch(path.ToString()))]);

using FileSystemPathEnumerator enumerator = FileSystemPathEnumerator.Create(projectDirectory, matcher);
```

Definitions are immutable and borrowed. Each enumerator creates and owns an independent
session. Direct path-predicate children in one framework composition share canonical
path construction; callback-native graphs do not construct paths.
On modern .NET, regex callers can use the span-based `Regex.IsMatch` overload to avoid
the `string` conversion shown for .NET Framework compatibility.

## MSBuild item specifications

`MSBuildEnumerator` is a separate compatibility-oriented path for MSBuild include
and exclude specifications. It handles the common `*`, `?`, and recursive `**`
forms. When `projectDirectory` is supplied, relative specifications produce
paths relative to that directory; with a null project directory, results are
fully qualified.

Create an [`MSBuildEnumerationRequest`](../touki/Touki/Io/MSBuildEnumerationRequest.cs)
and use `MSBuildEnumerator.CreateResult` when the distinction between a search, an
invalid specification returned verbatim, an empty result, and a rejected drive-root
search matters. The closed result types are `MSBuildSearchResult`,
`MSBuildReturnLiteralResult`, `MSBuildEmptyResult`, and `MSBuildRejectedResult`.
The caller owns `MSBuildSearchResult.Enumerator` and must dispose it after enumeration.
Invalid exclude specifications do not fail a wildcard search: valid excludes
still apply, while invalid originals are retained as exact literal result filters
in source order through `MSBuildSearchResult.InvalidExcludeSpecifications`.
Recursive drive or share enumeration is rejected by default; opt in with
`MSBuildEnumerationRequest.AllowDriveEnumeration`
only when the caller deliberately permits that scope.

Recursive `**` matching keeps a bounded set of active pattern states, so repeated
anchors such as `**/a/b/*.cs` can retry later `a` segments without recursive or
exponential backtracking. File-only excludes such as `**/obj/*.txt` filter files
without pruning `obj`; terminal subtree excludes such as `**/obj/**` can prune it.
Parent traversal after a wildcard segment is rejected before relative-segment
normalization can erase it. `SplitWithErrors` preserves source order after duplicate
and recursive-superset elimination.

The implementation targets MSBuild item semantics, but it is not a drop-in copy of
every internal `FileMatcher` path. Intentional boundaries include:

* No-wildcard includes are enumerated normally instead of being returned verbatim
    without checking the filesystem.
* `CreateResult` rejects recursive drive/share searches by default, while MSBuild's
    action depends on its process traits.
* Some historical invalid-character, colon-position, 8.3 short-name, lexical path,
    enumeration-order, symlink, and I/O-failure tuple details are not reproduced.

Callers that require exact oracle parity at one of these boundaries should validate
that scenario against their pinned `Microsoft.Build` version.
