# Extended glob (`AllowExtGlob`)

Reference for the extended-glob feature surface in
[`Touki.Io.Globbing`](../touki/Touki/Io/Globbing/). Covers the five extglob
constructs, how they relate to the &quot;normal&quot; glob metacharacters
(`*`, `?`, `[...]`), how to turn the feature on, and where touki agrees with
- or deliberately diverges from - bash.

If you only need the surface-level API contract, the
[`GlobOptions.AllowExtGlob`](../touki/Touki/Io/Globbing/GlobOptions.cs) doc
comment is canonical and shorter. This document expands on the **why** and
the **gotchas**.

## Normal glob (the baseline)

Without `AllowExtGlob` set, every dialect that touki ships understands the
classic three metacharacters, with per-dialect tweaks for path semantics and
escape characters:

| Token   | Meaning                                                              |
| ------- | -------------------------------------------------------------------- |
| `?`     | match exactly one character (path-aware dialects: not the separator) |
| `*`     | match zero or more characters (path-aware: not the separator)        |
| `**`    | globstar: match zero or more path segments - only when `AllowGlobStar` is set or the dialect has implicit globstar (`MSBuild`, `FileSystemGlobbing`, `Git`) |
| `[...]` | character class - only on dialects with `HasCharacterClasses()` (POSIX family, Bash, Git, FSG) |
| `[!...]` / `[^...]` | negated character class (same dialects)                    |
| `\<c>`  | escape `<c>` to its literal form - only on dialects with an escape character (POSIX family, Bash, Git use `\\`; PowerShell uses `` ` ``) |

Each `?` consumes exactly one character. `*` is greedy at the matcher's NFA
level. Neither `*` nor `?` crosses the path separator on path-aware
dialects, which is what keeps `*.cs` from matching `src/foo.cs`.

`(` and `)` are **literal characters** here. The bash-extglob constructs
described below are silently treated as ordinary text unless you opt in via
`AllowExtGlob`.

## Extended glob (what `AllowExtGlob` adds)

Extended glob (extglob in bash, `FNM_EXTMATCH` in POSIX, `extendedglob` in
ksh / zsh) layers five **alternation constructs** over the normal glob
grammar. Each consists of one of `?`, `*`, `+`, `@`, `!`, followed
immediately by `(`, a `|`-separated list of inner patterns, and a closing
`)`:

| Construct          | Quantifier semantics                                                                |
| ------------------ | ----------------------------------------------------------------------------------- |
| `?(p1\|p2\|...)`   | match **zero or one** occurrence of any alternative                                 |
| `*(p1\|p2\|...)`   | match **zero or more** occurrences of any alternative                               |
| `+(p1\|p2\|...)`   | match **one or more** occurrences of any alternative                                |
| `@(p1\|p2\|...)`   | match **exactly one** occurrence of any alternative                                 |
| `!(p1\|p2\|...)`   | match **any string that is not** one of the alternatives, as a single consumed slice |

Each inner pattern is itself a full glob - it may contain `*`, `?`,
character classes, escapes, and other extglob constructs recursively.

### Side-by-side examples

| Pattern             | Matches                                | Does not match                |
| ------------------- | -------------------------------------- | ----------------------------- |
| `*.cs`              | `foo.cs`, `bar.cs`                     | `foo.txt`, `foo.cs.bak`       |
| `@(*.cs\|*.txt)`    | `foo.cs`, `foo.txt`                    | `foo.json`, `foo.cs.bak`      |
| `*.cs`              | `foo.cs` (one extension)               | -                       |
| `?(*.cs)`           | `foo.cs`, *empty string*               | `foo.cs.bak`                  |
| `+(a\|b)`           | `a`, `b`, `ab`, `aabb`                 | `empty string`, `c`           |
| `*(a\|b)`           | `a`, `b`, `ab`, `aabb`, *empty string* | `c`, `ac`                     |
| `@(foo\|bar\|baz)`  | `foo`, `bar`, `baz`                    | `qux`, `foobar`               |
| `!(foo)`            | `bar`, `baz`, *empty string*           | `foo`                         |
| `!(*.cs)`           | `foo.txt`, `foo`                       | `foo.cs`                      |
| `foo@(x\|y)bar`     | `fooxbar`, `fooybar`                   | `foobar`, `fooxybar`          |
| `foo!(x\|y)bar`     | `foobar`, `foozbar`, `fooabcbar`       | `fooxbar`, `fooybar`          |

The first row contrasts a normal-glob alternation-of-sorts (`*.cs` matches
any name ending in `.cs`) with the explicit extglob list `@(*.cs|*.txt)`.
Extglob lets you spell out exactly which extensions are acceptable without
falling back to a character class or post-filtering.

### The negation form

`!(...)` is the only construct without a direct equivalent in the
normal-glob grammar. Read it as &quot;the surrounding pattern matches when
the input slice taken by this construct is **not** one of the listed
alternatives, taken as a whole consumed slice.&quot;

For `!(foo)bar`:

- Try consuming **`L = 0`** chars: the empty string is not `foo`; then `bar`
  must match the remainder of the input. So `bar` matches (`L = 0` then
  `bar`); `foobar` does not (no `L` leaves enough room for `bar` while
  avoiding the literal `foo`).
- Try `L = 1`, `L = 2`, etc., always checking that **no** alternative
  exactly matches the prefix of length `L` *and* the rest of the surrounding
  pattern matches the remainder.

The path-separator constraint applies: in a path-aware dialect, a single
`!(...)` construct cannot consume across `/`. Multi-segment matches need
multiple constructs joined with explicit separators (e.g., `!(foo)/!(bar)`).

### Nesting and recursion

Constructs nest freely up to the
[`MaxExtGlobDepth`](../touki/Touki/Io/Globbing/GlobSpecification.Factory.cs)
cap of 8 levels. Example: `*(a|@(b|c))d` matches any sequence built from
the &quot;literal `a`&quot; and &quot;exactly one of `b` or `c`&quot;
alternatives, followed by `d`. So `d`, `ad`, `bd`, `cd`, `abcd`, etc., all
match; `abxd` doesn't (the inner `@(b|c)` doesn't accept `x`).

## Turning it on

Pass `GlobOptions.AllowExtGlob` to
[`GlobSpecification.Compile`](../touki/Touki/Io/Globbing/GlobSpecification.cs).
The option is opt-in on every dialect:

```csharp
using Touki.Io.Globbing;

GlobSpecification spec = GlobSpecification.Compile(
    pattern: "@(*.cs|*.txt)",
    dialect: GlobDialect.Bash,
    options: GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

bool matched = spec.IsMatch("foo.cs");      // true
bool ignored = spec.IsMatch("foo.json");    // false
```

When `AllowExtGlob` is omitted, the pattern `@(*.cs|*.txt)` is interpreted
as the literal characters `@(*.cs|*.txt)` and matches no real-world file
name - the parser does not warn, so the silent no-match can be
surprising. If your pattern is user-supplied, prefer to pass the flag
unconditionally.

The same flag lights the feature up regardless of dialect - it is
honored by every dialect that uses the bytecode interpreter
(`Posix`, `PosixPath`, `Bash`, `Git`, `Simple`, `PowerShell`,
`FileSystemGlobbing`, `MSBuild`). Bash callers can think of it as the
in-code equivalent of `shopt -s extglob`.

## Limits and error cases

To keep the interpreter's recursion bounded and prevent pathological
backtracking, the compile pipeline enforces a small set of hard limits:

| Limit                                    | Default | Failure mode                                         |
| ---------------------------------------- | ------- | ---------------------------------------------------- |
| Nesting depth                            | 8       | `GlobFormatException(FeatureLimitExceeded)`          |
| Alternatives per construct               | 32      | `GlobFormatException(FeatureLimitExceeded)`          |
| Alternation block bytecode length        | 65535   | `GlobFormatException(PatternTooLarge)`               |
| Total `AltStart` opcodes in one program  | bounded by block length |                                       |

These caps are enforced **at compile time**, so a runtime match never
spins on a pathological pattern.

Compile-time errors specific to extglob:

| Error code                                       | Triggered by                                  |
| ------------------------------------------------ | --------------------------------------------- |
| `GlobCompileErrorCode.UnterminatedExtGlob`       | `?(foo`, `*(a|b`, &hellip;                    |
| `GlobCompileErrorCode.InvalidExtGlobBody`        | Empty body `?()`. Allowed: `?(|)` (explicit empty alternative). |
| `GlobCompileErrorCode.FeatureLimitExceeded`      | Nesting or alternative count exceeds the cap. |

`DanglingEscape` and `UnterminatedClass` continue to apply when an
extglob body contains malformed escapes or classes.

## Path-aware semantics

On path-aware dialects (`PosixPath`, `Bash`, `Git`, `MSBuild`,
`FileSystemGlobbing`):

- Inner wildcards (`*`, `?`, char classes) inside an extglob alternative do
  not cross `/`. `@(*.cs|*.txt)` against `src/foo.cs` is **no match**
  - the inner `*` can't consume `src/`.
- `!(...)` similarly cannot consume past `/`. Use explicit separators in the
  outer pattern to span segments: `dir/!(foo)` matches `dir/bar`,
  `dir/baz`; never `dir/foo` and never `dir/sub/bar`.
- Globstar (`**`) remains a separate, distinct construct that *can* cross
  segments. It composes with extglob normally: `**/@(*.cs|*.txt)` matches
  any `.cs` or `.txt` file at any depth.

## Performance notes

- When `AllowExtGlob` is **off** or the pattern contains no extglob
  construct, the encoder emits byte-identical bytecode and the matcher
  uses the same iterative two-slot loop as today. There is one extra
  branch per pattern character (the scanner check for the `kind` chars
  followed by `(`) in the compile path, and one extra branch in the
  matcher dispatch.
- When extglob is in use, the matcher takes a recursive path
  ([`CompiledGlobStrategy.ExtGlob.cs`](../touki/Touki/Io/Globbing/CompiledGlobStrategy.ExtGlob.cs))
  with a stack-allocated `Span<ProgramRange>` of 32 entries. No heap
  allocations on the match path.
- Specialized strategies (`Literal`, `Prefix`, `Suffix`, `Contains`,
  `PrefixSuffix`, `Any`, `GlobStarFileName`) disqualify themselves on
  extglob patterns and route to the general bytecode path. This is
  correctness-essential and trades off some specialization speed for
  alternation semantics.
- The compile-time tail-anchor optimization
  (`EndsWith` fast-fail on a literal suffix) is also suppressed for
  extglob programs - an alternative may consume what looks like a
  trailing literal.

## Bash parity

The `Bash` dialect with `AllowGlobStar | AllowExtGlob` set is compared
row-by-row against `bash -O extglob -O globstar` in
[`ExtGlobOracleTests.Bash`](../touki.tests/Touki/Io/Globbing/ExtGlobOracleTests.Bash.cs)
(~552 rows of 24 patterns &times; 23 inputs). The oracle runs on Linux
and Windows Git Bash; macOS is skipped because Apple ships GNU bash 3.2,
which predates several of the cases the oracle relies on
([`BashInterop.cs`](../touki.tests/Touki/Io/Globbing/BashInterop.cs)
short-circuits to `null` there).

### Documented divergence

Bash and touki agree on every oracle row in the suite **except one**:

| Pattern | Input | bash 5.x | touki | Notes                                                                                                       |
| ------- | ----- | -------- | ----- | ----------------------------------------------------------------------------------------------------------- |
| `!(*)`  | `""`  | match    | no match | Touki reads negation as &quot;no alternative matches the slice exactly.&quot; The inner `*` matches the empty slice exactly, so `L = 0` is rejected. Bash short-circuits this case. |

The row is skipped in the oracle so any other future drift surfaces as a
hard failure. If you write a pattern of the form `!(*)X`, prefer
`?(X)` or `+(X)` to express the intent more directly.

## When to reach for extglob

- Strong substitute for ad-hoc regex when the only thing you need is
  alternation. `@(*.cs|*.csx|*.cake)` reads better than maintaining a
  list of `Glob` matchers and OR-ing the results.
- Useful with `GlobDialect.Bash` to round-trip shell scripts that already
  use extglob.
- Useful inside `.gitignore`-style rule sets when you want to ignore
  &quot;everything but `keep.log`&quot; in a single rule: `!(keep).log`.

If your only goal is &quot;match either of these literal filenames,&quot;
the un-extended `[abc]` character class is still the cheapest answer:
`[ab]c` matches `ac` or `bc` without the alternation machinery.

## See also

- [`GlobOptions.cs`](../touki/Touki/Io/Globbing/GlobOptions.cs) -
  per-flag reference.
- [`GlobDialect.cs`](../touki/Touki/Io/Globbing/GlobDialect.cs) -
  per-dialect defaults.
- [`globbing-feature-plan.md`](globbing-feature-plan.md) - internal
  planning, including the F1.3 / F1.4 rollout history.
- [bash Pattern Matching](https://www.gnu.org/software/bash/manual/html_node/Pattern-Matching.html)
- [fnmatch(3) FNM_EXTMATCH](https://man7.org/linux/man-pages/man3/fnmatch.3.html)
