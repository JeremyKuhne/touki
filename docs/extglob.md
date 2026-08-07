# Extended glob (`AllowExtGlob`)

Reference for the extended-glob feature surface in
[`Touki.Io.Globbing`](../touki/Touki/Io/Globbing/). Covers the five extglob
constructs, how they relate to the "normal" glob metacharacters
(`*`, `?`, `[...]`), how to turn the feature on, and where Touki agrees with
- or deliberately diverges from - bash.

If you only need the surface-level API contract, the
[`GlobOptions.AllowExtGlob`](../touki/Touki/Io/Globbing/GlobOptions.cs) doc
comment is canonical and shorter. This document expands on the **why** and
the **gotchas**.

## Normal glob (the baseline)

Without `AllowExtGlob` set, every dialect supports `*`; the remaining syntax
varies by dialect. FileSystemGlobbing treats ordinary `?` characters as
literals, while the other dialects use `?` as a single-character wildcard:

* `?` matches exactly one character, excluding the separator in path-aware
  dialects.
* `*` matches zero or more characters, excluding the separator in path-aware
  dialects.
* `**` matches zero or more path segments when `AllowGlobStar` is set or the
  dialect enables it implicitly (`MSBuild`, `FileSystemGlobbing`, and `Git`).
* `[...]` and its `[!...]` / `[^...]` negated forms are character classes in the
  POSIX-family, Bash, Git, and PowerShell dialects.
* `\<c>` escapes a character in POSIX-family, Bash, and Git patterns.
  PowerShell uses backtick instead.

Where supported as a wildcard, each `?` consumes exactly one character. `*` is
greedy at the matcher's NFA level. Neither wildcard crosses the path separator
on path-aware dialects, which is what keeps `*.cs` from matching `src/foo.cs`.

`(` and `)` are **literal characters** here. The bash-extglob constructs
described below are silently treated as ordinary text unless you opt in via
`AllowExtGlob`.

## Extended glob (what `AllowExtGlob` adds)

Extended glob (extglob in bash, GNU/glibc `fnmatch(FNM_EXTMATCH)`, `extendedglob` in
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

Each inner pattern is itself a full dialect-specific glob - it may contain
wildcards, classes, escapes, and other extglob constructs according to that
dialect's rules.

When a FileSystemGlobbing pattern contains Touki-only extglob syntax, its
whole-pattern compatibility rewrites (`*.*`, leading `**.`, separator runs,
trailing separators, and parent-segment placement) are not applied. The
reference `Microsoft.Extensions.FileSystemGlobbing.Matcher` has no extglob
syntax to define that composition.

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
normal-glob grammar. Read it as "the surrounding pattern matches when
the input slice taken by this construct is **not** one of the listed
alternatives, taken as a whole consumed slice."

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
[`MaxExtGlobDepth`](../touki/Touki/Io/Globbing/GlobSpecification.Limits.cs)
cap of 8 levels. Example: `*(a|@(b|c))d` matches any sequence built from
the "literal `a`" and "exactly one of `b` or `c`"
alternatives, followed by `d`. So `d`, `ad`, `bd`, `cd`, `abcd`, etc., all
match; `abxd` doesn't (the inner `@(b|c)` doesn't accept `x`).

## Turning it on

Pass `GlobOptions.AllowExtGlob` to
[`GlobSpecification.Compile`](../touki/Touki/Io/Globbing/GlobSpecification.cs).
The option is opt-in on every dialect:

```csharp
using Touki.Io.Globbing;

using GlobSpecification specification = GlobSpecification.Compile(
    pattern: "@(*.cs|*.txt)",
    dialect: GlobDialect.Bash,
    options: GlobOptions.AllowExtGlob);

bool matched = specification.IsMatch("foo.cs");      // true
bool ignored = specification.IsMatch("foo.json");    // false
```

When `AllowExtGlob` is omitted, the pattern `@(*.cs|*.txt)` is interpreted
without extglob alternation - the parser does not warn, so the resulting match
behavior can be surprising. If your pattern language permits extglob, pass the
flag explicitly.

The same flag lights the feature up regardless of dialect - it is
honored by every dialect that uses the bytecode interpreter
(`Posix`, `PosixPath`, `Bash`, `Git`, `Simple`, `PowerShell`,
`FileSystemGlobbing`, `MSBuild`). Bash callers can think of it as the
in-code equivalent of `shopt -s extglob`.

## Limits and error cases

To bound pattern structure and alternation work, the compile pipeline enforces a
small set of hard limits:

| Limit                                    | Default | Failure mode                                         |
| ---------------------------------------- | ------- | ---------------------------------------------------- |
| Nesting depth                            | 8       | `GlobFormatException(FeatureLimitExceeded)`          |
| Alternatives per construct               | 32      | `GlobFormatException(FeatureLimitExceeded)`          |
| Alternation block bytecode length        | 65535   | `GlobFormatException(PatternTooLarge)`               |

These caps are enforced **at compile time**. The iterative matcher engages a
failure memo after 1,000 choice visits, records at most $2^{20}$ failed states,
and bounds native recursion by the compiled nesting limit. Its temporary frame
and range buffers can still grow through `ArrayPool<T>`. Matching cost and memory
therefore vary with the pattern and input, so applications accepting both from
untrusted sources should also pass an application-specific `maxPatternLength`
and enforce input length and time limits.

Compile-time errors specific to extglob:

| Error code                                       | Triggered by                                  |
| ------------------------------------------------ | --------------------------------------------- |
| `GlobCompileErrorCode.UnterminatedExtGlob`       | `?(foo`, `*(a|b`, ...                         |
| `GlobCompileErrorCode.InvalidExtGlobBody`        | Empty body `?()`. Allowed: `?(|)` (explicit empty alternative). |
| `GlobCompileErrorCode.FeatureLimitExceeded`      | Nesting or alternative count exceeds the cap. |

`DanglingEscape` continues to apply when an extglob body contains a malformed
escape. An unterminated character class is treated as literal text.

## Path-aware semantics

On path-aware dialects (`PosixPath`, `Bash`, `Git`, `MSBuild`,
`FileSystemGlobbing`):

- Inner wildcards inside an extglob alternative do
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
  construct, the encoder emits the ordinary glob bytecode and matching uses the
  ordinary iterative engine. Extglob detection adds a scanner check during
  compilation and a dispatch check during matching.
- When extglob is in use, the iterative extglob engine
  ([`CompiledGlobStrategy.ExtGlob.cs`](../touki/Touki/Io/Globbing/CompiledGlobStrategy.ExtGlob.cs))
  starts with stack-backed work buffers sized from the extglob depth. More
  complex matches can grow through pooled buffers and engage failure-memo
  storage.
- The ordinary path-unaware specializations (`Literal`, `Prefix`, `Suffix`,
  `Contains`, `PrefixSuffix`, `Any`) disqualify themselves on extglob patterns.
  The canonical `**/@(*suffix1|*suffix2|...)` shape can still use
  `GlobStarFileName` with a `MultiSuffixGlobStrategy`; other extglob shapes
  route to the general bytecode path.
- Extglob programs use an `EndsWith` fast-fail when every top-level alternative
  has a common literal suffix. The engine does not trim that suffix before
  matching because the alternation walker still needs the complete input.

## Bash parity

The `Bash` dialect with `AllowGlobStar | AllowExtGlob` set is compared
row-by-row against `bash -O extglob -O globstar` in
[`ExtGlobOracleTests.Bash`](../touki.tests/Touki/Io/Globbing/ExtGlobOracleTests.Bash.cs)
(~552 rows of 24 patterns x 23 inputs). The oracle runs on Linux
and Windows Git Bash; macOS is skipped because Apple ships GNU bash 3.2,
which predates several of the cases the oracle relies on
([`BashInterop.cs`](../touki.tests/Touki/Io/Globbing/BashInterop.cs)
short-circuits to `null` there).

### Documented divergence

Bash and Touki have a known negation divergence outside the current oracle row
set:

| Pattern | Input | bash 5.x | Touki |
| ------- | ----- | -------- | ----- |
| `!(*)`  | `""`  | match    | no match |

Touki reads negation as "no alternative matches the slice exactly." The inner
`*` matches the empty slice exactly, so `L = 0` is rejected. Bash short-circuits
this case.

The case is tracked separately from the oracle matrix. If you write a pattern
of the form `!(*)X`, prefer `?(X)` or `+(X)` to express the intent more directly.

There is also a leading-dot limitation in extglob alternations. If any
alternative starts with a literal dot, a sibling alternative led by `*`, `?`, a
character class, or globstar can consume a leading dot even when
`MatchLeadingDot` is not set. Avoid mixing literal-dot and wildcard-led
alternatives when hidden-name exclusion matters, or set `MatchLeadingDot` when
leading-dot matching is intentional.

## When to reach for extglob

- Strong substitute for ad-hoc regex when the only thing you need is
  alternation. `@(*.cs|*.csx|*.cake)` reads better than maintaining a
  list of `Glob` matchers and OR-ing the results.
- Useful with `GlobDialect.Bash` to round-trip shell scripts that already
  use extglob.
- Useful inside `.gitignore`-style rule sets when you want to ignore
  "everything but `keep.log`" in a single rule: `!(keep).log`.

If your only goal is "match either of these literal filenames,"
the un-extended `[abc]` character class is still the cheapest answer:
`[ab]c` matches `ac` or `bc` without the alternation machinery.

## See also

- [`GlobOptions.cs`](../touki/Touki/Io/Globbing/GlobOptions.cs) -
  per-flag reference.
- [`GlobDialect.cs`](../touki/Touki/Io/Globbing/GlobDialect.cs) -
  per-dialect defaults.
- [bash Pattern Matching](https://www.gnu.org/software/bash/manual/html_node/Pattern-Matching.html)
- [fnmatch(3) FNM_EXTMATCH](https://man7.org/linux/man-pages/man3/fnmatch.3.html)
