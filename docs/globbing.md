# Compiled Glob Matching and File-System Enumeration

Touki separates glob compilation from file-system enumeration. Use
[`GlobSpecification`](../touki/Touki/Io/Globbing/GlobSpecification.cs) to
compile and reuse a pattern, [`GlobEnumerator`](../touki/Touki/Io/GlobEnumerator.cs)
to apply compiled glob semantics to a directory tree, and
[`MSBuildEnumerator`](../touki/Touki/Io/MSBuildEnumerator.cs) when compatibility
with MSBuild item specifications is the primary requirement.

All three APIs are available on .NET 10 and .NET Framework 4.7.2.

## Compile and reuse a pattern

`GlobSpecification` is immutable and can be reused concurrently. Evaluation
reuses the compiled strategy; path-aware dialects that need to coalesce runs of
separators may rent a temporary buffer.

```csharp
using Touki.Io.Globbing;

using GlobSpecification specification = GlobSpecification.Compile(
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
| `IgnoreCase` | Enables the dialect's case-insensitive comparison mode. |
| `MatchLeadingDot` | Allows wildcards to consume a leading `.`. |
| `NoEscape` | Treats the dialect's escape character as a literal. |
| `AllowGlobStar` | Enables path-aware `**` matching where it is not enabled by default. |
| `AllowExtGlob` | Enables `?(...)`, `*(...)`, `+(...)`, `@(...)`, and `!(...)`. |

See [Extended Glob Patterns](extglob.md) for extglob syntax, limits, and the
intentional differences between supported dialects.

## Enumerate a directory tree

`GlobEnumerator` accepts one include and zero or more exclude patterns. Its default
dialect is `PosixPath`; select another dialect explicitly when the pattern comes
from another ecosystem.

```csharp
using Touki.Io;
using Touki.Io.Globbing;

string projectDirectory = @"C:\repos\my-project";

using GlobEnumerator enumerator = GlobEnumerator.Create(
    includePattern: "**/*.cs",
    excludePattern: "**/obj/**",
    rootDirectory: projectDirectory,
    dialect: GlobDialect.PosixPath,
    globOptions: GlobOptions.AllowGlobStar);

while (enumerator.MoveNext())
{
    Console.WriteLine(enumerator.Current);
}
```

Results are relative to `rootDirectory`. Use `GlobOptions.IgnoreCase` to select
case-insensitive matching. Pass `EnumerationOptions` to customize recursion,
inaccessible-directory handling, and other file-system traversal behavior.

## MSBuild item specifications

`MSBuildEnumerator` is a separate compatibility-oriented path for MSBuild include
and exclude specifications. It handles the common `*`, `?`, and recursive `**`
forms. When `projectDirectory` is supplied, relative specifications produce
paths relative to that directory; with a null project directory, results are
fully qualified.

Use `MSBuildEnumerator.CreateResult` when the distinction between a search, an
invalid specification returned verbatim, an empty result, and a rejected
drive-root search matters. Its
[`MSBuildSearchAction`](../touki/Touki/Io/MSBuildSearchAction.cs) reports that
disposition without collapsing every outcome into an empty sequence.
For `RunSearch`, the caller owns `result.Enumerator` and must dispose it after
enumeration.
Recursive drive or share enumeration is rejected by default; opt in with
[`MSBuildEnumerationOptions.AllowDriveEnumeration`](../touki/Touki/Io/MSBuildEnumerationOptions.cs)
only when the caller deliberately permits that scope.

The drive/share policy check is specific to `CreateResult`. The ordinary
`Create` overloads do not reject a recursive drive-root specification; prefer
`CreateResult` when a specification comes from an external source.

The implementation targets MSBuild item semantics, but it is not a drop-in copy of
every `FileMatcher` shortcut and edge case. Callers that require exact oracle parity
for unusual literal or trailing-separator inputs should validate those cases.
